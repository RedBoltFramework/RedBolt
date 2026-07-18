# 12 — Generated Output & Module API

V2 must define not just how bindings are generated, but what a generated bindings **module** looks like on disk and how other modules consume it.

This document defines:

1. generated source file contract
2. built artifact contract
3. exported module API contract
4. downstream import contract

Helper names and package layouts may still evolve. The file roles and import/export semantics are the hard requirement.

## Output categories

Each binding pipeline produces two classes of artifacts:

### 1. Generated source artifacts

Produced during generation under the generated root.

### 2. Built module artifacts

Produced by compiling those sources plus support/runtime dependencies.

Downstream generators import **module artifacts and metadata**, not arbitrary generated source files from another module's build tree.

## Generated source file contract

Per pipeline, the generator emits this normalized source set:

```text
<generated-root>/
  <stem>.Bindings.g.cs
  <stem>.Bindings.Aliases.g.cs
  <stem>.Bindings.g.cpp
  <stem>.Bindings.json
```

### `<stem>.Bindings.g.cs`

Owns managed binding source:

- generated wrapper types
- generated `LibraryImport` signatures
- generated delegate types
- generated property/method/operator surfaces
- generated structural-family marshaller helpers, when owned by generator rather than support runtime

Must not contain:

- hand-authored extension logic
- user customization blocks
- project-specific orchestration logic

Rules:

- deterministic ordering
- stable namespace layout
- one-way generated file: overwrite on regeneration
- no hidden dependency on sibling generated files except the alias companion and support/runtime references

### `<stem>.Bindings.Aliases.g.cs`

Owns alias/project-reference glue only.

Typical contents:

- `extern alias` support
- project-reference alias forwarding helpers
- small compatibility glue required by generated code shape

Rules:

- keep this file narrowly scoped
- do not mix main wrapper emission into alias support

### `<stem>.Bindings.g.cpp`

Owns native thunk source:

- exported C ABI thunks
- callback/director bridge functions
- field/property access thunks
- runtime metadata functions such as complete-object handle/type resolution

Must not contain:

- project build orchestration
- arbitrary handwritten library logic
- duplicated support runtime implementation that belongs in native support headers/sources

Rules:

- generated C++ is the only generated source that enters the native compile graph
- include set must be explicit and deterministic
- no feature policy should be re-derived in the emitted source

### `<stem>.Bindings.json`

Owns exported binding metadata:

- schema/version information
- exported managed/C++ type identities
- imported dependency references
- build fingerprint inputs
- marshaller surface facts needed by downstream generators

This file is the **generator-facing export artifact** for downstream import.

## Built module artifact contract

After compilation, a module exports a built artifact set conceptually shaped like this:

```text
managed/
  <ManagedAssembly>.dll
  <ManagedAssembly>.pdb         # optional

native/
  <NativeLibrary>.(dll|so|dylib)

metadata/
  <stem>.Bindings.json
```

Packaging may flatten or reorganize those files, but the roles remain distinct:

- managed runtime-facing assembly
- native runtime-facing thunk library
- generator-facing metadata export

## Generated file format rules

### Text generation rules

All generated text artifacts must be:

- deterministic
- overwrite-safe
- path-stable under the same pipeline name/stem
- encoded consistently
- free of process-local values

### Naming rules

`<stem>` must be stable for a given bindings project identity.

The stem must not vary based on:

- machine-local temp paths
- build configuration
- runtime type IDs
- unordered discovery results

### Layout rules

Generated file naming distinguishes role, not feature:

- `.Bindings.g.cs` = main managed surface
- `.Bindings.Aliases.g.cs` = alias/reference glue
- `.Bindings.g.cpp` = native thunk source
- `.Bindings.json` = exported metadata/API contract

Do not create ad-hoc sibling files per feature unless the output contract is intentionally expanded and versioned.

## Exported module API contract

A generated bindings module exports three APIs at once:

### 1. Managed API

Consumed by .NET code through the built assembly.

This is the public wrapper surface:

- classes/structs/enums/delegates
- properties/methods/operators
- support-base inheritance

### 2. Native ABI API

Consumed by the generated managed assembly through `LibraryImport`.

This is the exported thunk surface from the native library:

- C ABI entrypoints
- callback/director bridge functions
- metadata/type-registry hook functions

### 3. Generator API

Consumed by other generator pipelines through metadata import.

This is the `.Bindings.json` contract:

- exported type identities
- emission suppression facts
- reusable marshaller surface facts
- upstream/downstream composition information

These three APIs version together but serve different consumers.

## Downstream import contract

When module `B` imports module `A`, it must consume all relevant upstream artifacts explicitly:

### Required imports

1. **metadata import**
   `A`'s `.Bindings.json`

2. **managed reference import**
   `A`'s built managed assembly identity/reference

3. **native support usage requirements**
   any CMake/native support targets required to compile `B`'s generated native library against compatible headers/runtime support

### Optional imports

- additional native libraries from `A` when the downstream module directly depends on them at runtime
- support-runtime package references when split from the main managed assembly distribution

### Import flow

```text
Upstream module A
  ├─ A.Bindings.json      -> imported by downstream generator
  ├─ A.Managed.dll        -> referenced by downstream managed project
  └─ A native/runtime requirements -> linked/included by downstream native build as needed

Downstream module B
  ├─ imports A metadata during generation
  ├─ references A managed assembly during managed compile
  └─ consumes A native support requirements during native compile
```

## Import semantics

Importing another module means:

- downstream does not re-emit upstream public wrapper types
- downstream may reference upstream managed types in generated signatures
- downstream may reuse upstream marshaller/export facts according to metadata contract
- local bindings may extend around upstream types, but must not silently fork their identities

## Suppress-emission behavior

`suppress_emission` exists specifically to support import composition.

Typical case:

- upstream module exports `VectorBase<T>` or an owned wrapper type
- downstream imports upstream metadata
- downstream references the managed type but does not emit a duplicate definition

This makes imported modules part of the generated API graph instead of forcing textual duplication.

## CMake-facing import model

At the build orchestration level, a downstream binding pipeline should consume upstream module exports through a declarative input set:

- imported metadata files
- imported managed references
- imported native support targets / link requirements

In other words, the build API should express imports like:

```text
sharpcpp_add_binding_pipeline(
  ...
  IMPORTED_BINDING_METADATA_FILES ...
  IMPORTED_MANAGED_REFERENCES ...
  IMPORTED_BINDINGS_TARGETS ...
)
```

The exact function spelling may evolve, but the three import channels are required.

## Versioning rules for imported modules

Downstream import is valid only when:

- metadata schema is compatible
- support runtime contract is compatible
- managed type identities match the imported metadata
- required native support contracts are available

If any of those are incompatible, the generator must fail with an import/version diagnostic rather than guessing.

## What not to do

- do not import raw generated `.g.cs` files from another module as source text
- do not treat built managed assemblies alone as sufficient generator input
- do not treat metadata alone as sufficient runtime input
- do not duplicate upstream wrapper types in downstream modules by default
- do not let build-local paths leak into exported module identity

## Relationship to other contracts

- [[08-metadata-contract]] defines the metadata payload contract
- [[10-ownership-lifetime]] defines runtime identity/lifetime invariants those outputs must preserve
- [[11-project-shape-blueprint]] defines where these artifacts live in the repo/build graph

---

← [[11-project-shape-blueprint]] | [[13-rewrite-strategy]] →
