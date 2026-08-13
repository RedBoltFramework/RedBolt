# Allowed Deviations

These are the ONLY places C# is allowed to differ from C++:

- RefCounted / SharedPtr / WeakPtr are dropped ON PURPOSE. They must NOT exist.
- ResourceCache cannot observe external CLR references after the removal of intrusive reference counting. Its release methods therefore treat cached resources as releasable when `force` is false; managed references remain valid after cache eviction. `force` is retained for API parity and skips the native cross-type retry passes when true.
- SubscribeToEvent / SendEvent are upgraded to the new struct-based event format.
- CommandLineParser is used instead of CLI11.
- WorkQueue is rebuilt using a thread-pool.
- Managed platform and graphics composition is compile-time inside `RedBolt.Core.dll`, selected by `RedBoltPlatform`; there are no platform or graphics-backend adapter assemblies and platform functionality is guarded by target constants. Platform outputs use `build/bin/<configuration>/<platform>`, build-host tools and platform-neutral dependencies use `build/bin/<configuration>/host`, native libraries use `<platform>/extern/runtimes/<platform>/native`, desktop sample executables and sample-specific files use `<platform>/samples`, tests use `<platform>/tests`, and Web output uses `browser-wasm/web`.
- Native shared-library filenames use conventional platform-neutral names.
- Minor API tweaks are allowed ONLY to avoid GC churn.
- Managed tests may request hidden SDL windows through `EngineParameters.WindowHidden`; normal engine behavior remains visible by default.
- Managed Web builds create and preload compressed `CoreData.pak` and `Data.pak` through the managed `PackageTool` instead of CMake's native tool discovery.
- The managed Android host packages the Web-compatible sample catalog, raw `CoreData` and `Data` assets, and both `arm64-v8a` and `x86_64` native libraries in one debug APK. It uses the SDL Android activity and OpenGL ES 3. Android finalizes mobile window and `/apk` resource parameters after sample `Setup()` and does not expose desktop command-line or window controls.
- C# indices and sizes use `int`, including when the corresponding C++ type is unsigned.
- Prefer property syntax alone when it can faithfully represent a C++ member and its public accessors; do not add a private backing field only to wrap it in a public property. Private fields remain allowed when required by behavior or storage semantics.
- `IApproximateEquatable<T>` is a managed addition used to expose strongly typed epsilon-based equality across math value types and is to be kept.
- Do NOT re-port things C# already has, for example:
  - Threading primitives (locks, tasks, thread pools)
  - Math functions and constants
  - Standard containers / collections
  - String utilities
- Math type aliases are different, but used
  - `Vector[234]` -> `Vector[234]f`
  - `IntVector[234]` -> `Vector[234]i`
- `CString` is our addition and is to be kept, as well as related customizations of other APIs
- C++ `Object` is named `RedObject` in C# to avoid collision with `System.Object`.
- No URHO3D_OBJECT-related metadata code -> replaced by C# type system/reflection with `ObjectReflection` as a facade
- Object reflection and serializable attribute registration are declared with C# `[Reflected]`, `[Serialized]`, and `[RemoveSerialized]` attributes and generated at compile time, including for RedBolt user assemblies. Hand-written `RegisterObject`, `RegisterAttribute`, and `AddFactoryReflection` registration paths are not used. `[Serialized]` targets the instance field, property, getter, or setter being serialized and never a class. The generator infers its value type and available accessors; getter declarations name only a separate setter when needed, and setter declarations name only a separate getter when needed. Field, auto-property, and simple-property backing-field initializers provide defaults without `DefaultSource`; private named default sources remain only where the target cannot declare its required default. Already-serialized base members are inherited from the selected reflected attribute source without forwarding declarations, while new subtype-specific serialized properties backed by inherited state use private forwarding properties. Generated declarations preserve factory categories, explicit non-factory types, exact attribute order inferred from serialized member declaration order, explicit inheritance source and insertion point, removed attributes, defaults, modes, scope hints, enum names, custom accessors, and metadata configuration. Reflection-only source members remain private implementation details. Library registration entry points register generated assembly metadata; low-level reflection mutation is confined to the generator/runtime materialization infrastructure.
- C++ `friend` has no C# equivalent. Friendship grants access to the friend and does not change the member's declared access, so ported `public`, `protected`, and `private` members keep that exact access in C#. A mapped friend caller reaches an otherwise-inaccessible member through the smallest explicitly named C#-only `internal` forwarding method or accessor on the declaring type. The bridge must preserve virtual dispatch, must be used only by mapped friend callers, and must not expose a field directly when a narrower operation is sufficient. Do not change a ported member itself to `internal` or `protected internal`.
- Built-in rbfx UI (`UI`, `Window`, `Text`, `Sprite`, and related controls/subsystems) is not ported.
- `Text3D` is an intentional standalone Graphics component backed by HarfBuzz shaping and GPU-outline bindings, rbfx-compatible pre-baked SDF font metrics and atlas pages, and ordinary scene geometry; it does not depend on the built-in UI or RmlUi subsystems.
- In samples, replace every use of the built-in rbfx UI with Dear ImGui equivalents.
- Desktop sample entry points are static `Main` methods compiled without platform preprocessor guards. The Web sample explicitly selects its own static entry point, so desktop entry points are ignored when all sample sources are compiled into the Web host.
- All ported samples are windowed; do not preserve fullscreen defaults from the C++ sample when configuring the managed sample.
- The MultipleViewports sample may explicitly disable window maximization and configure default window dimensions so `--width` and `--height` control the actual window size.
- C#-only diagnostic flags may trigger a RenderDoc capture after a specified delay.
- The SkeletalAnimation sample may expose runtime skinning-mode and maximum-bone controls for web rendering diagnostics.
- The MultipleViewports sample may temporarily animate its camera forward when `REDBOLT_MULTIPLE_VIEWPORTS_ANIMATE_CAMERA=1` for automated rendering diagnostics.
- Engine construction sets the process working directory to the managed executable directory so CMake-staged resource-directory symlinks resolve consistently.
- All ported samples use `MouseMode.Free` with a visible cursor while idle. Free-fly camera controls follow rbfx: while the right mouse button is held, temporarily hide and lock the cursor in relative mode, then restore free mode and cursor visibility on release.
- All ported samples use `MM_FREE` memory mode unless a sample has a documented sample-specific reason to require another mode.
- WebAssembly frame pacing is controlled by the JavaScript `requestAnimationFrame` loop. Do not call `SDL_GL_SetSwapInterval` on WebAssembly because SDL's Emscripten implementation requires an Emscripten-owned main loop that the .NET host does not use.
- On WebAssembly, application window width and height default to the browser canvas dimensions in physical pixels before `Setup()`. `Setup()` may override either value. The Web shell follows rbfx: it rounds CSS canvas dimensions multiplied by the device pixel ratio, creates the SDL window without `SDL_WINDOW_ALLOW_HIGHDPI`, and resynchronizes canvas resolution on resize, fullscreen, and visibility changes.
- The BakedLighting sample omits the optional, unported Navigation/Crowd behavior, preserves the character behavior as `TODO: port` comments, and uses `FreeFlyController` on the scene camera.
- `SystemUI` is renamed to `DearImGui`.
- Dear ImGui uses an intentional RedBolt HarfBuzz GPU font loader that shapes contextual glyph runs and selects merged-font sources lazily; this is new RedBolt development rather than an rbfx feature.
- C# engine types use namespaces matching their source folders.
- Namespace-bearing C# files declare their file-scoped namespace before all non-global using directives. Namespace-local using directives resolve namespace/type collisions.
- Calls to `GetSubsystem<T>()` in ported subsystem consumers assume that the requested subsystem is registered and return a non-null object. C# code may use a non-null field/reference and omit C++ null checks that only guard a missing subsystem.
- The optional Actions subsystem and compile-gated Actions reflection/integration members are excluded from the current completion scope.

Anything not on this list must match C++.
