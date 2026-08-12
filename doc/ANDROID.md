# Android build

RedBolt supports Android API 24 or newer. The managed app targets API 36. The native build uses OpenGL ES 3 and produces `arm64-v8a` and `x86_64` libraries. Vulkan, 32-bit ABIs, production release signing, and the desktop-only SystemUI utility surface are not part of this target.

## Tools

Install the .NET 10 Android workload. RedBolt expects the Android tools below the user-local data directory by default:

```text
~/.local/share/redbolt-android/jdk
~/.local/share/redbolt-android/sdk
```

The SDK must contain these packages:

```text
build-tools;36.0.0
platform-tools
platforms;android-36
ndk;27.2.12479018
```

The project reads the root from the `RedBoltAndroidRoot` MSBuild property. Set that property when the tools are in another directory.

## Build the APK

Restore and build the Android sample host:

```sh
dotnet restore src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=android \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=all \
  -p:RedBoltEnableModules=all
dotnet build src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj \
  --configuration Debug --no-restore \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=android \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=all \
  -p:RedBoltEnableModules=all
```

The build configures and builds both native ABIs, generates the C# and SDL Java bindings, packages `bin/CoreData` and `bin/Data`, and applies the standard Android debug signature. It produces one APK per ABI:

```text
build/bin/Debug/android-x64/app/RedBolt.Sample.Android.apk
build/bin/Debug/android-arm64/app/RedBolt.Sample.Android.apk
```

Each APK contains only its matching native ABI and the complete sample data set.

Build both Release packages the same way:

```sh
dotnet build src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj \
  --configuration Release --no-restore \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=android \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=all \
  -p:RedBoltEnableModules=all
```

To build only one ABI, select its platform and disable the companion build:

```sh
dotnet build src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj \
  --configuration Debug --no-restore \
  -p:RedBoltPlatform=android-x64 \
  -p:RedBoltDisablePlatforms=all -p:RedBoltEnablePlatforms=android \
  -p:RedBoltDisableArchitectures=all -p:RedBoltEnableArchitectures=x64 \
  -p:RedBoltEnableModules=all \
  -p:RedBoltBuildAllAndroidPackages=false
```

The Release APKs are:

```text
build/bin/Release/android-x64/app/RedBolt.Sample.Android.apk
build/bin/Release/android-arm64/app/RedBolt.Sample.Android.apk
```

.NET applies the standard Android debug key unless explicit release signing properties are supplied; configure a production key before distribution.

## Run in the emulator

Install an x86_64 emulator image once:

```sh
android_root="${XDG_DATA_HOME:-$HOME/.local/share}/redbolt-android"
export JAVA_HOME="$android_root/jdk"
"$android_root/sdk/cmdline-tools/latest/bin/sdkmanager" \
  --sdk_root="$android_root/sdk" \
  emulator 'system-images;android-36;google_apis;x86_64'
```

Run the picker:

```sh
dotnet run --file scripts/run_android_emulator.cs
```

Launch one sample directly and save a screenshot:

```sh
dotnet run --file scripts/run_android_emulator.cs -- \
  --sample camera-shake \
  --screenshot build/android-samples-camera-shake.png
```

Run the x86_64 Release APK and capture the rendered scene used by the final Android test:

```sh
dotnet run --file scripts/run_android_emulator.cs -- \
  --apk build/bin/Release/android-x64/app/RedBolt.Sample.Android.apk \
  --sample camera-shake \
  --screenshot build/test-results/android-samples-camera-shake-release.png \
  --shutdown
```

The script creates a user-local `redbolt-api36` AVD when needed, starts it without a window by default, installs the signed Samples APK, and launches either the picker or the requested sample. It enables KVM after a successful emulator acceleration probe; otherwise it falls back to software CPU emulation and extends the boot and runtime timeouts for the slower launch. Set `REDBOLT_ANDROID_ACCEL=on` or `off` only to override that automatic choice for diagnostics. Requested samples must log both the packaged-asset and Dear ImGui readiness markers; crash and Diligent assertion checks are scoped to the launched RedBolt process.

The helper prefers a side-by-side emulator at `$android_root/emulator-stable/emulator` when present, then falls back to the SDK-managed emulator. Set `REDBOLT_ANDROID_EMULATOR` to select another executable. It uses the `swangle` software GPU mode by default (ANGLE with SwiftShader); override that with `REDBOLT_ANDROID_GPU` only when testing another renderer. CPU acceleration and GPU rendering are selected independently, so the default combines KVM CPU acceleration with software rendering. For a launched sample, `--screenshot` captures RedBolt's rendered back buffer rather than Android's system UI. Use `--window` for an interactive emulator and `--shutdown` to stop it after the run.
