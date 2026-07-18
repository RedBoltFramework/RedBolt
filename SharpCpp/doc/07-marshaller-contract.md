# 07 — Marshaller Contract

V2 only works if `Marshaller<T>` exposes a **small, explicit, extractable contract**. The generator must never depend on arbitrary C++ implementation details.

The contract in this document fixes semantics, not exact helper names. Support-runtime class names, generated helper names, and minor API shape details remain implementation-defined as long as they satisfy this contract.

## What the generator may read

The generator may read only these inputs from a marshaller specialization:

1. `[[sharpcpp::managed_type(...)]]` fields
2. named type aliases such as `cpp_type`, `abi_type`, and other fixed-schema aliases
3. `[[sharpcpp::expose_member(...)]]` attributes on static helper methods
4. a small set of named capability flags / traits with fixed semantics

The marshaller contract is resolved against AST-identified C++ types. The generator should not discover which marshaller applies by comparing raw type-name strings when canonical AST identity is available.

If a new feature needs a new fact, add a new attribute field or named trait. Do not teach the generator to inspect more arbitrary C++.

## What the generator must treat as opaque

- arbitrary function bodies inside `Marshaller<T>`
- template metaprogramming internals beyond named aliases / traits
- helper functions without SharpCpp attributes
- control flow inside support-library marshaller implementations
- native helper code used only by runtime support

## Required contract shape

At minimum, a marshaller surface must declare:

```cpp
template <>
struct [[sharpcpp::managed_type(
    public_type = "StdString",
    abi_type = "Utf8StringAbi",
    marshaller_type = "Utf8StringMarshaller"
)]]
Marshaller<std::string>
{
    using cpp_type = std::string;
    using abi_type = detail::Utf8StringAbi;
};
```

Optional contract elements:

- `parameter_marshaller_type`
- `return_marshaller_type`
- `managed_base_type`
- `projection`
- `polymorphic`
- `callback`
- `reuse_builtin_marshaller`
- `[[sharpcpp::expose_member(...)]]`

## C++ side contract

The native thunk layer is fixed:

- parameter conversion goes through `ParameterMarshaller<T>`
- return conversion goes through `ReturnMarshaller<T>`
- callbacks go through `MarshallerInvoker::invoke_callback_as(...)`
- field access goes through `MarshallerInvoker::get_field(...)` / `set_field(...)`

The generator assumes those entry points exist. It does not synthesize a new native calling convention per feature.

## C# side contract

Every `marshaller_type` named in metadata must resolve to one of two sources:

| Source | Used for |
|---|---|
| Support runtime | built-in surfaces such as strings, callbacks, handles, base container support |
| Generator-owned template emission | structural families such as optional / pair / tuple / per-instantiation helpers |

The generator does **not** translate arbitrary C++ marshaller bodies into C# `CustomMarshaller` code.

## Exposed member contract

`[[sharpcpp::expose_member(...)]]` is the only supported way for a marshaller to inject managed members.

Generator-readable facts:

- shape (`method`, `property_getter`, `property_setter`, `protected_override`)
- exposure flags
- static helper signature

Generator-opaque facts:

- helper implementation body
- custom control flow inside the helper

The helper body remains native code. The generator only projects its signature into the binding AST.

## Validation rules

Validation is fail-fast:

- missing required `managed_type` fields → error
- named marshaller type missing from support runtime / generated templates → error
- unsupported combination of capability flags → error
- `expose_member` helper with invalid shape/signature → error

Validation belongs to pre-render passes, not renderers.

## Why this matters

Without this contract, the plan collapses into "parse arbitrary C++ helper code and hope it means what we think it means". That is exactly the architectural weakness v2 is trying to remove.

---

← [[06-core-minimal]] | [[08-metadata-contract]] →
