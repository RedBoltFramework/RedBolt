# 01 — Architecture

## Design invariant

Every non-blittable type that crosses the C++/C# boundary has a `Marshaller<T>` specialization. The generator core reads declared metadata and emits code. It does not branch on type categories.

`Marshaller<T>` is the unit of marshaling configuration, but it is not the only source of truth. V2 stays maintainable by keeping each truth source narrow and explicit.

## Sources of truth

| Source | Owns | Does not own |
|---|---|---|
| Native AST shape | declarations, overloads, constness, indirection, inheritance, virtual-ness | managed type names, ABI types, container surfaces |
| `Marshaller<T>` metadata | public type, ABI type, marshaller types, injected members, projection policy | symbol discovery, ownership of native declarations |
| Use-site attributes | local overrides such as `count`, `string_binding`, `in/out/ref`, callback user-data | global type policy |
| Imported binding metadata | upstream managed identities, suppress-emission, reuse contracts | runtime dispatch identity |
| Support runtime | callback ABI, instance caching, keep-alives, type registry, disposal hooks | per-project binding policy |

## Pipeline

```text
Headers + Marshaller<T> specializations
    ↓
Phase 1: Parse (ClangSharp → BindingCompilation)
    ↓
Phase 2: Instantiate (read Marshaller<T> attributes → populate metadata)
    ↓
Passes (reshape BindingCompilation in place, using marshaller metadata)
    ↓
Renderers (emit LibraryImport + MarshalUsing + C# wrapper types + C++ thunks)
```

## The uniform model

The renderer asks one question per parameter/return: **what does the marshaller say?**

```csharp
// Renderer logic (schematic)
foreach (var param in function.Parameters)
{
    var marshaller = param.Marshaller;
    writer.Write($"{marshaller.PublicType} {param.Name}");

    if (marshaller.HasCustomMarshalling)
        writer.Write($"[MarshalUsing(typeof({marshaller.MarshallerType}))]");
}
```

No string branches. No contiguous-buffer branches. No callback branches. The renderer consumes already-derived facts; it does not rediscover policy.

## Core vs configuration

| Core (small, rarely changes) | Configuration (large, extensible) |
|---|---|
| Parse C++ headers into AST | `Marshaller<T>` specializations in C++ headers |
| Read `[[sharpcpp::managed_type(...)]]` attributes | Attribute vocabulary (see [[05-configuration-attributes]]) |
| Read `[[sharpcpp::expose_member(...)]]` attributes | Built-in stdlib marshallers (string, vector, optional, etc.) |
| Emit `LibraryImport` signatures | User-defined marshallers for custom types |
| Emit `MarshalUsing` annotations | `callback = true`, `[[sharpcpp::director]]`, and per-use-site attributes |
| Emit C# wrapper types from marshaller metadata | `[[sharpcpp::count(...)]]`, `[[sharpcpp::string_binding(...)]]` |
| Emit C++ thunks that call into `Marshaller<T>` | Marshaller base classes (`ValueMarshallerBase`, etc.) |
| Pass infrastructure | Per-project pass extensions |
| Diagnostic collection | Diagnostic suppression / configuration |

## Key architectural decisions

### 1. Marshaller is the unit of configuration

Every type that crosses the boundary has a `Marshaller<T>` specialization. The specialization carries:
- `cpp_type`, `abi_type` — C++ type identities
- `[[sharpcpp::managed_type(...)]]` attributes declaring managed-side types and marshallers
- Optional `[[sharpcpp::expose_member(...)]]` static methods declaring injected members
- Optional `MarshallerSelector` specializations for context-dependent marshalling

The C++ header is the single source of truth. No separate C# configuration objects.

### 1a. C++ matching is AST-first, not string-first

V2 should take full advantage of the C++ AST.

- type matching should use canonical/desugared AST type identity
- declaration relationships should use AST links, not spelling heuristics
- inheritance, virtual dispatch, alias resolution, and template specialization matching should come from AST structure
- textual type names are for diagnostics, serialization, and human-readable metadata only

String comparisons against raw C++ spellings should be a last resort compatibility path, not a normal semantic mechanism.

### 2. LibraryImport + MarshalUsing

Generate `[LibraryImport]` with `[MarshalUsing(typeof(TMarshaller))]` annotations. The .NET runtime's source-generated marshalling handles the conversion. No hand-rolled `ConvertToManaged`/`ConvertToUnmanaged` in generated C# — the marshaller type does it.

### 3. Callbacks reuse the same marshaller machinery

Callback signatures use `ParameterMarshaller<T>` and `ReturnMarshaller<T>` — the same `MarshallerSelector` path as non-callback parameters and returns. The C++ side already does this via `InvokeWithSignature`. The C# side matches via `MarshalUsing` on delegate parameters.

### 4. Directors are attribute-configured

A `[[sharpcpp::director]]` attribute on a class marks it as director-capable. The parser records this. A pass generates virtual-method-slot metadata. The renderer emits director C++ class and C# override infrastructure from that metadata.

### 5. Container surfaces are declared by marshaller attributes

`std::vector<T>` may surface as `Std.Vector<T>`. `std::span<T>` may surface as `ReadOnlySpan<T>` or `ArrayView<T>`. `std::array<T,N>` may surface as `T[]` or `ArrayView<T>`. The choice is in the `public_type` attribute field.

### 6. Strings follow the same marshaller path

Three string surfaces, all through the uniform marshaller path:
- **Span** — `ReadOnlySpan<byte>` via `Utf8SpanMarshaller`
- **Wrapper** — `StdString` inheriting `StringBase` via `Utf8StringMarshaller`
- **`string`** — managed string via `Utf8StringMarshaller`

No `string_kind` attribute. No string-specific code paths. Different surfaces are different marshaller specializations with different `public_type` values.

### 7. Runtime identity is based on complete-object handles

Managed wrapper identity is keyed by the normalized complete-object handle, not the incoming static-view pointer. Multiple-inheritance base views must normalize through `dynamic_cast<void*>(self)` before cache lookup or runtime-type dispatch.

Runtime type IDs are dispatch tokens only. They are process-local, support-runtime-local values and must never be serialized into exported metadata or used as stable build-time identity.

### 8. Ownership and lifetime are explicit cross-cutting contracts

Borrowed handles, owned handles, ref-counted handles, callback state, pinned string buffers, and keep-alive control blocks are all part of one ownership model. V2 documents those transitions explicitly instead of letting each feature invent its own lifetime story. See [[10-ownership-lifetime]].

## Stage ownership

| Stage | Produces | May mutate | Must not do |
|---|---|---|---|
| Phase 1 parse | native declaration tree + structural facts | AST shape | invent managed policy |
| Phase 2 instantiation | resolved marshaller metadata | metadata on existing nodes | reshape surface |
| Passes | synthetic members, naming, projected members, validation results | AST + metadata | infer new ABI policy in renderers |
| Renderers | C#, C++, metadata files | nothing upstream | reinterpret unsupported cases |

The consequence: if a renderer needs a fact, some earlier stage must materialize it explicitly.

## Illustrative helper shape

The plan fixes **contracts and invariants**, not exact helper names or support-API layouts.

Examples such as `CreateManagedWrapper`, `CreateManagedWrapperWithKeepAlive`, `NativeObjectRegistry`, `Utf8StringMarshaller`, or specific support-base method names are illustrative shapes taken from current experience. They are not a hard requirement on the final v2 implementation.

What is fixed:

- one normalized runtime identity path
- one explicit marshaller contract
- one explicit metadata contract
- one explicit ownership / lifetime model
- clear stage boundaries

What remains implementation-defined until build-out:

- exact helper method names
- exact support-runtime type names
- whether a helper lives in generated code or support runtime
- small API surface/layout choices that do not change the above contracts

## Layer dependencies

```text
Consumer layer (rbfx generator, test generators)
    ↓
Composition layer (pipeline assembly, project wiring)
    ↓
Implementation layer (ClangSharp parser, passes, renderers)
    ↓
Contract layer (parser/pass/renderer contracts)
    ↓
Foundation layer (AST nodes, diagnostics, utilities, metadata keys)
```

Renderers depend only on marshaller metadata, not on type-category detection logic.

## Reading guide

- [[00-rationale]] — design principle and what not to do
- [[02-marshaller-system]] — the marshaller model
- [[03-two-phase-parsing]] — how parsing works
- [[04-type-handling]] — how each type category uses marshallers
- [[05-configuration-attributes]] — the attribute vocabulary
- [[06-core-minimal]] — what stays in core vs configuration
- [[07-marshaller-contract]] — exact extractable marshaller surface
- [[08-metadata-contract]] — versioned export/import contract
- [[09-pass-boundaries]] — stage ownership and pass rules
- [[10-ownership-lifetime]] — unified lifetime model
- [[11-project-shape-blueprint]] — proposed repo/module layout
- [[12-generated-output-and-module-api]] — output file contract and cross-module import/export
- [[13-rewrite-strategy]] — parallel independent v2 implementation approach
- [[14-diagnostics-and-failure-model]] — stage-specific failure behavior
- [[15-compatibility-policy]] — generator/schema/runtime compatibility rules

---

← [[00-rationale]] | [[02-marshaller-system]] →
