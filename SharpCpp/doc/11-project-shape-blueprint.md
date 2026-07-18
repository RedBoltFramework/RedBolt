# 11 — Project Shape Blueprint

This document proposes a concrete repo/module layout for implementing v2.

It is a **blueprint**, not a frozen requirement. The hard requirement is responsibility split and dependency direction. Exact project names, folder names, and small packaging details can change during implementation.

During the rewrite, this blueprint applies to a **parallel v2 line**, not an in-place mutation of the v1 project graph. See [[13-rewrite-strategy]].

## Goals

The project shape should make these architectural rules hard to violate:

1. parser, pass pipeline, renderers, metadata, and support runtime are separate concerns
2. runtime support can version independently from generator composition
3. imported metadata is treated as a first-class artifact
4. tests can target each stage in isolation
5. project-specific policy lives at composition edges, not inside reusable core layers
6. v2 can coexist with v1 without target/package/output collisions

## Proposed top-level layout

```text
src/
  SharpCpp.Model/
  SharpCpp.Parsing/
  SharpCpp.Pipeline/
  SharpCpp.Rendering.CSharp/
  SharpCpp.Rendering.Cpp/
  SharpCpp.Metadata/
  SharpCpp.Tool/
  SharpCpp.Support/

include/
  SharpCpp/
  SharpCpp/detail/

tests/
  SharpCpp.Tests.Model/
  SharpCpp.Tests.Parsing/
  SharpCpp.Tests.Pipeline/
  SharpCpp.Tests.Rendering/
  SharpCpp.Tests.Metadata/
  SharpCpp.Tests.Support/
  SharpCpp.Tests.E2E.Generator/
  SharpCpp.Tests.E2E/

doc-v2/
```

## Parallel rewrite naming rule

While v1 and v2 coexist, v2 projects/targets/packages must use distinct identities.

Examples:

- managed projects: `SharpCpp2.*` or `SharpCpp.V2.*`
- CMake targets: `sharpcpp2-*`
- package/export namespace: `SharpCpp2::...`
- generated roots: `${CMAKE_BINARY_DIR}/generated-v2/...`

Exact spelling is implementation-defined. Distinctness is mandatory.

## Project responsibilities

### `SharpCpp.Model`

Owns stable binding model primitives:

- `BindingCompilation`
- AST node types
- type identity records
- canonical C++ type identity records / serializers
- metadata record types
- diagnostics model
- source references
- naming / provenance primitives

Must not know about:

- ClangSharp
- concrete pass scheduling
- code emission
- support runtime type names

This should be the lowest managed layer.

### `SharpCpp.Parsing`

Owns:

- ClangSharp integration
- phase 1 API parse
- phase 2 marshaller instantiation parse
- parse-time attribute extraction
- AST-first type matching / canonicalization services
- parse cache / closure hashing inputs

Depends on:

- `SharpCpp.Model`
- metadata contracts from `SharpCpp.Metadata`

Must not know about:

- concrete rendering syntax
- project-specific shaping policy

### `SharpCpp.Pipeline`

Owns:

- pass interfaces
- pass scheduling
- materialization / normalization / validation passes
- composition of parser output into renderer-ready model

Depends on:

- `SharpCpp.Model`
- `SharpCpp.Parsing`
- metadata contracts from `SharpCpp.Metadata`

Must not know about:

- `CodeWriter` details
- CLI argument parsing
- support runtime implementation details beyond declared contracts

### `SharpCpp.Rendering.CSharp`

Owns:

- managed code emission
- `LibraryImport` / `MarshalUsing` signatures
- wrapper/member/class/property emission
- generated C# marshaller template emission for structural families

Depends on:

- `SharpCpp.Model`
- renderer-ready facts from `SharpCpp.Pipeline`

Must not know about:

- ClangSharp
- pass scheduling
- project-specific discovery logic

### `SharpCpp.Rendering.Cpp`

Owns:

- native thunk emission
- callback/director thunk emission
- runtime metadata hook emission
- helper include selection driven by explicit metadata

Depends on:

- `SharpCpp.Model`
- renderer-ready facts from `SharpCpp.Pipeline`

Must not know about:

- pass scheduling
- arbitrary marshaller implementation internals

### `SharpCpp.Metadata`

Owns:

- metadata schema types
- schema versioning
- import/export serialization
- metadata merge rules
- provenance / fingerprint records

Depends on:

- `SharpCpp.Model`

Must not know about:

- code emission
- ClangSharp traversal

### `SharpCpp.Tool`

Owns composition and UX:

- CLI / MSBuild entrypoints
- project file loading
- pipeline wiring
- diagnostics presentation
- writing generated outputs / metadata outputs

Depends on:

- every reusable generator layer above

This is where project-specific build orchestration belongs.

### `SharpCpp.Support`

Owns managed runtime support:

- callback ABI types
- native object base types
- instance caches / registries
- keep-alive helpers
- built-in managed marshaller implementations
- support base classes like `StringBase`, `VectorBase<T>`

This is runtime code, not generator logic.

## Native support layout

The native runtime support remains header-first under `include/SharpCpp/`:

- `Marshaller.hpp`
- `detail/MarshallerSupport.hpp`
- optional specialized support headers (`OptionalSupport.hpp`, rbfx-specific helpers, etc.)

Rule: runtime support headers may expose fixed contracts the generator targets, but generator logic must not inspect arbitrary helper bodies.

## Build-system blueprint

MSBuild is the outer orchestrator. `SharpCpp.slnx` is the canonical restore,
build, and test entry point. Ordinary `ProjectReference` edges build generator
tools before the SDK-style `SharpCpp.Native` orchestration project invokes the
auxiliary CMake graph. Generated binding projects reference that orchestration
project with `ReferenceOutputAssembly="false"`, making generation and native
compilation explicit prerequisites of managed compilation.

CMake owns only native concerns:

- the reusable `SharpCpp::NativeSupport` interface target
- generation commands that invoke already-built generator DLLs
- generated native thunk libraries
- native runtime dependency deployment

CMake never restores or builds managed projects. Rule: CMake wires native
stages together; it must not become a second policy engine for binding semantics.

### CMake target classes

The combined build distinguishes these target classes explicitly:

1. **managed library/tool projects** — ordinary SDK-style C# projects
2. **native orchestration project** — `SharpCpp.Native`
3. **generation targets** — CMake commands invoking prebuilt tools
4. **native thunk library targets**
5. **the `sharpcpp-native` aggregate**

### Proposed core targets

```text
SharpCpp.Model
SharpCpp.Metadata
SharpCpp.Parsing
SharpCpp.Pipeline
SharpCpp.Rendering
SharpCpp.Tool
SharpCpp.Support
SharpCpp.Native
SharpCpp::NativeSupport   (CMake INTERFACE target)
```

Notes:

- managed projects use ordinary `ProjectReference` dependencies
- `SharpCpp.Native` is a build-only project reference from generated consumers
- `SharpCpp::NativeSupport` exports native include directories and compile features
- `add_subdirectory(SharpCpp ...)` exposes reusable native support without enabling tests or rbfx by default

### Proposed pipeline target pattern

Each bindings pipeline should expand to a predictable trio plus optional test target:

```text
<name>.Generator project   # MSBuild builds the generator assembly
sharpcpp-generate-<name>   # CMake invokes the prebuilt generator
<name> native target       # CMake compiles generated thunks
<name>.Bindings project    # MSBuild includes generated C# before CoreCompile
<name>.Tests project       # ordinary managed test project
```

This preserves a clean separation between:

- building the generator tool
- running generation
- compiling generated outputs

The only native aggregate required by MSBuild is `sharpcpp-native`. Managed and
test aggregation belongs to `SharpCpp.slnx` rather than duplicate CMake targets.

## Generated output layout

Generated outputs should stay under the build tree, not source tree:

```text
build/native/<preset>/generated/<pipeline-name>/
  <stem>.Bindings.g.cs
  <stem>.Bindings.g.cpp
  <stem>.Bindings.Aliases.g.cs
  <stem>.Bindings.json
```

Other build-state roots should also stay explicit:

```text
build/bin/<Configuration>/                  # managed and native runtime outputs
build/obj/managed/<Configuration>/<Project>/# managed compiler intermediates
build/obj/restore/<Project>/                # NuGet/MSBuild restore state
build/native/<preset>/                      # CMake state and generated sources
```

Rules:

- generated roots are per-pipeline
- normalized outputs are stable and deterministic
- metadata output is produced beside source outputs, not as a hidden side file
- source-tree generation is not the default workflow

The exact generated file contract and cross-module import rules are defined in [[12-generated-output-and-module-api]].

## Configure/preset blueprint

Presets are native-only conveniences. The `dev` preset enables the synthetic
E2E and rbfx development subtrees and writes to `build/native/dev`. The
`SharpCpp.Native` project configures that preset only when its CMake cache is
missing, then builds `sharpcpp-native` with the active MSBuild configuration.

## Consumer-facing build APIs

Managed consumers use direct `ProjectReference` entries to SharpCpp projects.
Central properties are overridable so a parent repository may place all SharpCpp
outputs in its own build tree.

Native consumers use the target API directly:

```cmake
add_subdirectory(SharpCpp "${CMAKE_CURRENT_BINARY_DIR}/SharpCpp" EXCLUDE_FROM_ALL)
target_link_libraries(MyBindings PRIVATE SharpCpp::NativeSupport)
```

When SharpCpp is embedded, the synthetic E2E and rbfx development graphs default
off. Embedding does not require the rbfx SDK or .NET and does not alter the
parent's `BUILD_SHARED_LIBS`, package-search paths, or imported configuration maps.

## Reuse rule for v1 build logic

Reuse from the current build is allowed by copy/adapt/re-own.

- acceptable: copy useful CMake helper structure, then simplify it under v2 ownership
- unacceptable: make v2 build correctness depend on v1 target graphs or v1 package identities remaining intact

The v2 CMake layer may start by borrowing good pieces from the existing `DotNet.cmake`, but the resulting module/target graph must stand alone.

## Native component details

The native side is not just a byproduct. The build shape must treat it as a first-class component:

- generated `.g.cpp` compiles into a normal shared library target
- support headers are exported through an interface/native support target
- generated native libraries link to any declared native support targets and consumer libraries
- compile features (`cxx_std_20`) and required include paths come from explicit target usage requirements

The generated native library target should be the only place where generated C++ enters the compile graph.

## Install / package blueprint

V2 should define install/package boundaries early:

### What gets packaged

- native support headers under `include/SharpCpp/`
- managed support runtime assemblies
- generator/tooling assemblies when distributed as a package/tool
- CMake integration files under `cmake/`
- metadata schema/runtime compatibility info

### What does not get packaged by default

- per-build generated binding outputs
- temporary template projects/state files
- E2E-only assets

### Install/export expectations

- exported CMake package should expose native support usage requirements
- managed consumers should get stable restore/build props/targets
- package contents must version support runtime and metadata schema together
- package names and export namespaces must remain distinct from v1 during parallel development

## CMake dependency direction

At the build-system level, the dependency graph should mirror the architecture graph:

```text
tool targets
    ↓
generation targets
    ↓
managed bindings target      native bindings target
            ↓                         ↓
          tests / packaging / install
```

If a new feature requires bespoke CMake orchestration that bypasses this shape, that is a sign the generator/runtime boundary is still too entangled.

## Dependency direction

```text
SharpCpp.Tool
    ↓
SharpCpp.Pipeline
    ↓
SharpCpp.Parsing        SharpCpp.Rendering.CSharp   SharpCpp.Rendering.Cpp
    ↓                            ↓                         ↓
                SharpCpp.Model   ←→   SharpCpp.Metadata

SharpCpp.Support   (runtime support, referenced by generated output; not a core dependency sink for parser/passes)
```

More concretely:

- `Model` has no dependency on generator implementation layers
- `Parsing` depends on `Model` and metadata contracts
- `Pipeline` depends on `Model`, `Parsing`, and metadata contracts
- each renderer depends on `Model` only
- `Tool` wires everything together
- `Support` is referenced by generated code, not used as an excuse for generator-layer coupling

## What should stay together

Do **not** over-split early. The minimum useful split is responsibility-based, not one-project-per-class.

Recommended early grouping if implementation cost matters:

1. `SharpCpp.Model`
2. `SharpCpp.Parsing`
3. `SharpCpp.Pipeline`
4. `SharpCpp.Rendering` (single project at first, split later if needed)
5. `SharpCpp.Metadata`
6. `SharpCpp.Tool`
7. `SharpCpp.Support`

That is enough to enforce the big boundaries without creating project-file sprawl.

## Test shape blueprint

### `SharpCpp.Tests.Model`

- AST invariants
- metadata record invariants
- identity / equality / serialization helpers

### `SharpCpp.Tests.Parsing`

- attribute extraction
- phase 1/phase 2 closure discovery
- imported metadata loading
- cache key invalidation inputs

### `SharpCpp.Tests.Pipeline`

- pass ordering
- surface materialization
- normalization / degradation / diagnostics
- ownership / projection / callback validation

### `SharpCpp.Tests.Rendering`

- golden-text tests for C# and C++ emission
- regression tests for helper selection / naming / signatures

### `SharpCpp.Tests.Metadata`

- schema versioning
- deterministic output
- merge precedence
- import/export round-trips

### `SharpCpp.Tests.Support`

- managed cache behavior
- callback ABI lifecycle
- keep-alive / disposal behavior
- built-in marshaller unit tests

### E2E remains essential

Keep both:

- `SharpCpp.Tests.E2E.Generator` for generation scenarios
- `SharpCpp.Tests.E2E` for compiled round-trip behavior

V2 should shift many current E2E-only regressions downward into unit/golden tests, but E2E remains the contract test for the whole pipeline.

## Consumer-specific extensions

Keep consumer-specific logic in consumer projects, not reusable core:

- rbfx-specific passes
- custom runtime support subclasses
- metadata imports from upstream generated bindings
- project-specific marshaller headers

That means a consumer package like rbfx should primarily contribute:

- marshaller headers
- support runtime additions
- composition-layer passes
- E2E fixtures

It should not require forking parser or renderer fundamentals for ordinary extension.

## Blueprint constraints

This blueprint is successful only if these remain true during implementation:

1. parser can evolve without touching renderers
2. renderers can evolve without ClangSharp knowledge
3. metadata schema can version independently of CLI UX
4. support runtime can evolve under an explicit compatibility contract
5. consumer-specific policy lands outside reusable generator layers

If a new feature forces edits across parser, passes, renderers, metadata, and support runtime for reasons other than its real cross-cutting nature, the project shape is still too entangled.

---

← [[10-ownership-lifetime]] | [[12-generated-output-and-module-api]] →
