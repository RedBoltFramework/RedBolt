# Build requirements

RedBolt is built from Linux with CMake, Ninja, and the .NET SDK. The native
dependencies are source-built; the first configure and restore therefore need
network access for CPM.cmake and NuGet downloads.

## Common tools

Install these tools and make them available on `PATH`:

- Git, including support for submodules;
- .NET SDK 10.0 or newer (the projects target `net10.0`);
- CMake 3.28 or newer;
- Ninja with the `Ninja Multi-Config` generator;
- a C++20 compiler and linker.

The versions used for the current build are .NET SDK 10.0.110, CMake 3.28.3,
and Ninja 1.11.1.

## Linux desktop build

The Linux build additionally needs the platform development files used by the
native engine, including the Vulkan loader, OpenGL, X11, audio, and threading
libraries. Mesa's `llvmpipe` and Vulkan `lavapipe` drivers plus Xvfb are needed
to run graphics tests without a physical display.

Configure and build the default Linux development set:

```sh
cmake --preset linux-x64
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
only projects that match the selected platforms, architectures, and modules.
Use these case-insensitive, comma- or semicolon-separated MSBuild properties:

| Property | Keys |
| --- | --- |
| `RedBoltEnablePlatforms`, `RedBoltDisablePlatforms` | `linux`, `windows`, `android`, `web`, `all` |
| `RedBoltEnableArchitectures`, `RedBoltDisableArchitectures` | `x64`, `arm64`, `all` |
| `RedBoltEnableModules`, `RedBoltDisableModules` | `actions`, `audio`, `dear-imgui`, `glow`, `ik`, `navigation`, `network`, `particles`, `physics-jolt`, `rml-ui`, `utility`, `editor`, `samples`, `tests`, `all` |

MSBuild itself treats commas and semicolons in a `-p:` switch as property
separators. Escape a multi-key command-line value, for example
`-p:RedBoltEnablePlatforms=linux%2cwindows`. Values in props files do not need
that command-line escaping.

Selection uses `(defaults - disabled) + enabled`, so an explicit enable wins.
The core engine and build tools are always enabled. Web uses WebAssembly and
does not depend on the architecture list. Platform/architecture pairs that no
project supports simply select no project; they are not configuration errors.

On a fresh checkout, the defaults are Linux x64, all engine modules, the
editor, and samples; tests are excluded. For example, opt into only core plus
Actions on Linux x64 with:

```sh
dotnet build RedBolt.slnx --configuration Debug \
  -p:RedBoltDisablePlatforms=all \
  -p:RedBoltEnablePlatforms=linux \
  -p:RedBoltDisableArchitectures=all \
  -p:RedBoltEnableArchitectures=x64 \
  -p:RedBoltDisableModules=all \
  -p:RedBoltEnableModules=actions
```

Properties passed to a successful solution `Build` or `Rebuild` are
canonicalized and saved atomically in `build/BuildConfig.props`. Later solution
builds load that file automatically. A command-line property overrides its
saved value and updates the file; properties not supplied retain their saved
values. A build or rebuild with none of the six selection properties leaves
the file and its modification time unchanged. Direct project builds,
`Restore`, and `Clean` never save selection changes.

Delete `build/BuildConfig.props` to return to fresh defaults. Passing an empty
property value clears just that saved list. Unknown keys are errors and leave
the previous saved configuration intact.

Modules with managed dependencies must include them: `network` requires
`physics-jolt`, `rml-ui` requires `audio`, and `editor` requires `dear-imgui`,
`glow`, `rml-ui`, and `utility`. The `samples` and `tests` sets require every
engine module. Disabled projects report that they were skipped and do not
compile their C# sources. Native builds likewise compile only the core wrappers
and wrappers needed by enabled modules.

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

```sh
llvm_mingw_root=/path/to/llvm-mingw
gcc_mingw_cxx=/path/to/gcc-mingw/bin/x86_64-w64-mingw32-g++

export PATH="$llvm_mingw_root/bin:$PATH"

cmake --preset win-x64 \
  -DREDBOLT_BINDING_CXX_COMPILER="$llvm_mingw_root/bin/x86_64-w64-mingw32-g++" \
  -DREDBOLT_TINYGLTF_BINDING_CXX_COMPILER="$gcc_mingw_cxx"

dotnet restore src/RedBolt/Editor/RedBolt.Editor.csproj \
  -p:RedBoltPlatform=win-x64 \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=windows \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64 \
  -p:RedBoltEnableModules=all
dotnet build src/RedBolt/Editor/RedBolt.Editor.csproj \
  --configuration Debug --no-restore \
  -p:RedBoltPlatform=win-x64 \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=windows \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64 \
  -p:RedBoltEnableModules=all
```

`RedBoltBuildWindows=true` remains as a deprecated compatibility alias for a
direct Windows build. Prefer the platform selectors and the low-level
`RedBoltPlatform=win-x64` property shown above.

`cmake --build --preset win-x64-debug` is the native-only equivalent. The
repository applies `extern/Diligent.patch` through CPM during population; do
not patch the Diligent checkout by hand.

## Direct3D smoke tests with Wine

Wine 11 or newer is recommended for the D3D12 smoke test. Wine 9 was also
tested: D3D11 renders successfully, but its built-in D3D12 implementation
rejects the generated root signatures and produces a black frame. A 64-bit
Wine installation is sufficient for the `win-x64` output.

Publish the Text3D sample after the Windows build:

```sh
dotnet restore src/RedBolt/Sample/Text3D/RedBolt.Sample.Text3D.csproj \
  --runtime win-x64 -p:RedBoltBuildWindows=true
dotnet publish src/RedBolt/Sample/Text3D/RedBolt.Sample.Text3D.csproj \
  --configuration Debug --no-restore --runtime win-x64 \
  --self-contained true -p:RedBoltBuildWindows=true

publish_dir=build/bin/Debug/win-x64/samples/win-x64/publish
cp -a build/bin/Debug/win-x64/core/*.dll "$publish_dir/"
cp -a build/bin/Debug/win-x64/extern/*.dll "$publish_dir/"
cp -a build/bin/Debug/win-x64/extern/runtimes/win-x64/native/*.dll "$publish_dir/"
cp -a build/bin/Debug/host/extern/*.dll "$publish_dir/"
```

The explicit staging keeps the repository's shared managed-output layout while
also producing a standalone directory for Wine. Run each backend with a
separate Wine prefix. The sample's `--timeout` option makes the smoke test exit
without input and `--screenshot-on-exit` proves that a frame was rendered.

```sh
repo_dir=$(pwd)
publish_dir="$repo_dir/build/bin/Debug/win-x64/samples/win-x64/publish"
wine_root=/path/to/wine-installation
wine_executable="$wine_root/bin/wine"
winepath_executable="$wine_root/bin/winepath"
wine_prefix=/path/to/wine-prefix
resource_path=$(WINEPREFIX="$wine_prefix" "$winepath_executable" -w "$repo_dir/bin")

for backend in d3d11 d3d12; do
  screenshot=$(WINEPREFIX="$wine_prefix" "$winepath_executable" -w \
    "$publish_dir/$backend.png")
  xvfb-run -a env \
    WINEPREFIX="$wine_prefix" \
    WINEDEBUG=-all \
    LIBGL_ALWAYS_SOFTWARE=1 \
    MESA_LOADER_DRIVER_OVERRIDE=llvmpipe \
    VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/lvp_icd.json \
    timeout 60 "$wine_executable" \
      "$publish_dir/RedBolt.Sample.Text3D.exe" \
      "--$backend" \
      --pp "$resource_path" \
      --timeout 5 \
      --screenshot-on-exit "$screenshot"
done
```

Use `/usr/bin/wine` for a distribution Wine install, or point
`wine_executable` at a user-local Wine build. Keep prefixes separate when
comparing Wine versions.

## Tests

The test project lives in the sibling `RedBolt.Tests` repository. Build it
against the already-built engine and run graphics tests through the offscreen
software-rendering harness:

```sh
cd ../RedBolt.Tests
dotnet build RedBolt.Tests.csproj \
  --configuration Debug --no-restore \
  -p:RedBoltEnableModules=all \
  -p:RedBoltKeepParentRuntimeAssemblies=true \
  -p:BuildProjectReferences=false

./scripts/with_software_renderer.sh \
  dotnet test RedBolt.Tests.csproj \
  --configuration Debug --no-build \
  --logger 'console;verbosity=minimal'
```

Build outputs, generated bindings, and downloaded native dependencies are kept
under `build/`.
