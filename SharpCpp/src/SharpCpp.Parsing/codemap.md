# src/SharpCpp.Parsing/

## Responsibility

The Parsing layer owns the translation of C++ API surface into SharpCpp's binding model. It is the sole point of ClangSharp integration and the producer of the `BindingCompilation` — the in-memory declaration tree consumed by all downstream passes and renderers.

It executes two discrete phases:

1. **Phase 1 (API parse):** Traverses the ClangSharp AST for configured C++ headers and produces a `BindingCompilation` mirroring the native declaration tree — namespaces, records, enums, free functions, methods, constructors, fields, with structural metadata (access level, constness, virtuality, override/final, operator tokens, polymorphism) and attribute-derived policies (`[[sharpcpp::ignore]]`, `[[sharpcpp::rename]]`, `[[sharpcpp::in/out/ref]]`, `[[sharpcpp::director]]`, `[[sharpcpp::instantiate]]`).

2. **Phase 2 (marshaller instantiation):** A post-parse pass that re-scans marshaller configuration files for `[[sharpcpp::managed_type(...)]]` attributes on `Marshaller<T>` and `ReturnMarshaller<T>` template specializations. It resolves managed type names, handle marshaller type names, managed base types, and injected members (`[[sharpcpp::expose_member(...)]]`), then back-patches these onto the `BindingCompilation`.

The layer does **not** know about concrete rendering syntax, pass scheduling, or project-specific shaping policy.

## Design

### Two-phase parsing

- **Phase 1** (`ClangSharpHeaderParser.Parse`) is driven by `CXTranslationUnit.Parse()` and a cursor visitor that dispatches on `CXCursorKind`. It populates `recordsByName` and `enumsByName` dictionaries as registries reachable during type-resolution recursion. When a cursor's defining location falls within an *owned file root*, it enters the parse; external/system declarations are skipped.
- **Phase 2** (`MarshallerMetadataPhase2.Apply`) is a separate static pass that operates on the already-built `BindingCompilation`. It uses regex to extract attribute bodies from marshaller `.h` source text rather than re-entering ClangSharp, then reconciles these with placeholder records and resolves `ManagedTypeFormat` on all `BindingType.Handle` nodes throughout the compilation.

### ClangSharp integration pattern

- Parsing is **non-recursive**: the top-level `Parse()` method visits the translation unit root cursor once. Namespace cursors recurse via `VisitNamespace`; record children are visited per-kind via dedicated `PopulateRecordMembers` blocks (methods, static methods, constructors, templates, fields). This avoids stack overflows on deep hierarchies and keeps ownership of the visitor explicit.
- **Owned file filtering** is enforced via `IsOwnedCursor` (compares resolved absolute file path against the set of *owned file roots*) — only declarations in project headers are bound.
- Cursor **source text** is obtained through `CXTranslationUnit.Tokenize` → `GetSourceText`, which concatenates token spellings for precise attribute matching within declaration extents.

### Ghost records and placeholder resolution

When `MapType` encounters a `CXType_Record` that has not yet been registered, it creates a **placeholder** `RecordBinding` (marked with `PlaceholderRecord` or `ForwardDeclaration` metadata) and inserts it into `recordsByName`. Later, when the record's full definition is visited in `RegisterRecord`, the placeholder is *detached* (removed from the compilation) and replaced by the real record. Phase 2 additionally runs `SyncPlaceholderRecords` to merge marshaller metadata from placeholders into real records that share a `CppQualifiedName`.

### Attribute extraction: inline + prescan

Inline attributes (`[[sharpcpp::ignore]]`, `[[sharpcpp::rename]]`, `[[sharpcpp::in/out/ref]]`, `[[sharpcpp::director]]`, `[[sharpcpp::flags]]`, `[[sharpcpp::instantiate]]`) are detected via regex on the tokenized cursor extent source text.

For attributes that appear on the **line before** a declaration (common in C++ style), a **prescan** phase reads all owned header files ahead of the ClangSharp parse and builds line-indexed caches (`s_ignoreAnnotations`, `s_renameAnnotations`, `s_instantiateAnnotations`). The `CheckAndApplyIgnoreAttribute` and `TryApplyRename` methods check both sources.

### Type mapping with fallback

`MapType` converts `CXType`/`CXTypeKind` into `BindingType` via a cascade:
1. Generic parameter lookup (template context)
2. Return-type-specific managed-type override
3. General managed-type override (from marshaller config files)
4. LValueReference dereference → recurse on pointee
5. Pointer → specialized handle type, void* override, or record handle
6. Record → handle type with lazy placeholder creation
7. Enum → enum binding type
8. `CXTypeKind` switch for primitives

If `MapType` throws `NotSupportedException`, the *try-wrapper* (`TryMapType`, `TryMapParameter`) catches it and produces a fallback `BindingType` (with an `IgnoreReason` set) so partial failure does not abort the entire parse.

### Type qualification resolution

`ResolveQualifiedTypeName` takes a `CXType` spelling and resolves unqualified type names to their `::`-qualified C++ names using the records registry. This is used to attach canonical C++ type strings to function signatures for downstream ABI matching.

## Data & Control Flow

```
ClangSharpParseRequest
  │
  ├─ ProjectName, ManagedNamespace, ManagedClassName, NativeLibraryName
  ├─ HeaderFilePath, HeaderIncludePath
  ├─ ClangArguments (compiler flags)
  ├─ MarshallerConfigurationFiles (marshaller .h paths)
  ├─ OwnedFileRoots (which files are "ours")
  └─ ImportedBindingMetadata (optional seed from upstream)
       │
       ▼
1. NormalizeOwnedFileRoots()
2. EnumerateOwnedHeaderFiles()
3. PreScanIgnoreAnnotations()     ─── s_ignoreAnnotations
4. PreScanRenameAnnotations()     ─── s_renameAnnotations
5. PreScanInstantiateAnnotations()─── s_instantiateAnnotations
6. CXTranslationUnit.Parse()
7. ValidateDiagnostics()
       │
       ▼
8. Cursor.VisitChildren (top-level cursor dispatch)
   ├─ CXCursor_Namespace    → VisitNamespace (recursive)
   ├─ CXCursor_StructDecl   │
   ├─ CXCursor_ClassDecl    ├─ RegisterRecord
   ├─ CXCursor_ClassTemplate│  ├─ PopulateRecordBases
   ├─ CXCursor_EnumDecl     │  ├─ PopulateRecordMembers (methods, ctors, fields, templates)
   ├─ CXCursor_FunctionDecl │  └─ UpdatePolymorphism
   └─ CXCursor_FunctionTemplate └─ RegisterEnum / CreateFunction / CreateTemplateFunction
        │
        ▼
   Type resolution (MapType / MapParameter / ResolveQualifiedTypeName)
   ├─ recordsByName dictionary (live registry, grows during parse)
   ├─ enumsByName dictionary
   ├─ managedTypeOverrides (from marshaller config regex scan)
   └─ genericParameters (template parameter scope)
        │
        ▼
9. AssignImportMethodNames()  ← deduplicate overloads with numbered suffixes
10. Return BindingCompilation
        │
        ▼
11. MarshallerMetadataPhase2.Apply(compilation, marshallerConfigFiles)
    ├─ Matches managed_type(...) attributes per file
    ├─ SyncPlaceholderRecords (merge metadata from placeholders → real records)
    └─ ApplyManagedTypeFormats (back-patches ManagedTypeFormat on all BindingType.Handle nodes)
```

## Integration

### Depends on

| Dependency | Contract |
|---|---|
| `SharpCpp.Model` | `BindingCompilation`, `RecordBinding`, `FunctionBinding`, `FieldBinding`, `EnumBinding`, `ParameterBinding`, `BindingType`, `BindingNode` metadata system, `MetadataKeys` |
| `SharpCpp.Metadata` | `BindingMetadataImportCatalog` for seeding imported binding metadata before Phase 1 |
| `ClangSharp` | `CXIndex`, `CXTranslationUnit`, `CXCursor`, `CXType`, `CXSourceRange`, cursor visitor pattern |

### Consumed by

| Consumer | How |
|---|---|
| `SharpCpp.Pipeline` | Receives the `BindingCompilation` (via `ClangSharpParseRequest.ImportedBindingMetadata` seeding and Phase 1+2 output) and runs pass scheduling / materialization before rendering |
| `SharpCpp.Rendering.*` | Renders from the enriched `BindingCompilation` after all passes run |
| `SharpCpp.Tool` | Composes the full pipeline by coordinating ParseRequest construction and pass execution |

### Must not depend on

- Concrete rendering syntax (C# project files, C++ source, etc.)
- Pass scheduling or pass interfaces
- Project-specific shaping or policy logic
