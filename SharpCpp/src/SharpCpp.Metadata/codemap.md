# src/SharpCpp.Metadata/

## Responsibility

The **Metadata layer** owns the schema definition, serialization, and import/export of binding metadata — the intermediate representation (IR) that flows between parsing and rendering. It is the serialization boundary: the parser produces a `BindingCompilation` AST (from `SharpCpp.Model`), the exporter converts it to a portable JSON document, and the import catalog reconstructs foreign types from previously exported documents without re-parsing.

This layer does **not** do parsing, rendering, or pipeline orchestration. It is a pure data-translation and persistence boundary.

## Design Patterns

### Schema-as-constants (`BindingMetadataSchema`)
- Centralizes version identifiers (`CurrentVersion = 1`), generator/support runtime version stamps, and the attribute vocabulary name.
- Single source of truth for metadata format versioning; changes incrementally to maintain backward compatibility.

### Document records (`BindingMetadataDocument`)
- Everything is an immutable `sealed record` with `IReadOnlyList<>` for collection properties — value semantics for serialization, no behavioral logic.
- Top-level document contains: `Source`, `Imports`, `Fingerprint`, `Types`, `Functions`.
- Type references use `BindingMetadataTypeReference` with a discriminated `Kind` (primitive/handle/enum) and optional `RecordId`/`MarshallerTypeName` for type-system resolution downstream.
- `BindingMetadataExportOptions` acts as a builder parameter object for the export path.

### Static exporter (`BindingMetadataExporter`)
- Pure stateless mapping from the mutable Model AST (`BindingCompilation`, `RecordBinding`, `EnumBinding`, `FunctionBinding`, `FieldBinding`) into the immutable metadata record tree.
- Filters out ignored/suppressed nodes via `IsIgnored()` / `SuppressEmission` metadata.
- Sorts all emitted entries deterministically (`ManagedName`, then `SymbolId`) for stable diffs.
- Recursively maps `BindingType` → `BindingMetadataTypeReference`, flattening the type hierarchy into a portable discriminator string.

### JSON serializer (`BindingMetadataSerializer`)
- Thin wrapper over `System.Text.Json` with `snake_case` naming policy and `WhenWritingNull` ignore.
- Append-newline terminator (`+ Environment.NewLine`) for streaming-friendly output.

### Import catalog (`BindingMetadataImportCatalog`)
- Loads previous metadata JSON files, deserializes and validates schema version.
- Seeds a `BindingCompilation` with imported types (records/enums) using synthetic `"import:{package}:{symbolId}"` identifiers.
- Imported types are marked `SuppressEmission = true` so they appear as dependency references but do not regenerate their own bindings.
- Uses `SHA256` content hashing for change detection.
- Employs a two-key lookup strategy (native name + qualified C++ name with cv-qualifiers stripped) so downstream type references resolve even when names differ slightly.

## Data & Control Flow

```
Model AST (BindingCompilation)
        │
        ▼
BindingMetadataExporter.Export()
        │
        ▼
BindingMetadataDocument  ──►  BindingMetadataSerializer.Serialize()
        │                              │
        │                              ▼
        │                        JSON file on disk
        │
        ▼
BindingMetadataImportCatalog.Load()
        │
        ▼
BindingMetadataImportCatalog.Seed(compilation)
        │
        ▼
Model AST (seeded with import stubs)
```

**Export path**: The caller (pipeline/tool) supplies a populated `BindingCompilation` and optional `BindingMetadataExportOptions`. The exporter filters, sorts, and projects every node into immutable records. The serializer renders the document as snake_case JSON.

**Import path**: The caller supplies file paths to previously emitted metadata. `Load()` validates schema version, computes content hashes, and builds an in-memory catalog. `Seed()` injects foreign record/enum stubs into a fresh compilation, keyed by native and qualified names, so that local parsing can reference types from other packages without re-analyzing their headers.

## Integration Points

### Depends on
- **`SharpCpp.Model`** (project reference in `.csproj`): The AST types (`BindingCompilation`, `RecordBinding`, `EnumBinding`, `FunctionBinding`, `FieldBinding`, `BindingType`, `MetadataKeys`, extension methods for ignored/warnings). The exporter is the sole consumer of the full AST; the import catalog also writes into it.

### Consumed by
- **Parsing layer** (upstream): Produces the `BindingCompilation` that the exporter consumes. No direct dependency — only the AST shape is shared.
- **Pipeline** (future): Will invoke `BindingMetadataExporter` to serialize results after parsing, and `BindingMetadataImportCatalog` to pre-seed foreign types before parsing.
- **Rendering layer** (future): Will consume deserialized `BindingMetadataDocument` records as input — no AST dependency, only the metadata record types.

### Assembly identity
- `SharpCpp.Metadata` is the assembly name; the `AssemblyMarker` class provides a reflection-friendly type for assembly scanning.
