# Single CMake Build

## Goal

RedBolt should have one top-level CMake project that builds every native component and drives every managed build step needed for development. Native dependencies should not require a preinstalled rbfx SDK.

## CMake Baseline

| Setting | Plan |
|---|---|
| Minimum CMake | `3.28`, matching SharpCpp. |
| Languages | `C`, `CXX`; managed builds are invoked through `dotnet`. |
| C++ standard | `C++20` for SharpCpp native support and generated native thunks. rbfx third-party dependencies may keep their own standards. |
| Output | `cmake-build/<preset>/bin/<config>` for native libraries and managed binaries. |
| Generated output | `cmake-build/<preset>/generated/<module>`. |
| Dependency source | Configurable `REDBOLT_RBFX_SOURCE_ROOT`, defaulting to `../rbfx`. |
| Build type | Multi-config friendly; Debug/Release/RelWithDebInfo mapped to .NET Debug/Release. |

## Top-Level Build Shape

```text
RedBolt CMake
  configure rbfx platform/options subset
  add rbfx Source/ThirdParty subdirectories needed by enabled RedBolt features
  add Diligent backend targets required by platform
  add RedBolt.Native generated-thunk shared library
  build RedBolt.Dependencies.Generator with dotnet
  run generator over dependency headers to produce bindings under build/generated
  build generated native thunks when the generated surface needs exports
  build RedBolt managed projects with generated C# included
  build and register samples/tests
```

## Why Not `add_subdirectory(SharpCpp)` Directly

The SharpCpp root CMake currently wires its own tests and rbfx SDK consumer targets. RedBolt should consume SharpCpp core as tooling, not inherit SharpCpp's rbfx package assumptions.

Initial RedBolt integration should do one of these:

| Option | Use |
|---|---|
| Preferred | Add a small `cmake/RedBoltSharpCpp.cmake` that invokes `dotnet build` on RedBolt generator projects which reference `SharpCpp/src/SharpCpp.*`. |
| Later cleanup | Extract reusable SharpCpp consumer CMake helpers into `SharpCpp/cmake/SharpCppConsumer.cmake` and include that from RedBolt. |

## Native Dependency Strategy

The original rbfx build already knows how to build the third-party dependency tree. RedBolt should reuse the source tree and relevant CMake setup, but gate aggressively.

| Dependency group | First-cube status | Notes |
|---|---|---|
| SDL | Required | Window creation, event polling, keyboard/mouse input. |
| Diligent | Required | Graphics device, swapchain, buffers, shaders, pipeline state. |
| EASTL | Indirect | Needed by rbfx-native code only if reused. C# runtime should not expose EASTL directly unless wrapping dependency APIs requires it. |
| fmt/spdlog | Optional | Native logging only; C# logging can start independently. |
| STB | Deferred | Texture loading after first cube. |
| PugiXml/rapidjson | Deferred | rbfx resource formats and configs after first cube. |
| FreeType | Deferred | Text/UI later. |
| Bullet/Box2D | Deferred | Physics later. |
| Recast/Detour | Deferred | Navigation later. |
| RmlUi/ImGui | Deferred | UI/editor later. |
| OpenSSL/libdatachannel/Civetweb | Deferred | Networking later. |
| glslang/SPIRV-Cross/SPIRV-Tools | Deferred | Shader translation after hardcoded shader path. |
| Tracy/capstone/zstd/nativefiledialog | Deferred | Profiling/tools/editor later. |
| OpenXRSDK | Deferred | XR later. |

## First-Cube CMake Options

RedBolt should start with feature options that default to the smallest useful desktop build.

| Option | Default | Meaning |
|---|---|---|
| `REDBOLT_ENABLE_ALL` | `OFF` | Do not mirror rbfx's broad default. |
| `REDBOLT_WITH_GRAPHICS` | `ON` | Required for first cube. |
| `REDBOLT_WITH_INPUT` | `ON` | Required for window close and Escape. |
| `REDBOLT_WITH_RESOURCES` | `ON` | Required for resource path and procedural resource registration. |
| `REDBOLT_WITH_SCENE` | `ON` | Required for node/component/camera/cube. |
| `REDBOLT_WITH_DILIGENT` | `ON` | Required graphics backend family. |
| `REDBOLT_WITH_SDL` | `ON` | Required platform window/input. |
| `REDBOLT_WITH_UI` | `OFF` | Defer. |
| `REDBOLT_WITH_AUDIO` | `OFF` | Defer. |
| `REDBOLT_WITH_PHYSICS` | `OFF` | Defer. |
| `REDBOLT_WITH_NETWORK` | `OFF` | Defer. |
| `REDBOLT_WITH_EDITOR` | `OFF` | Defer. |
| `REDBOLT_WITH_TOOLS` | `OFF` | Defer. |
| `REDBOLT_WITH_TESTS` | `ON` | Unit tests are cheap and should stay on for managed code. |

## Native Targets For First Cube

| Target | Type | Responsibility |
|---|---|---|
| `redbolt-native-support` | Interface | Include SharpCpp native support headers and dependency include paths. |
| `RedBolt.Native` | Shared library | Compile SharpCpp-generated native thunks and link them to native dependencies. |
| `redbolt-generate-native-bindings` | Custom target | Run SharpCpp-based generator over clang-parsed dependency headers. |
| `redbolt-managed` | Custom target | Build the main C# runtime library and any enabled optional modules. |
| `redbolt-first-cube` | Custom target | Build the sample and all runtime dependencies. |

## Managed Targets For First Cube

The first cube should build one required managed runtime library, `RedBolt`, with namespace folders for core, engine, graphics, resources, scene, input, and internal native bindings. Separate managed assemblies are only for optional or independently useful modules.

| Project | Depends on |
|---|---|
| `RedBolt` | `SharpCpp.Support`, generated dependency binding C# files, and required runtime namespaces: `RedBolt.Core`, `RedBolt.Engine`, `RedBolt.Graphics`, `RedBolt.Resources`, `RedBolt.Scene`, `RedBolt.Input`. |
| `RedBolt.Sample.FirstCube` | `RedBolt`. |

## Optional Managed Modules

| Module | Enabled when | Notes |
|---|---|---|
| `RedBolt.RmlUi` | UI/RmlUI support is enabled | Future optional module; should reference `RedBolt` and its own generated/native binding surface. |
| `RedBolt.Editor` | Editor support is enabled | Future optional tooling module; may depend on `RedBolt.RmlUi` or ImGui integration. |
| `RedBolt.Physics` | If physics is kept optional | Decision can be made when Bullet integration starts. |

## Runtime Deployment

| Platform | Plan |
|---|---|
| Linux | Set `BUILD_RPATH` and `INSTALL_RPATH` to `$ORIGIN`; copy dependency shared libraries next to sample if they are not linked statically. |
| Windows | Copy DLLs next to the sample exe using post-build dependency scanning. |
| macOS | Use `@loader_path` install names and copy frameworks/dylibs into app-local output. |

## Verification Targets

| Target | Purpose |
|---|---|
| `redbolt-verify-config` | Fails early if rbfx source root, dotnet, compiler, or required dependency source folders are missing. |
| `redbolt-generate-native-bindings` | Confirms SharpCpp generator can parse selected dependency headers and render generated bindings. |
| `redbolt-tests` | Runs managed unit tests. |
| `redbolt-first-cube` | Builds the first visible sample. |
| `redbolt-run-first-cube` | Optional convenience target to launch the sample from the correct output directory. |
