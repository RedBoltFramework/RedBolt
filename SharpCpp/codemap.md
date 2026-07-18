# Repository Atlas: SharpCpp v3

## Project Responsibility

SharpCpp is a C++/C# binding generator for the **[rbfx game engine](https://github.com/rbfx/rbfx)**. It takes C++ headers annotated with marshaller attributes and produces compilable C# wrappers, C++ P/Invoke thunks, and serialized binding metadata — enabling managed code to call native C++ APIs with full type safety, polymorphic wrapping, virtual dispatch (director callbacks), generic/template projection, multiple-inheritance interfaces, and cross-module composition.

**Architecture**: Marshaller-as-configuration-unit — every type that crosses the managed/native boundary has a `Marshaller<T>` specialization. The generator core is small and uniform; type-specific behavior is declared through C++ attributes, not encoded in renderer branching.

**Bootstrap status**: This repo implements the ground-up v2 redesign, currently in active bootstrap phase. See `doc/` for architectural design docs and `PROGRESS.md` for current status.

## System Entry Points

| File | Purpose |
|------|---------|
| `SharpCpp.slnx` | Primary build entry point for managed projects, generation, and native prerequisites |
| `native/SharpCpp.Native/SharpCpp.Native.csproj` | MSBuild orchestration project that invokes the auxiliary native CMake aggregate |
| `CMakeLists.txt` | Embeddable native-only CMake entry point; always exposes `SharpCpp::NativeSupport` |
| `src/SharpCpp.Tool/Program.cs` | CLI composition root — `sample-metadata` and `generate-sample` commands |
| `rbfx/SharpCpp.Rbfx.Generator/Program.cs` | rbfx-specific generator entry point (phase-1) |
| `rbfx/Rbfx.UserCode/Rbfx.UserCode.Generator/Program.cs` | User-code generator (phase-2, imports phase-1 metadata) |

## Layer Architecture

```text
                ┌─────────────────────────────────┐
                │         SharpCpp.Tool           │  ──  Composition/CLI
                │  rbfx/SharpCpp.Rbfx.Generator   │  ──  rbfx generator
                │  rbfx/Rbfx.UserCode.Generator   │  ──  Phase-2 generator
                └───────────┬─────────────────────┘
                            │ depends on
                ┌───────────▼─────────────────────┐
                │       SharpCpp.Pipeline         │  ──  Pass orchestration
                ├─────────────────────────────────┤
                │       SharpCpp.Parsing          │  ──  ClangSharp C++ parser
                ├─────────────────────────────────┤
                │       SharpCpp.Metadata         │  ──  JSON export/import
                ├─────────────────────────────────┤
                │       SharpCpp.Rendering        │  ──  C#/C++ code gen
                └───────────┬─────────────────────┘
                            │ depends on
                ┌───────────▼─────────────────────┐
                │        SharpCpp.Model           │  ──  Foundation (AST, types, diagnostics)
                └─────────────────────────────────┘

  ┌───────────────────────────────────────────────────┐
  │     SharpCpp.Support (managed runtime lib)         │  ──  Linked by generated wrappers
  │     include/SharpCpp/ (native headers)             │  ──  Included by generated .g.cpp
  └───────────────────────────────────────────────────┘
```

## Directory Map

| Directory | Responsibility Summary | Detailed Map |
|-----------|----------------------|--------------|
| `src/SharpCpp.Model/` | **Foundation layer** — binding AST (BindingNode composite tree, RecordBinding, FunctionBinding, EnumBinding, etc.), type system (BindingType, MarshallerDescriptor), diagnostics, CodeWriter utility, metadata key constants | [View Map](src/SharpCpp.Model/codemap.md) |
| `src/SharpCpp.Metadata/` | **Serialization boundary** — schema-defined JSON metadata export and import, enabling cross-module composition (suppressed duplicate emission for imported types) | [View Map](src/SharpCpp.Metadata/codemap.md) |
| `src/SharpCpp.Parsing/` | **C++ parsing** — two-phase pipeline: Phase 1 drives ClangSharp to produce the BindingCompilation AST; Phase 2 reads `Marshaller<T>` attributes from C++ headers and back-patches managed type metadata | [View Map](src/SharpCpp.Parsing/codemap.md) |
| `src/SharpCpp.Pipeline/` | **Pass orchestration** — ordered sequence of transformation passes (normalization, inheritance, polymorphism, generic instantiation, degradation, validation) that mutate the AST in place to renderer-ready form | [View Map](src/SharpCpp.Pipeline/codemap.md) |
| `src/SharpCpp.Rendering/` | **Code generation** — stateless, pure rendering of BindingCompilation → C# wrapper classes, C++ P/Invoke thunks, and type metadata. Supports partial-class decomposition and group-based file splitting | [View Map](src/SharpCpp.Rendering/codemap.md) |
| `src/SharpCpp.Support/` | **Managed runtime** — NativeObject (SafeHandle), instance caches, polymorphic wrapper registry, keep-alive, NativeObjectMetadata, StringBase for UTF-8 string marshalling | [View Map](src/SharpCpp.Support/codemap.md) |
| `src/SharpCpp.Tool/` | **Composition root** — CLI entry point that wires Model → Parsing → Pipeline → Rendering → Metadata export, drives the `generate-sample` and `sample-metadata` commands | [View Map](src/SharpCpp.Tool/codemap.md) |
| `include/SharpCpp/` | **Native support headers** — header-only C++ interop layer: Marshaller<T> templates (cpp_type/abi_type mapping), MarshallerInvoker, P/Invoke export macros, OptionalValue<T> ABI bridge | [View Map](include/SharpCpp/codemap.md) |
| `rbfx/` | **rbfx game engine integration** — Two-phase generation pipeline with staleness management, CMake target topology, runtime dependency deployment, and E2E validation against the SDK | [View Map](rbfx/codemap.md) |
| `cmake/` | **Native helpers** — shared output placement and generated-source staleness handling |
| `eng/` | **Managed build infrastructure** — centralized output paths, native dependency wiring, and late generated-source inclusion |

## Native Integration Points

| System | Role |
|--------|------|
| `rbfx/SharpCpp.Rbfx.Bindings/` | Compiled rbfx wrapper assembly and native thunk library |
| `rbfx/SharpCpp.Rbfx.Generator/` | Phase-1 generator for rbfx SDK headers |
| `rbfx/Rbfx.UserCode/` | Phase-2 user-code generator + bindings, imports SDK metadata |
| `rbfx/Rbfx.Application/` | Application shell composition root, references both bindings |
| `rbfx/SharpCpp.Rbfx.Tests.E2E/` | E2E tests for generated rbfx wrappers |


## Build & Test

```bash
dotnet restore SharpCpp.slnx
dotnet build SharpCpp.slnx --configuration Debug --no-restore
dotnet test SharpCpp.slnx --configuration Debug --no-build --no-restore
```

All outputs stay below `build/`: managed/native runtime artifacts in
`build/bin/<Configuration>`, managed state in `build/obj`, and CMake state plus
generated sources in `build/native/<preset>`.

Native consumers may embed only the reusable support target without enabling
SharpCpp's development graph:

```cmake
add_subdirectory(SharpCpp "${CMAKE_CURRENT_BINARY_DIR}/SharpCpp" EXCLUDE_FROM_ALL)
target_link_libraries(MyNativeBindings PRIVATE SharpCpp::NativeSupport)
```

## Reading Guide

- **New to the project**: Start with `doc/01-architecture.md` and `doc/02-marshaller-system.md`
- **Working on a feature**: Read the relevant layer codemap above + `doc/09-pass-boundaries.md`
- **Adding a new rendered type**: Read `src/SharpCpp.Rendering/codemap.md` + `doc/06-core-minimal.md`
- **Adding a new pipeline pass**: Read `src/SharpCpp.Pipeline/codemap.md` + `doc/09-pass-boundaries.md`
- **Modifying the model**: Read `src/SharpCpp.Model/codemap.md` — all other layers depend on this
- **rbfx-specific work**: Read `rbfx/codemap.md` + `rbfx/SharpCpp.Rbfx.Generator/codemap.md`
- **Cross-module composition**: Read `doc/08-metadata-contract.md` + `doc/12-generated-output-and-module-api.md`
