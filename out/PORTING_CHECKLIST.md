# RedBolt Port Checklist — `Source/Urho3D`

## Samples

- [x] `01_HelloWorld` — Hello World sample ported with Dear ImGui replacing the rbfx Text control
- [x] `02_HelloGUI` — control, title, close, and draggable-position demonstrations mapped to Dear ImGui widgets
- [x] `04_StaticScene` — plane, directional light, randomized mushroom field, camera/viewport lifecycle, and Dear ImGui instruction sample reconciled to rbfx
- [x] `05_AnimatingScene` — scene, zone, static-model, node-logic animation, camera, point-light, viewport, and Dear ImGui instruction sample ported
- [x] `09_MultipleViewports` — scene, rear camera, dual viewport sample, automatic render pipeline allocation, render-target viewport handling, and Dear ImGui instructions reconciled to rbfx
- [x] `34_DynamicGeometry` — cloned dynamic vertex buffers, from-scratch pyramid geometry, animation, camera, zone lighting, automatic render pipeline, and Dear ImGui instructions reconciled to rbfx
- [~] `10_RenderToTexture` — render-to-texture scene, queued RenderSurface viewport, and Dear ImGui instructions ported; runtime validation is limited by host SDL video-device availability
- [~] `54_WindowSettingsDemo` — window-settings scene and native fullscreen-mode, DPI, refresh-rate, and MSAA apply semantics ported with Dear ImGui; upstream comparison removed the extra C# `RenderPipeline` component and aligned settings reads with `RenderDevice`; runtime now starts successfully, but the 3D box remains absent in the screenshot and requires further engine-level investigation
- [ ] `03_Sprites` — skipped per user direction; it depends on the 2D submodule
- [~] `06_SkeletalAnimation` — AnimatedModel model/skeleton/morph ownership, threaded pose calculation and octree transform commits, component-owned cloned geometry, geometry bone mappings, hardware/software skin matrices, and SoftwareModelAnimator runtime morphing/skinning updates are ported; sample runtime validation remains
- [~] `07_Billboards` — `BillboardSet` drawable parity and unit tests are ported; sample runtime validation remains

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
- [x] `MultiVector.h` — vector-of-vectors operations ported; `IEnumerable<T>` supplies the C++ iterator surface and `Func<T>` supplies the variadic `EmplaceBack` factory adaptation
- [x] `ConstString.h` — immutable string/hash wrapper ported; the C++ global-constant macro is preprocessor-only and intentionally omitted

## [x] A2. Math — depends on Container

- [x] `Vector2.h` / `Vector3.h` / `Vector4.h` / `Quaternion.h` / `MathDefs.h` — vector constructors, constants, free helpers, and quaternion zero constant reconciled; C++ compound-assignment operators use C#'s built-in compound-assignment lowering
- [x] `SphericalHarmonics.h` — fixed-size coefficients, directional basis, color evaluation, packed dot representation, accumulation, scaling, and debug color match C++
- [x] `Ray.h` — object-level ray intersections and raw vertex/index buffer overloads are ported; C++ `void*` buffer surfaces map to `ReadOnlySpan<byte>`

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
- [x] `Timer.h` — `Time` frame-progress state, lifecycle disposal, and context reflection attribute access ported
- [x] `ProcessUtils.h` — console output and command-line argument parsing ported; platform-only dialog/FPU/console-input hooks remain pending
- [x] `Variant.h` — mutable reset, buffer/custom assignment, mutating string conversion, and hash API ported
- [x] `Serializable.h` — reflection access and binary/XML/JSON/auto-detected resource loading ported; prefab temporary-serialization overload remains pending
- [x] `ThreadSafeCache.h` — `Restore` is the managed equivalent of C++ assignment; no separate assignment operator is needed
- [x] `Object.h` event handler — old VariantMap handler TODOs removed; managed struct-based events are the accepted event-system deviation
  - [-] `EventReceiverGroup : RefCounted` — **EXCLUDED** · reason: named-event receiver grouping · replaced-by: allowed struct-event subscription buckets

## [x] A4. IO — depends on Container, Core

- [-] `FileSystem.h` — **EXCLUDED** · reason: use standard library functionality
- [-] `MacFileWatcher.h` — **EXCLUDED** · reason: macOS FSEvents backend · replaced-by: `FileSystemWatcher`
- [-] `RWOpsWrapper.h` — **EXCLUDED** · reason: SDL_RWops C++ adapter · replaced-by: generated SDL interop

## [x] A5. Resource — depends on IO, Core

## [x] A6. Scene — depends on Resource, Core


## A7. RenderAPI — depends on Core, Math (current effort)

- [x] `RenderAPIDefs.h`
  - [x] `struct FullscreenMode`
  - [x] `struct WindowSettings`
  - [x] `struct RenderDeviceSettingsVulkan`
  - [x] `struct RenderDeviceSettingsD3D12`
  - [x] `struct RenderDeviceSettings`
  - [x] `struct RenderDeviceCaps`
  - [x] `struct RenderDeviceStats`
  - [x] `struct VertexShaderAttribute`
  - [x] `struct SamplerStateDesc` — field equality and deterministic C++ tuple-combine `ToHash` are ported
  - [x] `struct PipelineStateOutputDesc`
  - [x] `struct InputLayoutElementDesc`
  - [x] `struct InputLayoutDesc`
  - [x] `struct ImmutableSamplersDesc`
- [~] `RenderAPIUtils.h`
  - [x] platform-default render backend selection and explicit-request fallback match C++
  - [~] RenderAPI free functions — backend/platform predicates, enum tables, shader-input parsing/name conversion, native shader-stage mapping, compile-derived supported-backend ordering and platform-default/requested backend selection, mip sizing, depth/stencil/color classification, managed texture mappings including Depth16/D32S8, managed Texture.SRGB integration, native sRGB mappings, direct VulkanDescriptorPoolSize serialization, and optional Vulkan/D3D12 render-setting serialization are ported; translator/optimizer support remains TODO because the managed APIs differ
- [-] `GAPIIncludes.h` — **EXCLUDED** · reason: Diligent header aggregation · replaced-by: generated Diligent bindings
- [x] `ShaderBytecode.h`
  - [x] `ShaderBytecode` — type, MIME, bytecode, vertex attributes, archive and compiled-file persistence, copying, equality, and version validation are ported
- [x] `DeviceObject.h`
  - [x] `DeviceObject` — device registration, flags, loss/restore/destroy dispatch, dependencies, debug names, and teardown are ported
- [x] `RawShader.h`
  - [x] `RawShader : Object, DeviceObject` — bytecode ownership, native creation, type validation, debug naming, and device loss/restore are ported
- [x] `ShaderProgramReflection.h`
  - [x] `ShaderProgramReflection : RefCounted` — uniform buffers/parameters, resources/UAVs, hashing, Diligent reflection, and binding connection are ported
- [x] `RawBuffer.h`
  - [x] `RawBuffer` — construction/access, shadow storage, native creation and restoration, dynamic/default/immutable updates, direct dynamic mapping, scratch mapping, resolve, and allocation-free hot paths are ported
- [x] `RawTexture.h`
  - [x] `struct RawTextureParams`
  - [x] `struct RawTextureUAVKey`
  - [x] `struct RawTextureHandles`
  - [x] `RawTexture : DeviceObject` — validation, native ownership, default and slice views, UAVs, mip generation, resolve, allocation-free updates, staging readback, device recovery hooks, handle wrapping, and generated platform-gated D3D11, D3D12, Vulkan, and OpenGL external imports are ported
- [x] `RenderTargetView.h`
- [x] `ConstantBufferCollection.h`
  - [x] `ConstantBufferCollection` — aligned block allocation, buffer rollover/sizing, parameter conversion, matrix/array storage, and CPU upload data are ported
- [~] `PipelineState.h`
  - [~] `struct GraphicsPipelineStateDesc`
  - [~] `struct ComputePipelineStateDesc`
  - [~] `PipelineStateDesc` — graphics/compute variant construction and typed accessors now preserve the C++ single-active-alternative invariant
  - [~] `PipelineStateDesc` — default variant now matches C++ graphics-alternative initialization
  - [~] `PipelineState : Object, DeviceObject, IDFamily<PipelineState>` — GPU creation, dependency-aware restore lifecycle, shader subscriptions, cache ownership, reflection-based validity, material-specific immutable sampler descriptors, Depth32Stencil8 native output mapping, OpenGL temporary-program vertex-input reflection/location binding, and Dispose-based destructor cleanup are ported; native descriptor parity remains under review
  - [~] `PipelineStateCache : Object, DeviceObject` — descriptor cache lookup, weak-state ownership, release diagnostics, and cached-data lifecycle match C++; native Diligent pipeline-cache creation/handle access remains TODO because the generated managed surface does not expose `IPipelineStateCache`
- [x] `RenderPool.h`
  - [x] `RenderPool : Object` — uniform buffers, transient/persistent textures, frame-age cleanup, allocation-free recycling, scratch allocation, and device invalidation/restore lifecycle are ported
- [x] `RenderScope.h`
  - [x] `RenderScope : NonCopyable` — conditional RAII-style Diligent debug groups are ported
- [x] `RenderContext.h` — `RenderContext : Object` is ported, including cached render-target output, dimensions, swap-chain state, viewport clamping, target dirty marking, disposal, platform-specific debug-scope enablement, device-lost clip-state reset, and SDL-resolved OpenGL clip-plane toggles
- [x] `DrawCommandQueue.h` — `DrawCommandQueue` is ported, including command recording, reflected data, execution, native state caching, temporary resource-view preparation, default raw-texture fallback, four-float blend-factor binding, and the C++ void shader-parameter API/group-mismatch behavior
- [~] `RenderDevice.h`
  - [~] `RenderDevice : Object` — native factory/SDL-window/device/context/swap-chain accessors, adapter-derived device capabilities, texture format capability and multisample queries, separate swap-chain/window size reporting, fullscreen mode enumeration/selection, destructor disposal, default sampler settings, DPI scaling, render statistics, present/frame lifecycle, unordered device-object ownership, queued pipeline reloads, screenshot capture, dynamic window settings, platform-default backend selection, post-initialize default raw textures, explicit device-state invalidation/restoration, render-pool loss/restore handling, OpenGL secondary swap-chain creation, generated GL/GLES and Vulkan lifecycle interface coverage, C++ Android GLES invalidation/restoration flow, Linux Vulkan initialization and invalidation/restoration with the C++ multisample proxy swap chain, Vulkan surface-capability resize, dynamic rotating backbuffer views, and device-object destruction are ported and Linux Vulkan is runtime-verified; Android GLES platform verification, other Vulkan native-window branches, and other backend-specific device initialization remain

## A8. Shader — depends on Core, IO (feeds Graphics)

- [x] `ShaderDefs.h` — `SpirVShader`, `TargetShaderLanguage`, and `ShaderDefineArray` are ported, including bytecode/compiler-output storage, validity conversion, define parsing/appending, size, unused-define detection, and name/value storage
- [x] `ShaderSourceLogger.h`
  - [x] shader source logging helpers — source logging and define header output are ported through Engine.Parameters and VirtualFileSystem
- [x] `ShaderOptimizer.h`
  - [x] shader optimizer helpers — target environment selection, diagnostic aggregation, Vulkan legalization and validation, interface-preserving performance passes, validator options, failure semantics, and output transfer match C++; focused tests cover valid Vulkan/non-Vulkan modules and malformed-input preservation
- [x] `ShaderTranslator.h`
  - [x] `struct TargetShader` — target language, source/compiler output storage, and validity conversion are ported
  - [x] `FindVersionTag`
  - [x] shader translation helpers — universal GLSL compilation to SPIR-V, HLSL 5.0 semantic/sampler remapping, GLSL 4.1 and GLSL ES 3.0 translation, clip-distance handling, and SPIR-V vertex-input reflection match C++; focused tests cover diagnostics, all target languages, remapping, and reflected semantic/location indices
- [x] `ShaderCompiler.h`
  - [x] shader compiler helpers — shader-stage profiles, optimization level 3, pixel-shader flow-control preference, debug-build flag, diagnostics, bytecode transfer, COM blob release, and unsupported-platform behavior match C++ through generated SharpCpp bindings and a platform-gated support implementation

## A9. Graphics — depends on RenderAPI, Scene, Resource, Shader

- [x] `GraphicsDefs.h`
  - [x] `struct VertexElement` — fields, constructors, equality, inequality, and ToHash are ported
  - [x] graphics enums / constants
- [~] `GraphicsEvents.h`
  - [~] graphics event definitions — all 16 C++ events are represented as struct-based C# events, window-position and render-pipeline per-view update/render/buffer events are dispatched, and render-surface update dispatch now queues managed texture surfaces; managed output targets still lack native RenderSurface sender integration
- [~] `DrawableEvents.h`
  - [~] drawable event definitions — all five C++ events are represented as struct-based C# events; Animation trigger and finished payloads are strongly typed and trigger dispatch is ported, while ParticleEffect remains opaque
- [~] `GraphicsUtils.h`
  - [~] graphics free functions — the C++ vertex-buffer-array helper, InitializeInputLayout, InitializeInputLayoutAndPrimitiveType, SetBuffersFromGeometry, AddShaderResource, and AddNullableShaderResource are ported; full Texture/RawTexture inheritance parity remains TODO
- [x] `Tangent.h`
  - [x] tangent generation — GenerateTangents and its index reader are ported with managed span equivalents for the C++ vertex/index pointers
- [x] `PipelineStateTracker.h`
  - [x] `PipelineStateSubscription` — managed IDisposable subscription with Clone for C++ copy-construction
  - [x] `PipelineStateTracker` — dependency tracking and atomic cached hash invalidation
- [x] `Skeleton.h`
  - [x] `Bone` — fields, defaults, cloning, and managed nullable Node reference
  - [x] `Skeleton` — serialization, hierarchy ordering, lookup, definition, and reset behavior
- [x] `Geometry.h` — `Geometry : Object, PipelineStateTracker, IDFamily<Geometry>` factory registration, dependency tracking, draw-range validation including the failed shadow-range fallback, raw-data overrides including shadowed index extraction, primitive metadata, pipeline hashing, headless-buffer tracking, and C++-equivalent raw triangle hit-distance with normal/UV output and inside-geometry traversal are ported; `IDFamily<Geometry>` is omitted because managed type identity replaces it, as allowed
- [x] `IndexBuffer.h` — `IndexBuffer : RawBuffer, PipelineStateTracker` and `DynamicIndexBuffer : Object` are ported, including CPU shadow/range behavior, pack/unpack helpers, size metadata, pipeline-state hashing, RawBuffer creation and direct range uploads, dynamic allocation/growth/discard/commit, and debug-name forwarding; C# composes `RawBuffer` because managed C# cannot represent C++ multiple inheritance
- [x] `VertexBuffer.h` — `VertexBuffer : RawBuffer, PipelineStateTracker` and `DynamicVertexBuffer : Object` are ported, including element layout, legacy masks, shadow-data conversion, pack/unpack/shuffle helpers, pipeline-state hashing, RawBuffer creation, direct range uploads, dynamic allocation/growth/discard/commit, and debug-name forwarding; C# composes `RawBuffer` because managed C# cannot represent C++ multiple inheritance
- [~] `Texture.h`
  - [~] `Texture : ResourceWithMetadata, RawTexture` — sampler/mip/size/data metadata, requested-versus-realized mip-level semantics, XML parameter parsing, format component/data-size calculations including depth formats, bind-flag queries, C++-matching render-target/depth-stencil `RenderSurface` slice preservation/materialization, render-surface update subscription/queue handling, resource restore, texture-budget material release, managed `RawTexture` GPU composition/update for ordinary texture resources, full-mip raw-texture material binding, immutable material sampler application, sampler-state synchronization, and RGBA8 GPU readback to `Image` are ported; full C++ inheritance parity and remaining image format transfer hooks remain TODO
- [~] `Texture2D.h`
  - [~] `Texture2D : Texture` — two-phase image loading including headless short-circuiting, texture-budget checks, mip selection, flag-aware size configuration, image conversion, object registration, and C++-matching RawTexture image creation, updates, mip generation, GPU readback, and render-target usage flags are ported; full Texture/RawTexture inheritance parity remains TODO
- [~] `Texture3D.h`
  - [~] `Texture3D : Texture` — two-phase volume/color-LUT loading including headless short-circuiting, mip selection, CPU volume upload/readback, usage-flag propagation, object registration, and RawTexture volume updates are ported; image-backed GPU transfer remains TODO
- [~] `TextureCube.h`
  - [~] `TextureCube : Texture` — two-phase ImageCube loading including headless short-circuiting, sampler defaults, face/mip upload, usage-flag propagation, multisample autoresolve semantics, CPU readback, image conversion, object registration, and RawTexture face updates are ported; image-backed GPU transfer remains TODO
- [~] `Texture2DArray.h`
  - [~] `Texture2DArray : Texture` — two-phase layer loading including headless short-circuiting, separate layer state, mip selection, CPU layer upload/readback, object registration, and RawTexture layer updates are ported; image-backed GPU transfer and flag-based allocation remain TODO
- [~] `RenderSurface.h` — viewport storage, update queue state, linked surfaces, texture-surface access, renderer queue integration, and destructor cleanup are ported; native GPU surface lifecycle remains managed/CPU-backed
  - [~] `RenderSurface : RefCounted` — viewport/update state, linked render/depth surfaces, parent texture dimensions/views, resolve state, and main-viewport helpers are ported; native GPU view restoration remains managed-resource dependent
- [~] `Shader.h`
  - [~] `Shader : Resource` — reload signaling, recursive include processing with dependency tracking, timestamps, normalized variation caching, C++ `v2/...` universal shader-name resolution, shader-file indexing, and source validation are ported; managed GLSL preparation remains alongside the C++ resource API and native shader-cache/dependency details remain TODO
- [~] `ShaderVariation.h`
  - [~] `ShaderVariation : RawShader` — variation lifecycle, reload recreation, virtual shader-cache load/save, source preprocessing, backend target selection, translation, optimization, D3D compilation, Vulkan/direct-source bytecode selection, SPIR-V vertex-attribute reflection, bytecode creation, and diagnostics are ported; remaining class-shape and native shader-cache/dependency details remain TODO
- [x] `ModelView.h`
  - [x] `struct ModelVertexFormat` — editable vertex format conversion, merging, hashing, and equality are ported
  - [x] `struct ModelVertex` — editable vertex access, semantic replacement, repair, pruning, and equality are ported
  - [x] `struct ModelVertexMorph` — morph state and normalization are ported
  - [x] `struct GeometryLODView` — primitive queries, triangle iteration, normalization, bounds, CPU normal recalculation, and C++-equivalent tangent generation are ported
  - [x] `struct GeometryView` — LOD normalization, morph counting, and equality are ported
  - [x] `struct BoneView` — transforms, offset-matrix recalculation, bounding-volume state, and equality are ported
  - [x] `struct ModelMorphView` — editable morph metadata is ported
  - [x] `ModelView : Object` — editable operations, native Model import/export, vertex/index buffer layout and composition, in-place buffer reuse, morph packing/ranges, metadata, geometry LODs, and skeleton conversion are ported
- [~] `Model.h`
  - [~] `Model : ResourceWithMetadata` — geometry loading and resource storage are ported; serialized skeleton retention, geometry bone mappings, morph metadata/packed data, vertex/index buffer ownership, and their accessors/validation are now ported, while clone/save parity remains incomplete
- [x] `AnimationTrack.h`
  - [x] `struct AnimationTrack : KeyFrameSet<AnimationKeyFrame>` — keyframe sampling, loop detection, and merged channel construction are ported; `AnimationKeyFrame` duplicates `Transform` fields because C# structs cannot inherit structs
  - [x] `struct VariantAnimationTrack : VariantCurve` — variant track weight and loop detection are ported; `VariantCurve` is made inheritable to match the C++ base relationship
- [~] `Animation.h`
  - [~] `Animation : ResourceWithMetadata, ObjectRevisionTracker` — resource state, XML and binary validation diagnostics, UANI/UAN2 serialization, trigger-save diagnostics, tracks, variant tracks, triggers, cloning, metadata, and revision tracking are ported; C# flattens `ObjectRevisionTracker` because it cannot express C++ multiple inheritance
  - [~] ResourceCache/VFS sibling XML dependency behavior is mapped through `VirtualFileSystem` and `FileIdentifier`; generic resource-cache dependency registration remains TODO
- [x] `Camera.h`
  - [x] `Camera : Component` — projection/view/frustum caches, camera frustum debug geometry, vector orthographic-size configuration, reflection and clipping planes, screen/world conversion, zone state, camera-facing rotations, and graphics-backend projection conversion are ported
- [x] `CameraOperator.h` — tracked-node attributes, scene/node listeners, camera movement, bounding-box/padding state, frustum fitting, factory registration, and action label/description metadata are ported; C# drops `WeakPtr` per the allowed deviations
- [x] `Viewport.h` — `Viewport : Object` base shape, constructors, camera/culling access, scene/rect/debug setters, effective rectangles, screen/world coordinate helpers, and render-pipeline view allocation including owner invalidation are ported; C# drops `WeakPtr`/`SharedPtr` per the allowed deviations
- [~] `Drawable.h`
  - [x] `struct SourceBatch`
  - [~] `Drawable : Component, PipelineStateTracker` — ray queries, geometry-update defaults/hooks, delayed main-thread batch update requests, LOD fallback, zone/reflection cache state, mutable cached-zone/reflection/light-probe-hint access, source-batch access, view-camera tracking, renderer-wide and explicit-frame view queries, batch distance/world-transform/LOD updates, distance/sort accessors, combined zone light/shadow masks, common debug bounding-box drawing, pipeline-state tracking/hash invalidation, and octree attach/detach/disposal are ported; reflection-probe manager selection and OcclusionBuffer integration remain incomplete
- [x] `OctreeQuery.h`
- [~] `Octree.h`
  - [~] `Octant` — child subdivision, bounds/culling initialization, drawable fit checks, recursive insertion/counting, removal, and moving existing drawables between octants are ported; full scene octree reinsertion and debug traversal remain incomplete
  - [~] `ZoneLookupIndex` — zone registration, priority ordering, cached bounds/transforms, signed-distance selection, mask filtering, default-zone fallback, background-zone lookup, and zone cached-lighting refresh are ported; full octant lifecycle integration remains incomplete
- [~] `Octree : Component` — managed scene drawable registration, manual-drawable separation, zone-index ownership and dirty notifications, drawable add/remove/index lifecycle, octant insertion/reinsertion, drawable queries and raycasts, conditional headless RenderUpdate handling, scene drawable-update-finished event dispatch, world-origin update/post-update forwarding, `QueryZone`/`GetBackgroundZone` wiring, all-drawables access, locking, and recursive visible-octant DebugRenderer bounds are ported; threaded transform-update support remains incomplete
- [x] `DebugRenderer` — component geometry accumulation for depth-tested and no-depth lines/triangles, color conversion, polygon/circle/cross/quad/frame/bounding-box/frustum/polyhedron/sphere/sphere-sector/cylinder/node/skeleton helpers, raw and node/static-model triangle-mesh overloads, object factory registration, capacity limits, end-frame event clearing, camera transforms, frustum containment, dynamic vertex-buffer packing, pipeline-state initialization, and draw-queue rendering are ported
- [~] `Zone.h` — zone attributes, texture subscription/cache invalidation, ambient lighting, gradient selection, drawable-zone invalidation, and reflection-probe data are ported; renderer-side zone lifecycle remains incomplete
  - [~] `Zone : Drawable` — cached lighting/reflection data, drawable-zone cleanup, world-bounding-box refresh, octant removal cleanup, texture reload subscription, ambient-gradient neighbor selection, and debug bounding-box drawing are ported; complete octant lifecycle hooks remain incomplete
- [~] `Light.h`
  - [~] `struct BiasParameters`
  - [~] `struct CascadeParameters`
  - [~] `struct FocusParameters`
  - [~] `Light : Drawable` — intensity divisor and distance-based sort-value calculation, validated cascade split counting, ramp/shape texture attributes and resource-cache loading, camera-distance `UpdateBatches` dispatch, world-space/view-space spotlight frustums, directional/spot/point world bounds, point/spot/directional volume transforms, directional/spot/point debug geometry, and AABB/OBB/triangle ray queries are ported; light-batch queue integration remains incomplete
- [~] `Technique.h`
  - [~] `Pass : RefCounted, PipelineStateTracker` — managed Pass now derives from PipelineStateTracker with tracker-backed hash invalidation; native shader-variation collections remain incomplete
  - [~] `Technique : Resource`
- [~] `Material.h`
  - [~] `struct TechniqueEntry`
  - [x] `ShaderParameterAnimationInfo : ValueAnimationInfo`
  - [~] `Material : Resource, PipelineStateTracker, IDFamily<Material>` — public construction, unique object-ID allocation/lifetime, C++ pipeline-state hash inputs, specular state, auxiliary-view frame tracking, scene-specific animation subscriptions, and shader release forwarding are ported; dependency propagation and batched parameter-update optimization remain incomplete
- [~] `StaticModel.h`
- [~] `StaticModel : Drawable` — model/material resource attributes, model reload reapplication, attached-node model assignment, component-owned geometry/LOD storage, per-geometry centers and LOD state, lightmap state, C++ LOD-geometry fallback semantics, material-aware occluder triangle counting, indexed occlusion drawing, custom-transform AABB/OBB/triangle/UV ray queries, bounding-box containment, C++ ray-based triangle containment, and material-list loading are ported; remaining class-shape parity is incomplete
- [x] `AnimatedModel.h`
  - [x] `AnimatedModel : StaticModel` — model/skeleton/morph ownership, deferred bone assignment, compatible skeleton reload, master/non-master bone-node sharing, listener/event lifecycle, local/final bone transforms, threaded animation calculation and octree transform commits, invisible and animation-LOD gates, bone/world bounds, per-view batch distances, model-bounds LOD scale, geometry-update thread classification, component-owned cloned geometries, morph-buffer updates, global/per-geometry skin matrices, hardware/software skinning selection and updates, morph weights, reusable morph attribute serialization buffer, debug skeleton rendering, model/bone/morph attribute helpers, and custom/public bone box/sphere ray queries are ported
- [~] `StaticModelGroup.h`
- [~] `StaticModelGroup : StaticModel` — instance-node listener lifecycle, transform bounds, ray-query distance tests, material-aware instance-scaled occluder triangle counts, per-instance indexed occlusion drawing, and batch distance/world-transform/LOD updates are ported; remaining class-shape parity is still incomplete
- [~] `Skybox.h`
  - [~] `Skybox : StaticModel`
- [~] `AnimationStateSource.h` — base component, managed animation-state list, throttling state, controller-to-model source connection, and model pose consumption are ported
  - [~] `AnimationStateSource`
- [~] `AnimationState.h` — enums, track/output records, lifecycle state, clamped time/weight changes with model dirty propagation, node/model/attribute track blending, ordinary/node-variable attribute application, AnimatedModel pose application, and morph reflection are ported
  - [~] `struct AnimatedAttributeReference`
  - [~] `AnimationState : RefCounted`
- [~] `AnimationController.h` — animation parameter management, playback lifecycle, replacement merge path, UpdateAnimation assertions, structured animation-parameter metadata, Update Pose action registration, and AnimatedModel pose/attribute integration are ported
  - [~] `AnimationParameters` — global resource lookups use a local context variable because this C++ value-style class has no Object.GetSubsystem() instance
  - [~] `AnimationController : AnimationStateSource` — ParseAnimatablePath, track-node lookup, trigger dispatch, node/attribute output aggregation, managed node/variant track setup, controller-to-model state connection, structured animation-parameter metadata, Update Pose action registration, and AnimatedModel skeleton/pose integration are ported
- [x] `SoftwareModelAnimator.h`
  - [x] `VertexBufferAnimationData`
  - [x] `SoftwareModelAnimator : Object` — model geometry and animated-stream cloning, morph reset/application, four-weight extraction and reduction, CPU position/normal/tangent skinning, and GPU commit are ported
- [x] `BillboardSet.h`
  - [x] `struct Billboard` — position, size, UV, color, rotation, direction, enabled, sort distance, and screen-scale factor match C++
  - [x] `BillboardSet : Drawable` — constructor defaults, material/num/relative/scaled/sorted/fixed-screen-size/face-camera/min-angle/animation-LOD API, commit, ray query, batch/geometry updates, buffer size/format rebuilds, default/direction/axis-angle vertex builds, fixed screen-size scaling, world bounds, and billboard attribute serialize/deserialize match C++
- [x] `ParticleEffect.h`
  - [x] `ParticleEffect : Resource` — defaults, XML load/save, color/texture frames, clone, random helpers, and material async load match C++
- [x] `ParticleEmitter.h`
  - [x] `ParticleEmitter : BillboardSet` — effect application, emission/period timers, particle/billboard attribute serialize, scene post-update animation, finished event, and auto-remove match C++
- [x] `RibbonTrail.h`
  - [x] `struct TrailPoint` — position, forward, parent position, elapsed length, lifetime, and sort distance match C++
  - [x] `RibbonTrail : Drawable` — face-camera/bone trail types, emission/update, buffer rebuilds, sorting, world-origin shifts, and scene post-update match C++
- [x] `DecalSet.h`
  - [x] `struct DecalVertex` — position, normal, UV, tangent, and optional skinning weights/indices match C++
  - [x] `struct Decal` — timer, TTL, local bounds, vertex/index storage, and AddVertex dedupe match C++
  - [x] `DecalSet : Drawable` — material/limits, AddDecal frustum clip path, buffer rebuild, skinning, attribute serialize, and time-limited cleanup match C++
- [~] `CustomGeometry.h`
  - [~] `CustomGeometry : Drawable` — dynamic custom-geometry definition, vertex serialization, resource-cache material attributes, C++ LOD-geometry and material-aware occluder triangle accounting, native occlusion-buffer drawing, material assignment, bounds, geometry access, and managed ray-query support are ported; full class-shape and GPU buffer lifecycle parity remain incomplete
- [~] `Terrain.h`
  - [~] `Terrain : Component` — factory and UpdatePatchLod stitch-range selection are ported; CreatePatchGeometry and heightmap/neighbor/material lifecycle remain TODO
- [x] `TerrainPatch.h`
  - [x] `TerrainPatch : Drawable` — LOD selection with neighbor correction, ray queries, occlusion, lightmap batch helpers, and geometry accessors match C++
- [x] `LightProbeGroup.h`
  - [x] `LightProbeGroup : Component` — auto placement, collection, baked-data reload hooks, masks, and debug spheres match C++
- [x] `GlobalIllumination.h`
  - [x] `GlobalIllumination : Component` — compile/reset probes, SH/ambient sample, file ref, offset, and world-origin shift match C++
- [~] `ReflectionProbeData.h`
  - [~] reflection probe data struct — cubemap center, projection box, `ReflectionProbeReference.Reset()`, validity, and importance comparison now match C++; manager/probe integration remains unported
- [ ] `ReflectionProbe.h`
  - [ ] `ReflectionProbeManager : TrackedComponentRegistryBase`
  - [ ] `ReflectionProbe : TrackedComponent<TrackedComponentBase, ReflectionProbeManager>`
- [ ] `CubemapRenderer.h`
  - [ ] `CubemapRenderer`
- [~] `OutlineGroup.h`
- [~] `OutlineGroup : Component` — drawable collection, deferred scene-ID attribute resolution, outline material caching, and cached-state updates are ported; remaining renderer integration is incomplete
- [x] `OcclusionBuffer.h` — software rasterization, clipping, depth hierarchy, visibility queries, threading buffers, and the `CustomGeometry`, `StaticModel`, and `StaticModelGroup` occlusion hooks are ported and covered by focused tests
  - [x] `OcclusionBuffer : Object`
- [x] `DebugRenderer.h` — geometry records and helpers, C++ content limits, camera state, end-frame cleanup, output-keyed static pipeline states, per-draw shader constants, and debug rendering lifecycle match C++; raw mesh pointers map to allocation-free byte spans and the managed dynamic vertex-buffer adapter
  - [x] `DebugRenderer : Component`
- [~] `Renderer.h`
  - [~] `Renderer : Object` — the complete C++ public method and state-field set, C++ field ordering/defaults, conditional screen-mode-driven initialization, screen-mode view reset handling, initialization-time black cubemap/light-volume creation, default-zone/default-technique/default-material/default-light-texture access, mutable frame information/statistics, end-of-frame statistic reset, texture-quality clamping and texture reload, material-shader release helper, separate render-update-event-driven update and render phases, per-frame render-pipeline view tracking, reverse main-view queueing and reverse render ordering, render-surface pipeline integration, queued-pair deduplication, render-device texture defaults, multi-viewport rendering, debug-geometry/light dispatch, frame-stat aggregation, nullable viewport-slot and scene-query semantics, backbuffer render-surface replacement/viewport synchronization, no-backbuffer clearing, and end-of-all-views render event dispatch are ported; built-in-UI mouse-position propagation is excluded, while final declaration-order/documentation parity and C# buffer-upload adaptation remain to audit
- [~] `Graphics.h`
  - [~] `Graphics : Object` — OpenGL rendering, C++ active-context attachment and primary proxy swap-chain initialization with effective SDL sRGB/depth/multisample properties, C++ texture color-space shader hints, linear-output camera ambient/fog uploads and fog clears, SDL window title/position/icon operations, screen-mode queries and lifecycle, monitor/display resolution enumeration, DPI reporting, screenshot capture, screen-mode diagnostics/events, and VFS-backed pipeline-state-cache save are ported; backend-specific paths remain
- [x] `LightBakingSettings.h` ⤴ pairs with `Glow` — all eleven settings structures, constructors, field ordering, defaults, and nested aggregate defaults match C++; focused tests cover aggregate and tracing defaults
- [ ] `LightBaker.h` ⤴ pairs with `Glow`
  - [ ] `LightBaker : Component`

## A10. RenderPipeline — depends on Graphics, RenderAPI, Scene (current effort)

- [~] `RenderPipelineDefs.h`
  - [x] `RenderPipelineInterface` — global context accessor, render-target-aware view definition, debug geometry/light dispatch, and per-pipeline debugger accessor match C++
  - [~] pipeline enums / settings structs — flags, frame statistics, default settings, validation, implied-setting propagation, and render-path adjustment are ported; native RG32F support is unavailable in the managed TextureFormat surface
- [x] `ShaderConsts.h` — all 67 built-in shader constants and 10 shader resources match C++ names and declaration order; focused parity tests cover every exported value
- [x] `PipelineBatchSortKey.h` — both 64-bit state-sort layouts, render-order/back-to-front comparison, batch groups, and the required `PipelineBatch` value surface match C++; managed `PipelineBatch` uses reference semantics to preserve the C++ sort-key pointer relationship
  - [x] batch sort-key structs
- [~] `SharedRenderPassState.h`
  - [~] `struct SharedRenderPassState : NonCopyable` — fixed 128-entry render-buffer capacity and shared identifiers are ported; managed Graphics convenience state remains for current render-pass integration
- [~] `RenderBuffer.h`
  - [~] `RenderBuffer : Object`
  - [~] `TextureRenderBuffer : RenderBuffer` — viewport sizing, color/depth bind-flag selection through the shared C++ texture-format predicates, multisampling, no-auto-resolve propagation, filtering, and 2D/cubemap render-target views are ported; RenderPool-backed transient/persistent texture ownership remains TODO
  - [x] `ViewportColorRenderBuffer : RenderBuffer`
  - [x] `ViewportDepthStencilRenderBuffer : RenderBuffer`
- [~] `RenderBufferManager.h`
  - [~] `RenderBufferManager : Object` — settings/frame-settings assignment, live swap-chain color/depth/multisample/sRGB output resolution, C++ color/AA/filter/usage output-matching criteria, viewport/output management, buffer swapping, clears, static output-keyed fullscreen pipeline states, allocation-free texture-copy parameters, fullscreen helpers, and depth-first multi-color target binding are ported; cached clear-quad pipeline states remain incomplete
- [x] `ShadowMapAllocator.h`
  - [x] `ShadowMapAllocator : Object`
- [x] `InstancingBuffer.h` — settings-driven vertex declarations, begin/end composition, instance indexing, element upload, buffer access, and enable state match C++; focused tests cover disabled, initialized, and repeated composition paths
- [x] `ShaderProgramCompositor.h` — shader-program descriptions, common/geometry/material/light/shadow/light-volume defines, texture color-space hints, instancing selection, and normal/tangent availability match C++
  - [x] `ShaderProgramCompositor : Object`
- [x] `StaticPipelineStateCache.h` — descriptor value ownership, stable IDs, output-keyed pipeline creation, invalidation, and sparse per-output state storage match C++
  - [x] `StaticPipelineStateCache : Object`
- [x] `BatchStateCache.h` — batch lookup and creation keys, output invalidation, object-hash validation, atomic lazy invalidation, callback recreation, and placeholder caching match C++
  - [x] `BatchStateCache : NonCopyable`
  - [-] `UIBatchStateCache : NonCopyable` — **EXCLUDED** with the retained-mode rbfx UI subsystem
- [~] `PipelineStateBuilder.h`
  - [~] `PipelineStateBuilder : Object, BatchStateCacheCallback` — construction, compositor access, input-layout/shader setup helpers, and placeholder pipeline-state creation match C++; full `CreateBatchPipelineState` body remains TODO (depends on complete SceneProcessor)
- [x] `CameraProcessor.h` — camera-setting validation, zone and automatic-aspect updates, OpenGL render-target projection flipping, render callback restoration, and pipeline-state hashing match C++
  - [x] `CameraProcessor : Object`
- [x] `LightProcessorQuery.h`
  - [x] `LightGeometryQueryResult`
  - [x] `PointLightGeometryQuery : SphereOctreeQuery` — lit/forward-lit/shadow-caster classification for point lights match C++ (`bool& hasLitGeometries` mapped to post-query property)
  - [x] `SpotLightGeometryQuery : FrustumOctreeQuery` — same classification for spot lights match C++
  - [x] `DirectionalLightShadowCasterQuery : FrustumOctreeQuery` — directional shadow-caster filtering match C++
- [x] `ShadowSplitProcessor.h`
  - [x] `ShadowSplitProcessor` — directional/spot/point initialization, caster processing via DrawableProcessor preprocess, FinalizeShadow texel snap/zoom, world-to-shadow matrix, and batch sort with DisableColorOutput match C++ (freestanding Node mapped to private temporary Scene)
- [x] `LightAccumulator.h` — light sampling data, pixel/vertex importance partitioning, overflow accumulation into spherical harmonics, deterministic vertex-light sorting and hashing, and reflection-probe state match C++
  - [x] `LightAccumulator`
- [x] `LightProcessor.h`
  - [x] `LightProcessor : NonCopyable` — lit-geometry queries, shadow split setup/processing, EndUpdate allocation, full CookedLightParams (including shadow matrices/fade/intensity/bias), and pipeline-state hashes match C++
  - [x] `LightProcessorCache : NonCopyable` — generation-based LRU cleanup with budget/TTL settings and get-or-create match C++
- [~] `DrawableProcessor.h` — geometry render flags, sorted occluders, resolved deferred/forward geometry batches, and pass-level batch resolution/update lifecycle match C++; main drawable processing remains pending
  - [x] `DrawableProcessorPass : Object`
  - [~] `DrawableProcessor : Object` — construction, pass/settings hooks, lights/geometries/light-processor/geometry-lighting/Z-range accessors, PreprocessShadowCasters, zone cache update, geometry-update queues, occluder pre-sort, and frame statistics collection are ported; full visible-drawable/light processing remains pending
- [~] `BatchCompositor.h` — `PipelineBatch`/`PipelineBatchDesc`, `BatchCompositorPass` scene composition, and `BatchCompositor` shadow/scene/light-volume composition match C++ control flow; full lighting still depends on complete `LightProcessor`/`ShadowSplitProcessor` update paths
  - [x] `BatchCompositorPass : DrawableProcessorPass` — deferred/forward batch composition, delayed pipeline-state resolution, placeholder states, and per-frame cache invalidation match C++
  - [x] `BatchCompositor : Object` — pass filtering, shadow batch composition hooks, scene-pass compose, light-volume batches, and `FillSortKeys` match C++
- [~] `BatchRenderer.h`
  - [~] `BatchRenderer : Object` — construction, settings, batch/light-volume render entry points, instancing-buffer prepare hooks, and debugger scene-batch reporting are ported; full DrawCommandCompositor GPU emission path remains
- [~] `RenderPass.h`
  - [~] `RenderPass` — pass-name and render-buffer connection semantics match C++; initial managed enable behavior, cloning, and render-path serialization remain managed-runtime adaptations
- [~] `RenderPath.h`
  - [~] `RenderPath : SimpleResource` — pass iteration, enabled-pass repair, pass ordering, and render-target lifecycle dispatch now follow C++; managed XML/JSON resource serialization and extended depth trait remain adaptations
- [x] `ScenePass.h`
  - [x] `ScenePass : BatchCompositorPass` — forward-lit and unlit constructors plus instancing prepare hook match C++
  - [x] `UnorderedScenePass : ScenePass` — deferred/base/light sort keys, partial negative-light sort, and batch-group flags match C++
  - [x] `BackToFrontScenePass : ScenePass` — distance factoring for additive/subtractive lights, NaN distance validation, refraction detection, and batch-group flags match C++
- [~] `SceneProcessor.h`
  - [~] `SceneProcessor : Object, LightProcessorCallback` — public API surface, owned processors/compositor/renderer wiring, pass list, and LightProcessorCallback interface mapping are ported; Update/Define/render bodies remain TODO
- [x] `OutlineScenePass.h`
  - [x] `OutlineScenePass : ScenePass` — outline group filtering, custom deferred batches, pipeline-state creation for outline mask, sort keys, and instancing prepare match C++
- [x] `RenderPipelineDebugger.h`
  - [x] `struct DebugFrameSnapshotBatch` — snapshot fields, formatting, and constructor from `DrawableProcessor` + `PipelineBatch` match C++
  - [x] `struct DebugFrameSnapshotQuad`
  - [x] `struct DebugFrameSnapshotPass`
  - [x] `struct DebugFrameSnapshot`
  - [x] `RenderPipelineDebugger` — snapshot state machine, pass/quad/scene-batch reporting, default-pipeline capture hotkey, and final logging match C++; BatchRenderer scene-batch reporting is wired
- [~] `Passes/FullScreenShaderPass.h`
  - [~] `FullScreenShaderPass : RenderPass` — parameter collection, per-view initialization, default-parameter disabling, render traits, C++ static-pipeline cache invalidation/restoration, fullscreen rendering, and cloning are ported; managed attribute/reflection registration remains adapted
- [~] `Passes/AmbientOcclusionPass.h`
  - [~] `AmbientOcclusionPass : RenderPass` — parameter processing, view initialization, cache/update flow, AO rendering, and cloning are ported; managed readable-depth trait integration and native shader-cache details remain adapted
- [~] `Passes/AutoExposurePass.h`
  - [~] `AutoExposurePass : RenderPass` — parameter processing, HDR gating, view initialization, luminance-buffer updates, exposure rendering, cloning, and managed device-reset subscription are ported; native pipeline-state/cache details remain adapted
- [~] `Passes/BloomPass.h`
  - [~] `BloomPass : RenderPass` — parameter processing, view initialization, HDR-aware texture cache, bloom rendering, and cloning are ported; native pipeline-state/cache details remain adapted
- [~] `Passes/ToneMappingPass.h`
  - [~] `ToneMappingPass : RenderPass` — HDR gating, mode metadata/clamping, view initialization, tone-mapping rendering, and cloning are ported; native pipeline-state/cache details remain adapted
- [~] `Passes/OutlineRenderPass.h`
  - [~] `OutlineRenderPass : RenderPass` — empty parameter/view lifecycle overrides, outline rendering, and cloning now match the C++ class shape; native pipeline-state details remain adapted
- [~] `RenderPipeline.h`
  - [~] `RenderPipelineView`
  - [~] `RenderPipeline : Component` — hash-keyed parameter updates and ResourceRef/VariantVector attribute accessors now match C++; remaining lifecycle and native pipeline integrations are incomplete
- [~] `DefaultRenderPipeline.h`
  - [~] `DefaultRenderPipelineView` — managed embedded view covers Define/Update/Render and frame/stat access; native debugger, scene-pass graph, output-flag calculation, and debug-draw members remain unported adaptations
- [ ] `StereoRenderPipeline.h`
  - [ ] `StereoRenderPipelineView : RenderPipelineView`
  - [ ] `StereoRenderPipeline : RenderPipeline`
- [ ] `LightmapRenderPipeline.h` ⤴ pairs with `Glow`
  - [ ] `LightmapRenderPipelineView`

## [x] A11. Input — depends on Core, Graphics (window)

- [x] `Input : Object` — core SDL lifecycle/window including the C++ OpenGL framebuffer/depth/stencil/sRGB/MSAA window-creation fallback order, frame state, keyboard/mouse/controller/generic joystick/touch polling, focus, modes, conversion helpers, raw and typed event dispatch, text/drop input, joystick/touch queries, native keyboard/gesture state helpers, gesture archive save/load, and window helpers are ported
- [-] `Input` screen joystick create/show/hide — **EXCLUDED** · reason: depends on built-in rbfx UI · replaced-by: Dear ImGui sample UI (AGENTS.md)
- [x] `InputMap.h`
  - [x] `struct KeyboardKeyMapping` — scancode wrapper with archive serialization ported
  - [x] `struct ControllerButtonMapping`
  - [x] `struct ControllerAxisMapping`
  - [x] `struct ControllerHatMapping` — hat position wrapper with archive serialization ported
  - [x] `struct MouseButtonMapping` — mouse button wrapper with mask conversion and archive serialization ported
  - [x] `struct ScreenButtonMapping` — element name wrapper with archive serialization ported
  - [x] `struct ActionMapping` — evaluation and archive persistence cover all managed mapping collections
  - [x] `InputMap : SimpleResource` — metadata, dead zone, unique remapping, evaluation, and JSON/XML/binary resource archives are ported
- [x] `MultitouchAdapter.h`
  - [x] `MultitouchAdapter : Object` — touch event subscriptions, active touch tracking, center/size/delta calculation, and struct-based multitouch event dispatch are ported
- [x] `PointerAdapter.h`
  - [x] `PointerAdapter : Object` — frame/input subscriptions, mouse/touch/joystick transitions, touch ownership, accelerated directional movement, pointer events, masks, and cleanup are ported
- [x] `DirectionAggregator.h`
  - [x] `DirectionAggregator : Object` — event subscriptions, per-source axis state and averaging, focus/disconnect cleanup, touch tracking, dead zones, and masks are ported
- [x] `DirectionalPadAdapter.h`
  - [x] `DirectionalPadAdapter : Object` — aggregated source state, keyboard/axis/hat subscriptions, hysteresis, focus/disconnect cleanup, synthesized transitions, and repeat timing are ported
- [x] `FreeFlyController.h`
  - [x] `FreeFlyController : Component` — activation, mouse/keyboard, controller/generic axes and hats, acceleration, multitouch rotation/pan, pitch limits, and transform tracking are ported
- [x] `MoveAndOrbitComponent.h`
  - [x] `MoveAndOrbitComponent : LogicComponent` — velocity, clamped pitch, yaw, node-rotation initialization, distance limits, and attributes are ported
- [x] `MoveAndOrbitController.h`
  - [x] `MoveAndOrbitController : Component` — cached input-map resources, action evaluation, sensitivity metadata, mouse/axis rotation, movement velocity, component connection, UI filters, and lifecycle updates are ported

## A12. Engine — top-level orchestration, depends on all Part-A above

- [x] `EngineDefs.h`
  - [x] engine parameter-name constants — ported as `EngineParameter` static class with C# string constants
- [x] `EngineEvents.h`
  - [x] engine event definitions — console, engine lifecycle, and plugin reload events ported as struct-based events
- [x] `StateManagerEvents.h`
  - [x] state-manager event definitions — transition lifecycle events ported as struct-based events
- [x] `ApplicationFlavor.h`
  - [x] `struct ApplicationFlavorPattern` — flavor component pattern matching with string parsing
  - [x] `struct ApplicationFlavor` — Universal/Empty/Platform flavors, pattern matching, ToString
- [x] `ConfigFile.h`
  - [x] `struct ConfigVariableDefinition` — variable metadata with defaults, override, and priority flags
  - [x] `ConfigFile : Object` — flavor-based config, variable management, and archive serialization
- [ ] `Engine.h`
  - [~] `Engine : Object` — engine initialization, frame loop with per-frame WorkQueue processing, C++ update/post-update/render-update ordering, BeginFrame/renderer/render-target-reset/EndFrame rendering lifecycle, application/resource/cache parameter defaults, FPS limiting, VFS resource-root/alias/root/package/autoload/conf mounting, separate application-preferences state, C++ ordering that mounts `conf://` before loading overrides, exact log URI mapping, PSO-cache save, exit handling, disposal, shutdown, canvas resizing, input pointer-state preservation, and the public command-line parameter-definition entry point are ported (parameter model uses `EngineParameters` class instead of `ConfigFile` for C# idiom); UI/RmlUI resize state, C++ diagnostics, console/debug HUD, and remaining config-file lifecycle behavior are incomplete
- [x] `StateManager.h`
  - [x] `ApplicationState : Object` — state lifecycle (activate/deactivate/update), mouse mode, viewport, and fog color management ported (UI-dependent features deferred with TODO)
  - [x] `StateManager : Object` — state queue, caching, fade transitions, and transition event dispatch ported (UI fade overlay deferred with TODO)
- [x] `Application.h`
  - [x] `Application : Object` — setup/start/stop lifecycle, application started/stopped events, CLI parsing, error handling, screenshot-on-exit, and main loop are ported (uses `EngineParameters` class for parameter model)

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

## B3. UI `OPTIONAL` — retained-mode UI; depends on Graphics, Resource, Input

- [ ] `UIEvents.h`
  - [ ] UI event definitions
- [ ] `FontFace.h`
  - [ ] `struct FontGlyph`
  - [ ] `FontFace : RefCounted`
- [ ] `FontFaceBitmap.h`
  - [ ] `FontFaceBitmap : FontFace`
- [ ] `FontFaceFreeType.h`
  - [ ] `FontFaceFreeType : FontFace`
- [ ] `Font.h`
  - [ ] `Font : Resource`
- [ ] `UIBatch.h`
  - [ ] `UIBatch`
- [~] `UIElement.h`
  - [~] `UIElement : Animatable`
- [ ] `UISelectable.h`
  - [ ] `UISelectable : UIElement`
- [ ] `BorderImage.h`
  - [ ] `BorderImage : UIElement`
- [ ] `Sprite.h`
  - [ ] `Sprite : UIElement`
- [ ] `Cursor.h`
  - [ ] `struct CursorShapeInfo`
  - [ ] `Cursor : BorderImage`
- [ ] `Button.h`
  - [ ] `Button : BorderImage`
- [ ] `CheckBox.h`
  - [ ] `CheckBox : BorderImage`
- [ ] `Slider.h`
  - [ ] `Slider : BorderImage`
- [ ] `ScrollBar.h`
  - [ ] `ScrollBar : BorderImage`
- [ ] `ProgressBar.h`
  - [ ] `ProgressBar : BorderImage`
- [ ] `LineEdit.h`
  - [ ] `LineEdit : BorderImage`
- [ ] `Text.h`
  - [ ] `Text : UISelectable`
- [ ] `Text3D.h`
  - [ ] `Text3D : Drawable`
- [ ] `TextRenderer3D.h`
  - [ ] `TextRenderer3D : LogicComponent`
- [ ] `ScrollView.h`
  - [ ] `ScrollView : UIElement`
- [ ] `ListView.h`
  - [ ] `ListView : ScrollView`
- [ ] `ToolTip.h`
  - [ ] `ToolTip : UIElement`
- [ ] `Menu.h`
  - [ ] `Menu : Button`
- [ ] `DropDownList.h`
  - [ ] `DropDownList : Menu`
- [ ] `Window.h`
  - [ ] `Window : BorderImage`
- [ ] `View3D.h`
  - [ ] `View3D : Window`
- [ ] `FileSelector.h`
  - [ ] `FileSelector : Object`
- [ ] `MessageBox.h`
  - [ ] `MessageBox : Object`
- [ ] `SplashScreen.h` ⤴ needs `Engine/StateManager`
  - [ ] `SplashScreen : ApplicationState`
- [ ] `UIComponent.h`
  - [ ] `UIComponent : Component`
- [ ] `UI.h`
  - [ ] `UI : Object`

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

- [~] `Script.h`
  - [~] `ScriptRuntimeApi`
  - [ ] `Script : Object`
  - [ ] `GCHandleRef`

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
