# RedBolt Port Checklist — `Source/Urho3D`

## Managed architecture

- [~] `RedBolt.Core.dll` is compiled separately per target and contains only that target's application loop, platform behavior, and graphics backends; platform-qualified managed output is complete, while one-invocation Linux/Web native-binding generation remains pending
- [x] Android host — API 24 minimum and API 36 target, SDL lifecycle bridge, OpenGL ES 3 backend, generated Android native bindings, `arm64-v8a` and `x86_64` native builds, debug-signed APK, Web-compatible sample picker, packaged resources, and emulator smoke harness
- [x] Move managed engine types into folder-matching namespaces and namespace-first file headers, update all consumers and generated reflection metadata, and restore the namespace-colliding subsystem and render-pipeline class names.
- [x] Replace all hand-written object-factory and serializable-attribute registration with generated `[Reflected]`, member-targeted `[Serialized]`, and `[RemoveSerialized]` declarations, including inferred field/property/getter/setter access, initializer defaults, factory categories and opt-outs, ordered inheritance, removals, modes, scope hints, enum names, custom accessors, metadata configuration, nested user types, assembly registration, and generated-registrar activation for assemblies and generic reflected types loaded after WebAssembly startup.

## Tools

- [x] `PackageTool` — UPAK and block-compressed ULZ4 creation, package information and file listing modes, ignored extension and hidden-path filtering, base-path prefixes, checksums, and package freshness checks are ported and round-trip verified

## Samples

- [x] WebAssembly host — dedicated CMake/Emscripten preset, generated native bindings, common managed engine publish, pthread-capable WebGL 2 host, compressed `CoreData.pak` and `Data.pak` virtual filesystem staging without raw resource directories, browser build-graph exclusion of desktop-only transitive projects, and a framework-free C#-to-C++ rendering prototype build and render successfully in the browser
- [x] Replace `REDBOLT_WEB_SAMPLE` source guards with per-sample static desktop entry points and an explicitly selected static Web host entry point; desktop and Web builds share the same sample sources without platform-specific preprocessor branches
- [x] `01_HelloWorld` — extensible native class/method order, explicit construction, base startup, empty update semantics, reverse deterministic UI-replacement teardown, and Hello World text mapped to Dear ImGui without the non-native 3D scene
- [x] `02_HelloGUI` — extensible native class/method order, POD integer drag state and placement, reverse deterministic UI-replacement teardown, and control, title, close, and draggable-position demonstrations mapped to Dear ImGui widgets without the non-native 3D scene
- [x] `04_StaticScene` — extensible native class/method order, explicit construction and base startup, strong scene/camera ownership, renderer-retained viewport aliases, reverse deterministic teardown, POD transforms and `int` object count/index mapping, plane, directional light, randomized mushroom field, and Dear ImGui instructions reconciled to rbfx
- [x] `05_AnimatingScene` — extensible sample class shape, native method/order structure, generated `[Reflected]` `Rotator` factory metadata matching rbfx `SamplesManager`, strong scene/camera/viewport ownership, reverse deterministic teardown, scene, zone, static-model, node-logic animation, camera, point-light, viewport, Dear ImGui instructions, and shared `Rotator` POD value semantics reconciled and runtime-verified
- [x] `09_MultipleViewports` — extensible native class/method/field order, explicit construction, angle-axis rear-camera rotation, strong scene/camera ownership, renderer-retained viewport aliases, reverse deterministic teardown, scene, rear camera, dual viewports, automatic render pipeline allocation, render-target viewport handling, Dear ImGui instructions, requested window sizing, delayed multi-frame screenshots, and stable Vulkan shadows are runtime-verified against rbfx
- [x] `11_Physics` — intentional Jolt implementation with a static floor, dynamic box pyramid, 20-unit free-fly camera, left-click pyramid-matching box spawning, Jolt collision and raycast coverage, deterministic teardown, and reviewed screenshot capture
- [x] `34_DynamicGeometry` — extensible sample class shape, native method/field order, deep model clones, retained animating-buffer aliases, POD vertex/index copies, guarded buffer mapping, strong scene/camera/viewport ownership, reverse deterministic teardown, from-scratch pyramid geometry, animation, camera, zone lighting, automatic render pipeline, and Dear ImGui instructions reconciled to rbfx
- [x] `10_RenderToTexture` — extensible native class/method/field order, explicit construction and base startup, generated `[Reflected]` `Rotator` factory metadata matching rbfx `SamplesManager`, two-scene render-to-texture composition, queued RenderSurface viewport, strong scene/camera/texture/material ownership, reverse deterministic teardown, POD value copies and `int` indices/sizes, Dear ImGui instructions, native texture-descriptor defaults, copied texture-view descriptors, render-surface scheduling, resolved shader-resource sampling, and shared `Rotator` semantics are reconciled and runtime-verified
- [x] `14_SoundEffects` — hidden scene and music-source ownership, three sound-effect resources, looping music playback and stop, scene pause/resume and time scale, sound/music master gains, pan, reach, LFE, no-device microphone handling, seeded recorded-stream playback, Dear ImGui controls, baseline/audit screenshots, and deterministic teardown are runtime-verified
- [x] `15_Navigation` — static navigation-mesh building and queries, partial rebuilds, path following, obstacle editing, tile streaming, debug rendering, camera, and Dear ImGui instructions are ported and runtime-verified
- [x] `16_Chat` — DataChannel server/client startup, direct address connection, user network message handling, server broadcast, disconnect behavior, rolling chat history, and Dear ImGui controls are ported and runtime-verified
- [x] `17_SceneReplication` — authoritative scene replication, owner controls, network statistics, Jolt rigid-body floor and player physics, local server/client startup, and Dear ImGui controls are ported and runtime-verified
- [x] `43_HttpRequestDemo` — managed HTTP request startup and state handling, response/status/error logging, JSON origin parsing, and Dear ImGui status output are ported and runtime-verified
- [x] `53_LANDiscovery` — discovery beacon data, server broadcast, host discovery/reannouncement, ten-second expiry, server listing, and Dear ImGui controls are ported and runtime-verified
- [x] `54_WindowSettingsDemo` — extensible native class/method order, explicit construction and base startup, screen-mode and scene-update event lifetimes, world-space box rotation, strong scene/camera ownership, renderer-retained viewport aliases, reverse deterministic teardown, POD fullscreen-mode values, `int` indices/sizes, native fullscreen/DPI/refresh-rate/MSAA Apply semantics, and Dear ImGui controls reconciled and runtime-verified
- [-] `03_Sprites` — **EXCLUDED** · reason: depends on the optional, unported Urho2D subsystem
- [x] `06_SkeletalAnimation` — sample, generated `[Reflected]` `Mover3D` factory metadata, animated models, controller playback, scene, lighting, camera, skeleton and bounds debug geometry, and Dear ImGui instructions are ported and runtime-verified
- [x] `07_Billboards` — scene population, grouped shadow casters, animated billboard sets, rotating shadow-casting spotlights, camera, debug geometry, and Dear ImGui instructions are ported and runtime-verified
- [x] `Particles` — intentional RedBolt 3D particle showcase with counter-rotating fire, smoke, burst, and disco effects, neon point lighting, camera controls, Dear ImGui instructions, deterministic teardown, and runtime-verified screenshot capture
- [x] `08_Decals` — scene population, occluders, per-viewport normalized mouse updates at input end, cursor raycast, decal creation and buffer upload, camera, debug geometry, and Dear ImGui instructions are ported and runtime-verified
- [x] `20_HugeObjectCount` — 62,500-object scene, grouped and ungrouped modes, animation toggle, camera, and Dear ImGui instructions are ported; startup, live group rebuild, and steady-state rendering are runtime-verified after restoring shared reflection attributes and linear reverse teardown
- [x] `30_LightAnimation` — animated point light, scene population, camera, animated Dear ImGui text, and five-frame Dear ImGui texture animation are ported and runtime-verified
- [x] `31_MaterialAnimation` — material shader-parameter animation, scene population, camera, and Dear ImGui instructions are ported and runtime-verified
- [x] `29_SoundSynthesis` — sound-source node ownership, buffered mono stream playback, reusable zero-churn synthesis buffer, two-oscillator generation, low-pass filtering, Dear ImGui coefficient display, baseline rendering, both filter-control branches and clamp endpoints, and deterministic teardown are runtime-verified
- [x] `39_CrowdNavigation` — dynamic navigation mesh, obstacles, off-mesh connections, crowd agents and callbacks, moving barrels, formation targets, tile streaming, debug rendering, camera, and Dear ImGui instructions are ported and runtime-verified
- [x] `106_BakedLighting` — native baked scene and lightmap/probe assets, native-compatible packed light-probe serialization, C++ light type/importance/mode enum-name serialization and baked-only lighting parity, complete rbfx Terrain attribute registration and legacy-scene terrain reconstruction, NavigationMesh rebuild, CrowdAgent character movement and animation, CrowdManager avoidance tuning, texture toggling, Dear ImGui instructions, desktop/Web wiring, and deterministic teardown are ported and runtime-verified
- [x] `107_HelloRmlUI` — reflected data-model window, scalar and custom `Variant` bindings, custom events, backbuffer and render-to-texture canvases, cube animation, HarfBuzz-rasterized text, native shared texture/listener lifetimes, and deterministic teardown are ported and runtime-verified
- [x] `110_AdvancedNetworking` — Jolt KCC movement, client prediction and reconciliation, animated player replication, distance filtering, lag-compensated temporal raycasts, hit markers, local server/client startup, cheat controls, and Dear ImGui UI are ported and runtime-verified
- [x] `112_AggregatedInput` — direction aggregation, directional-pad aggregation, raw key/joystick/hat/disconnect/touch logging, synthesized key logging and repeats, and Dear ImGui visualization are ported and runtime-verified
- [x] `113_Actions` — engine `ActionBuilder`, the native easing gallery, sequencing, parallel actions, movement, rotation, scale, repeat modes, attribute animation, shader-parameter animation, typed events, callbacks, enable/disable, blink, removal, per-track controls, Dear ImGui status, neon 3D presentation, deterministic teardown, and screenshot capture are runtime-verified
- [x] `113_InputLogger` — dual viewports, all input-event subscriptions and decoded parameters, C++ identity/parameter/time-threshold event merging, viewport-relative mouse reporting, bounded logging, and Dear ImGui output are runtime-verified; the standalone managed application replaces the optional, unported plugin application wrapper
- [~] `114_AdvancedUI` — reflected RmlUI data model, direct `StringVector` list binding, callback-driven main/load/about views, saved-game mutation, focus restoration, sound event attributes, subtle animated button hover, interactive overlapping load/about views, F9 debugger toggle, HarfBuzz-rasterized text, skybox scene, standalone state host, and Escape navigation are ported and runtime-verified; built-in UI `Text3D` is excluded
  - [x] Hovered UI items use a subtle animated enlargement.
  - [x] The Load Game menu reacts to mouse input.
- [x] `116_VirtualFileSystem` — mount-point enumeration, editable identifier query, file metadata, reverse lookup, editable scan path/filter/flags, recursive file scanning, non-recursive directory scanning, and Dear ImGui output are ported and runtime-verified
- [x] `117_PointerAdapter` — adapter lifecycle and synthesized pointer events, scene raycast, outline selection, and Dear ImGui instructions are ported and runtime-verified; the optional, unported physics subsystem behavior is excluded
- [x] `118_CameraShake` — scene archive loading, trauma injection, camera shake update, and Dear ImGui controls are ported and runtime-verified
- [x] `119_WebRTCSTUN` — direct STUN/ICE client and muxed-port server paths, retry handling, relay-room WebSocket signaling via generated libdatachannel bindings, native WebSocket ownership handoff, ICE state/candidate logging, multi-client chat, and Dear ImGui controls are ported and runtime-verified
- [x] `121_CameraOperator` — scene archive loading, both camera tracks, track removal, orthographic projection, and Dear ImGui controls are ported and runtime-verified
- [x] `122_TextureFormats` — extensible native class/method order, explicit construction and base startup, software/hardware texture setup, comparison scene, camera, viewport, reverse deterministic teardown, Dear ImGui labels, overview/normal screenshots, Escape exit, and apitrace replay are runtime-verified; unsupported-format RGBA8 conversion warnings match rbfx `Texture::SetData`, while built-in UI `Text3D` is excluded
- [x] standalone `Text3D` — HarfBuzz contextual shaping, GPU-outline encoding and append-only atlas upload, rbfx pre-baked SDF metrics, multi-page atlases and shader path, 3D geometry, alignment, wrapping, colors, serialized baked-scene attributes, dedicated free-look 3D sample, and the five labels in `106_BakedLighting` are ported without a UI subsystem dependency
- [x] Profile startup and remove the rbfx deviations responsible for the long `20_HugeObjectCount` load time: shared type-level attribute registration, registry-based component construction, and linear reverse scene/octree teardown now match rbfx
- [x] Restore the rbfx `Audio` constructor registration path for `Sound`, `SoundSource`, `SoundSource3D`, and `SoundListener`, including type-level base-attribute copying; runtime-verify every `14_SoundEffects` control path
- [x] Capture and resolve OpenGL warnings produced by `08_Decals`, `20_HugeObjectCount`, and the remaining sample audit
- [x] Profile steady-state GC allocations for every sample and remove hot-path GC churn that is absent from rbfx
- [x] Remove the `EndFrame(image)` API deviation; subscribe to the EndFrame event and perform frame capture from the event handler as in rbfx
- [x] Audit and port every rbfx `RegisterObject` body into generated reflection declarations, including exact factory categories and non-factory semantics, source-member attribute order/defaults/modes/scope hints/enum names, removals, metadata configuration, and type-level base-attribute copying; remove the manual registration APIs and call sites
- [x] Make unit tests headless by default so they never create short-lived desktop windows; permit a real window only for behavior that cannot be verified headlessly
- [x] Remove the `ContextEventSubscription` API deviation and restore rbfx-aligned event subscription ownership and lifetime behavior

Dependency-ordered plan for porting rbfx `Source/Urho3D` to C#.

## How this list is ordered

- **Subsystems (subfolders) are ranked so that base / depended-on subsystems come first.** Part A is the
  essential core needed for a running, scene-rendering engine. Part B lists optional subsystems (each marked
  `OPTIONAL`) that layer on top of the core and can be deferred / skipped.
- **Within each subsystem, files are ordered base-first**: base classes, structs, and defs/enums come before the
  types that use them. Port a file only after everything above it in its subsystem (and every Part-A subsystem
  above it) exists.
- **Each file is its own item; the classes/structs it declares are a nested sub-list, one per line.**
  `A : B` means `A` derives from `B` — port `B` first (it is either earlier in this list or in an earlier subsystem).
- Cross-subsystem back-edges (a "lower" file needing a "higher" one) are flagged inline with `⤴ needs <X>`.

## Status legend

- `[ ]` — not started
- `[~]` — partially ported
- `[x]` — done
- `[-]` — **EXCLUDED** (intentionally not ported)

## Exclusion convention

An item may be marked `[-] EXCLUDED` **only** when it is C++-only infrastructure with no meaningful C# analogue —
i.e. it is fully replaced by the .NET BCL, a C# language feature, generated interop, or the managed `Context`
pattern. Every exclusion MUST follow this format so the decision is auditable:

- `[-] FileOrClass` — **EXCLUDED** · reason: `<short justification>` · replaced-by: `<C# mechanism>`

Rules:
- Exclusion is a deliberate, reviewable decision, not a shortcut. When in doubt, keep it `[ ]` and port it.
- Excluding a *file* excludes all its classes; excluding a single *class* is done at the class sub-bullet.
- If porting later reveals behavior that the C# replacement does not cover, promote the item back to `[ ]`
  and port it (per AGENTS.md: deviations from the engine must be fixed).

## Existing RedBolt port surface (reconcile before re-porting)

Present under `src/RedBolt/`: `Container`, `Core`, `Math`, `IO`, `Resource`, `Scene`, `RenderAPI`,
`RenderPipeline`, `Graphics`, `Input`, `Engine`, `SystemUI`. Generated bindings exist for `Container`, `Core`,
`Math`, `Resource`, `Scene`, `Script`, `UI`. Update statuses as you reconcile.

## HUGE TODO execution progress

- [x] Packet 0 baseline — generated binding commands share one prebuilt dependency-generator binary, so clean
  regeneration is deterministic under `-j16`; CMake configuration is explicitly started only for a missing binary
  directory and otherwise relies on CMake's dependency-driven regeneration; binding outputs depend on their parsed
  headers, SharpCpp sources, and RedBolt generator sources; the dependency generator has one shared pipeline and one
  generator per native target; stale SDL, SPIRV-Cross, C primitive, invalid alias, and xatlas callback rewrites are removed
  after their SharpCpp causes or redundant projections were resolved; SharpCpp implements only dependency-agnostic C++
  shape support while named surfaces, semantic mappings, and Dear ImGui enum normalization remain in the target generators;
  every generated target log permanently lists all SharpCpp ignored/excluded nodes and warnings; unchanged native builds skip generation, compilation, and
  linking; direct native CMake builds are rejected outside MSBuild; all 1982 RedBolt tests and 572 SharpCpp tests pass;
  screenshot-on-exit retains the valid rendered
  buffer through final-frame capture; and `MoveAndOrbitController` acquires the scene input subsystem before
  evaluating its input map. The required two-second MultipleViewports capture is visibly correct on both the Radeon
  driver and the host's llvmpipe fallback.
- [~] Packet 1 generated binding foundation — SharpCpp now generates glslang, SPIRV-Cross C, SPIRV-Tools, and Linux Vulkan
  surfaces into the build tree; dedicated managed binding projects consume them; the native batch links the matching
  rbfx-version shader libraries; generated borrowed-record array element access supports SPIRV-Cross reflection lists;
  generated native-interface queries now use Diligent `QueryInterface` identifiers, own and release the acquired reference,
  and replace all interface-pointer `CreateManagedWrapper` reinterpretations; the GL proxy helper preserves its concrete
  `ISwapChainGL` type exactly as the C++ construction path does;
  the generated SPIR-V optimizer support surface preserves rbfx's interface-aware performance-pass sequence and
  deterministic native lifetime; and `ShaderLibraryBindingTests` verifies process/context creation and deterministic
  release through all three managed surfaces. Linux Vulkan device, X11 swap-chain, multisample proxy, surface-extent resize,
  shader, immutable-sampler, and rotating-backbuffer paths run directly with `--vulkan`; OpenGL remains the C++ platform
  default. The standard gate passes 1058 tests and the OpenGL MultipleViewports capture is visibly correct. D3DCompiler
  and the remaining backend/platform-specific Diligent surfaces remain pending under their feature conditions.

---

# PART A — ESSENTIAL CORE (port in this order)

## [x] A1. Container — foundation, no engine deps

- [-] `Allocator.h` — **EXCLUDED** · reason: custom pool allocator · replaced-by: .NET GC
- [-] `Hash.h` — **EXCLUDED** · reason: hash-combine helpers · replaced-by: `System.HashCode` / `GetHashCode`
- [-] `Functors.h` — **EXCLUDED** · reason: C++ functor adapters · replaced-by: delegates / `Func<>`
- [-] `Str.h` — **EXCLUDED** · reason: `ea::string` wrapper · replaced-by: `System.String`
- [-] `ByteVector.h` — **EXCLUDED** · reason: `ea::vector<u8>` alias · replaced-by: `byte[]` / `List<byte>`
- [-] `TransformedSpan.h` — **EXCLUDED** · reason: projecting span view · replaced-by: LINQ `Select` / `Span<T>`
- [-] `RefCounted.h` — **EXCLUDED** · reason: intrusive strong ref · replaced-by: C# references + GC
- [-] `Ptr.h` — **EXCLUDED** · reason: intrusive strong ref · replaced-by: C# references + GC
- [-] `FlagSet.h` — **EXCLUDED** · reason: ported as `[Flags]` enums

## [x] A2. Math — depends on Container

- [-] `ApproximateEqualityComparer.cs` — **REMOVED** · reason: unused C#-only generic comparer with no rbfx counterpart

## [x] A3. Core — depends on Container, Math

- [-] `NonCopyable.h` — **EXCLUDED** · reason: does not map to C# concepts
- [-] `Macros.h` — **EXCLUDED** · reason: preprocessor macros · replaced-by: attributes + source generators
- [-] `AssertBase.h` — **EXCLUDED** · reason: assert plumbing · replaced-by: `System.Diagnostics.Debug`
- [-] `Assert.h` — **EXCLUDED** · reason: assert macros · replaced-by: `Debug.Assert` / `Trace.Assert`
- [-] `TypeTrait.h` — **EXCLUDED** · reason: C++ template traits · replaced-by: generics + reflection
- [-] `Format.h` — **EXCLUDED** · reason: fmt-style formatting · replaced-by: interpolation / `string.Format`
- [-] `IteratorRange.h` — **EXCLUDED** · reason: iterator pair range · replaced-by: `IEnumerable<T>`
- [-] `EnumIterator.h` — **EXCLUDED** · reason: enum iteration helper · replaced-by: `Enum.GetValues`
- [-] `TupleUtils.h` — **EXCLUDED** · reason: tuple metaprogramming · replaced-by: value tuples
- [-] `Mutex.h` — **EXCLUDED** · reason: OS mutex wrappers · replaced-by: `System.Threading` (`lock`, `Mutex`)
- [-] `Condition.h` — **EXCLUDED** · reason: condition variable · replaced-by: `ManualResetEventSlim` / `Monitor`
- [-] `Thread.h` — **EXCLUDED** · reason: OS thread wrapper · replaced-by: `System.Threading.Thread` / TPL
- [-] `PE.h` — **EXCLUDED** · reason: Win32 PE parsing · replaced-by: n/a (platform-specific)
- [-] `TypeInfo.h` - **EXCLUDED** · reason: use C# type system
- [x] Managed `[Reflected]` / `[Serialized]` source generation — **DEVIATION** · replaces
  URHO3D_OBJECT metadata and supports reflection declarations in user assemblies
- [-] `StringUtils.h` — **EXCLUDED** · reason: string parsing and formatting utilities · replaced-by: .NET string APIs
- [-] `Signal.h` - **EXCLUDED** · reason: use C# events

## [x] A4. IO — depends on Container, Core

- [x] `TemporaryDir.h` — `IDisposable` preserves automatic recursive cleanup
- [-] `FileSystem.h` — **EXCLUDED** · reason: use standard library functionality
- [-] `MacFileWatcher.h` — **EXCLUDED** · reason: macOS FSEvents backend · replaced-by: `FileSystemWatcher`
- [-] `RWOpsWrapper.h` — **EXCLUDED** · reason: SDL_RWops C++ adapter · replaced-by: generated SDL interop

## [x] A5. Resource — depends on IO, Core

## [x] A6. Scene — depends on Resource, Core

## [x] A7. RenderAPI — depends on Core, Math (current effort)

## [x] A8. Shader — depends on Core, IO (feeds Graphics)

## [x] A9. Graphics — depends on RenderAPI, Scene, Resource, Shader

## [x] A10. RenderPipeline — depends on Graphics, RenderAPI, Scene

- [x] `RenderPipeline` serialized attributes — restored C++ registration order, defaults, enum labels, callbacks, and dynamic metadata

## [x] A11. Input — depends on Core, Graphics (window)

- [x] SDL window bootstrap and events — zero-sized windowed requests use the native 1024x768 fallback, implicit placement targets the configured monitor, maximize happens after graphics initialization, and resize events update the drawable-sized swap chain

## [x] A12. Engine — top-level orchestration, depends on all Part-A above

---

# PART B — OPTIONAL SUBSYSTEMS (port after Part A)

Ordered so that optional subsystems depended on by other optional subsystems come first.

## B1. Actions `OPTIONAL` — tweening/action system; depends on Scene

- [x] `ActionState.h`
  - [x] `ActionState`
- [x] `Attribute.h`
  - [x] attribute-action helpers
- [x] `BaseAction.h`
  - [x] `BaseAction : Serializable`
- [x] `FiniteTimeAction.h`
  - [x] `FiniteTimeAction : BaseAction`
- [x] `FiniteTimeActionState.h`
  - [x] `FiniteTimeActionState : ActionState`
- [x] `ActionInstant.h`
  - [x] `ActionInstant : FiniteTimeAction`
- [x] `ActionInstantState.h`
  - [x] `ActionInstantState : FiniteTimeActionState`
- [x] `AttributeAction.h`
  - [x] `AttributeAction : FiniteTimeAction`
- [x] `AttributeActionState.h`
  - [x] `AttributeActionState : FiniteTimeActionState`
- [x] `Move.h`
  - [x] `MoveBy : AttributeAction`
  - [x] `MoveByQuadratic : MoveBy`
  - [x] `JumpBy : AttributeAction`
  - [x] `ScaleBy : AttributeAction`
  - [x] `RotateBy : AttributeAction`
  - [x] `RotateAround : AttributeAction`
- [x] `Misc.h`
  - [x] `RemoveSelf : FiniteTimeAction` — Node behavior ported; built-in `UIElement` target is excluded
  - [x] `DelayTime : FiniteTimeAction`
  - [x] `Show : AttributeAction`
  - [x] `Hide : AttributeAction`
  - [x] `Enable : AttributeAction`
  - [x] `Disable : AttributeAction`
  - [x] `Blink : AttributeAction`
- [x] `Ease.h`
  - [x] `ActionEase : FiniteTimeAction`
  - [x] `EaseElastic : ActionEase`
  - [x] `EaseBackIn/Out : ActionEase`
  - [x] `EaseBackInOut : ActionEase`
  - [x] `EaseBounceOut/In : ActionEase`
  - [x] `EaseBounceInOut : ActionEase`
  - [x] `EaseSineOut/In : ActionEase`
  - [x] `EaseSineInOut : ActionEase`
  - [x] `EaseExponentialOut/In : ActionEase`
  - [x] `EaseExponentialInOut : ActionEase`
  - [x] `EaseElasticIn/Out : EaseElastic`
  - [x] `EaseElasticInOut : EaseElastic`
- [x] `CallFunc.h`
  - [x] `ActionCallHandler : RefCounted` — `RefCounted` dropped per allowed deviations
  - [x] `CallFunc : ActionInstant`
  - [x] `SendEvent<T> : ActionInstant`
- [x] `ShaderParameter.h` ⤴ needs `Graphics/Material`
  - [x] `ShaderParameterAction : FiniteTimeAction`
  - [x] `ShaderParameterTo : ShaderParameterAction`
  - [x] `ShaderParameterFromTo : ShaderParameterTo`
- [x] `Parallel.h`
  - [x] `Parallel : FiniteTimeAction`
- [x] `Sequence.h`
  - [x] `Sequence : FiniteTimeAction`
- [x] `Repeat.h`
  - [x] `Repeat : FiniteTimeAction`
  - [x] `RepeatForever : FiniteTimeAction`
- [ ] `ActionSet.h`
  - [ ] `ActionSet : Resource`
- [x] `ActionBuilder.h`
  - [x] `ActionBuilder`
- [x] `ActionManager.h`
  - [x] `ActionManager`

## [x] B2. Audio `OPTIONAL` — depends on Scene, Resource

## B3. UI `EXCLUDED` — retained-mode UI; depends on Graphics, Resource, Input

## B4. Physics `OPTIONAL` — Bullet; depends on Scene, Graphics

- [x] Intentional RedBolt Jolt core slice
  - [x] Lean generated Jolt binding target with common intrusive-reference, static-field, generic, and projected-value API support in SharpCpp
  - [x] `PhysicsWorld`-owned static Jolt registration and deterministic lifetime
  - [x] Direct RedBolt vector, quaternion, and ray projections with dedicated projected-ABI layout and native round-trip coverage
  - [x] Physics definitions and collision events
  - [x] `PhysicsWorld` fixed-step simulation, interpolation, contacts, collision filtering, and closest raycasts
  - [x] `CollisionShape` box, sphere, cylinder, and capsule support
  - [x] `RigidBody` static, dynamic, kinematic, trigger, transform, velocity, force, impulse, damping, gravity, CCD, and collision-layer support
  - [x] Desktop and Web Physics sample with a free-fly camera, matched pyramid and thrown boxes, click spawning, and a controls window

- [ ] `PhysicsDefs.h`
  - [ ] physics enums / constants
- [ ] `PhysicsEvents.h`
  - [ ] physics event definitions
- [ ] `PhysicsUtils.h`
  - [ ] Bullet <-> engine conversion helpers
- [ ] `CollisionGeometryDataCache.h`
  - [ ] `CollisionGeometryDataCache : Object`
- [ ] `PhysicsWorld.h`
  - [ ] `struct PhysicsRaycastResult`
  - [ ] `PhysicsWorld : Component, btIDebugDraw`
- [ ] `CollisionShape.h`
  - [ ] `CollisionShape : Component`
- [ ] `RigidBody.h`
  - [ ] `RigidBody : Component, btMotionState`
- [x] `Constraint.h` — ported with Jolt point, hinge, six-DOF slider, and swing-twist constraints
  - [x] `Constraint : Component`
  - Jolt contact validation provides reference-counted collision suppression for connected body pairs.
  - `ERP` and `CFM` remain reflected for rbfx serialization compatibility; Jolt has no per-constraint equivalents.
- [x] `KinematicCharacterController.h` — ported to Jolt `Character`
  - [x] `KinematicCharacterController : Component`
- [x] `RaycastVehicleWheel.h` — ported to the Jolt vehicle constraint adapter
  - [x] `RaycastVehicleWheel : Component`
- [x] `RaycastVehicle.h` — ported to Jolt `VehicleConstraint` and `WheeledVehicleController`
  - [x] `RaycastVehicle : LogicComponent`
  - Static wheel changes recreate the native vehicle because Jolt wheel settings are immutable after construction.
  - `Roll Influence` is mapped through Jolt's suspension force point. `Max Suspension Force` remains reflected for rbfx
    serialization compatibility; Jolt has no per-wheel suspension force cap.
- [x] `TriggerAnimator.h`
  - [x] `TriggerAnimator : Component`

## B5. Physics2D `OPTIONAL` — Box2D; depends on Scene, Graphics

- [ ] `Physics2D.h`
  - [ ] registration entry point
- [ ] `PhysicsEvents2D.h`
  - [ ] 2D physics event definitions
- [ ] `PhysicsUtils2D.h`
  - [ ] Box2D <-> engine conversion helpers
- [ ] `PhysicsWorld2D.h`
  - [ ] `struct PhysicsRaycastResult2D`
  - [ ] `PhysicsWorld2D : Component, b2ContactListener, b2Draw`
- [ ] `RigidBody2D.h`
  - [ ] `RigidBody2D : Component`
- [ ] `CollisionShape2D.h`
  - [ ] `CollisionShape2D : Component`
- [ ] `CollisionBox2D.h`
  - [ ] `CollisionBox2D : CollisionShape2D`
- [ ] `CollisionCircle2D.h`
  - [ ] `CollisionCircle2D : CollisionShape2D`
- [ ] `CollisionPolygon2D.h`
  - [ ] `CollisionPolygon2D : CollisionShape2D`
- [ ] `CollisionChain2D.h`
  - [ ] `CollisionChain2D : CollisionShape2D`
- [ ] `CollisionEdge2D.h`
  - [ ] `CollisionEdge2D : CollisionShape2D`
- [ ] `Constraint2D.h`
  - [ ] `Constraint2D : Component`
- [ ] `ConstraintDistance2D.h`
  - [ ] `ConstraintDistance2D : Constraint2D`
- [ ] `ConstraintFriction2D.h`
  - [ ] `ConstraintFriction2D : Constraint2D`
- [ ] `ConstraintGear2D.h`
  - [ ] `ConstraintGear2D : Constraint2D`
- [ ] `ConstraintMotor2D.h`
  - [ ] `ConstraintMotor2D : Constraint2D`
- [ ] `ConstraintMouse2D.h`
  - [ ] `ConstraintMouse2D : Constraint2D`
- [ ] `ConstraintPrismatic2D.h`
  - [ ] `ConstraintPrismatic2D : Constraint2D`
- [ ] `ConstraintPulley2D.h`
  - [ ] `ConstraintPulley2D : Constraint2D`
- [ ] `ConstraintRevolute2D.h`
  - [ ] `ConstraintRevolute2D : Constraint2D`
- [ ] `ConstraintRope2D.h`
  - [ ] `ConstraintRope2D : Constraint2D`
- [ ] `ConstraintWeld2D.h`
  - [ ] `ConstraintWeld2D : Constraint2D`
- [ ] `ConstraintWheel2D.h`
  - [ ] `ConstraintWheel2D : Constraint2D`

## B6. Urho2D `OPTIONAL` — 2D sprites/tiles; depends on Graphics, Physics2D

- [ ] `Urho2D.h`
  - [ ] registration entry point
- [ ] `Urho2DEvents.h`
  - [ ] Urho2D event definitions
- [ ] `Sprite2D.h`
  - [ ] `Sprite2D : Resource`
- [ ] `SpriteSheet2D.h`
  - [ ] `SpriteSheet2D : Resource`
- [ ] `Drawable2D.h`
  - [ ] `Drawable2D : Drawable`
- [ ] `StaticSprite2D.h`
  - [ ] `StaticSprite2D : Drawable2D`
- [ ] `StretchableSprite2D.h`
  - [ ] `StretchableSprite2D : StaticSprite2D`
- [ ] `SpriterData2D.h`
  - [ ] Spriter data structs
- [ ] `SpriterInstance2D.h`
  - [ ] `SpriterInstance2D`
- [ ] `AnimationSet2D.h`
  - [ ] `AnimationSet2D : Resource`
- [ ] `AnimatedSprite2D.h`
  - [ ] `AnimatedSprite2D : StaticSprite2D`
- [ ] `ParticleEffect2D.h`
  - [ ] `ParticleEffect2D : Resource`
- [ ] `ParticleEmitter2D.h`
  - [ ] `ParticleEmitter2D : Drawable2D`
- [ ] `TileMapDefs2D.h`
  - [ ] `struct TileMapInfo2D`
  - [ ] `PropertySet2D : RefCounted`
  - [ ] `Tile2D : RefCounted`
  - [ ] `TileMapObject2D : RefCounted`
- [ ] `TmxFile2D.h`
  - [ ] `TmxFile2D : Resource`
- [ ] `TileMapLayer2D.h`
  - [ ] `TileMapLayer2D : Component`
- [ ] `TileMap2D.h`
  - [ ] `TileMap2D : Component`
- [ ] `Renderer2D.h`
  - [ ] `Renderer2D : Drawable`

## B7. Navigation `OPTIONAL` — Recast/Detour; depends on Scene, Graphics

- [x] `NavigationDefs.h`
  - [x] navigation enums / constants
- [x] `NavigationEvents.h`
  - [x] navigation event definitions
- [x] `NavigationUtils.h`
  - [x] navigation helpers / registration
- [x] `NavBuildData.h`
  - [x] `struct NavAreaStub`
  - [x] `struct NavBuildData : NonCopyable`
  - [x] `struct SimpleNavBuildData : NavBuildData`
  - [x] `struct DynamicNavBuildData : NavBuildData`
- [x] `NavigationMesh.h`
  - [x] `struct NavigationPathPoint`
  - [x] `NavigationMesh : Component`
- [x] `DynamicNavigationMesh.h`
  - [x] `DynamicNavigationMesh : NavigationMesh`
- [x] `NavArea.h`
  - [x] `NavArea : Component`
- [x] `Navigable.h`
  - [x] `Navigable : Component`
- [x] `Obstacle.h`
  - [x] `Obstacle : Component`
- [x] `OffMeshConnection.h`
  - [x] `OffMeshConnection : Component`
- [x] `CrowdAgent.h`
  - [x] `CrowdAgent : Component`
- [x] `CrowdManager.h`
  - [x] `CrowdManager : Component`

## B8. Network `OPTIONAL` — depends on IO, Core

- [x] `Protocol.h`
  - [x] protocol message-id constants
- [x] `PacketTypeFlags.h`
  - [x] `PacketTypeFlags`
- [x] `NetworkEvents.h`
  - [x] network event definitions
- [x] `URL.h`
  - [x] `URL`
- [x] `MessageUtils.h`
  - [x] `LargeMessageWriter`
  - [x] `LargeMessageReader`
  - [x] `MultiMessageWriter`
- [x] `NetworkStatisticsCounter.h`
  - [x] `NetworkStatisticsCounter`
- [x] `ClockSynchronizer.h`
  - [x] `FilteredUint`
  - [x] `ClockSynchronizer`
- [x] `AbstractConnection.h` — removed on `rk/network`; superseded by transport connections
- [x] `Transport/NetworkConnection.h`
  - [x] `NetworkConnection : Object`
- [x] `Transport/NetworkServer.h`
  - [x] `NetworkServer : Object`
- [x] `Transport/DataChannel/DataChannelConnection.h`
  - [x] `DataChannelConnection : NetworkConnection`
- [x] `Transport/DataChannel/DataChannelServer.h`
  - [x] `DataChannelServer : NetworkServer`
- [x] `Connection.h` — removed on `rk/network`; superseded by `NetworkConnection`
- [x] `HttpRequest.h`
  - [x] `HttpRequest : Deserializer` with managed worker task
- [x] `LANDiscoveryManager.h`
  - [x] `LANDiscoveryManager : Object`
- [x] `NetworkMessageLogger.h`
  - [x] `NetworkMessageLogger`
- [x] `NetworkLoadableScene.h`
  - [x] `NetworkLoadableScene : Object`
- [x] `ReplicatedPeer.h`
  - [x] `ReplicatedPeer`
- [x] `Network.h`
  - [x] `Network : Object`

## B9. Replica `OPTIONAL` — scene replication; depends on Network, Scene, Graphics

- [x] `NetworkId.h`
  - [x] `NetworkId`
- [x] `NetworkSettingsConsts.h`
  - [x] network settings constants
- [x] `ProtocolMessages.h`
  - [x] protocol message structs
- [x] `NetworkCallbacks.h`
  - [x] network callback interfaces
- [x] `NetworkTime.h`
  - [x] `NetworkTime`
- [x] `NetworkValue.h`
  - [x] `NetworkValue<T>` / interpolation helpers
- [x] `TickSynchronizer.h`
  - [x] `TickSynchronizer`
  - [x] `SceneUpdateSynchronizer : Object` (non-Physics branch)
  - [x] `PhysicsTickSynchronizer : Object` — ported as Jolt-backed `TickSynchronizer`
- [x] `ClientInputStatistics.h`
  - [x] `ClientInputStatistics`
- [x] `NetworkObject.h`
  - [x] `NetworkObject`
- [x] `StaticNetworkObject.h`
  - [x] `StaticNetworkObject : NetworkObject`
- [x] `BehaviorNetworkObject.h`
  - [x] `NetworkBehavior`
  - [x] `BehaviorNetworkObject : StaticNetworkObject`
- [x] `BaseFeedbackBehavior.h`
  - [x] `BaseFeedbackBehavior<T>`
- [x] `ReplicatedTransform.h`
  - [x] `ReplicatedTransform : NetworkBehavior`
- [x] `ReplicatedParent.h`
  - [x] `ReplicatedParent : NetworkBehavior`
- [x] `ReplicatedAnimation.h`
  - [x] `ReplicatedAnimation : NetworkBehavior`
- [x] `FilteredByOwner.h`
  - [x] `FilteredByOwner : NetworkBehavior`
- [x] `FilteredByDistance.h`
  - [x] `FilteredByDistance : NetworkBehavior`
- [x] `TrackedAnimatedModel.h`
  - [x] `TrackedAnimatedModel : NetworkBehavior`
- [x] `PredictedKinematicController.h` ⤴ uses Jolt `KinematicCharacterController`
  - [x] `PredictedKinematicController : BaseFeedbackBehavior<PredictedKinematicControllerFrame>`
- [x] `ReplicationManager.h`
  - [x] `NetworkObjectRegistry : ReferencedComponentRegistryBase`
  - [x] `ReplicationManager : NetworkObjectRegistry`
- [x] `ClientReplica.h`
  - [x] `ClientReplicaClock : Object`
  - [x] `ClientReplica : ClientReplicaClock`
- [x] `ServerReplicator.h`
  - [x] `ServerReplicator : Object`

## B10. IK `OPTIONAL` — inverse kinematics; depends on Scene, Math/InverseKinematics

- [x] `IK.h`
  - [x] registration entry point
- [x] `IKEvents.h`
  - [x] IK event definitions
- [x] `AllSolvers.h`
  - [x] solver aggregation header (represented by the managed `RedBolt.IK` namespace)
- [x] `IKSolverComponent.h`
  - [x] `IKSolverComponent : Component`
- [x] `IKIdentitySolver.h`
  - [x] `IKIdentitySolver : IKSolverComponent`
- [x] `IKLimbSolver.h`
  - [x] `IKLimbSolver : IKSolverComponent`
- [x] `IKLegSolver.h`
  - [x] `IKLegSolver : IKSolverComponent`
- [x] `IKArmSolver.h`
  - [x] `IKArmSolver : IKSolverComponent`
- [x] `IKChainSolver.h`
  - [x] `IKChainSolver : IKSolverComponent`
- [x] `IKHeadSolver.h`
  - [x] `IKHeadSolver : IKSolverComponent`
- [x] `IKSpineSolver.h`
  - [x] `IKSpineSolver : IKSolverComponent`
- [x] `IKRotateTo.h`
  - [x] `IKRotateTo : IKSolverComponent`
- [x] `IKStickTargets.h`
  - [x] `IKStickTargets : IKSolverComponent`
- [x] `IKSolver.h`
  - [x] `IKSolver : LogicComponent`

Accepted deviations:
- Actions reflection hooks are excluded from the current completion scope.
- Object and serializable-attribute registration uses generated `[Reflected]` and `[Serialized]` metadata.

## B11. Particles `OPTIONAL` — particle graph system; depends on Scene, Graphics

- [x] `Emitter.h`
  - [x] `EmitFrom`
  - [x] `GetEmitFromNames`
- [ ] `Helpers.h`
  - [ ] particle helper free functions
- [x] `Span.h`
  - [x] `Span` view type
- [x] `SpanVariants.h`
  - [x] dormant span variant implementation comment
- [ ] `UpdateContext.h`
  - [ ] `UpdateContext`
- [ ] `ParticleGraphMemory.h`
  - [ ] `ParticleGraphSpan` (managed-reference value support pending)
  - [x] `ParticleGraphAttributeLayout`
  - [x] `ParticleGraphBufferLayout`
- [x] `ParticleGraphPin.h`
  - [x] `ParticleGraphPinFlag`
  - [x] `ParticleGraphPinRef`
  - [x] `ParticleGraphPin`
  - [x] `ParticleGraphTypedPin<T>`
- [ ] `Attribute.h`
  - [ ] particle attribute descriptors
- [ ] `ParticleGraphNode.h`
  - [ ] `ParticleGraphNode : Serializable`
- [ ] `ParticleGraphNodeInstance.h`
  - [ ] `ParticleGraphNodeInstance : NonCopyable`
- [ ] `ParticleGraphSystem.h`
  - [ ] `ParticleGraphSystem : Object, ObjectReflectionRegistry`
- [ ] `ParticleGraph.h`
  - [ ] `ParticleGraph : Object`
- [ ] `ParticleGraphLayer.h`
  - [ ] `ParticleGraphLayer : Serializable`
- [ ] `ParticleGraphLayerInstance.h`
  - [ ] `ParicleGraphUniform`
  - [ ] `ParticleGraphLayerInstance`
- [ ] `TemplateNode.h`
  - [ ] `TemplateNode<TInstance, ...>`
- [ ] `PatternMatchingNode.h`
  - [ ] `PatternMatchingNode : ParticleGraphNode`
- [ ] `ParticleGraphEffect.h`
  - [ ] `ParticleGraphEffect : Resource`
- [ ] `ParticleGraphEmitter.h`
  - [ ] `ParticleGraphEmitter : Component`
- [ ] `Nodes/` (graph node library — each `: TemplateNode<...>`, `: PatternMatchingNode`, or `: ParticleGraphNode`)
  - [ ] arithmetic: `Add`, `Subtract`, `Multiply`, `Divide`, `Negate`, `Clamp`, `Lerp`, `Slerp`, `Normalized`, `Make`, `Break`, `Cast`, `Length`
  - [ ] sources: `Constant`, `Curve`, `Random`, `Uniform` (`GetUniform`/`SetUniform`), `Attribute` (`GetAttribute`/`SetAttribute`), `Print`
  - [ ] lifecycle: `Emit`, `Expire`, `Destroy`
  - [ ] motion: `Move`, `ApplyForce`, `LimitVelocity`
  - [ ] time: `TimeStep`, `TimeStepScale`, `EffectTime`, `NormalizedEffectTime`
  - [ ] shapes: `Box`, `Circle`, `Cone`, `Sphere`, `Hemisphere`
  - [ ] noise: `Noise3D`, `CurlNoise3D`
  - [ ] emitter: `BurstTimer`, `EmitterPosition`, `EmitterRotation`, `EmitterScale`, `LocalToWorld`, `WorldToLocal`
  - [ ] render: `RenderBillboard`, `RenderMesh`

## B12. Glow `OPTIONAL` — lightmap baking (Embree); depends on Graphics, Scene

- [x] `Helpers.h`
  - [x] glow helper free functions
- [-] `EmbreeForward.h` — **EXCLUDED** · reason: Embree forward decls · replaced-by: generated Embree interop
- [x] `LightmapUVGenerator.h`
  - [x] `struct LightmapUVGenerationSettings`
- [x] `RaytracerScene.h`
  - [x] `RaytracerScene : RefCounted`
- [x] `BakedLight.h`
  - [x] baked light structs
- [x] `BakedLightCache.h`
  - [x] `struct BakedLightmap`
  - [x] `BakedLightCache`
  - [x] `BakedLightMemoryCache : BakedLightCache`
- [x] `BakedSceneBackground.h`
  - [x] baked scene background structs
- [x] `BakedSceneChunk.h`
  - [x] baked scene chunk structs and chunk creation
- [x] `BakedSceneCollector.h`
  - [x] `BakedSceneCollector`
  - [x] `DefaultBakedSceneCollector : BakedSceneCollector`
- [x] `StaticModelForLightmap.h`
  - [x] `StaticModelForLightmap : Drawable`
- [x] `LightmapCharter.h`
  - [x] chart layout helpers
- [x] `LightmapGeometryBuffer.h`
  - [x] geometry buffer structs, scene generation, and geometry-buffer baking
- [x] `LightmapStitcher.h`
  - [x] seam stitching helpers
- [x] `LightmapFilter.h`
  - [x] lightmap filtering helpers
- [x] `LightTracer.h`
  - [x] light tracing helpers
- [x] `IncrementalLightBaker.h`
  - [x] `IncrementalLightBaker`
- [x] `Graphics/LightBaker.h`
  - [x] `LightBaker : Component` with callable asynchronous bake action

## B13. RmlUI `OPTIONAL` — RmlUi HTML/CSS UI; depends on UI, Graphics

- [x] native prerequisites — RmlUi 6.2 with standard-library containers and HarfBuzz 14.2.1 raster; RmlUi font engine disabled, no FreeType
  - [x] generated HarfBuzz font/raster bindings — verified non-empty A8 glyph output
  - [x] generated RmlUi `SystemInterface` director — standard-library `Std.String`, no EASTL projection
  - [x] generated RmlUi `FontEngineInterface` lifecycle director — context initialization verified without the default FreeType engine
  - [x] native HarfBuzz font engine — shaping, A8 rasterization, dynamic atlas, and textured RmlUi geometry verified
  - [ ] native HarfBuzz font engine effects and fallback-face shaping
- [x] `RmlSystem.h`
  - [x] `RmlSystem : Rml::SystemInterface` — `RefCounted` and `WeakPtr<Context>` dropped per managed deviation
- [x] `RmlFile.h`
  - [x] `RmlFile : Rml::FileInterface` — `WeakPtr<Context>` dropped; file ownership mapped to `GCHandle`; loaded resources tracked with stdlib `HashSet`
- [x] `RmlRenderer.h`
  - [x] `RmlRenderer : Object, Rml::RenderInterface` — RmlUi 6.2 official compatibility adapter; `Object` event role composed because C# has single inheritance; queued generated textures preserve native `SharedPtr` lifetime through execution
  - [x] `Material::CreateBaseMaterial` prerequisite
  - [x] `Texture` device-loss forwarding prerequisite
  - [x] UI batch pipeline-state cache prerequisite from `RenderPipeline/BatchStateCache.h`
  - [x] generated RmlUi 6.2 `RenderInterfaceCompatibility` director and layout-checked legacy value ABI
- [x] `RmlEventListeners.h`
  - [x] generated RmlUi `EventListener` director prerequisite
  - [x] generated RmlUi `EventListenerInstancer` director and factory registration prerequisite
  - [x] generated typed `Event`, `Element`, `Context`, and `EventPhase` listener callback prerequisites
  - [x] `PipeEventListener : Rml::EventListener, NonCopyable`
  - [x] `SoundEventListener : Rml::EventListener, NonCopyable`
  - [x] `CustomEventListener : Rml::EventListener, NonCopyable` — custom `StringHash` dispatch mapped to typed `RmlCustomEvent`
- [x] `RmlUI.h`
  - [x] generated RmlUi lifecycle and base `Context` name, dimensions, DPI, update, and render prerequisites
  - [x] generated RmlUi `ContextInstancer` ownership and release lifecycle prerequisite
  - [x] generated RmlUi `Plugin` director and registration lifecycle prerequisite
  - [x] generated RmlUi font loading and document load, show, hide, close, and unload prerequisites
  - [x] generated RmlUi context keyboard, text, mouse, focus, hover, root, and element inspection prerequisites
  - [x] generated RmlUi debugger visibility and texture, style-sheet, and template cache release prerequisites
  - [x] generated RmlUi document enumeration, source, visibility, modal, position, and property reload prerequisites
  - [x] generated RmlUi element boolean and component-pointer attribute prerequisites
  - [x] generated stdlib `Rml::Variant`, `std::vector<Variant>`, functional data-model, and RedBolt math-type projection prerequisites
  - [x] generated RmlUi `VariableDefinition`, `DataVariable`, and `DataAddressEntry` director prerequisites with record-value callback ownership
  - [x] `Variant` scalar, string, pointer, vector, and color conversions
  - [x] `RmlUI : Object` — lifecycle, input, rendering, reload, context instancing, plugin, debugger, HarfBuzz font loading, variant conversions, drop-file dictionary dispatch, and managed `Variant`, `VariantVector`, `VariantMap`, and direct `StringVector` data-variable definitions ported
- [x] `RmlUIComponent.h`
  - [x] generated RmlUi document layout, focus, scrolling, property, computed-style, and RedBolt `Vector2f` projection prerequisites
  - [x] generated RmlUi data-model transform callbacks with value-return ownership transfer
  - [x] `RmlUIComponent : LogicComponent` — document, canvas, focus, position, size, modal, scalar property, event, data-model lifecycle, and custom `Variant`, `VariantVector`, `VariantMap`, and direct `StringVector` binding ported
- [x] `RmlCanvasComponent.h`
  - [x] generated RmlUi context document-unload prerequisite
  - [x] `RmlCanvasComponent : LogicComponent`
- [x] `RmlSerializableInspector.h`
  - [x] `RmlSerializableInspector : RmlUIComponent` — reflected bool, number, and enum attributes use generated custom data-variable directors

## B14. SystemUI `OPTIONAL` — Dear ImGui editor/debug; depends on Graphics, Input

- [x] `SystemUIEvents.h`
  - [x] system-UI event definitions
- [~] `ImGui.h`
  - [~] ImGui integration helpers
  - [x] intentional RedBolt HarfBuzz GPU loader shapes text runs, preserves wrapping and measurement semantics, selects merged-font sources by configured ranges, and lazily encodes visible outlines without exhausting the GPU atlas
  - [x] generated `imgui_internal.h` widget primitives with allocation-free `ImRect` value projection
  - [x] mutable scalar and fixed-array widget bindings preserve input values without native-array allocations
  - [x] `IdScopeGuard` — stack-only native RAII maps to an allocation-free C# value guard
  - [x] `ColorScopeGuard` — stack-only native RAII maps to an allocation-free C# value guard
- [~] `Widgets.h`
  - [~] shared widget free functions
  - [x] `GetSmallButtonSize`
  - [x] `ToolbarButton`
  - [x] `ToolbarSeparator`
  - [x] `ItemLabel`
  - [x] `ItemLabelScopeGuard` — native guard inheritance maps to allocation-free value composition
  - [x] `GetItemLabelColor`
  - [x] `GetItemBackgroundColor`
  - [x] `Underline`
  - [x] `TextURL` — UTF-8 spans preserve native string lifetime without per-frame conversions
  - [x] `FitContent`
  - [x] `Image`
  - [x] `ImageItem` — the caller retains one generated `ImTextureRef` wrapper because the current binding projects this native value type as an owned class
  - [x] `EditVariantOptions` — C# field names use `Value` and predicate forms where native fluent methods otherwise collide
  - [x] `EditResourceRef`
  - [x] `EditResourceRefList`
  - [x] `EditBitmask`
  - [x] `EditVariantType`
  - [x] `EditVariantVector`
  - [x] `EditStringVector`
  - [x] `EditStringVariantMap`
  - [x] `EditVariant` — every rbfx-supported scalar, string, resource, and container branch is ported; native TODO branches remain TODO
  - [x] empty ImGui labels preserve native valid-empty `ImStrv` semantics
- [~] `ImGuiDiligentRendererEx.h`
  - [~] `ImGuiDiligentRendererEx` — native constructor/destructor, base renderer ownership, main-viewport frame/render path, render pipelines, class shape, sizes, and failure unwinding reconciled; secondary OS-window callbacks and implicit-copy cloning remain exact `TODO: port` items because SharpCpp excludes `ImGuiPlatformIO` function-pointer fields and does not expose native `ImGuiDiligentRenderer` base copy construction
- [~] `SystemUI.h`
  - [~] `SystemUI : Object` — class shape, lifecycle, event flow, relative mouse mode, all three font-loading overloads, style, rendering, and texture-reference lifetime reconciled as `DearImGui`; nullable native glyph ranges, managed font buffers, and copied glyph-range arrays remain pinned for their native atlas lifetime, while contextual shaping and merged icon fonts are behavior- and visual-regression-tested; per-screen font atlases/TexID assignment, monitor refresh, ImGuizmo frame start, and managed `io.UserData` remain explicit `TODO: port` items until generated bindings expose the required native APIs
- [x] `Console.h`
  - [x] `Console : Object` — log ring history, filters, byte-accurate selection and copy, URI events, interpreter discovery and command dispatch, visibility/update flow, and deterministic UTF-8 storage are ported and software-renderer tested; Dear ImGui 1.92 cursor-boundary validation uses the prescribed zero-size item
- [x] `DebugHud.h`
  - [x] `DebugHud : Object` — mode cycling, application stats, frame/device/renderer metrics, event-driven viewport overlay, and deterministic unsubscription are ported and software-renderer tested; ordinal stat ordering preserves `ea::map`, reusable UTF-8 storage avoids render-path conversion allocation, and nullable `ImGui::Begin` close state remains an explicit generated-binding `TODO: port`
- [ ] `SystemMessageBox.h`
  - [ ] `SystemMessageBox : Object`
- [x] `BaseWidget.h`
  - [x] `BaseWidget : Object`
- [x] `ResourceInspectorWidget.h`
  - [x] `ResourceInspectorWidget : BaseWidget` — borrowed property descriptors, multi-resource undefined/default presentation, save disabling, deferred batched setters, and edit-boundary events are ported and interaction-tested
- [x] `SceneWidget.h`
  - [x] `SceneWidget : BaseWidget` — default scene, lazy render-to-texture setup, light tracking, orbit/zoom controls, skybox replacement, camera framing, and explicit owned-resource disposal are ported and software-renderer tested
- [x] `Texture2DWidget.h`
  - [x] `Texture2DWidget : BaseWidget` — strong texture lifetime, square preview area, aspect fitting, and horizontal centering are ported and software-renderer tested; the caller-owned generated `ImTextureRef` keeps rendering allocation-free
- [ ] `TransformGizmo.h`
  - [ ] `TransformGizmo`
  - [ ] `TransformNodesGizmo`
- [x] `DragDropPayload.h`
  - [x] `DragDropPayload : RefCounted` — managed payload remains strongly owned by the global custom `Variant`; ImGui receives an equivalent non-dereferenced nonzero marker
  - [x] `struct ResourceFileDescriptor` — managed reference type and `Clone` preserve C++ value-copy semantics
  - [x] `ResourceDragDropPayload : DragDropPayload`
  - [x] `NodeComponentDragDropPayload : DragDropPayload`
- [x] `SerializableInspectorWidget.h`
  - [x] `SerializableInspectorWidget : Object` — weak selection pruning, titles, hook registries and copy helpers, attribute/action rendering and edit boundaries, metadata-driven variant options, and explicit owned `Variant` cleanup are ported and software-renderer interaction-tested; borrowed enum metadata avoids per-frame allocation
- [x] `NodeInspectorWidget.h`
  - [x] `NodeInspectorWidget : Object` — weak node/component aliases preserve native non-ownership; ordered duplicate-type matching, inspector rebuilds, skipped counts, edit/action forwarding, and deferred multi-node removal are ported and software-renderer tested; persistent UTF-8 text and reference-identity ImGui IDs avoid render-path string allocation
- [ ] `SceneHierarchyWidget.h`
  - [ ] `SceneHierarchyWidget : Object`
- [x] `MaterialInspectorWidget.h`
  - [x] `MaterialInspectorWidget : Object` — preview scene and orbit controls, technique discovery/editing, multi-material undefined state, material properties and shader defines, default/custom textures, shader parameters, force-save signaling, and explicit owned-resource/`Variant` cleanup are ported and software-renderer interaction-tested; reusable UTF-8 and sorted scratch storage avoid render-path managed allocation, and the declaration-only native `UpdateCachedTechniques` remains an explicit `TODO: port`
- [x] `AnimationInspectorWidget.h`
  - [x] `AnimationInspectorWidget : ResourceInspectorWidget` — native length accessors and read-only save policy are ported and software-renderer tested
- [x] `ModelInspectorWidget.h`
  - [x] `ModelInspectorWidget : ResourceInspectorWidget` — native empty property table and read-only save policy are ported and software-renderer tested
- [x] `Texture2DInspectorWidget.h`
  - [x] `Texture2DInspectorWidget : ResourceInspectorWidget` — sRGB, linear-space, filter, and U/V address property mappings preserve the native enum labels and read-only save policy and are software-renderer tested
- [x] `TextureCubeInspectorWidget.h`
  - [x] `SphericalHarmonicsGenerator : Object` — native cubemap-face copy, GPU readback, weighted SH integration, XML replacement, and persistence are ported and software-renderer tested
  - [x] `TextureCubeInspectorWidget : ResourceInspectorWidget` — all six native texture properties, read-only policy, ImGui generation action, and deferred begin-rendering SH queue are ported and software-renderer tested
- [ ] `StandardSerializableHooks.h`
  - [ ] serializable inspector hooks registration

## B15. Plugins `OPTIONAL` — plugin/module system; depends on Core, Engine

- [ ] `Plugin.h`
  - [ ] `Plugin : Object`
- [ ] `PluginApplication.h`
  - [ ] `PluginApplication : Object`
  - [ ] `MainPluginApplication : PluginApplication`
- [ ] `DynamicModule.h`
  - [ ] `DynamicModule : Object`
- [ ] `ModulePlugin.h`
  - [ ] `ModulePlugin : Plugin`
- [ ] `ScriptBundlePlugin.h`
  - [ ] `ScriptBundlePlugin : Plugin`
- [ ] `PluginManager.h`
  - [ ] `PluginStack : Object`
  - [ ] `PluginManager : Object`

## [x] B16. Script `OPTIONAL` — managed scripting host; depends on Core


## B17. XR `OPTIONAL` — OpenXR VR; depends on Graphics, RenderAPI, Scene

- [-] `OpenXRAPI.h` — **EXCLUDED** · reason: OpenXR loader decls · replaced-by: generated OpenXR interop
- [ ] `VREvents.h`
  - [ ] VR event definitions
- [ ] `VRUtils.h`
  - [ ] `struct ButtonCommand`
- [ ] `VirtualReality.h`
  - [ ] `struct VRRigDesc`
  - [ ] `XRBinding : Object`
  - [ ] `XRActionGroup : Object`
  - [ ] `VirtualReality : Object`
- [ ] `OpenXR.h`
  - [ ] `OpenXR : VirtualReality`
- [ ] `VRRig.h`
  - [ ] `VRRig : LogicComponent`

## B18. Utility `OPTIONAL` — asset pipeline & editor tooling; depends on many (port last)

- [ ] `UtilityLibrary.h`
  - [ ] registration entry point
- [x] `AssetTransformer.h`
  - [x] `struct AssetTransformerInput` — managed reference type and `Clone` preserve C++ value-copy semantics
  - [x] `struct AssetTransformerOutput` — managed reference type and `Clone` preserve C++ value-copy semantics
  - [x] `struct AssetTransformerDependency` — managed reference type and `Clone` preserve C++ value-copy semantics
  - [x] `AssetTransformer : Serializable` — direct `System.IO` mapping replaces native `FileSystem` and `TemporaryDir`
- [x] `AssetTransformerHierarchy.h`
  - [x] `AssetTransformerHierarchy : Object`
- [x] `BaseAssetPostTransformer.h`
  - [x] `BaseAssetPostTransformer : AssetTransformer`
- [x] `CalculateAnimationVelocityTransformer.h`
  - [x] `struct CalculateAnimationVelocityParams` — managed reference type and `Clone` preserve C++ value-copy semantics
  - [x] `struct CalculateAnimationVelocityTask` — managed reference type, strong resource references, and `Clone` preserve C++ value and `SharedPtr` copies
  - [x] `CalculateAnimationVelocityTransformer : BaseAssetPostTransformer`
- [x] `GenerateWorldSpaceTracksTransformer.h`
  - [x] `struct GenerateWorldSpaceTracksParams` — managed reference type and `Clone` preserve C++ value-copy semantics; standard C# sets and maps replace EASTL containers
  - [x] `struct GenerateWorldSpaceTracksTask` — managed reference type, strong resource references, and `Clone` preserve C++ value and `SharedPtr` copies
  - [x] `GenerateWorldSpaceTracksTransformer : BaseAssetPostTransformer`
- [x] `RetargetAnimationsTransformer.h`
  - [x] `struct RetargetAnimationTask` — managed reference type and `Clone` preserve C++ value copies, strong resource references, and nested standard collections
  - [x] `RetargetAnimationsTransformer : BaseAssetPostTransformer` — the enabled native IK path uses the existing managed IK module
- [x] `AnimationMetadata.h` — faithful C# source conversion is present and builds in the optional Utility module
  - [x] animation metadata helpers
- [x] `AssetPipeline.h`
  - [x] `AssetPipeline : Resource`
- [ ] `FileSystemReflection.h`
  - [x] `struct FileSystemEntry` — managed reference type with `Clone` preserves recursive C++ value copies and stable parent references
  - [x] `FileSystemReflection : Object` — excluded native `FileSystem` calls map to `System.IO`
- [x] `PackedSceneData.h`
  - [x] `PackedNodeData`
  - [x] `PackedComponentData`
  - [x] `PackedNodeComponentData`
  - [x] `PackedSceneData`
- [x] `SceneSelection.h`
  - [x] `struct PackedSceneSelection` — managed reference type and `Clone` preserve C++ value-copy semantics
  - [x] `SceneSelection` — `WeakReference` sets preserve native weak ownership and target-identity set semantics without allocating during selection queries
- [x] `GLTFImporter.h` — faithful C# source conversion is present in the optional Utility module and the full solution builds
  - [x] `struct GLTFImporterSettings`
  - [x] `GLTFImporterCallback`
  - [x] `GLTFImporter : Object`
- [x] `SceneRendererToTexture.h`
  - [x] `CustomBackbufferTexture : Object`
  - [x] `SceneRendererToTexture : CustomBackbufferTexture`
- [ ] `SceneViewerApplication.h` ⤴ needs `Plugins`
  - [ ] `SceneViewerApplication : MainPluginApplication`

---

# EDITOR APPLICATION

The managed editor lives under `src/RedBolt/Editor`, matching rbfx's separate `EditorLibrary` boundary.
User-code reloading and engine/editor plugins are deferred by request. Keep their native call sites as explicit
`TODO: port` guidance when the surrounding editor class is ported; do not replace them with alternate reload or plugin paths.

## Editor/Core

- [x] `UndoManager.h`
  - [x] `UndoException`
  - [x] `EditorAction`
  - [x] `BaseEditorActionWrapper`
  - [x] `UndoManager`
- [x] `HotkeyManager.h`
  - [x] `EditorHotkey`
  - [x] `HotkeyManager`
- [x] `IniHelpers.h` — uses .NET UTF-8 and invariant integer conversion per allowed string-utility deviation
- [x] `SettingsManager.h` — C++ simple-page template contract maps to `ISimpleSettingsPageValues`, including construction and reset through `T{}` so struct default member initializers are preserved
  - [x] `SettingsPage`
  - [x] `SimpleSettingsPage<T>`
  - [x] `SettingsPageGroup`
  - [x] `SettingsManager`
- [x] `CommonEditorActions.h`
  - [x] `EmptyEditorAction`
  - [x] `CompositeEditorAction`
  - [x] `CreateRemoveNodeAction`
  - [x] `CreateRemoveComponentAction`
  - [x] `ChangeNodeTransformAction`
  - [x] `ChangeNodeAttributesAction`
  - [x] `ChangeComponentAttributesAction`
  - [x] `ReorderNodeAction`
  - [x] `ReorderComponentAction`
  - [x] `ReparentNodeAction`
  - [x] `ChangeComponentAction`
  - [x] `ChangeNodeSubtreeAction`
  - [x] `ChangeSceneAction`
- [x] `CommonEditorActionBuilders.h`
  - [x] `CreateNodeActionBuilder`
  - [x] `RemoveNodeActionBuilder`
  - [x] `CreateComponentActionBuilder`
  - [x] `RemoveComponentActionBuilder`
  - [x] `ChangeAttributeBuffer` — managed reference type preserves stored `ChangeAttributeBuffer&`; `Clone` and `Dispose` preserve implicit value and lifetime semantics
  - [x] `ChangeNodeAttributesActionBuilder`
  - [x] `ChangeComponentAttributesActionBuilder`
- [ ] `EditorPlugin.h` — **DEFERRED** · editor plugins will be ported later; keep explicit `TODO: port` call-site guidance
- [ ] `EditorPluginManager.h` — **DEFERRED** · editor plugins will be ported later; keep explicit `TODO: port` call-site guidance

## Editor/Project

- [x] `EditorTab.h`
  - [x] `EditorTabFlag` / `EditorTabFlags` — mapped to a C# flags enum
  - [x] `EditorTabPlacement`
  - [x] `EditorConfigurable`
  - [x] `SeparatorHelper`
  - [x] `EditorTab` — exact `ContentRegionRect` queries remain explicit `TODO: port` until internal Dear ImGui state is bound
- [x] `Project.h`
  - [x] `ProjectFlag` / `ProjectFlags` — mapped to a C# flags enum
  - [x] `CloseProjectResult`
  - [x] `ResourceCacheGuard` — internal no-dispose VFS detach preserves original mount identity and order across project lifetime
  - [x] `AnalyzeFileContext` — managed reference preserves const callback aliasing; `Dispose` preserves owned member destruction order
  - [x] `Project` — plugin, DockBuilder, and XR call sites retain explicit `TODO: port` guidance for deferred or unavailable APIs
- [x] `CreateDefaultScene.h`
  - [x] `DefaultSceneParameters`
  - [x] `CreateDefaultScene`
- [x] `LaunchManager.h`
  - [x] `LaunchConfiguration` — managed reference type preserves mutable pointer use; `Clone` and `Dispose` preserve C++ value and lifetime semantics
  - [x] `LaunchManager`
- [x] `CloseDialog.h` — request value copies, sorted persistent UTF-8 item storage, save enablement, modal fallback discard, save/discard/cancel callback exclusivity, active-state transitions, and deterministic storage cleanup are ported and software-renderer tested
  - [x] `CloseResourceRequest` — managed reference type with `Clone` preserves implicit C++ value-copy semantics
  - [x] `CloseDialog` — `CString` item storage preserves native UTF-8 ordering without per-frame conversions
- [x] `ProjectRequest.h`
  - [x] `ProjectRequest`
  - [x] `OpenResourceRequest`
  - [x] `BaseInspectRequest`
  - [x] `InspectResourceRequest`
  - [x] `InspectNodeComponentRequest` — managed weak references use unique scene IDs for stable canonical ordering because CLR object addresses are movable
  - [x] `CreateResourceRequest` — strong managed reference preserves dropped `SharedPtr` ownership
- [ ] `ResourceFactory.h`
  - [x] `ResourceFactory`
  - [x] `BaseResourceFactory` — generated raw ImGui callback and resizable `CString` storage preserve `ea::string` text input behavior
  - [x] `SimpleResourceFactory`
- [x] `ModifyResourceAction.h`
  - [x] `ModifyResourceAction`
- [x] `ResourceEditorTab.h`
  - [x] `ResourceEditorTab` — deterministic delayed callback preserves native shared-finally close sequencing
  - [x] `ResourceActionWrapper`
- [x] `AssetManager.h`
  - [x] `AssetManager`
- [x] `ToolManager.h`
  - [x] `ToolManager` — `.NET Process` provides the native `FileSystem::SystemRun` operation; resizable `CString` inputs preserve `ea::string` editing
- [x] `CreateComponentMenu.h` — uses the global managed `Context`; persistent UTF-8 labels preserve native menu rendering without per-frame conversions
- [ ] Remaining project layer inventory and port

## Editor/Foundation

- [x] `StandardFileTypes.h` — native callback order and all binary, XML, JSON, audio, scene, material, serializable-resource, render-path, texture, model, animation, asset-pipeline, font, and prefab type tags are ported and software-renderer tested
- [x] `ConcurrentAssetProcessing.h` — command-pipe input/output serialization, single-process command dispatch, remote batch processing, configurable concurrency, copied asynchronous inputs, and temporary-directory lifetime are ported and software-renderer tested; generated `SliderInt` input/output uses `ref`
- [x] `ConsoleTab.h` — console rendering, optional monospaced font, level controls, clear action, and per-level INI persistence are ported and software-renderer tested; the plugin entry point remains an explicit deferred `TODO: port`
- [x] `GameViewTab.h` — play/replay/stop sequencing, custom-backbuffer and RmlUI routing, one-shot render-target clear, input/project capture, Debug HUD overlay, INI persistence, and teardown restoration are ported and software-renderer tested; plugin start/stop/quit callbacks remain explicit deferred `TODO: port` paths, and built-in legacy UI branches remain explicit excluded `TODO` paths
- [x] `ResourceBrowserTab/AssetPipelineFactory.h` — factory metadata, ModelImporter option UI, empty-pipeline creation, optional default importer insertion, and asset-pipeline persistence are ported and software-renderer tested; the plugin entry point remains an explicit deferred `TODO: port`
- [x] `ResourceBrowserTab/MaterialFactory.h` — material-type and lighting option UI, all native technique-selection branches, shader-define composition, and material persistence are ported and software-renderer tested; generated `RadioButton` input/output uses `ref`, and the plugin entry point remains an explicit deferred `TODO: port`
- [x] `ResourceBrowserTab/SceneFactory.h` — scene/prefab metadata, scene-only quality and default-object UI, parameter transfer, native scene output, and native prefab output are ported and software-renderer tested; the plugin entry point remains an explicit deferred `TODO: port`
- [x] `AnimationViewTab.h` — animation-type dispatch, animated-model preview ownership, looped exclusive layer-zero playback, deferred controller stop semantics, optional model selection, camera framing, render gating, unload reference semantics, and reverse teardown are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `ModelViewTab.h` — model-type dispatch, single-resource preview ownership, shadow-casting static-model display, camera-operator bounding-box fitting, controller-state handoff, unload, and reverse shared-reference release are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `Texture2DViewTab.h` — Texture2D type dispatch, single-resource gating, native shadowed image-preview ownership, title/child rendering, replacement ordering, unload disposal, and reverse base-preview teardown are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `TextureCubeViewTab.h` — cubemap-type dispatch, single-resource gating, skybox and zone binding, conditional tab rendering, explicit cubemap rendering, unload reference semantics, and deterministic shared-reference release are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `Shared/HierarchyBrowserSource.h` — C++ multiple-inheritance role mapped to a C# interface with native no-op defaults
- [x] `Shared/InspectorSource.h` — C++ signal maps to a C# event; multiple-inheritance role maps to an interface with native defaults
- [x] `Shared/CameraController.h` — native camera settings and page state, nine passive hotkeys, free-look/orbit/scroll motion, camera-state restoration, orthographic aspect preservation, and focus smoothing are ported and software-renderer tested; a static key map avoids per-frame managed allocation, and generated `InputFloat` input/output uses `ref`
- [x] `Shared/CustomSceneViewTab.h` — default preview-scene ownership, active-resource title/reset control, child sizing, camera-controller handoff, and reverse deterministic disposal are ported and software-renderer tested; reusable UTF-8 title storage avoids per-frame allocation
- [x] `Shared/InspectorWithPreview.h` — typed request priority, stable sorted selection reuse, undo/save edit boundaries, single-resource preview ownership, and both native child layouts are ported and software-renderer tested
- [x] `HierarchyBrowserTab.h` — native weak source ownership is preserved; `Foundation_HierarchyBrowserTab` plugin entry point is deferred
- [x] `InspectorTab.h` — owns registered addons, connects on activation, and weakly forwards the active source; plugin entry point is deferred
- [x] `InspectorTab/AnimationInspector.h` — animation type dispatch and read-only animation widget construction are ported; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/Texture2DInspector.h` — texture type dispatch, read-only property widget construction, and aspect-fitted texture preview are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/TextureCubeInspector.h` — texture-cube type dispatch, read-only cube widget construction, and skybox scene preview are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/ModelInspector.h` — model type dispatch, read-only model widget construction, and framed static-model scene preview are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/PrefabInspector.h` — prefab type dispatch, read-only model widget construction, instantiated-prefab normalization, optimal camera framing, and orbit limits are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/SerializableResourceInspector.h` — serializable-resource dispatch, filtered weak selections, shared attribute/action widget wiring, undo capture, delayed saves, and deterministic widget cleanup are ported and software-renderer tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/NodeComponentInspector.h` — node/component request dispatch, native selection precedence, shared widget wiring, attribute and subtree undo capture, component add/remove, and mixed-type summaries are ported and software-renderer tested; stable scene node IDs replace sorting by movable CLR object addresses, persistent UTF-8 text avoids render-path conversion allocation, and the plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/MaterialInspector.h` — all-material request dispatch, stable sorted selection reuse, material-widget ownership, grouped undo capture, delayed saves, and periodic technique refresh are ported and software-renderer interaction-tested; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/RenderPathInspector.h` — single-resource dispatch, reload invalidation and suppression, per-pass attribute widgets, grouped undo/save capture, pass add/remove/reorder controls, and reload broadcasts are ported and software-renderer tested; reference-identity ImGui IDs, persistent UTF-8 text, and direct weak-target comparison preserve stable behavior without per-frame managed allocation; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/AssetPipelineInspector.h` — single-resource dispatch, reload invalidation, transformer attribute widgets, explicit Apply/Discard action completion, asset-cache invalidation, transformer add/remove controls, and pending-change feedback are ported and software-renderer tested; reference-identity ImGui IDs, persistent UTF-8 text, and direct weak-target comparison preserve stable behavior without per-frame managed allocation; plugin entry point remains an explicit deferred `TODO: port`
- [x] `InspectorTab/EmptyInspector.h` — lowest-priority inspect-request sink and activation signal are ported; plugin entry point is deferred
- [x] `InspectorTab/PlaceholderResourceInspector.h` — non-empty resource selections are summarized at native fallback priority; plugin entry point is deferred
- [x] `InspectorTab/SoundInspector.h` — sorted sound selections, metadata, playback controls, and timeline seeking are ported; generated `SliderFloat` input/output uses `ref`; plugin entry point is deferred
- [x] `SettingsTab.h` — responsive tree/page layouts, selection, hotkey forwarding, reset controls, and unsaved state are ported; persistent UTF-8 labels and page unique names preserve stable ImGui IDs; plugin entry point is deferred
- [x] `SettingsTab/KeyBindingsPage.h` — non-serializable settings registration contract and command/press/hold binding table are ported and software-renderer tested; a reusable UTF-8 buffer avoids per-frame encoding allocation, and the plugin entry point remains an explicit deferred `TODO: port`
- [x] `SettingsTab/LaunchPage.h` — configuration rendering, unused-name generation, descending removals, native-width hash change detection, unsaved marking, and the unspecified-plugin choice are ported and software-renderer interaction-tested; loaded plugin choices and the plugin entry point remain explicit deferred `TODO: port` paths
- [ ] Foundation tabs, inspectors, scene tools, and resource browser inventory and port

## Editor/Assets

- [x] `ModelImporter.h`
  - [x] `struct ResetRootMotionInfo` — managed value type preserves native archive layout
  - [x] `struct TransformerParams` — composition preserves `GLTFImporterSettings` inheritance; `Clone` and `Dispose` preserve native value and metadata ownership semantics
  - [x] `ModelImporter : AssetTransformer` — callback forwarding replaces C++ secondary inheritance; Glow UV generation, GLTF/FBX/Blend inputs, parameter overlays, animation metadata, root-motion reset, nested transformers, and temporary cleanup are ported
  - [x] Editor default asset pipeline uses reflected `ModelImporter` type name
  - [ ] `Assets_ModelImporter` plugin entry point — **DEFERRED** with the editor plugin system; its `EditorApplication` call site must remain explicit `TODO: port` guidance

## Editor application and data

- [x] `EditorApplication` — command-line/setup state, recent-project serialization and previews, project lifecycle, external `Project.json`/project-directory drops with empty-directory creation and graceful active-project replacement, custom fonts/style/settings handlers, dockspace/start page/menu/toolbar rendering with collision-free launch-tile IDs, an unbounded desktop run loop and graphics-safe project-preview shutdown, console links, and profiler launch are ported and software-renderer runtime-tested; editor plugins, user plugin reload, NFD folder picking, console-command suppression, and resource-prefix discovery remain explicit `TODO: port` call sites
- [x] Editor executable and desktop build integration — the Linux apphost, runtime dependency manifest, native-module probing, command-line entry point, and real llvmpipe launch are wired and tested
- [x] `EditorData` resources and runtime staging — all 53 authoritative rbfx resources are tracked and symlinked beside desktop build outputs by the native build target
- [ ] User-code reloading — **DEFERRED** · port later; keep explicit `TODO: port` call-site guidance
