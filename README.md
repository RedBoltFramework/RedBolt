# RedBolt

RedBolt is a C# port of rbfx. Its C#-centric build drives managed compilation, native dependencies, generated SharpCpp bindings, desktop samples, tests, and the Android and WebAssembly sample hosts.

Contributor and coding-session rules now live in `AGENTS.md`.

## Build

```bash
cmake --preset linux-x64
dotnet build RedBolt.slnx --configuration Debug
```

The CMake configure step downloads the pinned dependencies with CPM.cmake.
The first solution build defaults to Linux x64 with all engine modules, the
editor, and desktop samples. Build selections passed as MSBuild properties are
saved in `build/BuildConfig.props`; see
[Build requirements](doc/BUILD_REQUIREMENTS.md#saved-build-selection) for all
platform, architecture, and module options.

All generated files and binaries are written below `build/`. CMake remains
available for native-only diagnosis, but the solution is the supported
selective-build entry point.

Cross-compile the Windows x64 build from Linux with MinGW-w64 on `PATH`:

```bash
cmake --preset win-x64
dotnet build RedBolt.slnx --configuration Debug \
  -p:RedBoltDisablePlatforms=all \
  -p:RedBoltEnablePlatforms=windows \
  -p:RedBoltDisableArchitectures=all \
  -p:RedBoltEnableArchitectures=x64
```

Use the `win-x64-debug` or `win-x64-release` CMake build presets for a
native-only cross-build.

The test suite is maintained in the sibling `RedBolt.Tests` repository. Enable
the `tests` module to include it in a solution build; the default configuration
does not build tests.

For architecture notes, porting strategy, and binding details, see `doc/`.

Android builds use OpenGL ES 3, require API 24 or newer, and produce a universal Debug APK with `arm64-v8a` and `x86_64` libraries or an x86_64 Release APK for emulator testing. See [doc/ANDROID.md](doc/ANDROID.md) for setup, build, and emulator commands.
