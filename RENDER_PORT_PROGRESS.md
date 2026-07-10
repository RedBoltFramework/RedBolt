# Render Port Progress

Keep this file concise. Remove stale guesses, completed low-value notes, and superseded plans. Summarize old information, remove old information that is lon longer relevant for future work. Then, continue working on the task.

## Goal

Incrementally port rbfx `Source/Urho3D/RenderAPI` and `Source/Urho3D/RenderPipeline` into RedBolt without losing session context.

Scope guard:

- The end state is a full port of graphics API and pipeline classes plus their behavior/hookups.
- Temporary compatibility layers are acceptable only as short-lived landing slices.
- Only C++ utility-only infrastructure with no meaningful C# analogue may be omitted.

## Current State

- RedBolt currently has a custom managed renderer in [src/RedBolt/Graphics/Graphics.cs](/home/rk/Desktop/src/rbfx/RedBolt/src/RedBolt/Graphics/Graphics.cs) backed by generated SDL and Diligent bindings.
- RedBolt is not yet using an rbfx-style `RenderAPI` / `RenderPipeline` architecture.
- The checked-in `SharpCpp/rbfx/SharpCpp.Rbfx.Bindings` surface does not currently include `RenderAPI` or `RenderPipeline` namespaces/files, so this work is not already available there in a reusable form.

## Verified Facts

- rbfx source target:
  - `/home/rk/Desktop/src/rbfx/rbfx/Source/Urho3D/RenderAPI`
  - `/home/rk/Desktop/src/rbfx/rbfx/Source/Urho3D/RenderPipeline`
- Combined source size is about `24.6k` lines, so this must be landed in slices.
- Recent render-related commits already in RedBolt:
  - `4844031` `Fix static scene mushroom transforms`
  - `9287b4a` `Port rbfx image and texture resource loading`

## Active Porting Strategy

Port in dependency order instead of trying to transplant the full subsystem at once:

1. Foundational data/contracts:
   - `RenderAPIDefs`
   - `RenderTargetView`
   - `RenderPipelineDefs`
   - shared render-pass state types
2. Render-path model layer:
   - `RenderPass`
   - `RenderPath`
   - minimal pass parameter plumbing
3. Render-buffer layer:
   - `RenderBuffer`
   - `RenderBufferManager`
4. Higher-level pipeline orchestration:
   - `RenderPipeline`
   - `RenderPipelineView`
   - scene/drawable/light processors
5. Real rendering integration with existing RedBolt graphics backend

## Current Slice

Math generic-vector realignment on `2026-07-10`:

- The old concrete `Vector2` / `Vector3` float structs have been removed instead of being kept as a compatibility-name layer.
- `src/RedBolt/Math/Vector2.Generic.cs` and `src/RedBolt/Math/Vector3.Generic.cs` are now the canonical rbfx-style vector surface, with sequential-layout generic structs constrained to unmanaged IEEE floating-point scalars.
- The generic vector types now cover the float-side helpers that previously only existed on the concrete structs, including integer conversion constructors, line-projection scalar helpers, signed angles, renormalization helpers, orthogonal helpers, and per-component min/max/floor/round/ceil plus `*ToInt`.
- The engine now uses explicit `Vector2<float>` / `Vector3<float>` types throughout the managed codebase instead of a concrete-name wrapper or alias layer, while double-vector support continues through `Vector2<double>` / `Vector3<double>` and `DoubleTransform`.
- `IntVector2`, `IntVector3`, `Vector4`, `Variant`, serializer/deserializer helpers, render-path parsing, scene transforms, and render/pipeline code were updated to the generic float vectors so the main `RedBolt` project builds without the deleted concrete vector types.

Core `Spline` parity follow-up on `2026-07-10`:

- `src/RedBolt/Core/Spline.cs` no longer uses an `object`-based placeholder surface; it now follows the rbfx `VariantVector`/`Variant` shape, including rbfx interpolation-mode dispatch, type-checked knot insertion, full-path Catmull-Rom endpoint duplication/looping, double interpolation support, equality operators, and `ICloneable.Clone()` as the copy-constructor equivalent.
- `SplinePath` and focused spline tests were updated to consume `Variant` results explicitly instead of relying on the earlier object-cast placeholder path.

Core `Variant` / resource-ref parity follow-up on `2026-07-10`:

- `Variant` now matches the current rbfx value-contract more closely for the managed streamable subset: it has the rbfx type-name table, `Variant(VariantType)` default-value construction, `GetStringHash()`, `GetTypeName()`, `GetTypeFromName(...)`, `IsZero()`, explicit `uint`/`ulong`/`StringHash` handling through the rbfx `VAR_INT` / `VAR_INT64` slots, and rbfx-style `ToString()` behavior for primitive/math/resource/buffer cases instead of falling back to boxed `object.ToString()`.
- `ResourceRef` and `ResourceRefList` now follow rbfx string semantics more closely too: single-token parses become type-only refs, `ResourceRefList` uses semicolon-separated names, and type names round-trip through the global `StringHash` reverse-register instead of serializing only numeric hashes when the original type text is known.
- `Matrix3.ZERO` and `Matrix3x4.ZERO` are now present, which closes an audited rbfx constant gap that the `Variant(VariantType)` port depends on.
- Verification for this slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-restore` (`464/464` passing), `cmake --build --preset Debug --target restore`, `cmake --build --preset Debug --target RedBolt`, and `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png` with a valid `1280x720` PNG output.

IO packed-stream parity follow-up on `2026-07-10`:

- `src/RedBolt/IO/IODefs.cs` now ports rbfx `VectorBinaryEncoding`, so the IO layer no longer has a missing upstream enum dependency for packed vector serialization.
- `Deserializer` and `Serializer` now cover the current rbfx packed binary helpers too: `ReadPackedVector3`, `WritePackedVector3`, `ReadPackedQuaternion`, `WritePackedQuaternion`, and the in-place `ReadBuffer(...)` helper are in-tree with the same float/double/int32/int16 encoding split that upstream uses for the currently supported cases.
- Focused IO coverage now verifies packed vector/quaternion round-trips across the supported encodings and the in-place buffer helper shape, and the full verification loop for this slice passed with `466/466` tests green, a successful `cmake --build --preset Debug --target restore`, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

IO buffer pointer parity follow-up on `2026-07-10`:

- `MemoryBuffer` no longer drops the data pointer for managed backing storage: byte-array and string-backed buffers are now pinned so `GetData()` always exposes actual buffer memory like rbfx instead of returning null for managed-backed instances.
- Focused IO coverage now also verifies the rbfx default-name behavior for `VectorBuffer`, managed-byte-array `MemoryBuffer.GetData()` pointer exposure, and UTF-8 string-backed `MemoryBuffer` pointer contents.
- Full verification for this follow-up passed with `468/468` tests green, a successful `cmake --build --preset Debug --target restore`, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

IO empty-buffer pointer parity follow-up on `2026-07-10`:

- `VectorBuffer.GetData()` and `GetModifiableData()` now match rbfx for the empty-buffer case instead of exposing a stale non-zero pointer after `Clear()` or zero-size state; they return `0` when `Size == 0`, like upstream `nullptr`.
- Focused IO coverage now verifies the empty-buffer null-pointer contract before write, after write, and again after `Clear()`.
- Full verification for this follow-up passed with `469/469` tests green, a successful `cmake --build --preset Debug --target restore`, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

IO identifier contract follow-up on `2026-07-10`:

- `src/RedBolt/IO/ScanFlags.cs` now ports rbfx `ScanFlags.h` as a managed flags enum, covering the upstream file/directory/hidden/append/recursive scan-bit contract.
- `src/RedBolt/IO/FileIdentifier.cs` now ports the upstream `FileIdentifier` URI contract: scheme/path storage, `FromUri(...)`, `ToUri()`, `AppendPath(...)`, comparison/equality operators, and file-name sanitization that collapses `.` / `..` path segments for relative and absolute paths.
- Focused IO coverage now verifies the key upstream URI cases: empty-scheme relative names, absolute file paths, `file:///...` parsing, other schemes such as `alias://`, URI reserialization, append-path behavior, and path-segment sanitization.
- Full verification for this follow-up passed with `473/473` tests green, a successful `cmake --build --preset Debug --target restore`, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core event contract follow-up on `2026-07-10`:

- `src/RedBolt/CoreEvents.cs` now matches rbfx `CoreEvents.h` more closely for the managed struct-event surface: `BeginFrameEvent.FrameNumber` is `uint`, which aligns with upstream `unsigned` instead of narrowing the frame counter to signed `int`.
- `src/RedBolt/Core/Time.cs` now sends the begin-frame event without the redundant cast, preserving the rbfx frame-number type through event dispatch.
- Focused engine coverage continues to verify begin/end frame event dispatch, and the full verification loop for this follow-up passed with `473/473` tests green, a successful `cmake --build --preset Debug --target restore`, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core object-category contract follow-up on `2026-07-10`:

- `src/RedBolt/Core/ObjectCategory.cs` now ports rbfx `ObjectCategory.h` as the managed constant surface, preserving the upstream category names, order, and string values for built-in components plus the plugin/user, render-pass, transformer, and UI categories.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/ObjectCategoryTests.cs` now verifies the managed constants against the rbfx string values.
- Full verification for this follow-up passed with `474/474` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core subsystem-cache contract follow-up on `2026-07-10`:

- `src/RedBolt/Core/SubsystemCache.cs` now ports rbfx `SubsystemCache.h` as a managed subsystem cache object with the upstream add/remove/clear/get container shape, cached fast-path entries for the currently ported built-in subsystems, and a dynamic `StringHash` map for non-cached subsystem types.
- `src/RedBolt/Core/Context.cs` now routes subsystem registration and removal through that cache while preserving the existing managed exact-type and interface lookup behavior that RedBolt already depends on.
- `src/RedBolt/Core/ObjectReflection.cs` now ignores inherited base static type-name/type-id methods when resolving type metadata, so types without their own declared `GetTypeStatic()` no longer collapse incorrectly to `Object` and the new subsystem cache uses the real managed type identity.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/SubsystemCacheTests.cs` now verifies the type-id fallback path plus cached/dynamic subsystem add, remove, and clear behavior.
- Full verification for this follow-up passed with `476/476` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core object-revision contract follow-up on `2026-07-10`:

- `src/RedBolt/Core/ObjectRevisionTracker.cs` now ports rbfx `ObjectRevisionTracker.h` as the managed revision utility surface, including the upstream `InvalidRevision` sentinel, non-zero initial revision, `GetRevision()`, and wrap-safe `MarkRevisionUpdated()` behavior.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/ObjectRevisionTrackerTests.cs` now verifies the initial revision value, monotonic updates, and zero-skipping wraparound contract.
- Full verification for this follow-up passed with `478/478` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core process/platform utility follow-up on `2026-07-10`:

- `src/RedBolt/Core/ProcessUtils.cs` now ports the process/CPU side of rbfx `ProcessUtils.h` as a managed static utility surface: `PlatformId`, dynamic library suffix selection, platform name/id, physical/logical CPU counts, minidump directory storage, total memory, login name, host name, OS version, UUID generation, process ID, and URL opening.
- The Linux-side implementations now use the platform information available in the current managed/runtime environment (`/proc/cpuinfo`, `/proc/meminfo`, `RuntimeInformation`, `Environment`, `Guid`, and shell-executed URL opening) while preserving the upstream function names and return-shape contracts.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/ProcessUtilsTests.cs` now verifies platform/name consistency, physical-vs-logical CPU count invariants, minidump directory trailing-slash behavior, total-memory/process-id/system-string availability, and UUID parseability.
- Full verification for this follow-up passed with `482/482` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core thread-safe cache follow-up on `2026-07-10`:

- `src/RedBolt/Core/ThreadSafeCache.cs` now ports rbfx `ThreadSafeCache.h` as a managed generic cache with the upstream invalidation flag, guarded one-time restore semantics, unchecked `Get()`, and default-invalid initial state.
- The implementation uses the managed threading primitives allowed by the port rules (`lock` plus `Volatile.Read/Write`) in place of rbfx `SpinLockMutex` and `std::atomic_bool`, while preserving the same caller-visible restore/invalidate contract.
- The C++ `operator=(const T&)` alias is left as an explicit `// TODO: port` note because C# has no direct assignment-operator surface for generic reference/value types.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/ThreadSafeCacheTests.cs` now verifies the initial invalid state, restore/get behavior, reinvalidation behavior, and concurrent restore semantics where the first successful restore wins.
- Full verification for this follow-up passed with `492/492` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core stop-token follow-up on `2026-07-10`:

- `src/RedBolt/Core/StopToken.cs` now ports rbfx `StopToken.h` as a managed value-type token with shared internal stop state, `Stop()`, and `IsStopped()`, preserving the upstream copy-by-value behavior where all copies observe the same stop flag.
- The managed implementation uses a shared `StrongBox<int>` plus `Volatile.Read/Write` to represent the rbfx `shared_ptr<atomic<bool>>` state without introducing any larger custom synchronization layer.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/StopTokenTests.cs` now verifies the default non-stopped state, the stop transition, and the shared-state behavior across value copies.
- Full verification for this follow-up passed with `495/495` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core profiler shim follow-up on `2026-07-10`:

- `src/RedBolt/Core/Profiler.cs` now ports the callable rbfx `Profiler.h` surface as the allowed thin shim: the upstream event/resource color constants plus `SetProfilerThreadName(...)`.
- The managed implementation maps thread naming onto `Thread.CurrentThread.Name`, setting it only when the CLR thread has not yet been named because .NET thread names are write-once.
- The rbfx Tracy profiling macros remain intentionally unported as macros; the checklist allowance for a thin shim is satisfied by the managed callable surface that current RedBolt code can actually invoke.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/ProfilerTests.cs` now verifies the constant values, first-time thread naming, and the non-overwrite behavior for already named threads.
- Full verification for this follow-up passed with `498/498` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core attribute-accessor follow-up on `2026-07-10`:

- `src/RedBolt/Core/Attribute.cs` now ports rbfx `AttributeAccessor` as the managed abstract base for attribute reads/writes, and `AttributeInfo` can now store a real accessor object instead of only delegate placeholders.
- `src/RedBolt/Scene/Serializable.cs` now supports accessor-backed attribute registration directly and routes `OnGetAttribute(...)` / `OnSetAttribute(...)` through the installed accessor when present, preserving the managed fallback storage path for plain attributes.
- The new accessor-backed surface was kept free of `?` type annotations after the follow-up correction, matching the port rule against introducing nullable type syntax in new code.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/AttributeTests.cs` now verifies that `Serializable` get/set calls are routed through a concrete `AttributeAccessor` implementation.
- Full verification for this follow-up passed with `499/499` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core work-queue helper follow-up on `2026-07-10`:

- `src/RedBolt/Core/WorkQueue.cs` now ports the rbfx `ForEachParallel(...)` helper surface as static overloads on `WorkQueue`, which is the closest managed equivalent to the upstream free templates while preserving the name, bucketed range partitioning, immediate-task scheduling, and collection/index callback behavior.
- The managed implementation uses the existing `WorkQueue` task API and the allowed thread-pool-based scheduler underneath, including the same direct-call fast path when `size <= bucket` and a thread-count fallback of at least one processing lane before `Initialize(...)` has run.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/WorkQueueTests.cs` now verifies the small-range direct path plus complete index coverage for both range and collection overloads.
- Full verification for this follow-up passed with `485/485` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core event-handler contract follow-up on `2026-07-10`:

- `src/RedBolt/Core/EventHandler.cs` now ports rbfx `EventHandler` as the managed event-dispatch metadata surface, preserving receiver, sender-filter, and event-type tracking around the struct-event system.
- `src/RedBolt/Core/Context.cs` now tracks the active event sender and handler during dispatch, and `src/RedBolt/Core/Object.cs` exposes the matching rbfx-style `GetEventSender()` and `GetEventHandler()` helpers through the global managed context.
- Sender-specific subscriptions now stay constrained to `RedBolt.Object`, matching the upstream object-event contract instead of accepting arbitrary managed senders.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/ContextTests.cs` now verifies that dispatch exposes the current sender and handler metadata only during active invocation and clears it afterward.
- Full verification for this follow-up passed with `486/486` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/WorkQueueTests.cs` now verifies the direct-call small-range path, full coverage of a partitioned index range, and the collection overload’s index/value pairing.
- Full verification for this follow-up passed with `485/485` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

Core command-line parser follow-up on `2026-07-10`:

- `src/RedBolt/Core/CommandLineParser.cs` now serves as the completed managed replacement for rbfx `CommandLine.h` / CLI11 integration: flag and value options are represented by separate non-nullable option types, parse results are non-nullable, and the parser retains the existing help, `--name=value`, and usage-generation behavior without `?` type syntax.
- `src/RedBolt/Engine/Application.cs` now keeps the immediate command-line error path non-nullable as well, with a non-null `ErrorMessage` surface and explicit `Run()` vs `Run(string[] args)` entry points instead of a nullable argument array.
- Focused coverage in `tests/RedBolt.Tests/CoreTests/CommandLineParserTests.cs` now verifies help handling, flag/option application including empty inline option values, usage text, and value-mismatch failures, while `tests/RedBolt.Tests/EngineTests/ApplicationCommandLineTests.cs` continues to verify engine parameter application and unknown-option rejection through the application entry point.

Math integer-rect follow-up on `2026-07-10`:

- `src/RedBolt/Math/IntRect.cs` now ports the missing integer-side `Rect.h` helpers more closely: `IntRect` gained the upstream array constructor shape, scalar multiply/divide operators, rect-vs-rect `IsInside(...)`, `Contains(...)`, `Data`, `ToHash()`, and managed `Begin()` / `End()` helpers.
- `IntRectIterator` now exists in-tree with the rbfx row-by-row increment semantics and rectangle/index equality behavior, which closes the previously missing `IntRectIterator` checklist gap even though the float-side `Rect` file remains only partially aligned overall.
- Focused coverage in `tests/RedBolt.Tests/MathTests/GeometryTypesTests.cs` now verifies integer rect containment, row-major iterator traversal, and the exclusive-max containment rule.

Math float-rect follow-up on `2026-07-10`:

- `src/RedBolt/Math/Rect.cs` now ports the missing float-side `Rect.h` surface more closely too: the array constructor, `Data`, `Define(...)` overloads, `ToVector4()`, `ToHash()`, and the upstream scalar `*` / `/` operators are now in place, replacing the earlier non-rbfx componentwise `Rect * Rect` / `Rect / Rect` operators.
- Focused coverage in `tests/RedBolt.Tests/MathTests/GeometryTypesTests.cs` now verifies the float rect define/copy path, clipping, scalar scaling round-trip, and vector4 conversion alongside the earlier merge/containment checks.

Math vector4 follow-up on `2026-07-10`:

- `src/RedBolt/Math/Vector4.cs` now ports more of the upstream `Vector4.h` instance surface: the two-`Vector2` and float-array constructors, `Length`, `LengthSquared`, `IsInf`, the indexed component accessor, axis projection, `ToVector2()` / `ToVector3()` / `ToIntVector2()` / `ToIntVector3()`, and the rbfx raw-float `ToHash()` contract are now in place.
- `src/RedBolt/Math/MathDefs.cs` now also carries the remaining upstream namespace-level `Vector4` helper functions (`VectorLerp`, `VectorMin`, `VectorMax`, `VectorFloor`, `VectorRound`, `VectorCeil`) as the managed analogue of rbfx free functions, which closes the remaining `Vector4.h` checklist gap.
- Focused coverage in `tests/RedBolt.Tests/MathTests/GeometryTypesTests.cs` now verifies the new constructors, indexer, projection helper, conversions, hash behavior, and the per-component free helper functions.

StaticScene allocation triage on `2026-07-08`:

- StaticScene is now visually correct again and late steady-state managed allocations have been removed from the final tree.
- The washed-out regression came from reconstructing swap-chain output state from the wrong source in `Graphics`; the fix was to keep rbfx-aligned render-target semantics and cache the effective swap-chain view formats once instead of rebuilding them from lossy descriptor paths every frame.
- The allocation work was a sequence of real hot-path fixes rather than a single leak:
  - remove `Enum.HasFlag` boxing in scene and render-buffer flag checks
  - stop per-draw/per-pass `byte[]`, `PinnedBuffer`, `IBuffer[]`, and `RenderTargetView[]` churn
  - stop creating owned Diligent `Viewport` wrappers on every bind; reuse one wrapper object instead
  - stop `params` array churn in `PipelineStateOutputDesc`
  - stop per-frame delegate creation in `RenderBufferManager` and `Application.Run`
  - stop repeated `RenderOutputTargetInfo` allocation for the unchanged swap-chain target
  - replace interface-shaped `foreach` / LINQ hot paths that boxed enumerators (`RenderPass` scans, custom-uniform field iteration)
  - cache fullscreen shader-define strings for pipeline lookup reuse
- Final verification on `2026-07-08`:
  - same-run allocation sampling showed `GC.GetTotalAllocatedBytes()` flat from `20s` through `60s` after startup
  - a clean-source late `dotnet-trace` window (`35s` to `55s`) showed only EventSource/trace-attachment allocations and no app-side `GCAllocationTick` events
  - `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 70 --screenshot-on-exit /tmp/staticscene-final-clean.png` produced the expected non-washed-out scene with the GC HUD visible

SystemUI / Dear ImGui WIP status on `2026-07-07`:

- SharpCpp now walks `extern "C"` linkage blocks when parsing headers, which unblocks standard generated bindings for `native/RedBolt.Native/include/RedBolt/ImGuiBackend.hpp`.
- `src/RedBolt/SystemUI/ImGuiNative.cs` was removed; `SystemUI` and `ImGuiDiligentRendererEx` now call generated `RedBolt.Bindings.DearImGui.ImGui.ImGuiBackend*` entry points instead of handwritten `LibraryImport` bindings.
- Diligent binding generation no longer uses `DILIGENT_SHARP_GEN=1`; the generator now handles the upstream headers directly, keeps third-party Diligent headers unpatched, flattens anonymous record fields correctly, and emits constructor default-dispatch variants without depending on ambiguous native overload resolution.
- The managed graphics layer and focused tests now use upstream Diligent names (`FirstArraySlice` / `NumArraySlices`) and explicit `TextureDesc` array-vs-depth fields instead of the old Sharp-only aliases.
- `native/RedBolt.Native/thirdparty/ImGuiDiligentRenderer.cpp` now applies rbfx-style gamma-to-linear correction for ImGui output when the backbuffer format is sRGB, which addresses the washed-out UI path seen with the stock Diligent ImGui shader.
- `src/RedBolt.DearImGui/DearImGui.cs` now follows rbfx `SystemUI` frame timing more closely again: it opens the ImGui frame on `InputEnd`, emits a dedicated `DearImGuiEvents.EndRenderingDearImGui` hook during `GraphicsEvents.EndRendering`, and renders draw data immediately there instead of relying on the later general `EndFrame` fallback.
- `cmake --build --preset Debug --target redbolt-csharp` is green after the generator cleanup, focused graphics/Diligent tests pass, and `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2` starts and exits cleanly with the generated Dear ImGui backend path.
- Remaining verification gap: automated `StaticScene` screenshots still do not capture the ImGui overlay on this machine even though the generated binding path is live, so GC stats visibility in the sample still needs runtime follow-up.

Slice 1 through the first runtime integration layer are landed:

- foundational `RenderPipeline` defs and shared state are in tree
- `RenderPass` / `RenderPath` are in tree with focused tests
- `RenderBufferManager` now exists as a managed runtime object for viewport/output state
- `RenderPipeline` / `RenderPipelineView` now exist as scene-side runtime objects
- `Renderer` now routes scene viewports through `RenderPipeline` when a scene has that component
- `RedBolt.Sample.StaticScene` now attaches a `RenderPipeline` component, so the sample uses the new path instead of bypassing it
- `RenderPath` is being upgraded from a RedBolt-only XML subset to rbfx-compatible `.renderpath` loading and default render-path resolution
- `.renderpath` JSON loading now instantiates real built-in pass classes for the upstream default path (`AutoExposurePass`, `AmbientOcclusionPass`, `BloomPass`, `ToneMappingPass`, `OutlineRenderPass`, `FullScreenShaderPass`) instead of collapsing everything into a generic placeholder
- focused tests now verify loading the real upstream `rbfx/bin/CoreData/RenderPaths/Default.renderpath` asset and collecting its exposed parameters
- `Graphics.RenderViewport` has been split into reusable target-binding, scene-draw, clear, present, and unbind steps so the managed `RenderPipeline` owns frame execution order instead of delegating the whole frame to the legacy viewport helper
- `RenderBufferManager` can now bind its writable output buffer through `Graphics`, and render-buffer textures are uploaded as real shader-resource plus render-target GPU textures when they have no CPU pixel payload
- `RenderBufferManager` now routes read/write color-output frames through offscreen primary and secondary color buffers and resolves the final color back to the viewport target when needed
- `Graphics` now has a managed fullscreen-quad draw path with cached pipeline state, reflection-driven custom-uniform layouts, and generic shader-backed postprocess execution for render-path passes
- `ToneMappingPass` and generic `FullScreenShaderPass` now execute managed fullscreen postprocess draws instead of only contributing metadata and trait flags
- `RenderBufferManager` now honors `RenderBufferManagerSettings.ReadableDepth` by allocating a readable depth texture even on frames that do not need color ping-pong, and shared render-pass state now exposes that offscreen depth buffer through the normal viewport depth slot
- `Graphics.DrawFullscreenQuad` now has generic managed bindings for `sDepthBuffer` in addition to the existing color inputs, so upstream fullscreen shaders that sample depth can be ported without adding manual interop
- render-path trait plumbing now reaches farther into the managed runtime:
  - per-frame render-buffer requirements now carry readable-depth and filtered-color requests in addition to color read/write
  - `AmbientOcclusionPass` can now request depth-backed frames through that trait path instead of being permanently disabled by the base pipeline settings
- the regular scene-material path now shares the same managed custom-uniform plumbing as fullscreen passes:
  - `Graphics` reflects compiled `Custom` uniform-buffer members from native Diligent shader metadata, allocates a matching `Custom` constant buffer for geometry pipelines, and binds it on both shader stages when present
  - material shader parameters marked custom now upload through that buffer, which is the remaining prerequisite for upstream-style outline producer work instead of RedBolt-only postprocess shortcuts
- managed custom-uniform uploads now also move closer to the intended rbfx hot path instead of rebuilding boxed parameter maps for built-in passes:
  - `Graphics` can now write typed fullscreen and clear-quad parameter structs directly into parsed custom uniform layouts using cached member writers, including uniform-name fallback for rbfx `cName` conventions
  - fullscreen/material pipeline resources now keep reusable custom-uniform scratch buffers, so built-in passes and material custom uploads no longer allocate fresh `byte[]` payloads on every draw
  - built-in postprocess passes (`AutoExposurePass`, `BloomPass`, `AmbientOcclusionPass`, `OutlineRenderPass`) and subrect clear quads now use that typed upload path instead of temporary `Dictionary<string, object?>` parameter maps on the render hot path
  - managed fullscreen-quad rendering now also honors the existing `flipVertical` flag by writing a flipped fullscreen projection into the camera constant buffer, matching the upstream rbfx quad path instead of silently ignoring vertical-flip requests
  - managed GPU texture uploads now also recreate cached native textures when the backing managed `Texture` changes size, format, array depth, cube/array kind, or mip count, which fixes the rbfx-style dynamic render-buffer resize path instead of leaving stale Diligent texture objects bound after viewport-driven reallocations
  - managed render-target textures now carry rbfx-style multisample metadata through `Texture`, `TextureRenderBuffer`, `RenderTargetView`, and `RenderOutputTargetInfo`, so viewport/output MSAA settings stop collapsing back to `1x` once the pipeline allocates or inspects managed texture outputs
  - managed Diligent texture creation and cache matching now also honor texture sample count, so multisampled render buffers recreate stale native textures when the requested MSAA level changes and write the expected `TextureDesc.SampleCount` into GPU texture allocation
- the outline producer side is now in-tree as a managed scene feature instead of a missing shared-buffer hole:
  - `OutlineGroup` now exists as a scene component that tracks selected drawables, caches outline materials, and rewrites eligible material passes to `v2/M_OutlinePixel` with the rbfx custom `OutlineColor` uniform
  - `RenderPipeline` now allocates and publishes a dedicated `outline` render buffer, renders selected `StaticModel` drawables into it before postprocessing, and lets `OutlineRenderPass` consume that buffer through the existing managed fullscreen path
- the next shared scene-buffer producer is now wired for SSAO too:
  - `RenderPipeline` now allocates and publishes `SharedRenderPassState.NormalBufferId` when an enabled `AmbientOcclusionPass` is present, renders a managed normal prepass into that buffer using the current scene depth, and keeps the output format aligned with rbfx `DecodeNormal` expectations (`RGBA8`, `normal * 0.5 + 0.5`)
  - the new `M_NormalBuffer` shader reuses the upstream material helper stack so alpha-masked materials still discard correctly and tangent-space normal maps are applied when available, while staying within the current single-render-target managed renderer
- viewport/output target handling moved closer to upstream rbfx `RenderBufferManager` semantics:
  - `CommonFrameInfo.RenderTarget` now carries an explicit managed output-surface descriptor instead of an opaque `object?`, and the default pipeline populates it from the active swap-chain output
  - `RenderBufferManager` now derives its writable viewport color/depth outputs from that descriptor, can keep rendering directly into matching texture-backed targets when readable depth is already available, and falls back to substitute buffers only when the output surface actually fails rbfx-style matching checks
  - final color resolves now preserve the existing swap-chain presentation fast path for backbuffer output while using the managed fullscreen copy path with `ColorSpaceTransition.Automatic` for texture-backed output surfaces
  - `SwapColorBuffers` now mirrors rbfx more closely too: unsupported swaps still fail fast, and buffer synchronization now goes through the managed `RenderBufferManager.DrawTexture` path instead of the older `Graphics.DrawCopyTexture` shortcut
  - matching full-viewport texture outputs can now stay bound as the primary color target even for color read/write frames, with rbfx-style flip-state tracking so later frames may reuse that direct output instead of always forcing a substitute primary buffer
  - readable-depth frames now also match rbfx more closely for partial or otherwise non-simple texture outputs by forcing a substitute depth buffer instead of incorrectly sampling the linked output depth texture directly
  - `ReadableColor` requests without actual color read/write no longer allocate a redundant secondary color buffer; they now follow rbfx by only forcing a simple readable primary output, with direct texture output reuse still requiring the same filter/depth matching checks as upstream
  - output color-buffer format selection now follows rbfx `RenderPipelineColorSpace` semantics instead of always inheriting the raw output surface format: non-optimized modes pick the pipeline-requested format, while `Optimized` keeps the actual target format
  - managed output-surface metadata now also treats sRGB targets as linear-color-space pipeline outputs like rbfx does, and managed color render buffers mark their RGBA8 linear-LDR outputs as sRGB so fullscreen copy paths make the same gamma/linear decisions
  - managed output-surface and intermediate-buffer metadata now also treat both supported float color formats (`RG16F` and `RGBA16F`) as linear/HDR outputs, which brings our simplified format checks closer to rbfx's component-type-based `isHDR_` and linear-color-space inference
  - `RenderBufferManager.SetFrameSettings` now matches rbfx more closely for readable-color frames too: `SupportColorReadWrite` still implies `ReadableColor`, but plain `ReadableColor` no longer forces bilinear filtering, so matching nearest-filter texture outputs can remain direct instead of allocating substitute buffers unnecessarily
  - partial output clears now match rbfx's OpenGL fullscreen-quad depth convention too: managed `RenderBufferManager.ClearOutputRect` remaps `[0, 1]` depth into clip-space `[-1, 1]` before drawing the clear quad, which avoids backend-specific depth mismatches in subrect clear paths
  - partial output clears now follow the rbfx branch split for all clear-target combinations instead of only color clears: full-viewport clears still use direct API clears, while subrect clears now route through a dedicated managed clear-quad pipeline that honors color-write masking, depth writes, stencil writes, and stencil ref setup
  - depth/stencil-only clear paths now bind render targets more like rbfx as well: managed `Graphics.SetRenderTargets` can bind a depth attachment without any color targets, and `RenderBufferManager.ClearDepthStencil` now uses that path instead of quietly keeping the writable color buffer attached during depth-only clears
  - frame-only readable-depth requests now feed the same output-matching decisions as persistent readable-depth settings: managed `RenderBufferManager` uses the combined per-frame/per-pipeline depth-readability requirement when deciding whether direct texture output is still valid or whether substitute color/depth buffers are required
  - frame-derived readable-depth requests now also influence the “simple texture output” fast path itself, so managed direct-output reuse and partial-output substitution now match rbfx for both persistent and per-frame depth-readability requirements instead of only the persistent settings path
  - pass-created managed `TextureRenderBuffer`s now behave more like rbfx’s frame-driven buffers too: `RenderBufferManager` tracks intermediate texture buffers and refreshes their dimensions/metadata from the current viewport on each `OnViewportDefined`, so cached bloom/SSAO/auto-exposure targets no longer get stuck at their first-use size after later viewport changes
  - render-target view slicing is now closer to rbfx too: managed `TextureRenderBuffer` honors `RenderBufferFlag.CubeMap` by allocating `TextureCube` backing storage and exposing face-specific `RenderTargetView.TextureSlice` views even for face 0, while `Graphics` now resolves cubemap and 2D-array slice views into single-slice Diligent render-target/read-only-depth views instead of rejecting every non-`Texture2D` slice outright
- the remaining built-in fullscreen passes are no longer metadata-only:
  - `BloomPass` now allocates its rbfx-style mip-chain work buffers, gathers bright regions, applies separable blur, and blends the bloom result back into the output
  - `AmbientOcclusionPass` now allocates its intermediate buffers, evaluates/blur/combine SSAO through `v2/P_SSAO`, and binds optional normal + depth inputs through the managed fullscreen path
  - `OutlineRenderPass` now executes `v2/P_Outline` when an outline color buffer is supplied by shared render-pass state
- real sample verification exposed two regressions introduced during the managed pipeline work and both are now fixed:
  - CPU-backed textures were created on GPU with full rbfx mip counts but only uploaded level 0, which made mipmapped sampling go black; managed GPU uploads now keep CPU-backed textures single-mip until full subresource upload is implemented
  - render-pipeline offscreen color buffers were being paired with the viewport depth target; managed `RenderBufferManager` now allocates a substitute depth buffer for read/write offscreen frames, matching the upstream rbfx substitute-buffer path more closely
- `RedBolt.Sample.FirstCube` and `RedBolt.Sample.StaticScene` now both exit cleanly again and produce non-black screenshots with `--screenshot-on-exit`
- core event/object parity moved closer to rbfx:
  - `Object`/`Context` now support sender-scoped bulk unsubscribe, block-events gating, and context global vars
  - named-event dispatch no longer snapshots subscriptions with `ToArray()` on every send; subscription removal is deferred and compacted after dispatch instead
- hot-path and parity cleanup continued in the same area:
  - `VariantMap` now stores a managed `Variant` tagged-union struct instead of boxed `object?` values, and value-animation event-frame serialization now round-trips that variant data through XML and JSON
  - `Object`/`Context` now also expose `HasSubscribedToEvent`, `HasEventHandlers`, and `UnsubscribeFromAllEventsExcept` parity helpers
- render-pipeline mutation behavior is closer to upstream rbfx:
  - `RenderPipeline` now raises managed settings/parameters/render-path change events, ignores no-op render-path parameter updates, and no longer dirties revision state for frame shader parameter writes
  - `RenderPath.RepairEnabledRenderPasses` now preserves already-matching pass arrays without copying, and `PipelineStateOutputDesc.RenderTargetFormats` no longer allocates on access
  - `RenderPipeline` now listens for `RenderPath` resource reload completion and re-collects newly exposed render-path parameters without requiring a fresh `SetRenderPath` call
  - render-pass buffer hookups now rebind when shared-state render buffers are replaced and clear optional cached buffers when they disappear, which keeps managed postprocess state aligned with dynamically reallocated intermediate textures such as the new outline buffer
- forward-looking render API / pipeline surface is much closer to rbfx:
  - the remaining canonical `RenderAPIDefs` enums were moved into `RenderAPIDefs.cs`, and the missing `RenderPipelineDefs` flags/callback shells now exist in-tree for future batch-renderer / scene-processor porting
  - `Graphics.GetShader(ShaderType, name, defines)` now has a managed lookup surface via `ShaderVariation`, and `RenderBufferManager` now exposes the missing copy/clear/render callbacks (`DrawTexture*`, `CopyTextureRegion`, `Clear*`, `OnRenderBegin`, `OnRenderEnd`) needed by upcoming post-process ports
- low-level managed hot-path cleanup in this slice also includes reusing `CommonFrameInfo` objects across `Define()` calls and making `RenderBufferManagerFrameSettings` a value type
- focused unit coverage was expanded again for readable-depth trait aggregation, frame-derived depth allocation, bloom buffer sizing, dynamic intermediate-buffer resizing, reflection-driven custom-uniform packing, typed custom-uniform struct uploads, cubemap render-buffer creation, sliced render-target view resolution, outline-material rewriting, render-buffer rebinding, the normal-buffer shader/publication path, explicit output-target matching behavior, and the new partial-clear quad decision/state helpers; `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-build --no-restore` is green again with `328/328` passing after the latest render-target slice-view cleanup
- focused unit coverage now also includes the managed fullscreen camera-buffer flip path via `GraphicsMatrixPackingTests`, verifying that fullscreen postprocess draws write the flipped projection sign into `cViewProj` when `flipVertical` is requested
- focused graphics coverage now also verifies that resizing a managed render-target texture causes the cached uploaded Diligent texture descriptor to follow the new dimensions instead of staying stale, via `GraphicsRenderTargetViewTests`
- focused coverage now also verifies that managed output-target metadata derives multisample from backing textures, that `RenderBufferManager` propagates explicit non-inherited MSAA settings into managed color/depth outputs, and that `Graphics` writes managed texture sample counts into native Diligent texture descriptors
- sample validation for this checkpoint used `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-outputtarget-2.png`, and the captured frame shows the scene geometry rendering correctly again after restoring the swap-chain resolve path
- sample validation was rerun again after the `SwapColorBuffers` cleanup using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-swapbuffers-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after allowing matching texture outputs to remain primary for color read/write frames using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-direct-output-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the readable-depth partial-output fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-readabledepth-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the `ReadableColor` buffer-allocation cleanup using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-readablecolor-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the color-space-aware output-format fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-colorspace-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the sRGB/linear-LDR metadata fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-srgb-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the float-format linear/HDR metadata fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-floatformats-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the readable-color filter matching fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-readablecolor-filter-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the partial-clear depth remap fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-partialcleardepth-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the depth-only clear binding fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-depthonlyclear-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the frame-readable-depth output-matching fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-framereadabledepth-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the partial-clear quad pipeline fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-partialclearquad-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the dynamic intermediate-buffer refresh fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-dynamicbuffers-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the typed custom-uniform upload cleanup using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-typedcustom-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the cubemap/slice-view render-target cleanup using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-sliceviews-1.png`, and the captured frame remained visually stable
- SharpCpp fixed-size array support now uses a shared `SharpCpp.Support.NativeArray<T>` plus generated per-field proxy accessors for handle-backed and nested fixed arrays, so bindings no longer emit duplicate `NativeArray<>` types per assembly
- fixed-array fields like Diligent `BlendStateDesc.RenderTargets[8]` can now be configured from managed code through item getter/setter thunks instead of raw memory aliasing
- `cmake --build --preset Debug --target redbolt-generate-native-bindings` and `cmake --build --preset Debug --target redbolt-csharp` are green again after the array-support slice
- sample verification was rerun against the current render-pipeline path on `2026-07-06`; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-outline-3.png` completed successfully after the managed outline-producer slice and produced a valid `1280x720` PNG with the expected mushroom field, explicit foreground outline test geometry, and black background (the sample does not create a `Zone` or `Skybox`)
- sample verification was rerun again on `2026-07-06` after the normal-buffer slice; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-normalbuffer-1.png` completed successfully and still produced a valid `1280x720` frame with the expected mushroom field and ground plane, confirming that the added normal prepass did not regress the visible sample output
- sample verification was rerun again on `2026-07-06` after fixing managed fullscreen `flipVertical` propagation; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-fullscreen-flip-1.png` completed successfully and still produced a valid `1280x720` frame with the expected mushroom field, ground plane, and black background
- sample verification was rerun again on `2026-07-06` after fixing uploaded-texture cache invalidation for resized managed render targets; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-uploadedtexture-resize-1.png` completed successfully and still produced a valid `1280x720` frame with the expected mushroom field, ground plane, and black background
- sample verification was rerun again on `2026-07-06` after threading managed texture multisample metadata through render-buffer allocation and native texture upload; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-multisample-1.png` completed successfully and still produced a valid `1280x720` frame with the expected mushroom field, ground plane, and black background
- managed multisample resolve/readback parity moved closer to rbfx too:
  - cached/default Diligent texture views are now retained before RedBolt stores and later releases them, fixing the direct OpenGL readback crash that appeared once managed render-target tests started exercising default SRV/RTV paths
  - `Graphics.SaveTexture` now creates an explicit staging `TextureDesc` instead of mutating a borrowed `GetDesc()` wrapper, which fixes single-sample and MSAA managed render-target readback through the resolved shader-resource texture path
- focused graphics coverage now also includes direct OpenGL smoke readback plus managed single-sample/MSAA render-target readback checks via `DiligentOpenGLTests` and `GraphicsRenderTargetViewTests`
- sample verification was rerun again on `2026-07-06` after the managed resolve/readback ownership fix; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-readback-1.png` completed successfully and still produced a valid `1280x720` frame with the expected mushroom field, ground plane, and black background
- managed fullscreen/postprocess shader lookup is now aligned with the shader loader contract instead of double-prefixing `Shaders/GLSL/v2/`:
  - hardcoded fullscreen/pass shader names no longer include the redundant `v2/` prefix (`X_CopyTexture`, `X_CopyFramebuffer`, `X_ClearFramebuffer`, `P_*`, `M_OutlinePixel`), which fixes the silent no-op fullscreen copy/postprocess path that had regressed the sample back to a black screen
  - fullscreen pipeline compilation now also injects `URHO3D_GEOMETRY_STATIC` for the fullscreen quad path, which lets rbfx postprocess shaders such as `P_Outline` compile their shared `_VertexTransform.glsl` vertex path correctly
- focused graphics coverage now also verifies that `Graphics.DrawCopyTexture` can copy both CPU-backed textures and render-target textures into a destination render target, closing the exact regression that previously left the destination black in the managed fullscreen copy path
- sample verification was rerun again on `2026-07-07` after fixing the fullscreen shader lookup/define path; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-fixed.png` completed successfully and now again captures visible scene geometry plus outline compositing instead of a fully black frame
- foundational `RenderAPI` object-model porting has now started in-tree instead of staying only in audit notes:
  - `RenderDevice` is now a real engine subsystem registered in `Context`, backed by the existing managed `Graphics`/`Renderer` subsystems for current caps/settings queries and device-object lifecycle dispatch
  - `DeviceObject` now exists as the shared managed GPU-object base with rbfx-style data-loss/debug-name helpers plus invalidate/restore/destroy event routing
  - `PipelineState` now inherits `DeviceObject` instead of remaining a free-standing POCO, which gives the existing stub a real place in the future RenderAPI object graph
  - `RenderBackend.Count` and `DeviceObjectFlag` now exist in `RenderAPIDefs.cs`, closing two audited foundational enum gaps
  - `RenderContext`, `RenderScope`, `RenderPool`, and `DrawCommandQueue` now exist as managed rbfx-named RenderAPI surfaces, with `RenderDevice` owning default instances and delegating current viewport/render-target state to the existing `Graphics` backend
  - `Graphics` now tracks current viewport, current render-target size, swap-chain-vs-texture binding state, and current `PipelineStateOutputDesc`, so the new `RenderContext` can expose rbfx-style getters without re-owning backend state prematurely
  - managed swap-chain output tracking now also covers the default Diligent `D32` depth attachment by adding `TextureFormat.Depth32` plus the matching Diligent format translations and depth-texture predicates used by render-buffer allocation
  - the next RenderAPI groundwork layer is now in-tree too:
    - `PipelineStateCache` is now a real engine subsystem, registered in `Context`, caching managed `PipelineState` instances by rbfx-style `PipelineStateDesc` keys
    - `GraphicsPipelineStateDesc`, `ComputePipelineStateDesc`, and `PipelineStateDesc` now exist with hash/equality semantics, defensive cloning of mutable nested descriptors, and initialization checks
    - `NamedSamplerStateDesc` and `StencilOp` now exist in `RenderAPIDefs.cs`, while the shared render enums were nudged closer to rbfx by restoring trailing `Max` members and `PrimitiveType.TriangleFan`
    - `InputLayoutDesc`, `ImmutableSamplersDesc`, and `PipelineStateOutputDesc` now expose copy constructors suitable for descriptor freezing, and immutable samplers now publish names/descriptions spans instead of being write-only
  - the next shader/resource identity slice is now in-tree too:
    - `ShaderBytecode` now exists as a managed rbfx-named shader-binary container with bytecode MIME, vertex-attribute metadata, stream save/load helpers, and value semantics
    - `RawShader` now exists as a `DeviceObject` that owns `ShaderBytecode`, exposes rbfx-style type/bytecode/handle accessors, and can create a native Diligent shader from stored GLSL source or binary payload
    - `ShaderVariation` now inherits `RawShader` instead of remaining a standalone identity object, compiling prepared shader source into managed `ShaderBytecode` and native OpenGL shader handles
    - `GraphicsPipelineStateDesc` and `ComputePipelineStateDesc` now accept `RawShader` instead of only `ShaderVariation`, keeping shader equality/hash behavior aligned with upstream variation identity when possible and falling back to bytecode equality for non-variation shaders
  - the next foundational support-type slice from the API audit is now in-tree too:
    - `ConstantBufferCollection` and `ConstantBufferCollectionRef` now exist as managed rbfx-named constant-buffer packing helpers with aligned block allocation, buffer growth, and typed parameter storage
    - `RenderDeviceSettings` now exposes rbfx-style `Vulkan` and `D3D12` subsettings surfaces instead of flattening everything into one thin object
    - `RenderPoolSettings` now exists and `RenderPool` exposes default settings through a managed property, even though the wider pooling/cache behavior is still placeholder-level
  - the next buffer-layer dependency slice is now in-tree too:
    - `RawBufferParams` and `RawBuffer` now exist as managed rbfx-named GPU buffer surfaces with `DeviceObject` lifecycle, native Diligent `IBuffer` creation, shadow-data tracking, update/resolve helpers, and restore-after-loss behavior
    - `RenderPool` now allocates and caches quantized uniform `RawBuffer` instances by `(id, size)` instead of remaining only a placeholder shell
  - the next command-recording slice from the API audit is now in-tree too:
    - `DrawCommandDescription` and `ShaderResourceRange` now exist as managed rbfx-named queue payload types
    - `DrawCommandQueue` now records pipeline state, scissor/stencil state, vertex/index buffers, graphics draw calls, and compute dispatches as rbfx-style command snapshots instead of remaining only a clip-plane shell
    - `DrawCommandQueue.ExecuteInContext(...)` now replays clip-plane state and accumulated draw/dispatch stats through `RenderContext`, while real backend PSO/resource binding playback is still pending the reflection/resource layer
  - the next shader-reflection slice from the API audit is now in-tree too:
    - `ShaderProgramReflection`, `UniformBufferReflection`, `UniformReflection`, and `ShaderResourceReflection` now exist as managed rbfx-named RenderAPI surfaces
    - reflection is currently native-only: it walks compiled Diligent `IShader` metadata from `RawShader` handles, discovers constant buffers plus SRV/UAV resources, and threads that data into `PipelineState.GetReflection()`
    - `ShaderProgramReflection` now also walks native constant-buffer member arrays, exposing rbfx-style uniform names/offsets/sizes plus variable-class/type metadata and hashing uniform buffers by reflected member layout instead of buffer size only
    - `Graphics` no longer parses shader source or uses regex fallbacks for `Custom` uniform buffers; geometry, fullscreen, and clear-quad pipelines now derive custom uniform layouts from compiled shader reflection only
    - the current Diligent binding surface now exposes rbfx-style constant-buffer variable array access through a parsed RedBolt native support header plus a small managed partial-class convenience layer, replacing the temporary local descriptor overlay without adding handwritten bindings
    - `ShaderProgramReflection.ConnectToShaderVariables(...)` now matches resources to native SRB variables by enumerating bound shader variables instead of parsing shader source or adding RedBolt-only fallback behavior
  - the next `RenderTargetView` parity slice from the API audit is now in-tree too:
    - `RenderTargetView.GetView()` now exposes the effective native Diligent texture view through the active managed `RenderDevice`/`Graphics` backend instead of forcing callers to reflect into private helpers
    - `RenderTargetView.MarkDirty()` now propagates to managed render-target textures, matching rbfx’s resolve-dirty behavior for multi-sampled render targets with separate readable textures
    - swap-chain `RenderTargetView` instances now report live format and multisample metadata instead of falling back to `Unknown` / `1x`
  - the next raw-texture/pool slice from the API audit is now in-tree too:
    - `RawTexture`, `RawTextureParams`, and `RawTextureUAVKey` now exist as managed rbfx-named RenderAPI surfaces
    - `RawTexture` is currently a `DeviceObject` wrapper over the existing managed `Texture` hierarchy, preserving rbfx-style parameter and sampler-state ownership while the backend-facing GPU handles are still delegated to `Graphics`
    - `RenderPool.GetTexture(...)` and `RecycleTextures()` now exist with rbfx-style transient versus persistent texture caching semantics keyed by `RawTextureParams` plus optional persistence key
    - `RenderTargetView` now also accepts `RawTexture` inputs directly instead of only the lower-level managed `Texture` resources
    - `RawTexture.GetHandle()` now exists too, exposing the current native Diligent texture handle through `RenderDevice`/`Graphics` without forcing callers back into private graphics helpers
  - the next raw-texture/pool support slice from the API audit is now in-tree too:
    - `OptionalRawTextureRTV` now exists as a managed helper wrapper around an optional `RenderTargetView`, replacing the older local nullable alias in `RenderContext`
    - `RenderPool.AllocateScratchBuffer(...)` and `ReleaseScratchBuffer(...)` now exist with rbfx-style 16-byte alignment, persistent scratch-block reuse, temporary overflow allocations, and growth after overflow release
  - focused validation for these RenderAPI slices used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ContextTests|FullyQualifiedName~RenderDeviceTests|FullyQualifiedName~RenderContextTests|FullyQualifiedName~PipelineStateCacheTests"` and `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - sample validation was rerun for the graphics-touching alias fix on `2026-07-07`; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-pipelinestatecache.png` completed successfully and still produced the expected mushroom field, outlines, ground plane, and black background (the sample still has no `Zone`/`Skybox`)
  - focused validation for the shader slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-build --filter 'FullyQualifiedName~RawShaderTests|FullyQualifiedName~PipelineStateCacheTests|FullyQualifiedName~RenderContextTests|FullyQualifiedName~ShaderFileTests'` and `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - sample validation was rerun again on `2026-07-07` after the `RawShader` / `ShaderBytecode` slice using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-rawshader-checkpoint.png`; the captured `1280x720` frame still shows the expected mushroom field, magenta outline test geometry, ground plane, and black background (the sample still has no `Zone`/`Skybox`)
  - focused validation for the support-type slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~ConstantBufferCollectionTests|FullyQualifiedName~RenderDeviceTests|FullyQualifiedName~RawShaderTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - focused validation for the buffer slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~RawBufferTests|FullyQualifiedName~RenderDeviceTests|FullyQualifiedName~ConstantBufferCollectionTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - focused validation for the command-queue slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~DrawCommandQueueTests|FullyQualifiedName~PipelineStateCacheTests|FullyQualifiedName~RenderDeviceTests|FullyQualifiedName~ConstantBufferCollectionTests|FullyQualifiedName~RawBufferTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - focused validation for the native shader-reflection slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~ShaderProgramReflectionTests|FullyQualifiedName~RawShaderTests|FullyQualifiedName~PipelineStateCacheTests|FullyQualifiedName~DrawCommandQueueTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - focused validation for the `RenderTargetView` slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~GraphicsRenderTargetViewTests|FullyQualifiedName~RenderContextTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - sample validation was rerun again on `2026-07-07` after the `RenderTargetView` slice using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-rendertargetview-1.png`; the captured `1280x720` frame still shows the expected mushroom field, magenta outlines, ground plane, and black background
  - focused validation for the raw-texture/pool slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~RawTextureTests|FullyQualifiedName~RawBufferTests|FullyQualifiedName~RenderDeviceTests|FullyQualifiedName~RenderContextTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - focused validation for the raw-texture handle follow-up used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~RawTextureTests|FullyQualifiedName~GraphicsRenderTargetViewTests|FullyQualifiedName~RenderContextTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - sample validation was rerun again on `2026-07-07` after the `RawTexture.GetHandle()` follow-up using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-rawtexture-handle-1.png`; the captured `1280x720` frame still shows the expected mushroom field, magenta outlines, ground plane, and black background
  - focused validation for the pool-support slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~RenderPoolTests|FullyQualifiedName~RawTextureTests|FullyQualifiedName~RawBufferTests|FullyQualifiedName~RenderDeviceTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - the next raw-texture follow-up from the audit is now in-tree too:
    - `RawTexture.CreateUAV(...)` and `GetUAV(...)` now exist with rbfx-style key normalization/validation and cached native UAV view ownership
    - `RenderDevice` / `Graphics` now create and cache native Diligent texture UAV views for managed `RawTexture` wrappers without adding source-parsing fallbacks
    - validation on the current OpenGL backend confirmed that explicit single-mip UAV keys materialize native views, while the rbfx-style default zero-count key still expands to the remaining mip chain and needs broader backend-parity follow-up
  - focused validation for the raw-texture UAV slice used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~RawTextureTests|FullyQualifiedName~GraphicsRenderTargetViewTests|FullyQualifiedName~RenderPoolTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - sample validation was rerun again on `2026-07-07` after the raw-texture UAV slice using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-rawtexture-uav-checkpoint.png`; the captured `1280x720` frame still shows the expected mushroom field, magenta outlines, ground plane, and black background
  - focused validation for the reflection-driven custom-uniform follow-up used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~ShaderProgramReflectionTests|FullyQualifiedName~GraphicsCustomUniformTests'` plus `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - sample validation for the same checkpoint used `apitrace trace -a gl -o /tmp/reflection-layout.trace out/build/dev/bin/Debug/RedBolt.Sample.StaticScene`, then `apitrace dump-images -o /tmp/reflection-layout /tmp/reflection-layout.trace`; the captured frame `/tmp/reflection-layout0000211058.png` shows live scene geometry plus outline compositing, but the upper portion of the frame remains black, so the renderer still has an unresolved output/composition deviation to debug
  - the reflection-array follow-up now uses a parsed native support helper instead of generated ad-hoc binding files, and the replacement path was revalidated on `2026-07-07` with `cmake --build --preset Debug --target redbolt-csharp`, `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ShaderProgramReflectionTests|FullyQualifiedName~GraphicsCustomUniformTests"`, plus `SDL_VIDEODRIVER=x11 apitrace trace -a gl -o /tmp/reflection-support.trace out/build/dev/bin/Debug/RedBolt.Sample.StaticScene` followed by `apitrace dump-images -o /tmp/reflection-support /tmp/reflection-support.trace`; the captured frame `/tmp/reflection-support0000211058.png` still shows the same black upper-region rendering deviation, so the support-helper cleanup did not regress or fix that separate compositor issue

## Current Cross-Cutting Requirement

- Managed `Context` infrastructure is now in tree with:
  - global access via `Context.SetInstance(ctx)` and `Context.Instance`
  - concurrent subsystem registration/lookup
  - named events based on `StringHash`, intended for main-thread use
- `Engine` owns the active `Context`, but the core subsystem instances are created and registered by `Context`.
- Use this instead of threading `Context` through constructors during the incremental port.

## Important Constraints

- No manual native bindings. If new native interop is needed, use/fix the generator path instead of adding handwritten binding glue.
- Prefer idiomatic C# APIs even when mirroring rbfx concepts.
- Trim this file regularly. Keep only current facts, decisions, blockers, and next actions.

## Next Useful Checks

- Investigate the still-bad sample frame with `apitrace`: the latest capture shows valid mushroom/outline rendering only in the lower portion of the viewport while the upper region stays black.
- Replace the local native `ShaderCodeVariableDesc` overlay with a generated SharpCpp array surface for Diligent reflection members, keeping the current no-fallback behavior but removing the hand-marshaled array walk.
- Thread the richer native shader reflection deeper into real pipeline-state/resource-binding setup instead of leaving it as lookup metadata plus custom-uniform packing.
- Deepen the new raw-texture/pool slice toward fuller rbfx parity:
  - move backend-facing raw-texture handle/UAV ownership out of ad-hoc `Texture`/`Graphics` helpers and behind `RawTexture`
  - reconcile the current OpenGL single-mip UAV behavior with rbfx's zero-count/default `RawTextureUAVKey` expansion instead of silently normalizing it away
  - add the remaining pool helpers (richer cache cleanup/logging parity and any missing transient helpers beyond scratch allocation)
  - route more render-buffer and output-target code through `RawTexture` instead of direct `Texture` references
- Continue from the new `RawBuffer`, `ConstantBufferCollection`, `ShaderProgramReflection`, and `DrawCommandQueue` slices into real backend playback:
  - thread shader reflections and resource binding through recorded commands
  - upload/bind constant buffers from `ConstantBufferCollection`
  - replace the current stats-only `RenderContext.Execute(...)` replay with Diligent draw/dispatch submission
- Move higher-level buffer identity closer to rbfx:
  - route `VertexBuffer` / `IndexBuffer` behavior through `RawBuffer` instead of keeping parallel managed buffer hierarchies
- Deepen the new RenderAPI slice by moving more transient draw-state/cache ownership behind `RenderContext`, `DrawCommandQueue`, `ConstantBufferCollection`, and `PipelineStateCache` instead of leaving it in ad-hoc `Graphics` helpers.
- Start hooking the new managed `PipelineStateCache` to real pipeline creation paths once the shader/resource-layer slice is present, so `Graphics` stops being the only place where pipeline identity exists.
- Continue from the new pipeline-owned scene-render seam into real offscreen output selection and pass-to-pass data flow.
- Replace the current direct scene draw in `Graphics.RenderScene` with pipeline-owned processors and batching once the post-process/output plumbing is in place.
- Keep rebuilding with `cmake --build --preset Debug --target redbolt-csharp` after each slice.

## Scene: Node 1:1 alignment (slice)
- Ported rbfx world-transform caching into `Node` (`_worldTransform`/`_worldRotation`/`_dirty` + `UpdateWorldTransform`); world getters now match `Node.cpp` (`if (dirty_) UpdateWorldTransform()`), removing per-read `Matrix3x4` allocation (GC win). `LocalToWorld` now uses `GetWorldTransform() * v` like C++.
- Reordered backing fields to mirror C++ member layout (NodeImpl group, vars, worldOriginUpdateMode_).
- Added missing C++ members: `WorldOriginUpdateMode` (new enum + property/get/set), void-dest collectors `GetChildren`/`GetChildrenWithComponent`/`GetChildrenWithTag` + recursive helpers, `GetChildByNameOrIndex`, `GetDependencyNodes`.
- Serialization/prefab surface (`SerializeInBlock`, `Load/Save*`, `InstantiatePrefab`, `GeneratePrefab`, `GetEffectiveScopeHint`, `SetScene`/`ResetScene`, `SafeCreateComponent`, `GetMutableVar*`) preserved as commented C++ — blocked on Archive/Variant/prefab/Serializer layers not yet ported.
- Updated `NodeTests.Silent_transform_helpers_do_not_mark_dirty` to reflect faithful rbfx semantics (silent set leaves cached world transform stale until `MarkDirty`).
- Build green, 443 tests pass, StaticScene sample renders.

## IO: AbstractFile / buffer streams (slice)
- Added managed `AbstractFile` so `MemoryBuffer` and `VectorBuffer` now match rbfx’s `AbstractFile`-based class hierarchy instead of inheriting `Deserializer` directly.
- `AbstractFile` now carries rbfx-style file metadata members (`SetName`, `Name`, `AbsoluteName`, `IsOpen`, `Close`), and `VectorBuffer` now returns the upstream default name `"VectorBuffer"` until explicitly renamed.
- `VectorBuffer(Deserializer, size)` and `SetData(Deserializer, size)` now clamp to the actual bytes read like rbfx instead of always preserving the requested size.
- `VectorBuffer` now stores its payload in a managed `byte[]` without long-lived pinning, which keeps the public buffer surface much closer to rbfx `ByteVector`: `GetBuffer()` now exposes the live underlying storage instead of manufacturing a copied helper array on every call.
- `MemoryBuffer(VectorBuffer)` now exposes the writable backing-buffer path over that shared managed storage, matching the upstream non-const `VectorBuffer&` constructor semantics without an intermediate copy layer.
- `MemoryBuffer` and `VectorBuffer` now also expose rbfx-style pointer-surface entry points for native-backed data copies: `MemoryBuffer(nint, size, readOnly)`, `MemoryBuffer(VectorBuffer)`, `VectorBuffer(nint, size)`, and `VectorBuffer.SetData(nint, size)`.
- Focused IO tests were added for the default-name path, writable `MemoryBuffer(VectorBuffer)`, and stream-backed `VectorBuffer` short-read behavior.
- Focused IO tests now also cover the new pointer-surface copy path and verify that `MemoryBuffer(VectorBuffer)` writes through to the same underlying managed storage.
- `MemoryBuffer` no longer carries the RedBolt-only `IEnumerable<byte>` and size constructors. Its managed constructor surface is now closer to rbfx: mutable `byte[]` maps to the non-const `ByteVector&` case, `ReadOnlyMemory<byte>` represents the const byte-vector case, and `string` stays explicitly read-only.
- Managed-array-backed `MemoryBuffer` instances now also track segment offsets correctly for `ReadOnlyMemory<byte>` sources instead of assuming every pinned array starts at offset zero.
- Focused IO coverage now also verifies that `MemoryBuffer(ReadOnlyMemory<byte>)` reports read-only state while `MemoryBuffer(byte[])` remains writable and writes through to the original managed storage.
- Full verification for this follow-up passed with `488/488` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.
- `VectorBuffer` now uses the upstream constructor set directly (`VectorBuffer()`, `VectorBuffer(byte[])`, `VectorBuffer(nint, size)`, `VectorBuffer(Deserializer, size)`) instead of carrying extra RedBolt-only enumerable and truncated-array constructor overloads.
- `VectorBuffer.SetData(Deserializer, size)` now matches rbfx more closely by preserving only the actual byte count returned from the source read without doing extra trailing-byte clearing work.
- To avoid long-lived pinning, `VectorBuffer.GetData()` and `GetModifiableData()` now remain null-pointer placeholders for managed storage; live code paths use `GetBuffer()` or `MemoryBuffer(VectorBuffer)` instead of relying on stable raw pointers.
- Focused IO coverage now also verifies that the byte-array constructor copies source contents like the upstream `ByteVector` copy constructor, that `GetBuffer()` exposes live storage like rbfx `ByteVector&`, and that the managed `VectorBuffer` path does not expose a stable raw pointer.
- Full verification for this follow-up passed with `489/489` tests green, a successful `cmake --build --preset Debug --target RedBolt`, and a valid `1280x720` StaticScene screenshot from `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/screenshot.png`.

## IO / Core: serializer + variant follow-up (slice)
- `VariantType` numeric ordering now matches rbfx for the stream-relevant entries that RedBolt currently supports, instead of using a local reordered enum that broke binary compatibility assumptions.
- Managed `Variant` now covers the plain rbfx streamable subset needed by current IO work: `Int64`, `Double`, `Rect`, `Buffer`, `ResourceRef`, `ResourceRefList`, `Matrix3`, `Matrix3x4`, `Matrix4`, `VariantVector`, `StringVector`, `VariantMap`, and `StringVariantMap`.
- `Serializer` and `Deserializer` now implement the corresponding matrix/resource/vector/map/variant helpers instead of leaving that whole surface absent.
- Packed vector/quaternion helpers are still explicit `// TODO: port` gaps because `VectorBinaryEncoding` is not in-tree yet.
- Focused IO coverage now also verifies matrix/resource helper round-trips, extended variant container round-trips, and rbfx `VariantType` numeric IDs used by stream serialization.

## Math: Transform follow-up (slice)
- `Transform` now matches the current rbfx `Transform.h` behavior surface much more closely: `Identity`, `FromMatrix3x4`, `ToMatrix3x4`, `Lerp`, `Inverse`, transform composition, point transform, and rotation transform are all in-tree.
- `Matrix3x4` now exposes `Decompose(out translation, out rotation, out scale)`, which closes the immediate dependency for `Transform.FromMatrix3x4` and aligns this math slice better with upstream decomposition-based callers.
- `DoubleTransform` is now in-tree and uses the generic `Vector3<double>` core directly for its position component instead of a dedicated compatibility wrapper type.
- The current double-precision math follow-up also switched the variant/stream helper surface from bespoke `DoubleVector2` / `DoubleVector3` structs to `Vector2<double>` / `Vector3<double>`, keeping the migration moving toward generic vector usage.
- A first attempt to push the same generic conversion through the float path showed that `Transform` / `Matrix3x4` are not an isolated cut: the next coherent float-side migration slice has to include at least `BoundingBox` and `Node`, which currently mix concrete `Vector3` with float-generic transform outputs and trigger ambiguous operator resolution when only the math leaf types are updated.
- The temporary implicit bridge between concrete `Vector2` / `Vector3` and generic `Vector2<float>` / `Vector3<float>` was removed again; it made the tree compile briefly, but it also introduced ambiguous operator resolution and switch-expression typing in downstream scene/math code, so the generic migration now proceeds without compatibility glue.
- Focused math coverage now verifies `Matrix3x4.Decompose(...)`, `Transform.FromMatrix3x4(...)`, `Transform.Lerp(...)`, `Transform.Inverse()`, and transform composition under the valid uniform-scale case.

## Math: Vector2 follow-up (slice)
- `Vector2<T>` now covers more of `Vector2.h`: the scalar-array constructor is in-tree, `ToHash()` is available, vector-parameter static `Lerp(...)` matches the upstream free helper shape, and `ReNormalized(...)` is back to the rbfx-style optional `defaultValue` / `eps` signature now that nullable native parameters are explicitly allowed.
- `IntVector2` now also carries the upstream scalar-array constructor, lexicographic comparison surface (`<`, `>`, `<=`, `>=`), and `ToHash()`.
- `NormalizedOrDefault(...)` on the generic vector side is still not shape-identical yet because current call sites use the property form; that follow-up remains under `Vector2.h`.
- Focused math coverage now also verifies the new array constructors, lexicographic compare behavior, `ToHash()`, and the restored `ReNormalized(...)` default-value path.
