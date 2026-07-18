# rbfx/

## Responsibility

The rbfx integration layer bridges SharpCpp's generic C++/C# binding generation pipeline to the rbfx game engine. It owns the complete end-to-end build flow for generating, compiling, and deploying rbfx-specific managed bindings, native glue libraries, and user-code extensions. This directory is the composition boundary where SharpCpp's library components (Model, Metadata, Parsing, Pipeline, Rendering, Support) are instantiated into a working binding generator for a concrete C++ SDK.

## Design

### Two-Phase Generation Architecture

The integration implements a **phase-1 / phase-2** generation model:

- **Phase 1 (SharpCpp.Rbfx.Generator)**: Parses the installed rbfx SDK headers via ClangSharp, applies the bootstrap pipeline, and emits C# bindings, C++ native glue, and a `Bindings.json` metadata document. The metadata document captures the shape of all SDK types and serves as the import contract for phase 2.

- **Phase 2 (Rbfx.UserCode.Generator)**: Parses user-authored C++ headers (e.g., `RotatingComponent.hpp`), imports the phase-1 metadata, and generates bindings for user types that reference SDK types. This allows user types to extend SDK classes while enjoying full managed projection.

### Build-Time Code Generation

`SharpCpp.slnx` is the primary build graph. The `SharpCpp.Native` SDK-style
orchestration project references both rbfx generators, then invokes one
`sharpcpp-native` CMake aggregate after those ordinary managed dependencies have
built. CMake runs the prebuilt generator DLLs and compiles generated C++ thunks;
the binding projects discover generated C# immediately before `CoreCompile`.

### Staleness Management

The shared `cmake/SharpCppGeneratedStaleness.cmake` helper compares each
generator DLL timestamp against its generation stamp. If the tool changed, the
corresponding generated directory is cleared before regeneration.

### Runtime Dependency Deployment

A CMake template (`CopyRbfxRuntimeDependencies.cmake.in`) uses `file(GET_RUNTIME_DEPENDENCIES)` to discover and copy rbfx SDK shared libraries (`libUrho3D.so`, transitive deps) into the binary output directory alongside the generated native library. This ensures that managed test runners and the application shell can locate native symbols at load time.

### CMake Target Topology

```
SharpCpp.Rbfx.Generator ─┐
Rbfx.UserCode.Generator ─┼─> SharpCpp.Native (MSBuild)
SharpCpp.Tool ───────────┘             |
                                      v
                              sharpcpp-native (CMake)
                               /       |        \
              SharpCppTestsE2E_Native  |  sharpcpp-generate-rbfx-usercode
                                       v        |
                          sharpcpp-generate-rbfx v
                                       |  SharpCppRbfxUserCode_Native
                                       v
                              SharpCppRbfx_Native
```

## Flow

### End-to-End Generation Pipeline

1. **SDK Discovery**: `RBFX_SDK_ROOT` points to an installed rbfx SDK. `find_package(Urho3D)` locates the SDK CMake config. Build flags, include directories, and compile definitions are recursively collected from the `Urho3D` CMake target into `RBFX_URHO3D_INCLUDE_DIRECTORIES`, `RBFX_URHO3D_COMPILE_DEFINITIONS`, `RBFX_URHO3D_COMPILE_OPTIONS`.

2. **Flags File Generation**: A per-config `urho3d-sdk-flags.txt` is written with `compiler_path`, `language_standard`, include directories, defines, and clang arguments. This file is consumed by both generators.

3. **Phase 1 Generation**: `SharpCpp.Rbfx.Generator.dll` is invoked with `--sdk-root`, `--flags-file`, `--generated-root`, `--project-root`, `--sdk-library`, `--log-file`. It:
   - Loads SDK parse flags
   - Discovers the host Clang toolchain (target triple, resource directory, system include paths)
   - Builds Clang arguments (standard, include dirs, defines, SIMD disable, warning suppress, toolchain flags)
   - Parses `Urho3D/Urho3DAll.h` via `ClangSharpHeaderParser`
   - Applies `BindingCompilationBootstrapPipeline` for marshaller rewrites
   - Filters functions not exported by the linked SDK library via `nm -D --demangle` symbol inspection
   - Renders C#/C++ output files via `BindingOutputRenderer`
   - Exports `SharpCpp.Rbfx.Bindings.json` metadata
   - Writes a `generate.stamp` to signal completion

4. **Phase 1 Native Build**: `SharpCppRbfx_Native` compiles the generated `.g.cpp` files with Unity (Jumbo) build, links against `sharpcpp-native-support` and `Urho3D`, and copies SDK runtime dependencies post-build.

5. **Phase 1 Managed Build**: after the `SharpCpp.Native` dependency completes,
   `SharpCpp.Rbfx.Bindings.csproj` includes generated `.g.cs` files immediately
   before compilation.

6. **Phase 2 Generation**: `Rbfx.UserCode.Generator.dll` is invoked with `--sdk-root`, `--flags-file`, `--user-code-root`, `--generated-root`, `--import-binding-metadata` (pointing to phase 1 metadata). It:
   - Loads phase-1 binding metadata as imports
   - Parses `RbfxUserCode/RotatingComponent.hpp` (and future headers)
   - Applies the bootstrap pipeline with phase-1 types available as imported references
   - Renders output and exports its own metadata

7. **Phase 2 Native + Managed Build**: same ordering as phase 1; the managed
   assembly references `SharpCpp.Rbfx.Bindings`, `SharpCpp.Support`, and the
   build-only native orchestrator.

8. **E2E Tests**: `dotnet test SharpCpp.slnx` runs the managed tests. Generated
   runsettings point Linux tests at the shared `build/bin/<Configuration>` native
   output directory.

9. **Application Shell**: `Rbfx.Application` is an ordinary solution project
   referencing both binding assemblies.

## Integration

| Consumer | What it depends on | Mechanism |
|---|---|---|
| `SharpCpp.Rbfx.Generator` | `SharpCpp.Model`, `SharpCpp.Metadata`, `SharpCpp.Parsing`, `SharpCpp.Pipeline`, `SharpCpp.Rendering` | .NET project references |
| `SharpCpp.Rbfx.Bindings` | `SharpCpp.Support`, `SharpCpp.Native`, generated `*.g.cs` | .NET project references + late generated-source inclusion |
| `SharpCppRbfx_Native` | `SharpCpp::NativeSupport`, `Urho3D` SDK target, generated `*.g.cpp` | CMake `target_link_libraries` + Unity build |
| `Rbfx.UserCode.Generator` | Same pipeline libraries as rbfx generator | .NET project references |
| `Rbfx.UserCode.Bindings` | `SharpCpp.Support`, `SharpCpp.Rbfx.Bindings`, `SharpCpp.Native`, generated `*.g.cs` | .NET project references + late generated-source inclusion |
| `SharpCppRbfxUserCode_Native` | `SharpCpp::NativeSupport`, `Urho3D`, user `.cpp` files, generated `.g.cpp` | CMake `target_link_libraries` + Unity build |
| `Rbfx.Application` | `SharpCpp.Rbfx.Bindings`, `Rbfx.UserCode.Bindings` | .NET project references |
| `SharpCpp.Rbfx.Tests.E2E` | `SharpCpp.Rbfx.Bindings`, xUnit, .NET Test SDK | .NET project + package references |
| `rbfx/CMakeLists.txt` | `rbfx-sdk` installed at `RBFX_SDK_ROOT` | `find_package(Urho3D CONFIG)` |

### Directory Map

| Path | Role |
|---|---|
| `SharpCpp.Rbfx.Generator/` | Phase-1 SDK binding generator (ClangSharp-based, pipeline-enabled) |
| `SharpCpp.Rbfx.Bindings/` | Phase-1 managed assembly container (generated C# consumed via wildcard) |
| `SharpCpp.Rbfx.Tests.E2E/` | End-to-end xUnit tests exercising generated SDK wrappers |
| `Rbfx.UserCode/` | Phase-2 user-code generation + compilation (generator, bindings, native sources) |
| `Rbfx.Application/` | Application shell composing rbfx + user-code bindings |
| `CheckGeneratedStaleness.cmake` | Generator staleness checker (timestamp-based invalidation) |
| `CopyRbfxRuntimeDependencies.cmake.in` | Runtime library deployment script template |
