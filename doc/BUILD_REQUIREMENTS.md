# Build requirements

RedBolt is built from Linux with CMake, Ninja, and the .NET SDK. The native
dependencies are source-built; the first configure and restore therefore need
network access for CPM.cmake and NuGet downloads.

## Common tools

Install these tools and make them available on `PATH`:

- Git, including support for submodules;
- .NET SDK 10.0 or newer (the projects target `net10.0`);
- CMake 3.28 or newer;
- Ninja (single-configuration generator; each configuration uses its own build tree);
- a C++20 compiler and linker.

The versions used for the current build are .NET SDK 10.0.110, CMake 3.28.3,
and Ninja 1.11.1.

## Linux desktop build

The Linux build additionally needs the platform development files used by the
native engine, including the Vulkan loader, OpenGL, X11, audio, and threading
libraries. Mesa's `llvmpipe` and Vulkan `lavapipe` drivers plus Xvfb are needed
to run graphics tests without a physical display.

Build the default Linux development set through MSBuild, which configures its
native prerequisites:

```sh
dotnet restore RedBolt.slnx
dotnet build RedBolt.slnx --configuration Debug --no-restore
```

The default set is Linux x64, all engine modules, the editor, and desktop
samples. Tests and non-Linux platforms are disabled by default. Enable the Web
platform before installing its SDK workload:

```sh
dotnet workload install wasm-tools
```

To build only the desktop editor, use:

```sh
dotnet restore src/RedBolt/Editor/RedBolt.Editor.csproj
dotnet build src/RedBolt/Editor/RedBolt.Editor.csproj \
  --configuration Debug --no-restore
```

## Saved build selection

`RedBolt.slnx` keeps every project visible, while its traversal project builds
only projects that match the selected platforms and architectures.
Use these case-insensitive, comma- or semicolon-separated MSBuild properties:

| Property | Keys |
| --- | --- |
| `RedBoltEnablePlatforms`, `RedBoltDisablePlatforms` | `linux`, `windows`, `android`, `web`, `all` |
| `RedBoltEnableArchitectures`, `RedBoltDisableArchitectures` | `x64`, `arm64`, `all` |

Use `-p:RedBoltFull` to enable all platforms and architectures:

```sh
dotnet build RedBolt.slnx -p:RedBoltFull
```

MSBuild itself treats commas and semicolons in a `-p:` switch as property
separators. Escape a multi-key command-line value, for example
`-p:RedBoltEnablePlatforms=linux%2cwindows`. Values in props files do not need
that command-line escaping.

Selection starts from the saved defaults and then applies explicit values, so
explicit values win. Web uses WebAssembly and does not depend on the architecture
list. Platform/architecture pairs that no
project supports simply select no project; they are not configuration errors.

On a fresh checkout, the defaults are Linux x64, all engine modules, the
editor, and samples with tests disabled. For example, limit a build to Linux x64:

```sh
dotnet build RedBolt.slnx --configuration Debug \
  -p:RedBoltDisablePlatforms=all \
  -p:RedBoltEnablePlatforms=linux \
  -p:RedBoltDisableArchitectures=all \
  -p:RedBoltEnableArchitectures=x64
```

Properties passed to a successful solution `Build` or `Rebuild` are
canonicalized and saved atomically in `build/BuildConfig.props`. Later solution
builds load that file automatically. A command-line property overrides its
saved value and updates the file; properties not supplied retain their saved
values. A build or rebuild with none of the selection properties leaves
the file and its modification time unchanged. Direct project builds,
`Restore`, and `Clean` never save selection changes.

Delete `build/BuildConfig.props` to return to fresh defaults. Passing an empty
property value clears just that saved list. Unknown keys are errors and leave
the previous saved configuration intact.

The build root is selected statically by `Directory.Build.props` and is not
recorded by builds. A global `-p:RedBoltBuildRoot=<dir>` wins. Otherwise, when
the repository sits beside the monorepo's `RedBolt.slnx`, every entry point
(root solution, module solution, direct `.csproj`) builds into the monorepo
`build/`; a repository checked out on its own builds into its own `build/`.
The MSBuild task assembly is built under
`<build root>/bin/tools/module-build-tasks/<configuration>/net10.0/`, and VSTest
results go to `<build root>/TestResults`.

Modules with managed dependencies must include them: `rml-ui` requires
`audio`, and `editor` requires `dear-imgui`,
`glow`, `rml-ui`, and `utility`. Disabled projects report that
they were skipped and do not compile their C# sources. Native builds likewise
compile only the core wrappers and wrappers needed by active modules.

## Windows x64 cross-build

The Windows build uses MinGW-w64, which is the Windows ABI, headers, and C/C++
runtime. MinGW-w64 is not tied to GCC: LLVM-MinGW combines those components
with Clang, LLD, and the other LLVM tools.

The current Direct3D build needs:

- a recent LLVM-MinGW distribution with D3D11 and D3D12 headers;
- a GCC-based MinGW-w64 C++ compiler for the TinyGLTF binding scan.

LLVM-MinGW snapshot 20260616 and a GCC MinGW-w64 compiler were used for the
current build. Put LLVM-MinGW first on `PATH`, then provide both binding
compiler paths when configuring. Starting from a fresh `win-x64` build
directory avoids reusing compilers cached by an older configuration.

Discover compilers with `command -v` after selecting the required toolchains on
`PATH`; the LLVM and GCC scanners must be resolved separately before combining
PATH entries. For this host, use the monorepo's `doc/LOCAL_TOOLCHAINS.md` fallback
locations. Inspect `cmake --list-presets` and the active MSBuild native configuration
before configuring: a standalone preset can use a different build root.

```sh
# With LLVM-MinGW selected on PATH:
llvm_mingw_cxx=$(command -v x86_64-w64-mingw32-g++)
# Set gcc_mingw_cxx to the GCC scanner resolved with its toolchain selected.
# Verify both compiler versions before configuration.
cmake --preset win-x64 \
  -DREDBOLT_BINDING_CXX_COMPILER="$llvm_mingw_cxx" \
  -DREDBOLT_TINYGLTF_BINDING_CXX_COMPILER="$gcc_mingw_cxx"

dotnet restore src/RedBolt/Editor/RedBolt.Editor.csproj \
  -p:RedBoltPlatform=win-x64 \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=windows \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64
dotnet build src/RedBolt/Editor/RedBolt.Editor.csproj \
  --configuration Debug --no-restore \
  -p:RedBoltPlatform=win-x64 \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=windows \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64
```

`RedBoltBuildWindows=true` remains as a deprecated compatibility alias for a
direct Windows build. Prefer the platform selectors and the low-level
`RedBoltPlatform=win-x64` property shown above.

`cmake --build --preset win-x64-debug` is the native-only equivalent. The
repository applies `extern/Diligent.patch` through CPM during population; do
not patch the Diligent checkout by hand.

## Direct3D smoke tests with Wine

Use Wine 11 or newer from `PATH`; resolve `wine` and `winepath` with `command -v`
and check `wine --version`. On the pinned development host, system Wine 9 has an
unusable server-socket location and its D3D12 backend is not an adequate smoke
test. See the monorepo's local toolchain reference when PATH needs adjustment.

Publish the relevant sample for `win-x64` with `dotnet publish --configuration
Debug --runtime win-x64 --self-contained true` and the matching RedBolt platform
properties. Select a temporary output using the publish command's `--output`
option rather than assuming a binary directory. Preserve the existing shared
managed/native staging requirements. When staging needs source directories,
query the sample project with the same configuration and platform properties:

```sh
dotnet msbuild src/RedBolt/Sample/Text3D/RedBolt.Sample.Text3D.csproj \
  -p:Configuration=Debug -p:RedBoltPlatform=win-x64 \
  -getProperty:RedBoltRoot,RedBoltCoreOutputDir,RedBoltExternOutputDir,RedBoltHostExternOutputDir,RedBoltNativeOutputRoot,TargetPath
```

Use the returned directories to stage core, external, host-external, and native
DLLs into the selected publish output. Use `winepath -w` for paths passed to
Windows code. Resolve resources from the built/staged sample and project inputs;
do not substitute a guessed repository `bin` directory. Run each backend with a
separate `WINEPREFIX` under the software-rendering harness, passing `--d3d11` or
`--d3d12`, `--timeout 5`, and `--screenshot-on-exit <screenshot>`. Inspect the
captured frame; process exit alone does not establish correct rendering.

## Tests

The test project lives in the sibling `RedBolt.Tests` repository. Build it
against the already-built engine and run graphics tests through the offscreen
software-rendering harness:

```sh
cd ../RedBolt.Tests
export XDG_DATA_HOME=/home/rk/.local/share
dotnet build RedBolt.Tests.csproj \
  --configuration Debug --no-restore \
  -p:RedBoltKeepParentRuntimeAssemblies=true \
  -p:BuildProjectReferences=false

./scripts/with_software_renderer.sh \
  dotnet test RedBolt.Tests.csproj \
  --configuration Debug --no-build \
  --logger 'console;verbosity=minimal'
```

Build outputs, generated bindings, and downloaded native dependencies use the
selected build root. Query project properties or build output rather than
assuming the module-local `build/` tree. For the full verification policy and
coverage/sharding commands, use the monorepo `doc/VERIFICATION.md`.
