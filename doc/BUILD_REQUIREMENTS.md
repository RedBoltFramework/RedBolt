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

Configure and build the complete solution:

```sh
cmake --preset linux-x64
dotnet restore RedBolt.slnx
dotnet build RedBolt.slnx --configuration Debug --no-restore
```

The solution includes the WebAssembly sample. Install its SDK workload if the
build reports that the Emscripten toolchain is missing:

```sh
dotnet workload install wasm-tools
```

To build only the desktop editor, use:

```sh
dotnet restore src/RedBolt/Editor/RedBolt.Editor.csproj
dotnet build src/RedBolt/Editor/RedBolt.Editor.csproj \
  --configuration Debug --no-restore
```

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
  -p:RedBoltBuildWindows=true
dotnet build src/RedBolt/Editor/RedBolt.Editor.csproj \
  --configuration Debug --no-restore \
  -p:RedBoltBuildWindows=true
```

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

publish_dir=build/bin/Debug/samples/win-x64/publish
cp -a build/bin/Debug/core/*.dll "$publish_dir/"
cp -a build/bin/Debug/extern/*.dll "$publish_dir/"
cp -a build/bin/Debug/extern/runtimes/win-x64/native/*.dll "$publish_dir/"
```

The explicit staging keeps the repository's shared managed-output layout while
also producing a standalone directory for Wine. Run each backend with a
separate Wine prefix. The sample's `--timeout` option makes the smoke test exit
without input and `--screenshot-on-exit` proves that a frame was rendered.

```sh
repo_dir=$(pwd)
publish_dir="$repo_dir/build/bin/Debug/samples/win-x64/publish"
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
  -p:RedBoltKeepParentRuntimeAssemblies=true \
  -p:BuildProjectReferences=false

./scripts/with_software_renderer.sh \
  dotnet test RedBolt.Tests.csproj \
  --configuration Debug --no-build \
  --logger 'console;verbosity=minimal'
```

Build outputs, generated bindings, and downloaded native dependencies are kept
under `build/`.
