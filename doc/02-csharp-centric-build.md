# C#-Centric Build

## Entry Point

The supported full build is the solution:

```bash
dotnet build RedBolt.slnx --configuration Debug
```

MSBuild owns restore, platform selection, dependency ordering, managed compilation, and invocation of auxiliary CMake targets. CMake does not invoke `dotnet build` and is not the top-level build orchestrator.

## Build Graph

```text
RedBolt.slnx
  SharpCpp managed projects
  RedBolt.Dependencies.Generator
  RedBolt.Native / RedBolt.Native.Web dummy C# projects
    SharpCpp.Native dummy C# project
      configure the platform CMake preset
      build sharpcpp-native
    build the platform RedBolt CMake target
  generated binding C# projects
  RedBolt managed runtime
  tests and samples
  WebAssembly sample
```

`ProjectReference` edges express this order. The native projects intentionally produce only dummy managed assemblies; their build targets configure and build the corresponding CMake graph. Native orchestration is locked so duplicate references cannot configure or build the same tree concurrently.

## Output Layout

Every generated artifact is below `build/`:

```text
build/
  bin/
    linux-gcc-x64-dll/<configuration>/
    web-emscripten-wasm/<configuration>/
    tools/<platform>/<configuration>/
  obj/
    managed/<platform>/<project>/
    restore/<project>/
  native/
    linux-gcc-x64-dll/
    web-emscripten-wasm/
```

The repository's tracked `bin/CoreData` and `bin/Data` directories are runtime resource inputs, not build output. Desktop output contains links to those inputs.

## Platform Presets

| Platform | Configure preset | Native build directory | Primary target |
|---|---|---|---|
| Linux x64 | `linux-gcc-x64-dll` | `build/native/linux-gcc-x64-dll` | `RedBolt.Native` |
| WebAssembly | `web-emscripten-wasm` | `build/native/web-emscripten-wasm` | `redbolt-wasm-native` |

The solution builds both required platforms. Building a single managed project builds only the native platform selected by its `RedBoltPlatform` property.

## CMake Ownership

CMake files are directory-local:

| Directory | Responsibility |
|---|---|
| `cmake/` | shared CMake helpers and toolchain selection |
| `extern/CMakeLists.txt` | external dependency integration aggregation |
| `extern/*.cmake` | one integration file per native dependency |
| `generators/RedBolt.Dependencies.Generator/` | binding-generation commands and outputs |
| `src/RedBolt.Bindings.*/` | one generated binding surface per directory |
| `native/RedBolt.Native/` | final native library and native support code |
| `samples/RedBolt.Sample.Web/` | WebAssembly archive merge and sample native link |

The root `CMakeLists.txt` only establishes project-wide settings and adds these directories. Third-party source trees keep their upstream CMake files.

## Native Build Diagnostics

CMake is an implementation detail of the dummy C# native projects. Use `dotnet build` with the relevant `.slnx` or `.csproj` for normal development, clean-checkout verification, and native build diagnostics.
