# 00 — Rationale

## The problem

Adding a new type category or ABI shape to a C++/C# binding generator requires touching renderers in multiple places, understanding subtle interaction effects, and hoping nothing else breaks. This is because type-specific behavior lives in renderer branching logic instead of in a single, uniform configuration layer.

## The design principle

**The marshaller is the unit of marshaling configuration.**

Every C++ type that crosses the boundary has a `Marshaller<T>` specialization. The marshaller's C++ attributes declare everything the generator needs:

- What the managed type is
- What the ABI type is
- How to marshal in each context (parameter, return, callback, field)
- What extra members to inject
- What the managed base type is

The generator core is small and uniform. It reads native AST facts, marshaller metadata, and use-site attributes, then emits `LibraryImport` + `MarshalUsing` code. Type-specific behavior (strings, arrays, callbacks, inheritance) is configured through marshaller attributes and built-in marshaller specializations — not through renderer branching.

## What to do

- Configure wrapping behavior through `Marshaller<T>` attributes on C++ specializations
- Use `LibraryImport` + `MarshalUsing` for P/Invoke marshalling
- Let the `.NET` runtime's source-generated marshalling handle type conversion
- Use `[[sharpcpp::expose_member(...)]]` for member injection
- Use `[[sharpcpp::director]]` for director-based inheritance
- Use `MarshallerSelector` for context-dependent marshalling (parameter vs return)
- Keep renderers uniform: read metadata, emit code, no type-category branching

## What not to do

- Do not add type-category detection methods to renderers
- Do not branch on `IsString`, `IsContiguousBuffer`, `IsCallback`, etc. in rendering code
- Do not duplicate marshalling logic for callback parameters vs non-callback parameters
- Do not create separate configuration objects (`ManagedValueTypeInteropBinding`, `ManagedStringBinding`, `BindingContiguousBufferSourceKind`) alongside the marshaller attribute
- Do not hardcode library-specific policy in core renderers
- Do not scatter ABI-type decisions across multiple files

## Reading guide

- [[01-architecture]] — architecture overview
- [[02-marshaller-system]] — the central marshaller design
- [[03-two-phase-parsing]] — how parsing works
- [[04-type-handling]] — how each type category maps to marshallers
- [[05-configuration-attributes]] — the attribute vocabulary
- [[06-core-minimal]] — what stays in core vs configuration
- [[07-marshaller-contract]] — what the generator may read from `Marshaller<T>`
- [[08-metadata-contract]] — export/import schema and versioning
- [[09-pass-boundaries]] — stage ownership and pass contracts
- [[10-ownership-lifetime]] — identity, ownership, disposal, caching

---

← [[index]] | [[01-architecture]] →
