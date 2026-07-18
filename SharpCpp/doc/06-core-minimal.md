# 06 — Core Minimal

Core is small and uniform. Configuration is large and extensible.

## What is core

Core code is code that every generator user needs, regardless of which C++ library they are binding. Core does not know about specific type categories.

### Foundation layer

| Component | Responsibility |
|---|---|
| `BindingCompilation` | Mutable shadow AST root |
| `RecordBinding`, `FunctionBinding`, `FieldBinding`, `PropertyBinding`, `EnumBinding`, `AliasBinding` | AST nodes |
| `BindingType` | Type representation (with marshaller reference) |
| `IRMetadata` + `MetadataKeys` | Extensible metadata dictionary |
| `BindingNode` | Base node with metadata storage |
| `DiagnosticCollector`, `Diagnostic` | Diagnostic infrastructure |
| `CodeWriter` | Text emission with brace-inferred indentation |
| `BindingSourceReference` | Source provenance tracking |

### Parser

| Component | Responsibility |
|---|---|
| `ClangSharpCompilationParser` | Parse headers, orchestrate two-phase extraction |
| `ManagedTypeMetadataDefinition` | Phase-2 attribute extraction |
| `ConfiguredTypeAliasResolver` | Type alias resolution |

Core parser knows about `[[sharpcpp::*]]` attributes but does not hardcode their semantics. It extracts them into metadata.

### Passes

| Pass | Role |
|---|---|
| `AssignDefaultManagedNamesPass` | Assign PascalCase/camelCase names |
| `MaterializeMarshallerSurfacePass` | Create synthetic records and members from marshaller metadata |
| `CollectGenericInstantiationsPass` | Discover concrete instantiations |
| `GenerateFieldPropertiesPass` | Create getter/setter accessors |
| `ProjectInheritedHiddenVirtualsPass` | Materialize inherited secondary-base / hidden virtual surface |
| `NormalizeOverrideAccessModifiersPass` | Normalize projected override access and dispatch records |
| `EnsureManagedLayoutPass` | Wrap in namespace/class containers |
| `ValidateSupportedSurfacePass` | Validate marshaller metadata completeness |

These passes operate on generic AST state and marshaller metadata. They do not branch on type categories.

### Renderers

| Component | Responsibility |
|---|---|
| `CSharpBindingsRenderer` | Emit `LibraryImport` + `MarshalUsing` + wrapper types |
| `CppBindingsRenderer` | Emit C++ thunks that call through marshallers |
| `BindingMetadataRenderer` | Emit versioned metadata contract for downstream consumers |

Core renderer logic:

```
for each function:
    emit LibraryImport signature using marshaller metadata
    emit wrapper method that calls the LibraryImport
for each record:
    emit class declaration with handle/base/interface from marshaller metadata
    emit injected members from expose_member metadata
for each enum:
    emit enum declaration
```

No string branches. No contiguous-buffer branches. No callback branches. Marshaller metadata drives everything.

## What is configuration

Configuration varies per C++ library. It lives outside core.

### Marshaller specializations (C++ headers)

| Category | Where |
|---|---|
| Primitive marshallers | `SharpCpp/Marshaller.hpp` (built-in) |
| String marshallers | `SharpCpp/detail/MarshallerSupport.hpp` (built-in) |
| Optional marshallers | `SharpCpp/detail/OptionalSupport.hpp` (built-in) |
| Container marshallers | `SharpCpp/detail/MarshallerSupport.hpp` (built-in) |
| Callback marshallers | `SharpCpp/detail/MarshallerSupport.hpp` (built-in) |
| User type marshallers | User project headers |
| Custom stdlib marshallers (e.g., EASTL) | Library-specific headers |

### Support library (C#)

| Component | Why it's configuration |
|---|---|
| `Utf8StringAbi`, `Utf8StringMarshaller`, `Utf8SpanMarshaller` | String marshallers — one possible configuration |
| `ContiguousBufferAbi`, `ContiguousBufferMarshaller` | Buffer marshallers — one possible configuration |
| `OptionalValueAbi`, `OptionalValueMarshaller` | Optional marshallers — one possible configuration |
| `ArrayView<T>`, `ReadOnlyArrayView<T>` | One possible container surface |
| `StringBase`, `VectorBase<T>`, `MapBase<TKey, TValue>` | Container base types |
| `NativeObjectRegistry` | Polymorphic wrapping configuration |

Core generator does not reference these types by name. It reads their names from marshaller attributes and emits references.

### Composition code (project-specific)

| Component | Why it's configuration |
|---|---|
| `BindingProject` config | Headers, namespace, native library name |
| Generic instantiation requests | Which concrete types to generate |
| Project-specific passes | Library-specific surface shaping |
| Imported binding metadata | Upstream type bindings |
| Marshaller include paths | Where to find marshaller specializations |

### Per-use-site attributes

| Attribute | Role |
|---|---|
| `[[sharpcpp::count(...)]]` | Per-parameter buffer size |
| `[[sharpcpp::string_binding(...)]]` | Per-parameter string surface override |
| `[[sharpcpp::in/out/ref]]` | Per-parameter pass kind |
| `[[sharpcpp::callback_user_data(...)]]` | Per-parameter callback user data |

## What leaves core

| Component | Replaced by |
|---|---|
| `ManagedValueTypeInteropBinding` (17+ properties) | `MarshallerMetadata` (flat schema from attributes) |
| `ManagedStringBinding` enum + resolver | `public_type` / `abi_type` / `marshaller_type` on string marshaller specializations; default surface is span, `string` is opt-in |
| `BindingContiguousBufferBinding` + `BindingContiguousBufferSourceKind` | Container marshaller specializations declaring their own surface |
| `BindingCallbackSignature` + `BindingCallbackKind` | `callback = true` attribute + callback marshaller |
| `VirtualMethodSlotBinding` | `director = true` attribute + director pass |
| `ManagedClassBinding` / `ManagedFieldBinding` | Handle marshaller metadata |
| `StandardLibraryBindingConfiguration` | Built-in C++ marshaller headers |
| `ConfiguredTypeBinding` (C# config object) | `[[sharpcpp::managed_type(...)]]` attributes |
| `ConfiguredFunctionBinding` (C# config for injected members) | `[[sharpcpp::expose_member(...)]]` attributes |
| Degrade unsupported surface pass (1300+ lines) | Narrow validation/degradation passes over explicit facts (`no metadata`, `virtual base`, invalid callback/director combo, etc.) |
| String-surface detection methods (50+ in renderers) | `public_type` / `marshaller_type` from marshaller metadata |
| Contiguous-buffer SourceKind dispatch (5+ locations) | Each container type has its own `Marshaller<T>` |
| Callback marshalling duplication (420+ lines) | Same `MarshalUsing` path as non-callback |

## Renderer complexity targets

| Renderer | Target | Approach |
|---|---|---|
| CSharpBindingsRenderer | ~1500 lines | Read marshaller metadata per parameter/return, emit `LibraryImport` + `MarshalUsing`, no feature detection |
| CppBindingsRenderer | ~1000 lines | Read marshaller metadata, emit thunks that call `ParameterMarshaller<T>` / `ReturnMarshaller<T>`, no feature detection |
| Adding a new type category | Add 1 marshaller specialization | No renderer changes |

## Design decisions

1. **The pass pipeline stays.** V2 removes feature-branching from renderers, not structural shaping from passes. Parser facts, marshaller facts, shaping passes, and validation remain distinct stages. See [[09-pass-boundaries]].

2. **Generated C# marshallers come from a fixed contract.** The core may emit marshaller code only from generator-owned templates and explicit metadata. It does not translate arbitrary C++ marshaller bodies into C#. See [[07-marshaller-contract]].

3. **`CodeWriter` stays.** Continue using interpolated raw strings with brace-inferred indentation. The simplification goal is fewer branches, not a new emitter model.

4. **`ConfiguredTypeBinding` leaves the core binding model.** Foreign/system-header edge cases may still need composition-layer include hints or metadata imports, but not a parallel semantic configuration object inside the core AST.

5. **Imported metadata is first-class.** Metadata export/import, schema versioning, and merge precedence are core contracts, not optional extras. See [[08-metadata-contract]].

6. **Runtime identity uses complete-object handles.** Runtime type IDs are local dispatch tokens only. Exported metadata and cache keys must not depend on serialized `typeid(...).hash_code()` values. See [[10-ownership-lifetime]] and [[04-type-handling]].

7. **Project/module boundaries must reflect stage boundaries.** The implementation should split by responsibility so parser, passes, renderers, metadata, and support runtime can evolve independently. See [[11-project-shape-blueprint]].

8. **The rewrite runs in a parallel v2 line.** New projects may copy/adapt useful v1 pieces, but must not remain structurally dependent on v1 code or build identities. See [[13-rewrite-strategy]].

## Remaining research spikes

1. Which structural-family marshaller helpers belong in generated code versus the support runtime?
2. Whether Phase 2 instantiation should be sharded per closure hash for better incremental throughput.

---

← [[05-configuration-attributes]] | [[07-marshaller-contract]] →
