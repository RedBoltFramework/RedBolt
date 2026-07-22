# C++ Lifetime and Copy-Semantics Verification Inventory

Generated from refreshed jcodemunch indexes: 2026-07-22

## Status

This is the pending portion of the exhaustive **future inspection inventory** of the currently hand-authored managed port. Rows are removed once constructors, destruction, and copy/assignment have all been verified. The remaining rows are not claims that semantic parity has been verified and are not a list of accepted deviations. The rbfx C++ implementation remains the only authority.

The repository indexes were refreshed before generating this inventory. Candidate counterparts were resolved from `Source/Urho3D` and `Source/Samples` by matching relative path first and basename second. Every unresolved entry must be searched again by class name, namespace, callers, and related classes before inspection.

## Scope

Included:

- `src/RedBolt/**/*.cs`
- `src/RedBolt.DearImGui/**/*.cs`
- `samples/**/*.cs`

Excluded:

- Generated native bindings and generated/build-output files.
- Tests and vendored third-party code.
- Global aliases/usings files that contain no ported class implementation.

Pending managed files inventoried: **0**
Pending files with likely C++ counterpart paths: **0**
Pending files requiring counterpart discovery: **0**

## Required review for every row

### Constructors

- [ ] Open the complete C++ class declaration and record bases and every field in declaration order.
- [ ] Read all constructor declarations and complete bodies, including delegated constructors, base initialization, initializer lists, default member initializers, and relevant call sites.
- [ ] Verify C# preserves the observable C++ initialization order: bases first, then fields in declaration order, then constructor body.
- [ ] Verify every overload, default value, side effect, ownership decision, and copy/move performed during construction.
- [ ] Identify implicit/defaulted constructors that may not appear as out-of-line bodies.

### Destructors and automatic field destruction

- [ ] Read the complete C++ destructor declaration/body and teardown call path.
- [ ] Enumerate **every field whose destructor runs automatically in C++**, even when the containing destructor is empty, defaulted, or implicit.
- [ ] Classify each mapped field as value, borrowed alias, owned managed object, owned native wrapper, container of owned values, or non-owning pointer/reference.
- [ ] Verify the C# `Dispose()` performs destructor-body actions and explicitly disposes every owned/disposable mapped field at the corresponding lifetime boundary.
- [ ] Verify reverse member-destruction order, derived-before-base teardown, callback/event unregistration, container clearing, and native release behavior.
- [ ] Verify RAII locals and temporaries use deterministic C# scope and are neither retained too long nor disposed too early.

### Returns by copy and copy-producing expressions

- [ ] Read every complete C++ method body returning a class/struct/container/value alias by value.
- [ ] Inspect named local returns, conditional/comma expressions, aggregate/braced expressions, and getter results.
- [ ] Inspect pass-by-value parameters, local initialization, assignment, lambda captures, container insertion/emplacement, and algorithm callbacks.
- [ ] Read copy/move constructors and copy/move assignment operators of every involved type.
- [ ] At each C# call site, preserve independent value copies with generated copy constructors or `ICloneable.Clone()` only where C++ copies; preserve aliases where C++ uses pointer/reference semantics.
- [ ] For generated wrappers, distinguish borrowed native storage from owned copies before mutation, retention, assignment, return, or disposal.
- [ ] Do not assume C# struct assignment is sufficient when fields contain managed references or native handles.

## Inventory

No pending managed implementation files remain.

## Outcome notation for future passes

Add one indented note below the relevant row or in a dated follow-up section:

- `VERIFIED`: complete relevant C++ declarations, bodies, fields, and call paths match.
- `DEVIATION`: exact C++ → C# difference, with involved symbols and required faithful port.
- `ALLOWED`: exact AGENTS.md allowed-deviation clause.
- `BLOCKED`: searches performed and the missing authoritative C++ source.
- `N/A`: no class/member in that category, confirmed from the complete C++ class declaration.

## Completion rule

A file is complete only when all ported types in it have been reviewed, including nested types and implicit/defaulted special members. A green build or test is not verification. If C# already matches the initially inspected C++ method but a symptom remains, continue through the authoritative C++ call path until the actual missing or divergent port is found.
