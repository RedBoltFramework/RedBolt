# RedBolt Port Checklist — `Source/Urho3D`

## Samples

- [~] WebAssembly host — dedicated CMake/Emscripten preset, generated native bindings, full managed engine publish, pthread-capable WebGL 2 host, virtual filesystem staging, and a framework-free C#-to-C++ rendering prototype build successfully; browser rendering awaits user verification
- [x] `01_HelloWorld` — extensible native class/method order, explicit construction, base startup, empty update semantics, reverse deterministic UI-replacement teardown, and Hello World text mapped to Dear ImGui without the non-native 3D scene
- [x] `02_HelloGUI` — extensible native class/method order, POD integer drag state and placement, reverse deterministic UI-replacement teardown, and control, title, close, and draggable-position demonstrations mapped to Dear ImGui widgets without the non-native 3D scene
- [x] `04_StaticScene` — extensible native class/method order, explicit construction and base startup, strong scene/camera ownership, renderer-retained viewport aliases, reverse deterministic teardown, POD transforms and `int` object count/index mapping, plane, directional light, randomized mushroom field, and Dear ImGui instructions reconciled to rbfx
- [x] `05_AnimatingScene` — extensible sample class shape, native method/order structure, standalone `Rotator` factory registration matching rbfx `SamplesManager`, strong scene/camera/viewport ownership, reverse deterministic teardown, scene, zone, static-model, node-logic animation, camera, point-light, viewport, Dear ImGui instructions, and shared `Rotator` POD value semantics reconciled and runtime-verified
- [x] `09_MultipleViewports` — extensible native class/method/field order, explicit construction, angle-axis rear-camera rotation, strong scene/camera ownership, renderer-retained viewport aliases, reverse deterministic teardown, scene, rear camera, dual viewports, automatic render pipeline allocation, render-target viewport handling, Dear ImGui instructions, requested window sizing, delayed multi-frame screenshots, and stable Vulkan shadows are runtime-verified against rbfx
- [x] `34_DynamicGeometry` — extensible sample class shape, native method/field order, deep model clones, retained animating-buffer aliases, POD vertex/index copies, guarded buffer mapping, strong scene/camera/viewport ownership, reverse deterministic teardown, from-scratch pyramid geometry, animation, camera, zone lighting, automatic render pipeline, and Dear ImGui instructions reconciled to rbfx
- [x] `10_RenderToTexture` — extensible native class/method/field order, explicit construction and base startup, standalone `Rotator` factory registration matching rbfx `SamplesManager`, two-scene render-to-texture composition, queued RenderSurface viewport, strong scene/camera/texture/material ownership, reverse deterministic teardown, POD value copies and `int` indices/sizes, Dear ImGui instructions, native texture-descriptor defaults, copied texture-view descriptors, render-surface scheduling, resolved shader-resource sampling, and shared `Rotator` semantics are reconciled and runtime-verified
- [x] `14_SoundEffects` — hidden scene and music-source ownership, three sound-effect resources, looping music playback and stop, scene pause/resume and time scale, sound/music master gains, pan, reach, LFE, no-device microphone handling, seeded recorded-stream playback, Dear ImGui controls, baseline/audit screenshots, and deterministic teardown are runtime-verified
- [x] `54_WindowSettingsDemo` — extensible native class/method order, explicit construction and base startup, screen-mode and scene-update event lifetimes, world-space box rotation, strong scene/camera ownership, renderer-retained viewport aliases, reverse deterministic teardown, POD fullscreen-mode values, `int` indices/sizes, native fullscreen/DPI/refresh-rate/MSAA Apply semantics, and Dear ImGui controls reconciled and runtime-verified
- [ ] `03_Sprites` — skipped per user direction; it depends on the 2D submodule
- [x] `06_SkeletalAnimation` — sample, constructor-time `Mover3D` factory registration, animated models, controller playback, scene, lighting, camera, skeleton and bounds debug geometry, and Dear ImGui instructions are ported and runtime-verified
- [x] `07_Billboards` — scene population, grouped shadow casters, animated billboard sets, rotating shadow-casting spotlights, camera, debug geometry, and Dear ImGui instructions are ported and runtime-verified
- [x] `08_Decals` — scene population, occluders, per-viewport normalized mouse updates at input end, cursor raycast, decal creation and buffer upload, camera, debug geometry, and Dear ImGui instructions are ported and runtime-verified
- [x] `20_HugeObjectCount` — 62,500-object scene, grouped and ungrouped modes, animation toggle, camera, and Dear ImGui instructions are ported; startup, live group rebuild, and steady-state rendering are runtime-verified after restoring shared reflection attributes and linear reverse teardown
- [x] `30_LightAnimation` — animated point light, scene population, camera, animated Dear ImGui text, and five-frame Dear ImGui texture animation are ported and runtime-verified
- [x] `31_MaterialAnimation` — material shader-parameter animation, scene population, camera, and Dear ImGui instructions are ported and runtime-verified
- [x] `29_SoundSynthesis` — sound-source node ownership, buffered mono stream playback, reusable zero-churn synthesis buffer, two-oscillator generation, low-pass filtering, Dear ImGui coefficient display, baseline rendering, both filter-control branches and clamp endpoints, and deterministic teardown are runtime-verified
- [x] `112_AggregatedInput` — direction aggregation, directional-pad aggregation, raw key/joystick/hat/disconnect/touch logging, synthesized key logging and repeats, and Dear ImGui visualization are ported and runtime-verified
- [~] `113_InputLogger` — dual viewports, all input-event subscriptions and decoded parameters, C++ identity/parameter/time-threshold event merging, viewport-relative mouse reporting, bounded logging, and Dear ImGui output are runtime-verified; the managed sample remains a standalone application because the plugin application API is not ported
- [x] `116_VirtualFileSystem` — mount-point enumeration, editable identifier query, file metadata, reverse lookup, editable scan path/filter/flags, recursive file scanning, non-recursive directory scanning, and Dear ImGui output are ported and runtime-verified
- [~] `117_PointerAdapter` — adapter lifecycle and synthesized pointer events, scene raycast, outline selection, and Dear ImGui instructions are ported and runtime-verified; the unported physics subsystem line is retained as a TODO
- [x] `118_CameraShake` — scene archive loading, trauma injection, camera shake update, and Dear ImGui controls are ported and runtime-verified
- [x] `121_CameraOperator` — scene archive loading, both camera tracks, track removal, orthographic projection, and Dear ImGui controls are ported and runtime-verified
- [~] `122_TextureFormats` — extensible native class/method order, explicit construction and base startup, software/hardware texture setup, comparison scene, camera, viewport, reverse deterministic teardown, Dear ImGui labels, overview/normal screenshots, Escape exit, and apitrace replay are runtime-verified; unsupported-format RGBA8 conversion warnings match rbfx `Texture::SetData`, while the excluded `Text3D` component body remains an exact `TODO: port`
- [x] Profile startup and remove the rbfx deviations responsible for the long `20_HugeObjectCount` load time: shared type-level attribute registration, registry-based component construction, and linear reverse scene/octree teardown now match rbfx
- [x] Restore the rbfx `Audio` constructor registration path for `Sound`, `SoundSource`, `SoundSource3D`, and `SoundListener`, including type-level base-attribute copying; runtime-verify every `14_SoundEffects` control path
- [x] Capture and resolve OpenGL warnings produced by `08_Decals`, `20_HugeObjectCount`, and the remaining sample audit
- [x] Profile steady-state GC allocations for every sample and remove hot-path GC churn that is absent from rbfx
- [ ] Remove the `EndFrame(image)` API deviation; subscribe to the EndFrame event and perform frame capture from the event handler as in rbfx
- [ ] Audit and port every rbfx `RegisterObject` body, including exact factory categories, attribute order/defaults/modes, removals, and type-level base-attribute copying
- [ ] Make unit tests headless by default so they never create short-lived desktop windows; permit a real window only for behavior that cannot be verified headlessly
- [ ] Remove the `ContextEventSubscription` API deviation and restore rbfx-aligned event subscription ownership and lifetime behavior

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
  headers, SharpCpp sources, and RedBolt generator sources; unchanged native builds skip generation, compilation, and
  linking; direct native CMake builds are rejected outside MSBuild; all 1520 tests pass; screenshot-on-exit retains the valid rendered
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

- [x] `Variant.h` / `Variant.cpp` — explicit CLR-safe union storage, deterministic owned-payload teardown, native archive type identifiers for managed math aliases, independent value copies and borrowed reference returns, owning container and direct-field assignment, replacement and destruction, and struct-event payload lifetime reconciled
- [x] `Attribute.h` — non-POD AttributeInfo value copies, pointer-like POD AttributeHandle class kind and borrowed alias copies, owned Variant/default/metadata cloning, replacement/removal/clear teardown, registration temporary lifetimes, retained ObjectReflection destruction, per-instance reflection-facade teardown, and approved managed `int` enum indices reconciled
- [x] `Context.h` / `Context.cpp` — extensible noncopyable class kind, independent owned global-Variant assignment, borrowed read-only global access, replacement/reset destruction, subsystem/event teardown, and inherited reflection-registry lifetime reconciled
- [x] `ObjectReflection.h` / `ObjectReflection.cpp` — noncopyable class kinds, independent attribute/metadata value copies, borrowed reflection APIs, signal-before-destruction removal lifetime, registry ownership, and Context base teardown reconciled
- [x] `Spline.h` / `Spline.cpp` — extensible copyable non-POD class kind, independent owned-Variant construction/copy/return semantics, replacement/removal/clear destruction, allocation-free temporary lifetimes, and embedded SplinePath teardown reconciled
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
- [~] `Exception.h` — native `ArchiveException` type and archive exception boundaries reconciled using the standard managed exception hierarchy; remaining engine exception types use C# equivalents
- [-] `StringUtils.h` — **EXCLUDED** · reason: string parsing and formatting utilities · replaced-by: .NET string APIs
- [-] `Signal.h` - **EXCLUDED** · reason: use C# events

## [x] A4. IO — depends on Container, Core

- [-] `FileSystem.h` — **EXCLUDED** · reason: use standard library functionality
- [-] `MacFileWatcher.h` — **EXCLUDED** · reason: macOS FSEvents backend · replaced-by: `FileSystemWatcher`
- [-] `RWOpsWrapper.h` — **EXCLUDED** · reason: SDL_RWops C++ adapter · replaced-by: generated SDL interop

## [x] A5. Resource — depends on IO, Core


## [~] A6. Scene — depends on Resource, Core

- [x] `ObjectAnimation.h` / `ObjectAnimation.cpp` — extensible resource shape, owned animation-info replacement/removal/teardown, shared animation aliases and owner transitions, XML/JSON temporary lifetimes, typed add/remove events, and automatic map destruction reconciled
- [x] `ValueAnimation.h` / `ValueAnimation.cpp` and `ValueAnimationInfo.h` / `ValueAnimationInfo.cpp` — extensible class shapes, non-POD key/event value structs, independent Variant and VariantMap copies, owned key/event/tangent teardown, exact interpolation and event traversal, copy-construction time reset, weak target observation, deterministic applied-value lifetime, and derived attribute/shader-parameter animation copies reconciled
- [x] `PrefabReference.h` / `PrefabReference.cpp` — extensible noncopyable component lifetime, prefab value copies, instance teardown, and sender-specific struct prefab-reload observation reconciled
- [x] `SceneResolver.h` / `SceneResolver.cpp` — extensible noncopyable class shape, weak node/component ownership, owned attribute-value temporaries, ID remapping control flow, default destruction, stack-local lifetimes, and scene-owned teardown reconciled
- [x] `ShakeComponent.h` / `ShakeComponent.cpp` — extensible noncopyable component shape, construction and default destruction, reflection category and attribute registration, native property accessors and falloff-getter quirk, exact trauma/update flow, external transform reconciliation, Perlin-noise ownership, and automatic field lifetime reconciled
- [x] `SplinePath.h` / `SplinePath.cpp` — extensible noncopyable component shape, native field/member order, weak control/controlled node observation, listener registration and teardown, owning VariantVector attributes, spline-knot value copies, exact attribute resolution, movement, enabled/dirty callbacks, 1,000-step length calculation, and reverse-order destruction reconciled
- [x] `TrackedComponent.h` / `TrackedComponent.cpp` — complete base/registry/template class shapes, POD reference-entry value storage, borrowed component and registry aliases, stable reference packing/version reuse, swap-erase ordering, property accessors, approved managed `int` indices/sizes, scene reconnection, and reverse-order container teardown reconciled
- [x] `WorldOrigin.h` / `WorldOrigin.cpp` — extensible noncopyable logic-component shape, construction and default destruction, reflection registration, native attribute state and accessors, selected-axis distance and snapped-offset calculation, delayed weak scene/component rebasing, epsilon movement validation, diagnostics, and automatic field lifetime reconciled
- [x] `UnknownComponent.h` / `UnknownComponent.cpp` — extensible fieldless noncopyable component shape, empty construction and implicit destruction, uncategorized reflection registration, input-ignoring binary/XML/JSON loads, temporary-state event behavior, placeholder creation and prefab type replacement, and automatic lifetime reconciled
- [x] `Serializable.h` / `Serializable.cpp` — concrete class shape, native state ordering, context-owned shared type attributes with local fallback for unregistered managed types, lazy owned instance-default variants, default reset, hash-matched deep attribute copy, factory-based nullable cloning, deterministic variant teardown, native load/apply timing and diagnostics, archive value lifetimes, and approved `int` attribute indices/sizes reconciled
- [x] `LogicComponent.h` / `LogicComponent.cpp` — complete concrete noncopyable class shape, native `UpdateEvent` flag kind and values, event-mask state, typed virtual update-event selection, direct sender-filtered Scene subscriptions, delayed-start/update/post-update/world-origin dispatch, enable and attachment lifecycle, and deterministic unsubscription reconciled; optional physics step branches are compile-time excluded in the current configuration
- [x] `SceneEvents.h` — all native event payloads mapped to value structs under the approved typed-event deviation, declaration and field order, native parameter names, reference aliasing, copied immutable strings, integer progress values, world-origin values, update-event dispatch helper, and automatic payload lifetime reconciled
- [x] `Node.h` / `Node.cpp` — complete class shape, registry-based component construction, lifetime/copy behavior, prefab and clone paths, scene attachment, transforms, tags/variables, listener cleanup, iterator-equivalent linear reverse child teardown, `SceneLookupFlag`/`TransformSpace`/`WorldOriginUpdateMode` value kinds, and int-mapped indices/sizes reconciled
- [x] `SceneResource.h` / `SceneResource.cpp` — extensible noncopyable resource shape, owned scene and staged format-resource lifetimes, native begin/end load split, reload signals, source-name propagation, JSON/XML/binary and legacy load paths, direct destination saves, prefab-resource export, save-format selection, file output, and reverse-order deterministic teardown reconciled
- [x] `Scene.h` / `Scene.cpp` — native root-`Node` inheritance and identity, extensibility, self registration and ID allocation, reflected scene-only attributes, `AsyncProgress` POD value shape with approved `int` indices and counts, root serialization/loading call paths, deterministic component/child teardown, surviving-node scene reset, source-file aliasing, and archive exception boundaries reconciled

## [~] A7. RenderAPI — depends on Core, Math (current effort)

- [x] `ConstantBufferCollection.h` — extensible collection class, mutable 12-byte POD block reference, native field/default order, reusable owned buffer storage, exact block allocation and parameter copies, full GPU-buffer upload range, approved managed `int` indices/sizes, and automatic lifetime reconciled
- [x] `DeviceObject.h` / `DeviceObject.cpp` — complete concrete class shape, construction/registration, deterministic teardown, device-event state transitions, protected state, debug name, dependency restore, implicit copy semantics, and affected shutdown/reload call paths reconciled
- [x] `DrawCommandDescription` in `DrawCommandQueue.h` — POD class kind, exact field/default order, inline fixed arrays, implicit value copies, pointer alias identity, approved managed `int` indices/sizes, queue insertion snapshots, and automatic lifetime reconciled
- [x] `DrawCommandQueue.h` / `DrawCommandQueue.cpp` — extensible noncopyable reference class, exact nested/member order, constructor/reset behavior, command accumulation, shader parameter/resource diagnostics and ranges, reusable temporary storage, complete execution/state caching, automatic lifetime, and affected buffer setup/RenderDevice ownership reconciled
- [x] `PipelineState.h` / `PipelineState.cpp` — non-POD reference class and multiple-base mapping, explicit destructor/ID teardown, cache release and reload subscriptions, graphics/compute GPU creation, reflection, input layout, immutable samplers, render-state mapping, OpenGL temporary lifetime, Diligent resource ownership, approved managed `int` indices/sizes, and affected invalid-test construction paths reconciled
- [x] `PipelineStateCache` in `PipelineState.h` / `PipelineState.cpp` — extensible noncopyable reference class, constructor/member order, cache byte copies, supported-backend GPU creation, Diligent handle replacement and blob RAII, descriptor-keyed weak state reuse, owner weak-reference expiration, implicit member destruction, approved managed `int` sizes, and exact diagnostics reconciled
- [x] `RawBuffer.h` / `RawBuffer.cpp` — extensible noncopyable reference class, `RawBufferParams` POD value shape, constructor/member order, explicit destructor, device invalidation/restoration/destruction, CPU shadow ownership, mapped-buffer lifetime, exact upload/resolve flow, Diligent handle ownership, diagnostics, and approved managed `int` indices/sizes reconciled
- [x] `RawShader.h` / `RawShader.cpp` — extensible noncopyable reference class, constructor/member order, explicit destructor, device lifecycle, backend-specific Diligent creation, shader-bytecode copy/move boundary, reload signal, handle ownership, diagnostics, and SharedPtr identity semantics reconciled
- [x] `RenderScope.h` / `RenderScope.cpp` — extensible noncopyable RAII shape, disabled-scope construction, typed deferred formatting, balanced debug-group teardown, OpenGL error consumption, borrowed render-context lifetime, and allocation-free disabled hot path reconciled; generated Diligent nullable color-pointer support remains an exact `TODO: port`
- [x] `ShaderBytecode.h` / `ShaderBytecode.cpp` — non-POD value-struct kind, independent implicit copies, native archive version/root/vector/item format, file save/load/reset behavior, deterministic temporary lifetime, and approved managed `int` vertex-attribute indices reconciled
- [x] `RawTexture.h` / `RawTexture.cpp`, `RenderAPIDefs.h`, and `RenderAPIUtils.h` / `RenderAPIUtils.cpp` — complete declarations and bodies, native POD/class kinds, field/default/member order, inline fixed-array value copies, graphics/compute descriptor mutation by reference, approved `int` index/size mapping, unified Diligent texture format including sRGB variants, exact format metadata and serialization, native handle aggregate ownership and teardown, parameter validation, GPU creation/view ownership, independently retained Texture render-surface handle copies, external-handle behavior, update/read block-row logic, diagnostics/comments, and unsupported-platform assertions reconciled
- [x] `RenderContext.h` / `RenderContext.cpp` — complete noncopyable class shape, construction after immediate-context creation, empty destructor and automatic field lifetime, render-target/view borrowing, cached output and viewport state, statistics, command execution, OpenGL clip-plane state, device-loss cache reset, diagnostics, and approved managed `int` indices reconciled
- [x] `RenderDevice.h` / `RenderDevice.cpp` — constructor state and ordering, SDL window and Diligent handle ownership, cached capabilities, device loss/restoration, presentation, value copies, and reverse member destruction are reconciled; managed bootstrap still creates native objects before transferring their destruction responsibility to `RenderDevice`

## [x] A8. Shader — depends on Core, IO (feeds Graphics)

## [~] A9. Graphics — depends on RenderAPI, Scene, Resource, Shader

- [x] `AnimatedModel.h` / `AnimatedModel.cpp` — extensible noncopyable class kind, complete member/body order, constructor and explicit destructor, weak animation-state ownership, independent morph and bone-mapping copies, approved managed `int` indices/sizes, master/sibling attachment ordering, animation/morph/skinning updates, LOD calculations, sender-specific struct reload handling, and last-model bone-hierarchy destruction reconciled
- [x] `Animation.h` / `Animation.cpp` — extensible reference-class and trigger value-struct kinds, complete member/body order, constructor/factory, owned trigger/track teardown, independent trigger/transform/variant-track copies, approved managed `int` indices/sizes, source-selected XML load, cached sidecar dependency load, direct binary serialization, destination-selected sidecar save, and borrowed trigger pointers reconciled
- [x] `AnimationController.h` / `AnimationController.cpp` — complete declaration and method bodies, constructor and controller-owned reverse-order teardown, animation-instance and parameter value copies, deferred state synchronization, model/bone track connection, renderer statistics, attribute subspan serialization, borrowed trigger resolution, controller-owned Variant boundaries, native warning paths, and approved managed `int` indices/sizes reconciled
- [x] `AnimationParameters` in `AnimationController.h` / `AnimationController.cpp` — all constructors, implicit field teardown, independent wrapped-time copies, borrowed animation aliases, fluent helpers, delayed removal, and variant/binary serialization reconciled
- [x] `BillboardSet.h` / `BillboardSet.cpp` — constructor defaults, billboard value storage and mutable references, effective shared Geometry/buffer teardown order, and non-copyable component semantics reconciled; dependent VertexBuffer ownership is now closed by its completed audit
- [x] `Camera.h` / `Camera.cpp` — non-copyable component semantics, value-owned projection/view/frustum caches, weak Graphics observation, borrowed Zone ownership, and default derived destruction reconciled
- [x] `CameraOperator.h` / `CameraOperator.cpp` — weak tracked-node ownership, value-owned serialization and point caches, listener registration/removal, and default derived destruction reconciled
- [x] `CubemapRenderer.h` / `CubemapRenderer.cpp` — weak scene and texture observations, independent parameter value copies, shared pipeline-state aliases, non-copyable object semantics, and managed temporary-camera teardown reconciled
- [x] `CustomGeometry.h` / `CustomGeometry.cpp` — vertex value semantics, owned geometry/buffer teardown, borrowed batch materials, cached material attributes, mapped commit including VertexBuffer data-loss completion, and ray queries reconciled
- [x] `DebugLine` / `DebugTriangle` in `DebugRenderer.h` — zero-default construction, field order, independent value copies, trivial destruction, and DebugRenderer value-list storage verified
- [x] `DebugRenderer.h` / `DebugRenderer.cpp` — weak camera observation, independent frustum value copy, owned vertex-buffer and pipeline-cache teardown, non-copyable component semantics, and value-owned debug primitives reconciled
- [x] `DecalSet.h` / `DecalSet.cpp` — decal and vertex value-copy semantics, owned geometry and buffer teardown, cloned bone values with borrowed weak node listeners, skinned face extraction and bone remapping, and shared tangent generation reconciled
- [x] `Drawable.h` / `Drawable.cpp` — non-copyable base semantics, SourceBatch shallow value copies, independent cached reflection values, weak borrowed zone/camera observations, queued-update cancellation, deterministic batch release, and abstract bounding-box contract reconciled
- [x] `DrawableEvents.h` — all five struct event payloads preserve declaration order and borrowed object pointers; ParticleEffect typing plus independent Variant trigger-data assignment and post-dispatch teardown reconciled
- [x] `Geometry.h` / `Geometry.cpp` — retained stream/dependency resize semantics, shared buffer and raw-array ownership, deterministic subscription and ID teardown, allocation-free shadow-data access, and non-indexed ray fallback reconciled
- [x] `Material.h` / `Material.cpp` — resource inheritance, technique/parameter/texture value ownership, shader-parameter animation copy construction and deterministic replacement/removal, pipeline-state tracking, sender-specific struct texture reload observation, exact cloning/copying, and reverse-order teardown reconciled
- [x] `Model.h` / `Model.cpp` — POD/descriptor value copies, packed morph-data aliasing, deep clone behavior, synchronous and staged loading, UMD3 saving, metadata sidecars, geometry/range shape, and default shared-reference teardown reconciled
- [x] `ModelView.h` / `ModelView.cpp` — container-backed value copies, metadata ownership/destruction, exact clear semantics, import/export staging, headless buffers, morph splitting, geometry transforms, approved managed `int` bone indices, and bone processing reconciled
- [x] `OcclusionBuffer.h` / `OcclusionBuffer.cpp` — construction defaults, reverse-order owned-buffer teardown, shared thread-buffer storage, borrowed batch data, value-copy boundaries, and logical buffer view reconciled
- [x] `Octree.h` / `Octree.cpp` — owned child-octant destruction and root relocation, recursive whole-tree detachment, borrowed drawable/zone aliases, delayed transform values, threaded updates, recursive queries, and deterministic reverse-order teardown reconciled
- [x] `OctreeQuery.h` / `OctreeQuery.cpp` — query-object class kinds, POD result structs, independent value copies, borrowed result/pointer aliases, and builtin ray-result storage ownership reconciled
- [x] `OutlineGroup.h` / `OutlineGroup.cpp` — component class kind, POD material-cache keys, independent VariantVector assignment, borrowed drawable/texture aliases, owned artificial materials, and reverse-order teardown reconciled
- [x] `ParticleEffect.h` / `ParticleEffect.cpp` — reference resource class, POD color/texture frame structs and defaults, independent frame-vector assignment and cloning, borrowed material aliasing, XML temporary lifetimes, and reverse-order teardown reconciled
- [x] `ParticleEmitter.h` / `ParticleEmitter.cpp` — non-copyable reference component, POD particle value type and zero initialization, owned particle-vector teardown, shared effect aliasing and sender-specific struct reload observation, exact attributes, emission defaults, and event ordering reconciled
- [x] `PipelineStateTracker.h` / `PipelineStateTracker.cpp` — class kinds, RAII subscription clone/copy/move assignment, value subscriber-count records, borrowed tracker aliases, atomic cache invalidation, dependency temporary lifetimes, and deterministic teardown reconciled
- [x] `ReflectionProbe.h` / `ReflectionProbe.cpp` — reference component/manager classes, POD BVH/queue/cache records, independent internal cache copies with borrowed probe-data aliases, exact spatial queries and setters, reflected registration, device-reset restoration, and reverse-order cubemap teardown reconciled
- [x] `ReflectionProbeData.h` — POD data/reference aggregates, public field order, independent value copies, stable borrowed data-pointer identity, reset behavior, zone/probe storage, light accumulation, render-state caching, and probe/zone blend call path reconciled
- [x] `RenderSurface.h` / `RenderSurface.cpp` — noncopyable reference-class kind, borrowed/shared managed aliases, retained native texture-view ownership, atomic update state, surface recreation identity, and reverse-order Texture-owned teardown reconciled
- [x] `Renderer.h` / `Renderer.cpp` — noncopyable subsystem kind, POD statistics/frame-data value semantics, borrowed renderer relationships, reverse-order owned-resource teardown, and viewport/update/render ordering reconciled
- [x] `Shader.h` / `Shader.cpp` — noncopyable resource kind, shared variation alias identity, dependency reset, distinct owned-variation teardown, and reload-signal lifetime reconciled
- [x] `ShaderCompiler.h` / `ShaderCompiler.cpp` — namespace-function shape, borrowed source lifetime, independent output values, deterministic native support-wrapper teardown, and platform/compiler flag behavior verified
- [x] `ShaderDefs.h` — value-struct class kinds, independent owned-vector copies through `ICloneable`, enum values, constructors, define operations, and shader validity semantics reconciled
- [x] `ShaderOptimizer.h` / `ShaderOptimizer.cpp` — namespace-function shape, reference aliasing, optimized-bytecode move replacement, failure preservation, diagnostics, and deterministic native support-wrapper teardown verified
- [~] `ShaderSourceLogger.h` / `ShaderSourceLogger.cpp` — namespace-function shape, borrowed arguments, and exception-safe local-file teardown reconciled; Graphics settings access remains `TODO: port` until that settings API exists
- [~] `ShaderTranslator.h` / `ShaderTranslator.cpp` — TargetShader value-copy semantics, borrowed const-reference parameters, independent outputs, and deterministic native/local teardown reconciled; `RemappingCompilerHLSL` remains `TODO: port` until generated bindings support native subclassing
- [~] `ShaderVariation.h` / `ShaderVariation.cpp` — non-POD reference class kind, constructor/event order, borrowed cache identifiers, independent compiled values, Web cache guard, and inherited GPU-handle teardown reconciled; Graphics settings access remains `TODO: port` until that API exists
- [x] `Skeleton.h` / `Skeleton.cpp` — class kinds, non-POD Bone identity, independent Bone/Skeleton value copies, Define node-alias clearing, Model assignment/clone semantics, approved managed `int` bone indices and counts with unsigned wire serialization, hierarchy ordering, and deterministic owned-Skeleton teardown reconciled
- [x] `Skybox.h` / `Skybox.cpp` — extensible noncopyable class kind, borrowed camera identity, stable per-camera transform aliases, insert-first frame semantics, reflected defaults, and deterministic map teardown reconciled
- [x] `SoftwareModelAnimator.h` / `SoftwareModelAnimator.cpp` — aggregate animation-data value type, independent owned-array copies, noncopyable animator identity, borrowed published collections, reduced-bone ordering, and deterministic cloned-resource teardown reconciled
- [x] `StaticModel.h` / `StaticModel.cpp` — POD geometry-data value type, extensible noncopyable model identity, stable borrowed transform alias, cached material attributes, sender-specific struct model-reload ownership, and deterministic reverse-order teardown reconciled
- [x] `StaticModelGroup.h` / `StaticModelGroup.cpp` — extensible noncopyable class kind, weak instance-node observations, VariantVector node-ID cache, contiguous transform aliases, ray/LOD behavior, listener invalidation, and deterministic teardown reconciled
- [x] `Tangent.h` / `Tangent.cpp` — namespace-function shape, borrowed raw-buffer spans, aliased temporary tangent halves, byte-width index reads, and in-place tangent generation verified
- [x] `Technique.h` / `Technique.cpp` — Pass/Technique extensible class kinds, noncopyable Pass semantics, exact state/hash/vector behavior, Technique deep-copy/cache ownership, XML RAII, sparse lookup, lazy pass indices, and deterministic teardown reconciled
- [x] `Terrain.h` / `Terrain.cpp` — extensible noncopyable class kind, weak patch/neighbor observations, shared index-buffer aliases, height-array ownership, nullable node paths, sender-specific struct heightmap reload observation, and allocation-free propagation reconciled
- [x] `TerrainPatch.h` / `TerrainPatch.cpp` — extensible noncopyable class kind, weak owner/neighbor observations, shared geometry/buffer aliases, inherited transform storage, and derived-before-base teardown reconciled
- [x] `Texture.h` / `Texture.cpp` — concrete extensible noncopyable class kind, public Resource-chain constructors, stable composed RawTexture identity, virtual GPU loss/restoration callbacks, independently retained render-surface handle copies, pre-GPU POD sampler state, exact sampler mutations/XML lookup, native mip defaults, shared backup alias release, exact effective RG8/compressed/sRGB/BGRX format behavior, final member order and dirty fields, approved `int` index/size mapping, image temporary lifetime, and derived-texture direct creation reconciled
- [x] `Texture2D.h` / `Texture2D.cpp` — extensible noncopyable class kind, exact member order, derived load-image ownership and reverse-order teardown, repeated-load replacement lifetime, native direct `RawTextureParams` creation and failure propagation, flags/multisample API, approved `int` mip indices, image temporary lifetime, comments, and complete method bodies reconciled
- [x] `Texture2DArray.h` / `Texture2DArray.cpp` — extensible noncopyable class kind, exact field and teardown order, dependency-loading ownership, native direct `RawTextureParams` creation and failure propagation, approved `int` layer/mip indices, native flags-only array sizing, direct raw update/read semantics, image RAII, comments, and complete method bodies reconciled
- [x] `Texture3D.h` / `Texture3D.cpp` — extensible noncopyable class kind, exact field and teardown order, dependency-loading ownership, native direct `RawTextureParams` creation and failure propagation, approved `int` mip indices, ignored-flags sizing, direct raw update/read semantics, image RAII, error logging, comments, and complete method bodies reconciled
- [x] `TextureCube.h` / `TextureCube.cpp` — extensible noncopyable class kind, exact field and teardown order, ImageCube dependency-loading ownership, native direct `RawTextureParams` creation and failure propagation, flags/multisample sizing, approved `int` mip indices, direct raw update/read semantics, image/readback RAII, comments, and complete method bodies reconciled
- [x] `RibbonTrail.h` / `RibbonTrail.cpp` — POD TrailPoint value semantics, noncopyable Drawable identity, nullable point-link mapping, deterministic owned-buffer teardown, and allocation-free index-buffer mapping reconciled
- [x] `VertexBuffer.h` / `VertexBuffer.cpp` — complete VertexBuffer and DynamicVertexBuffer class shapes, extensible noncopyable identity, RawBuffer/DeviceObject composition forwarding, owned buffer replacement/destruction, shadow-data ownership, typed element-mask state, dynamic staging ownership, and affected call paths reconciled
- [x] `Viewport.h` / `Viewport.cpp` — extensible noncopyable reference-class kind, weak scene/camera/eye/cull/pipeline observations, retained pipeline-view ownership, nullable pointer APIs, automatic derived-pipeline selection, and deterministic teardown reconciled
- [x] `Zone.h` / `Zone.cpp` — extensible noncopyable reference-class kind, POD reflection-probe mapping, retained texture and weak gradient-zone ownership, cache storage/copies, sender-specific struct texture reload observation, octree invalidation, and deterministic teardown reconciled
- [~] `GlobalIllumination.h` / `GlobalIllumination.cpp` — owned mesh/baked-data reset semantics, world-origin mutation, debug geometry, reflected attributes, nullable resource loading, binary-compatible GI serialization, and default destruction reconciled; the reload editor action remains `TODO: port` until ObjectReflection supports actions
- [x] `Graphics.h` / `Graphics.cpp` — construction-context and SDL ownership, device-before-window close/destruction, frame and resize events, cached shader observation, independent window-settings copies, and managed native COM/unmanaged-storage teardown reconciled
- [x] `RenderBackend` in `RenderAPIDefs.h` — duplicate `GraphicsBackend` removed; exact enum values are used directly and the automatic backend request preserves C++ optional-value semantics through nullable `RenderBackend`
- [x] `GraphicsDefs.h` / `GraphicsDefs.cpp` — enum values and aliases, `VertexElement` default/parameterized construction and independent value copies, immutable shader hashes/vector constant, and const legacy-element/type-size tables reconciled
- [x] `GraphicsEvents.h` — all 16 event payload shapes and value/borrowed-pointer semantics, render-surface/texture sender selection, default and stereo view event ordering, and frame-level versus per-view rendering boundaries reconciled
- [x] `GraphicsUtils.h` / `GraphicsUtils.cpp` — fixed-capacity vertex-buffer/input-layout temporaries, borrowed geometry and resource aliases, draw-queue binding, backup-texture propagation, and null-texture diagnostics reconciled
- [x] `Image::Save`, `Image::SavePNG`, and `Image::SaveBMP` in `Image.h` / `Image.cpp` — non-source `ImageFileWriter` removed; generated stb encoding, original component count, compressed/empty guards, and deterministic native PNG-buffer release reconciled (complete `Image` class audit remains pending)
- [x] `ImageDDS.h` / `ImageDDS.cpp` — all five DirectDraw/DX10 header aggregates are explicit-layout POD value structs with exact native sizes, union aliases, member order, and independent copies; FourCC construction, exact DXGI/FourCC/simple-format selection, component-mask matching, and exported visibility are reconciled
- [x] `Image.h` / `Image.cpp` — complete class shape, member order, construction, implicit reverse-order destruction, noncopyable semantics, uncompressed/DDS/KTX/PVR/WebP loading, native save paths and compression behavior, pixel/mip ownership, compressed-format handling, conversion, flips, resizing, subimages, SDL surfaces, and deterministic native-buffer/wrapper lifetimes reconciled; approved managed `int` indices and sizes retained
- [x] `ImageCube.h` / `ImageCube.cpp` — extensible class shape, factory construction, face-image and parameter ownership, layout loading, asynchronous precalculation, independent decompressed levels, exact cube projection and sampling, spherical-harmonics calculation/cache semantics, approved `int` mip indices, member order, comments, and deterministic teardown reconciled
- [x] `IndexBuffer.h` / `IndexBuffer.cpp` — stable composed RawBuffer identity, device flags, pending shadow state, raw shadow-data access, offset-correct pack/update flow, pipeline hash, dynamic growth/commit behavior, and deterministic owned-buffer teardown reconciled
- [~] `Light.h` / `Light.cpp` — parameter POD value copies, constructor defaults, shared texture ownership, setter validation, dirty propagation, resource attributes, and virtual ray queries reconciled; the borrowed `LightBatchQueue` API remains `TODO: port` until that dependency exists
- [~] `LightBaker.h` / `LightBaker.cpp` — non-GLOW construction, settings value semantics, update subscription, task-data ownership, future waiting/release, and disabled-build control flow reconciled; GLOW baking fields and the dynamic reflection action remain `TODO: port`
- [x] `LightBakingSettings.h` — all eleven settings aggregates, defaults, constructors, independent nested value copies, immutable string mappings, and trivial destruction verified
- [x] `LightProbeGroup.h` / `LightProbeGroup.cpp` — probe POD values, deep collection copies, reflected attributes, collection/arrangement/bounds, binary/base64 serialization, baked-data load/save, resource aliases, and default derived teardown reconciled

## [x] A10. RenderPipeline — depends on Graphics, RenderAPI, Scene


## [x] A11. Input — depends on Core, Graphics (window)


## [~] A12. Engine — top-level orchestration, depends on all Part-A above

- [x] `StateManagerEvents.h` — struct-event payload mapping reconciled
- [~] `StateManager.h` / `StateManager.cpp` — core application-state lifecycle, queue value copies, weak cache, transitions, property-form getters, `int` viewport indices/sizes, fog, mouse state, and engine registration reconciled; built-in UI is excluded, while compile-gated Actions members remain `TODO: port` until B1 exists

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

## [x] B2. Audio `OPTIONAL` — depends on Scene, Resource

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

- [x] `SystemUIEvents.h`
  - [x] system-UI event definitions
- [ ] `ImGui.h`
  - [ ] ImGui integration helpers
- [ ] `Widgets.h`
  - [ ] shared widget free functions
- [~] `ImGuiDiligentRendererEx.h`
  - [~] `ImGuiDiligentRendererEx` — native constructor/destructor, base renderer ownership, main-viewport frame/render path, render pipelines, class shape, sizes, and failure unwinding reconciled; secondary OS-window callbacks and implicit-copy cloning remain exact `TODO: port` items because SharpCpp excludes `ImGuiPlatformIO` function-pointer fields and does not expose native `ImGuiDiligentRenderer` base copy construction
- [~] `SystemUI.h`
  - [~] `SystemUI : Object` — class shape, lifecycle, event flow, relative mouse mode, style, rendering, and texture-reference lifetime reconciled as `DearImGui`; three font-loading overloads, per-screen font atlases/TexID assignment, monitor refresh, ImGuizmo frame start, and managed `io.UserData` remain explicit `TODO: port` items until generated bindings expose the required native APIs
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
