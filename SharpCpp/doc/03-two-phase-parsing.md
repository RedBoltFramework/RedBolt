# 03 — Two-Phase Parsing

The generator uses two-phase parsing: first parse the C++ API, then instantiate marshaller templates to read configuration.

## Phase 1: API parse

Parse all configured C++ headers through ClangSharp. Produce a `BindingCompilation` that mirrors the C++ declaration tree: namespaces, records, enums, functions, fields, aliases.

### What Phase 1 captures

- Record declarations (classes, structs) with members
- Enum declarations
- Free functions and methods
- Field declarations
- Type alias / typedef chains
- `[[sharpcpp::director]]` attributes on records
- `[[sharpcpp::count(...)]]` attributes on parameters and fields
- inferred parameter pass kinds from native signature shape
- `[[sharpcpp::in]]`, `[[sharpcpp::out]]`, `[[sharpcpp::ref]]` pass-kind overrides
- `[[sharpcpp::string_binding(...)]]` on parameters
- `[[sharpcpp::callback_user_data(...)]]` on parameters

### What Phase 1 does NOT capture

- `Marshaller<T>` specialization details (these require explicit instantiation)
- Managed type names (declared on marshaller specializations)
- ABI type mappings (declared on marshaller specializations)
- Marshaller type names (declared on marshaller specializations)
- Injected members (declared on marshaller static methods)

Phase 1 records which types appear in the API but does not yet know how they marshal.

### Marshaller index lookup

During Phase 1, when the parser encounters a C++ type, it checks whether a `Marshaller<T>` specialization exists for that type. This should be done through a marshaller index keyed by canonical AST type identity, not by raw type-name spelling.

The registry is populated from:
1. Built-in stdlib marshallers (declared in SharpCpp support headers)
2. User-project marshallers (declared in project headers or extra include paths)
3. Imported binding metadata (from upstream generated metadata files)

When a type matches, the parser creates a `BindingType` with a reference to the marshaller, but does not yet resolve the marshaller's attributes.

### AST-first rule

Phase 1 should prefer AST structure over text wherever possible:

- canonical/desugared `CXType` identity over source spelling
- declaration/cursor relationships over qualified-name string matching
- alias/typedef resolution through AST links over manual text normalization
- specialization matching through instantiated AST types over template spelling comparisons

Textual type names still matter for diagnostics and exported metadata, but they should not be the primary semantic key inside the pipeline.

## Phase 2: Marshaller instantiation

After Phase 1 produces the `BindingCompilation`, Phase 2 builds a temporary `.cpp` file that explicitly instantiates `Marshaller<T>` for every concrete type that appears in the API:

```cpp
// Generated temp file
#include <SharpCpp/Marshaller.hpp>
#include <project/header1.hpp>
#include <project/header2.hpp>

template struct sharpcpp::interop::Marshaller<MyClass>;
template struct sharpcpp::interop::Marshaller<std::string>;
template struct sharpcpp::interop::Marshaller<std::vector<int>>;
// ... one per concrete type
```

Clang parses this file. The explicit instantiations force the compiler to evaluate each specialization (including partial specializations). The parser visits the instantiation cursors and extracts:

1. **`[[sharpcpp::managed_type(...)]]` attributes** — managed type name, ABI type name, marshaller type name, context-specific overrides, managed base type, suppress-emission flag.

2. **`[[sharpcpp::expose_member(...)]]` attributes** — on static methods within the specialization, declaring members to inject into the managed wrapper.

3. **Type aliases** — `cpp_type`, `abi_type`, `return_abi_type` (if present) from the specialization body.

### Performance considerations

- Restrict traversal to temp-file cursors only (not the entire std namespace)
- Check for fatal parse errors before traversal, abort early if present
- Use canonical/desugared AST type identity as the semantic key to avoid duplicate instantiations from typedef aliases
- Derive stable textual identifiers from AST-resolved canonical types only when needed for export/debug output
- Cache instantiation results by closure hash so incremental builds reuse Phase 2 output

### Phase 2 cache key inputs

Phase 2 is cacheable only if all inputs are part of the fingerprint:

- canonical instantiation closure
- project headers and marshaller headers
- clang args / target triple / defines / include paths
- support runtime version
- imported metadata schema versions and content hashes
- SharpCpp attribute vocabulary version

Any change to those inputs invalidates the instantiation cache.

### Generic type instantiations

For generic types like `Marshaller<std::vector<T>>`, the partial specialization carries the attribute. But Clang cannot extract attributes from uninstantiated partial specializations — they need concrete type arguments.

The `CollectGenericInstantiationsPass` discovers which concrete instantiations are needed from:
1. Type references in the API
2. Manual instantiation configuration
3. Auto-discovery from return types and parameter types

Phase 2 includes all discovered concrete instantiations.

## After Phase 2: metadata population

Phase 2 results are written back onto the `BindingCompilation`:

- Each `RecordBinding` whose type has a marshaller gets `MarshallerMetadata` with: public type, ABI type, marshaller type, context overrides, injected members.
- Each `FunctionBinding` parameter gets its marshaller metadata resolved.
- Injected members from `[[sharpcpp::expose_member(...)]]` become `FunctionBinding` nodes on the record.

After metadata population, the pass pipeline runs and uses this metadata for shaping decisions.

At this point the binding model has four explicit fact classes:

1. native AST facts from Phase 1
2. resolved marshaller metadata from Phase 2
3. use-site override metadata from Phase 1 attributes
4. imported metadata facts from upstream binding packages

Renderers consume the merged result. They do not recompute policy.

## Phase 2 output: the metadata record

```csharp
// Simplified schema
record MarshallerMetadata(
    string CppTypeName,           // Stable serialized textual form of canonical C++ type identity
    string PublicTypeName,        // Managed public type (e.g. "Std.Vector<T>", "ReadOnlySpan<byte>")
    string AbiTypeName,           // ABI type (e.g. "Utf8StringAbi", "OptionalValueAbi<T>")
    string MarshallerTypeName,    // C# marshaller type for MarshalUsing
    string? ParameterMarshaller,  // Override for parameter context
    string? ReturnMarshaller,     // Override for return context
    string? ManagedBaseType,      // Base class override (e.g. "VectorBase<T>")
    bool SuppressEmission,       // Don't emit the managed type itself
    ImmutableArray<InjectedMember> InjectedMembers
);
```

This replaces a large configuration object with a flat, explicit schema driven by the C++ attribute.

## Metadata export and import contract

Phase 2 metadata is not just an in-memory convenience. It is a first-class exported contract used by downstream generators.

Every exported metadata file must carry:

- schema version
- generator version
- support runtime version
- source identity / package identity
- canonical AST-backed type identities
- imported metadata dependencies
- content hash / fingerprint inputs used to produce it

Merge precedence is explicit:

1. local project metadata
2. imported upstream metadata
3. built-in fallback metadata

Runtime-only values such as `typeid(...).hash_code()` are never exported.

See [[08-metadata-contract]] for the full contract.

## Reading guide

- [[02-marshaller-system]] — what the metadata is used for
- [[05-configuration-attributes]] — the attribute vocabulary that populates the metadata
- [[08-metadata-contract]] — versioning, provenance, merge rules
- [[09-pass-boundaries]] — who owns which derived facts

---

← [[02-marshaller-system]] | [[04-type-handling]] →
