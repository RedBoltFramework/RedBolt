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
dotnet restore src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj
dotnet build src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj \
  --configuration Debug --no-restore
```

The build configures and builds both native ABIs, generates the C# and SDL Java bindings, packages `bin/CoreData` and `bin/Data`, and applies the standard Android debug signature. The signed APK is:

```text
build/obj/managed/android-x64/staging/RedBolt.Sample.Android/Debug/org.redbolt.samples-Signed.apk
```

The universal Debug APK contains both native ABIs and the complete sample data set, so it is large.

For a smaller x86_64 Release package suitable for the emulator, build only that runtime:

```sh
dotnet build src/RedBolt/Sample/Android/RedBolt.Sample.Android.csproj \
  --configuration Release --no-restore \
  -p:RedBoltAndroidRuntimeIdentifiers=android-x64 \
  -p:RedBoltAndroidPackageArm64=false
```

The resulting APK is:

```text
build/obj/managed/android-x64/staging/RedBolt.Sample.Android/Release/org.redbolt.samples-Signed.apk
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
scripts/run_android_emulator.sh
```

Launch one sample directly and save a screenshot:

```sh
scripts/run_android_emulator.sh \
  --sample hello-world \
  --screenshot build/android-hello-world.png
```

The script creates a user-local `redbolt-api36` AVD when needed, starts it without a window by default, installs the signed APK, and launches either the picker or the requested sample. Use `--window` for an interactive emulator and `--shutdown` to stop it after the run.
