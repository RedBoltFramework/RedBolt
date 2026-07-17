# RedBolt Port Checklist — `Source/Urho3D`

## Samples

- [x] `01_HelloWorld` — Hello World sample ported with Dear ImGui replacing the rbfx Text control
- [x] `02_HelloGUI` — control, title, close, and draggable-position demonstrations mapped to Dear ImGui widgets
- [x] `04_StaticScene` — plane, directional light, randomized mushroom field, camera/viewport lifecycle, and Dear ImGui instruction sample reconciled to rbfx
- [x] `05_AnimatingScene` — scene, zone, static-model, node-logic animation, camera, point-light, viewport, and Dear ImGui instruction sample ported
- [x] `09_MultipleViewports` — scene, rear camera, dual viewport sample, automatic render pipeline allocation, render-target viewport handling, Dear ImGui instructions, requested window sizing, delayed multi-frame screenshots, and stable Vulkan shadows are runtime-verified against rbfx
- [x] `34_DynamicGeometry` — cloned dynamic vertex buffers, from-scratch pyramid geometry, animation, camera, zone lighting, automatic render pipeline, and Dear ImGui instructions reconciled to rbfx
- [x] `10_RenderToTexture` — render-to-texture scene, queued RenderSurface viewport, Dear ImGui instructions, render-surface scheduling, and resolved shader-resource sampling are ported and runtime-verified on OpenGL and Vulkan
- [~] `54_WindowSettingsDemo` — window-settings scene and native fullscreen-mode, DPI, refresh-rate, and MSAA apply semantics ported with Dear ImGui; upstream comparison removed the extra C# `RenderPipeline` component and aligned settings reads with `RenderDevice`; runtime now starts successfully, but the 3D box remains absent in the screenshot and requires further engine-level investigation
- [ ] `03_Sprites` — skipped per user direction; it depends on the 2D submodule
- [~] `06_SkeletalAnimation` — sample, `Mover3D`, animated models, controller playback, scene, lighting, camera, and Dear ImGui instructions are ported and runtime-verified; renderer debug-geometry submission remains unavailable
- [~] `07_Billboards` — sample is ported and builds; runtime currently fails in the existing shadow-light processor sort and is deferred
- [~] `08_Decals` — scene population, occluders, cursor raycast, decal creation, camera, and Dear ImGui instructions are ported and runtime-verified; renderer debug-geometry submission remains unavailable
- [x] `20_HugeObjectCount` — 62,500-object scene, grouped and ungrouped modes, animation toggle, camera, and Dear ImGui instructions are ported and render successfully; initial population is intentionally expensive
- [~] `30_LightAnimation` — animated point light, scene population, camera, and animated Dear ImGui text replacement are ported and runtime-verified; the built-in UI sprite animation still needs a Dear ImGui texture equivalent
- [x] `31_MaterialAnimation` — material shader-parameter animation, scene population, camera, and Dear ImGui instructions are ported and runtime-verified
- [~] `112_AggregatedInput` — direction aggregation, directional-pad aggregation, synthesized key logging, and Dear ImGui visualization are ported and runtime-verified
- [~] `113_InputLogger` — dual viewports, input-event subscriptions, event merging/logging, and Dear ImGui output are ported and runtime-verified as a standalone managed application rather than a plugin entry point
- [~] `116_VirtualFileSystem` — mount-point enumeration, identifier query, file metadata, reverse lookup, recursive scanning, and Dear ImGui output are ported and runtime-verified; editable Dear ImGui query controls remain
- [~] `117_PointerAdapter` — adapter lifecycle and synthesized pointer events, scene raycast, outline selection, and Dear ImGui instructions are ported and runtime-verified; the unported physics subsystem line is retained as a TODO
- [~] `118_CameraShake` — sample is ported and builds; runtime scene archive loading fails in the existing managed deserializer and is deferred
- [~] `121_CameraOperator` — sample and Dear ImGui controls are ported and build; runtime scene archive loading fails in the existing managed deserializer and is deferred
- [~] `122_TextureFormats` — software/hardware texture setup, comparison scene, camera, and Dear ImGui labels are ported and build; the upstream sample DDS resources are not found by the current runtime resource mounts

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
  regeneration is deterministic under `-j16`; all 1010 tests pass; screenshot-on-exit retains the valid rendered
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
- [-] `Exception.h` - **EXCLUDED** · reason: use C# `RuntimeException` / `ArgumentException` or similar.
- [-] `StringUtils.h` — **EXCLUDED** · reason: string parsing and formatting utilities · replaced-by: .NET string APIs
- [-] `Signal.h` - **EXCLUDED** · reason: use C# events

## [x] A4. IO — depends on Container, Core

- [-] `FileSystem.h` — **EXCLUDED** · reason: use standard library functionality
- [-] `MacFileWatcher.h` — **EXCLUDED** · reason: macOS FSEvents backend · replaced-by: `FileSystemWatcher`
- [-] `RWOpsWrapper.h` — **EXCLUDED** · reason: SDL_RWops C++ adapter · replaced-by: generated SDL interop

## [x] A5. Resource — depends on IO, Core

## [~] A6. Scene — depends on Resource, Core

- [~] `Node.h` / `Node.cpp` — listener ownership and expired-listener cleanup reconciled while auditing CameraOperator; the complete Node lifetime/copy audit remains pending

## [x] A7. RenderAPI — depends on Core, Math (current effort)

## [x] A8. Shader — depends on Core, IO (feeds Graphics)

## [~] A9. Graphics — depends on RenderAPI, Scene, Resource, Shader

- [~] `AnimatedModel.h` / `AnimatedModel.cpp` — constructor, factory registration, weak animation-state ownership, animator replacement, and last-model bone-hierarchy destruction reconciled; final lifetime/copy closure depends on pending Skeleton, SoftwareModelAnimator, Model morph, StaticModel, and Node removal audits
- [~] `Animation.h` / `Animation.cpp` — constructor/factory and owned trigger/track teardown reconciled; trigger, transform-track, and variant-track copies are independent, while final closure depends on ResourceWithMetadata, Variant call sites, and AnimationController pointer mapping
- [~] `AnimationController.h` / `AnimationController.cpp` — constructor, controller-owned teardown, animation-instance value copies, deferred state synchronization, model/bone track connection, renderer statistics, and attribute subspan serialization reconciled; final lifetime/copy closure depends on AnimationStateSource/AnimationState, Variant, Skeleton/Bone, AnimatedModel, and borrowed trigger-pointer audits
- [x] `AnimationParameters` in `AnimationController.h` / `AnimationController.cpp` — all constructors, implicit field teardown, independent wrapped-time copies, borrowed animation aliases, fluent helpers, delayed removal, and variant/binary serialization reconciled
- [~] `BillboardSet.h` / `BillboardSet.cpp` — constructor defaults, billboard value storage and mutable references, derived resource teardown, and non-copyable component semantics reconciled; final ownership closure depends on pending IndexBuffer and VertexBuffer audits
- [x] `Camera.h` / `Camera.cpp` — non-copyable component semantics, value-owned projection/view/frustum caches, weak Graphics observation, borrowed Zone ownership, and default derived destruction reconciled
- [x] `CameraOperator.h` / `CameraOperator.cpp` — weak tracked-node ownership, value-owned serialization and point caches, listener registration/removal, and default derived destruction reconciled
- [x] `CubemapRenderer.h` / `CubemapRenderer.cpp` — weak scene and texture observations, independent parameter value copies, shared pipeline-state aliases, non-copyable object semantics, and managed temporary-camera teardown reconciled
- [~] `CustomGeometry.h` / `CustomGeometry.cpp` — vertex value semantics, owned geometry/buffer teardown, borrowed batch materials, cached material attributes, mapped commit, and ray queries reconciled; final closure depends on the pending VertexBuffer data-loss-state audit
- [x] `DebugLine` / `DebugTriangle` in `DebugRenderer.h` — zero-default construction, field order, independent value copies, trivial destruction, and DebugRenderer value-list storage verified
- [x] `DebugRenderer.h` / `DebugRenderer.cpp` — weak camera observation, independent frustum value copy, owned vertex-buffer and pipeline-cache teardown, non-copyable component semantics, and value-owned debug primitives reconciled
- [x] `DecalSet.h` / `DecalSet.cpp` — decal and vertex value-copy semantics, owned geometry and buffer teardown, cloned bone values with borrowed weak node listeners, skinned face extraction and bone remapping, and shared tangent generation reconciled
- [x] `Drawable.h` / `Drawable.cpp` — non-copyable base semantics, SourceBatch shallow value copies, independent cached reflection values, weak borrowed zone/camera observations, queued-update cancellation, deterministic batch release, and abstract bounding-box contract reconciled
- [x] `DrawableEvents.h` — all five struct event payloads preserve declaration order and borrowed object pointers; ParticleEffect typing and independent Variant trigger-data assignment reconciled
- [x] `Geometry.h` / `Geometry.cpp` — retained stream/dependency resize semantics, shared buffer and raw-array ownership, deterministic subscription and ID teardown, allocation-free shadow-data access, and non-indexed ray fallback reconciled
- [~] `GlobalIllumination.h` / `GlobalIllumination.cpp` — owned mesh/baked-data reset semantics, world-origin mutation, debug geometry, reflected attributes, nullable resource loading, binary-compatible GI serialization, and default destruction reconciled; the reload editor action remains `TODO: port` until ObjectReflection supports actions


## [x] A10. RenderPipeline — depends on Graphics, RenderAPI, Scene (current effort)

## [x] A11. Input — depends on Core, Graphics (window)

## [~] A12. Engine — top-level orchestration, depends on all Part-A above

- [x] `StateManagerEvents.h` — struct-event payload mapping reconciled
- [~] `StateManager.h` / `StateManager.cpp` — core application-state lifecycle, queue value copies, weak cache, transitions, viewports, fog, mouse state, and engine registration reconciled; built-in UI is excluded, while compile-gated Actions members remain `TODO: port` until B1 exists

---

# PART B — OPTIONAL SUBSYSTEMS (port after Part A)

Ordered so that optional subsystems depended on by other optional subsystems come first.

## B1. Actions `OPTIONAL` — tweening/action system; depends on Scene

- [ ] `ActionState.h`
  - [ ] `ActionState`
- [ ] `Attribute.h`
  - [ ] attribute-action helpers
- [ ] `BaseAction.h`
  - [ ] `BaseAction : Serializable`
- [ ] `FiniteTimeAction.h`
  - [ ] `FiniteTimeAction : BaseAction`
- [ ] `FiniteTimeActionState.h`
  - [ ] `FiniteTimeActionState : ActionState`
- [ ] `ActionInstant.h`
  - [ ] `ActionInstant : FiniteTimeAction`
- [ ] `ActionInstantState.h`
  - [ ] `ActionInstantState : FiniteTimeActionState`
- [ ] `AttributeAction.h`
  - [ ] `AttributeAction : FiniteTimeAction`
- [ ] `AttributeActionState.h`
  - [ ] `AttributeActionState : FiniteTimeActionState`
- [ ] `Move.h`
  - [ ] `MoveBy : AttributeAction`
  - [ ] `MoveByQuadratic : MoveBy`
  - [ ] `JumpBy : AttributeAction`
  - [ ] `ScaleBy : AttributeAction`
  - [ ] `RotateBy : AttributeAction`
  - [ ] `RotateAround : AttributeAction`
- [ ] `Misc.h`
  - [ ] `RemoveSelf : FiniteTimeAction`
  - [ ] `DelayTime : FiniteTimeAction`
  - [ ] `Show : AttributeAction`
  - [ ] `Hide : AttributeAction`
  - [ ] `Enable : AttributeAction`
  - [ ] `Disable : AttributeAction`
  - [ ] `Blink : AttributeAction`
- [ ] `Ease.h`
  - [ ] `ActionEase : FiniteTimeAction`
  - [ ] `EaseElastic : ActionEase`
  - [ ] `EaseBackIn/Out/InOut : ActionEase`
  - [ ] `EaseBounceOut/In/InOut : ActionEase`
  - [ ] `EaseSineOut/In/InOut : ActionEase`
  - [ ] `EaseExponentialOut/In/InOut : ActionEase`
  - [ ] `EaseElasticIn/Out/InOut : EaseElastic`
- [ ] `CallFunc.h`
  - [ ] `ActionCallHandler : RefCounted`
  - [ ] `CallFunc : ActionInstant`
  - [ ] `SendEvent : ActionInstant`
- [ ] `ShaderParameter.h` ⤴ needs `Graphics/Material`
  - [ ] `ShaderParameterAction : FiniteTimeAction`
  - [ ] `ShaderParameterTo : ShaderParameterAction`
  - [ ] `ShaderParameterFromTo : ShaderParameterTo`
- [ ] `Parallel.h`
  - [ ] `Parallel : FiniteTimeAction`
- [ ] `Sequence.h`
  - [ ] `Sequence : FiniteTimeAction`
- [ ] `Repeat.h`
  - [ ] `Repeat : FiniteTimeAction`
  - [ ] `RepeatForever : FiniteTimeAction`
- [ ] `ActionSet.h`
  - [ ] `ActionSet : Resource`
- [ ] `ActionBuilder.h`
  - [ ] `ActionBuilder`
- [ ] `ActionManager.h`
  - [ ] `ActionManager`

## B2. Audio `OPTIONAL` — depends on Scene, Resource

- [x] `AudioDefs.h`
  - [x] audio enums / constants — SoundMaster/Effect/Ambient/Voice/Music constants and SpeakerMode enum ported
- [x] `AudioEvents.h`
  - [x] audio event definitions — SoundFinished, RecordingUpdated/Started/Ended events ported
- [x] `Sound.h`
  - [x] `Sound : ResourceWithMetadata` — WAV loading, raw data, loop/size/format, decoder stream, sample properties, and optional XML format/loop/metadata parameters ported
- [x] `SoundStream.h`
  - [x] `SoundStream` — abstract base with Seek/GetData/SetFormat/StopAtEnd ported, including the 44100 Hz default frequency
- [x] `BufferedSoundStream.h`
  - [x] `BufferedSoundStream : SoundStream` — thread-safe buffered data with AddData/Clear ported
- [x] `OggVorbisSoundStream.h`
  - [x] `OggVorbisSoundStream : SoundStream` — stb_vorbis-backed compressed stream, format metadata, seeking, looping, and PCM decoding ported
- [x] `SoundSource.h`
  - [x] `SoundSource : Component` — Play/Stop, frequency/gain/panning/attenuation, mixing, and serialization ported
- [x] `SoundSource3D.h`
  - [x] `SoundSource3D : SoundSource` — distance/angle attenuation parameters, listener-relative attenuation calculation, and sphere/cone debug geometry are ported
- [x] `SoundListener.h`
  - [x] `SoundListener : Component` — listener component ported
- [ ] `Microphone.h`
  - [ ] `Microphone : Object` — recording buffer, linked stream, enable state, wake threshold, sleeping state, recording events, and queued SDL capture/device lifecycle are ported; SDL callback binding remains unavailable
- [ ] `Audio.h`
  - [ ] `Audio : Object` — SDL audio device init, queued output, direct managed mix output, master gain types, sound source management, render-update driven mixing, native microphone enumeration, and queued microphone capture are ported; SDL callback binding remains unavailable
  - [x] `14_SoundEffects` sample — sound effect playback, music control, volume/panning/reach/LFE, microphone controls, and ImGui controls ported; startup WAV playback verified
  - [x] `29_SoundSynthesis` sample — procedural audio synthesis ported with rbfx buffering, degree-based dual oscillators (including the C++ 180-degree second-oscillator start), filter, and ImGui instructions

## B3. UI `EXCLUDED` — retained-mode UI; depends on Graphics, Resource, Input

## B4. Physics `OPTIONAL` — Bullet; depends on Scene, Graphics

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
- [ ] `Constraint.h`
  - [ ] `Constraint : Component`
- [ ] `KinematicCharacterController.h`
  - [ ] `KinematicCharacterController : Component`
- [ ] `RaycastVehicleWheel.h`
  - [ ] `RaycastVehicleWheel : Component`
- [ ] `RaycastVehicle.h`
  - [ ] `RaycastVehicle : LogicComponent`
- [ ] `TriggerAnimator.h`
  - [ ] `TriggerAnimator : Component`

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

- [ ] `NavigationDefs.h`
  - [ ] navigation enums / constants
- [ ] `NavigationEvents.h`
  - [ ] navigation event definitions
- [ ] `NavigationUtils.h`
  - [ ] navigation helpers / registration
- [ ] `NavBuildData.h`
  - [ ] `struct NavAreaStub`
  - [ ] `struct NavBuildData : NonCopyable`
  - [ ] `struct SimpleNavBuildData : NavBuildData`
  - [ ] `struct DynamicNavBuildData : NavBuildData`
- [ ] `NavigationMesh.h`
  - [ ] `struct NavigationPathPoint`
  - [ ] `NavigationMesh : Component`
- [ ] `DynamicNavigationMesh.h`
  - [ ] `DynamicNavigationMesh : NavigationMesh`
- [ ] `NavArea.h`
  - [ ] `NavArea : Component`
- [ ] `Navigable.h`
  - [ ] `Navigable : Component`
- [ ] `Obstacle.h`
  - [ ] `Obstacle : Component`
- [ ] `OffMeshConnection.h`
  - [ ] `OffMeshConnection : Component`
- [ ] `CrowdAgent.h`
  - [ ] `CrowdAgent : Component`
- [ ] `CrowdManager.h`
  - [ ] `CrowdManager : Component`

## B8. Network `OPTIONAL` — depends on IO, Core

- [ ] `Protocol.h`
  - [ ] protocol message-id constants
- [ ] `PacketTypeFlags.h`
  - [ ] `PacketTypeFlags`
- [ ] `NetworkEvents.h`
  - [ ] network event definitions
- [ ] `URL.h`
  - [ ] `URL`
- [ ] `MessageUtils.h`
  - [ ] `LargeMessageWriter`
  - [ ] `LargeMessageReader`
  - [ ] `MultiMessageWriter`
- [ ] `ClockSynchronizer.h`
  - [ ] `FilteredUint`
  - [ ] `ClockSynchronizer`
- [ ] `AbstractConnection.h`
  - [ ] `AbstractConnection : Object, IDFamily<AbstractConnection>`
- [ ] `Transport/NetworkConnection.h`
  - [ ] `NetworkConnection : Object`
- [ ] `Transport/NetworkServer.h`
  - [ ] `NetworkServer : Object`
- [ ] `Transport/DataChannel/DataChannelConnection.h`
  - [ ] `DataChannelConnection : NetworkConnection`
- [ ] `Transport/DataChannel/DataChannelServer.h`
  - [ ] `DataChannelServer : NetworkServer`
- [ ] `Connection.h`
  - [ ] `Connection : AbstractConnection`
- [ ] `HttpRequest.h`
  - [ ] `HttpRequest : RefCounted, Deserializer, Thread`
- [ ] `LANDiscoveryManager.h`
  - [ ] `LANDiscoveryManager : Object`
- [ ] `Network.h`
  - [ ] `Network : Object`

## B9. Replica `OPTIONAL` — scene replication; depends on Network, Scene, Graphics

- [ ] `NetworkId.h`
  - [ ] `NetworkId`
- [ ] `NetworkSettingsConsts.h`
  - [ ] network settings constants
- [ ] `ProtocolMessages.h`
  - [ ] protocol message structs
- [ ] `NetworkCallbacks.h`
  - [ ] network callback interfaces
- [ ] `NetworkTime.h`
  - [ ] `NetworkTime`
- [ ] `NetworkValue.h`
  - [ ] `NetworkValue<T>` / interpolation helpers
- [ ] `TickSynchronizer.h`
  - [ ] `TickSynchronizer`
  - [ ] `SceneUpdateSynchronizer : Object`
- [ ] `ClientInputStatistics.h`
  - [ ] `ClientInputStatistics`
- [ ] `NetworkObject.h`
  - [ ] `NetworkObject`
- [ ] `StaticNetworkObject.h`
  - [ ] `StaticNetworkObject : NetworkObject`
- [ ] `BehaviorNetworkObject.h`
  - [ ] `NetworkBehavior`
  - [ ] `BehaviorNetworkObject : StaticNetworkObject`
- [ ] `BaseFeedbackBehavior.h`
  - [ ] `BaseFeedbackBehavior<T>`
- [ ] `ReplicatedTransform.h`
  - [ ] `ReplicatedTransform : NetworkBehavior`
- [ ] `ReplicatedParent.h`
  - [ ] `ReplicatedParent : NetworkBehavior`
- [ ] `ReplicatedAnimation.h`
  - [ ] `ReplicatedAnimation : NetworkBehavior`
- [ ] `FilteredByOwner.h`
  - [ ] `FilteredByOwner : NetworkBehavior`
- [ ] `FilteredByDistance.h`
  - [ ] `FilteredByDistance : NetworkBehavior`
- [ ] `TrackedAnimatedModel.h`
  - [ ] `TrackedAnimatedModel : NetworkBehavior`
- [ ] `PredictedKinematicController.h` ⤴ needs `Physics`
  - [ ] `PredictedKinematicController : BaseFeedbackBehavior<PredictedKinematicControllerFrame>`
- [ ] `ReplicationManager.h`
  - [ ] `NetworkObjectRegistry : ReferencedComponentRegistryBase`
  - [ ] `ReplicationManager : NetworkObjectRegistry`
- [ ] `ClientReplica.h`
  - [ ] `ClientReplicaClock : Object`
  - [ ] `ClientReplica : ClientReplicaClock`
- [ ] `ServerReplicator.h`
  - [ ] `ServerReplicator : Object`

## B10. IK `OPTIONAL` — inverse kinematics; depends on Scene, Math/InverseKinematics

- [ ] `IK.h`
  - [ ] registration entry point
- [ ] `IKEvents.h`
  - [ ] IK event definitions
- [ ] `AllSolvers.h`
  - [ ] solver aggregation header
- [ ] `IKSolverComponent.h`
  - [ ] `IKSolverComponent : Component`
- [ ] `IKIdentitySolver.h`
  - [ ] `IKIdentitySolver : IKSolverComponent`
- [ ] `IKLimbSolver.h`
  - [ ] `IKLimbSolver : IKSolverComponent`
- [ ] `IKLegSolver.h`
  - [ ] `IKLegSolver : IKSolverComponent`
- [ ] `IKArmSolver.h`
  - [ ] `IKArmSolver : IKSolverComponent`
- [ ] `IKChainSolver.h`
  - [ ] `IKChainSolver : IKSolverComponent`
- [ ] `IKHeadSolver.h`
  - [ ] `IKHeadSolver : IKSolverComponent`
- [ ] `IKSpineSolver.h`
  - [ ] `IKSpineSolver : IKSolverComponent`
- [ ] `IKRotateTo.h`
  - [ ] `IKRotateTo : IKSolverComponent`
- [ ] `IKStickTargets.h`
  - [ ] `IKStickTargets : IKSolverComponent`
- [ ] `IKSolver.h`
  - [ ] `IKSolver : LogicComponent`

## B11. Particles `OPTIONAL` — particle graph system; depends on Scene, Graphics

- [ ] `Helpers.h`
  - [ ] particle helper free functions
- [ ] `Span.h`
  - [ ] `Span` view type
- [ ] `SpanVariants.h`
  - [ ] span variant types
- [ ] `UpdateContext.h`
  - [ ] `UpdateContext`
- [ ] `ParticleGraphMemory.h`
  - [ ] particle graph memory pool
- [ ] `ParticleGraphPin.h`
  - [ ] `ParticleGraphPin`
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

- [ ] `Helpers.h`
  - [ ] glow helper free functions
- [-] `EmbreeForward.h` — **EXCLUDED** · reason: Embree forward decls · replaced-by: generated Embree interop
- [ ] `LightmapUVGenerator.h`
  - [ ] `struct LightmapUVGenerationSettings`
- [ ] `RaytracerScene.h`
  - [ ] `RaytracerScene : RefCounted`
- [ ] `BakedLight.h`
  - [ ] baked light structs
- [ ] `BakedLightCache.h`
  - [ ] `BakedLightCache`
  - [ ] `BakedLightMemoryCache : BakedLightCache`
- [ ] `BakedSceneBackground.h`
  - [ ] baked scene background structs
- [ ] `BakedSceneChunk.h`
  - [ ] baked scene chunk structs
- [ ] `BakedSceneCollector.h`
  - [ ] `BakedSceneCollector`
  - [ ] `DefaultBakedSceneCollector : BakedSceneCollector`
- [ ] `StaticModelForLightmap.h`
  - [ ] `StaticModelForLightmap : Drawable`
- [ ] `LightmapCharter.h`
  - [ ] chart layout helpers
- [ ] `LightmapGeometryBuffer.h`
  - [ ] geometry buffer structs
- [ ] `LightmapStitcher.h`
  - [ ] seam stitching helpers
- [ ] `LightmapFilter.h`
  - [ ] lightmap filtering helpers
- [ ] `LightTracer.h`
  - [ ] light tracing helpers
- [ ] `IncrementalLightBaker.h`
  - [ ] `IncrementalLightBaker`

## B13. RmlUI `OPTIONAL` — RmlUi HTML/CSS UI; depends on UI, Graphics

- [ ] `RmlSystem.h`
  - [ ] `RmlSystem : RefCounted, Rml::SystemInterface`
- [ ] `RmlFile.h`
  - [ ] `RmlFile : Rml::FileInterface`
- [ ] `RmlRenderer.h`
  - [ ] `RmlRenderer : Object, Rml::RenderInterface`
- [ ] `RmlEventListeners.h`
  - [ ] `PipeEventListener : Rml::EventListener, NonCopyable`
  - [ ] `SoundEventListener : Rml::EventListener, NonCopyable`
  - [ ] `CustomEventListener : Rml::EventListener, NonCopyable`
- [ ] `RmlUI.h`
  - [ ] `RmlUI : Object`
- [ ] `RmlUIComponent.h`
  - [ ] `RmlUIComponent : LogicComponent`
- [ ] `RmlCanvasComponent.h`
  - [ ] `RmlCanvasComponent : LogicComponent`
- [ ] `RmlSerializableInspector.h`
  - [ ] `RmlSerializableInspector : RmlUIComponent`

## B14. SystemUI `OPTIONAL` — Dear ImGui editor/debug; depends on Graphics, Input

- [ ] `SystemUIEvents.h`
  - [ ] system-UI event definitions
- [ ] `ImGui.h`
  - [ ] ImGui integration helpers
- [ ] `Widgets.h`
  - [ ] shared widget free functions
- [ ] `ImGuiDiligentRendererEx.h`
  - [ ] `ImGuiDiligentRendererEx`
- [ ] `SystemUI.h`
  - [ ] `SystemUI : Object`
- [ ] `Console.h`
  - [ ] `Console : Object`
- [ ] `DebugHud.h`
  - [ ] `DebugHud : Object`
- [ ] `SystemMessageBox.h`
  - [ ] `SystemMessageBox : Object`
- [ ] `BaseWidget.h`
  - [ ] `BaseWidget : Object`
- [ ] `ResourceInspectorWidget.h`
  - [ ] `ResourceInspectorWidget : BaseWidget`
- [ ] `SceneWidget.h`
  - [ ] `SceneWidget : BaseWidget`
- [ ] `Texture2DWidget.h`
  - [ ] `Texture2DWidget : BaseWidget`
- [ ] `TransformGizmo.h`
  - [ ] `TransformGizmo`
  - [ ] `TransformNodesGizmo`
- [ ] `DragDropPayload.h`
  - [ ] `DragDropPayload : RefCounted`
  - [ ] `struct ResourceFileDescriptor`
  - [ ] `ResourceDragDropPayload : DragDropPayload`
  - [ ] `NodeComponentDragDropPayload : DragDropPayload`
- [ ] `SerializableInspectorWidget.h`
  - [ ] `SerializableInspectorWidget : Object`
- [ ] `NodeInspectorWidget.h`
  - [ ] `NodeInspectorWidget : Object`
- [ ] `SceneHierarchyWidget.h`
  - [ ] `SceneHierarchyWidget : Object`
- [ ] `MaterialInspectorWidget.h`
  - [ ] `MaterialInspectorWidget : Object`
- [ ] `AnimationInspectorWidget.h`
  - [ ] `AnimationInspectorWidget : ResourceInspectorWidget`
- [ ] `ModelInspectorWidget.h`
  - [ ] `ModelInspectorWidget : ResourceInspectorWidget`
- [ ] `Texture2DInspectorWidget.h`
  - [ ] `Texture2DInspectorWidget : ResourceInspectorWidget`
- [ ] `TextureCubeInspectorWidget.h`
  - [ ] `SphericalHarmonicsGenerator : Object`
  - [ ] `TextureCubeInspectorWidget : ResourceInspectorWidget`
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

## B16. Script `OPTIONAL` — managed scripting host; depends on Core

- [x] `Script.h`

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
- [ ] `AssetTransformer.h`
  - [ ] `struct AssetTransformerInput`
  - [ ] `struct AssetTransformerOutput`
  - [ ] `struct AssetTransformerDependency`
  - [ ] `AssetTransformer : Serializable`
- [ ] `AssetTransformerHierarchy.h`
  - [ ] `AssetTransformerHierarchy : Object`
- [ ] `BaseAssetPostTransformer.h`
  - [ ] `BaseAssetPostTransformer : AssetTransformer`
- [ ] `CalculateAnimationVelocityTransformer.h`
  - [ ] `CalculateAnimationVelocityTransformer : BaseAssetPostTransformer`
- [ ] `GenerateWorldSpaceTracksTransformer.h`
  - [ ] `GenerateWorldSpaceTracksTransformer : BaseAssetPostTransformer`
- [ ] `RetargetAnimationsTransformer.h`
  - [ ] `RetargetAnimationsTransformer : BaseAssetPostTransformer`
- [ ] `AnimationMetadata.h`
  - [ ] animation metadata helpers
- [ ] `AssetPipeline.h`
  - [ ] `AssetPipeline : Resource`
- [ ] `FileSystemReflection.h`
  - [ ] `struct FileSystemEntry`
  - [ ] `FileSystemReflection : Object`
- [ ] `PackedSceneData.h`
  - [ ] `PackedNodeData`
  - [ ] `PackedComponentData`
  - [ ] `PackedNodeComponentData`
  - [ ] `PackedSceneData`
- [ ] `SceneSelection.h`
  - [ ] `struct PackedSceneSelection`
  - [ ] `SceneSelection`
- [ ] `GLTFImporter.h`
  - [ ] `struct GLTFImporterSettings`
  - [ ] `GLTFImporterCallback`
  - [ ] `GLTFImporter : Object`
- [ ] `SceneRendererToTexture.h`
  - [ ] `CustomBackbufferTexture : Object`
  - [ ] `SceneRendererToTexture : CustomBackbufferTexture`
- [ ] `SceneViewerApplication.h` ⤴ needs `Plugins`
  - [ ] `SceneViewerApplication : MainPluginApplication`
