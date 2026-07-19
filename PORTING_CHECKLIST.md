# RedBolt Port Checklist — `Source/Urho3D`

## Samples

- [~] WebAssembly host — dedicated CMake/Emscripten preset, generated native bindings, full managed engine publish, pthread-capable WebGL 2 host, virtual filesystem staging, and a framework-free C#-to-C++ rendering prototype build successfully; browser rendering awaits user verification
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
- [x] `KeyFrameSet.h` — non-POD value-container copies, cloned element insertion, deterministic element/vector teardown, derived curve/track copy dispatch, and mutable stored references reconciled

## [x] A2. Math — depends on Container

- [-] `ApproximateEqualityComparer.cs` — **REMOVED** · reason: unused C#-only generic comparer with no rbfx counterpart
- [x] `AreaAllocator.h` / `AreaAllocator.cpp` — extensible copyable class kind, independent free-rectangle container copies, POD rectangle values, constructors, allocation paths, and automatic field lifetime reconciled
- [x] `BoundingBox.h` / `BoundingBox.cpp` — 32-byte all-float value layout, independent copies, native undefined construction at stored call sites, equality/merge surface, padding and intersection behavior, and automatic lifetime reconciled
- [x] `Color.h` / `Color.cpp` — 16-byte standard-layout, trivially copyable non-POD value class, POD channel mask, opaque-white default construction, independent copies, conversions, color-space logic, and automatic lifetime reconciled
- [x] `EaseMath.h` — stateless namespace function module, all 15 formulas and branches reconciled; no instance, value type, copy, or lifetime state exists
- [x] `Frustum.h` / `Frustum.cpp` — extensible non-POD copyable class kind, independent fixed-array copies, degenerate construction, geometry operations, and automatic lifetime reconciled
- [x] `InverseKinematics.h` / `InverseKinematics.cpp` — solver value aggregates and all chain classes reconciled, including native defaults, extensible non-POD class kinds, independent container/fixed-storage/value copies with borrowed node aliases, derived clone types, weight ownership, and automatic lifetime
- [x] `Rect.h` / `Rect.cpp` — `IntRect` and `IntRectIterator` sequential value types, independent scalar copies, contiguous data view, iterator behavior, and automatic lifetime reconciled
- [x] `Vector2.h` / `Vector3.h` / `Vector4.h` — `BaseIntegerVector2<T>` and `BaseIntegerVector3<T>` POD-style sequential structs, exact generic names, independent scalar copies, contiguous data views, conversions, integer lengths, hashes, and automatic lifetime reconciled
- [x] `MathDefs.h` — `Intersection` enum kind, integer backing, ordered values, value-copy semantics, and automatic lifetime reconciled
- [x] `MathDefs.h` / `MathDefs.cpp` — stateless module kind, constant types/order, scalar and vector helpers, variable-length integer bit offsets, native-width zigzag semantics, documentation, and compatibility-only surface cleanup reconciled
- [x] `NumericRange.h` — sequential reference-free pair value type, implicit independent copies, scalar aliases, invalid defaults, predicates, union/intersection operators, equality, and automatic lifetime reconciled
- [x] `PerlinNoise.h` / `PerlinNoise.cpp` — non-final reference class shape, unsigned constant, borrowed random-engine construction, fixed permutation storage, deep copy semantics, complete noise flow, comments, and automatic lifetime reconciled
- [x] `Plane.h` / `Plane.cpp` — reference-free value type, explicit copy construction and independent assignment semantics, complete constructor/method/field order, transforms, intersections, comments, and automatic lifetime reconciled
- [x] `Polyhedron.h` / `Polyhedron.cpp` — extensible non-POD class kind, independent nested-face copy construction and assignment, preserved scratch lifetimes, native face-resize identity, clipping and transform behavior, stack temporaries, and automatic container lifetime reconciled
- [x] `Quaternion.h` / `Quaternion.cpp` — sequential four-float value kind, explicit independent copy construction, constructor and mutating definition surface, allocation-free contiguous data, exact thresholds/hash/platform interpolation, look-rotation state preservation, and automatic lifetime reconciled
- [x] `RandomEngine.h` / `RandomEngine.cpp` — extensible non-POD class kind, independent engine-state copy construction, thread-local shared reference, constructor/load/save semantics, exact deterministic generation, assertions, helper visibility, documentation, and automatic lifetime reconciled
- [x] `Ray.h` / `Ray.cpp` — sequential reference-free value kinds, mutable DistanceAndNormal defaults, explicit independent Ray copy construction, member/type order, exact intersection and indexed-geometry branches, non-normalizing transformed direction, comments, and automatic lifetime reconciled
- [x] `Rect.h` / `Rect.cpp` — sequential four-float Rect value kind, explicit independent copy construction, undefined defaults, native member order, geometry operations, allocation-free contiguous data, hash, documentation, and automatic lifetime reconciled
- [x] `Sphere.h` / `Sphere.cpp` — sequential reference-free Circle and Sphere value kinds, native defaults and member order, explicit independent Sphere copy construction, exact definition/merge/classification/intersection behavior, documentation, and automatic lifetime reconciled
- [x] `SphericalHarmonics.h` / `SphericalHarmonics.cpp` — three sequential POD-style fixed-storage value kinds, independent inline-array and pass-by-value copies, native constructors/member order/operators/evaluation/repacking, serialization documentation, and automatic lifetime reconciled
- [x] `TetrahedralMesh.h` / `TetrahedralMesh.cpp` implementation internals — extensible mesh class kind, POD 20-byte edge storage, non-POD value context, independent tetrahedron compaction, scratch initialization, warning gate, and automatic container lifetime reconciled
- [x] `TetrahedralMesh.h` / `TetrahedralMesh.cpp` public class — implicit default construction and independent copy/clone semantics for all owned containers, native field order, statically typed sampling instantiations, and automatic container lifetime reconciled
- [x] `TetrahedralMesh.h` / `TetrahedralMesh.cpp` helper types — sequential POD sphere/triangle/edge/tetrahedron layouts with inline fixed storage, non-POD surface deep copy/clone, native reference parameter shapes, adjacency behavior, and automatic container lifetime reconciled
- [x] `Transform.h` / `Transform.cpp` — sequential reference-free Transform and DoubleTransform POD layouts, native defaults and identities, independent assignment/return copies, const-reference call paths, derived AnimationKeyFrame layout, and automatic lifetime reconciled
- [x] `Vector2.h` real vectors — sequential reference-free float/double POD layouts, independent assignment copies, allocation-free inline data access, native defaults/constants/hash/conversions/helpers, and automatic lifetime reconciled
- [x] `Vector3.h` real vectors — sequential reference-free float/double POD layouts, independent assignment copies, allocation-free inline data access, native defaults/constants/hash/conversions/signed-angle behavior/helpers, and automatic lifetime reconciled
- [x] `Vector4.h` / `Vector4.cpp` — sequential 16-byte reference-free value layout, explicit independent copy construction, allocation-free inline data access, native constructors/constants/index/hash/conversions/free helpers, approved managed `int` indexing, and automatic lifetime reconciled
- [x] Dear ImGui `ImVec2` / `ImVec4` SharpCpp marshalling — generated binding selection, matching inline float layouts, independent value conversion, borrowed const-reference duration, and automatic lifetime verified
- [x] `WrappedScalar.h` — reference-free `WrappedScalar<T>` and `WrappedScalarRange<T>` value kinds, implicit independent copies, native constructors/member order/update and containment behavior, and automatic lifetime reconciled

## [x] A3. Core — depends on Container, Math

- [x] `Attribute.h` — non-POD AttributeInfo value copies, pointer-like POD AttributeHandle class kind and borrowed alias copies, owned Variant/default/metadata cloning, replacement/removal/clear teardown, registration temporary lifetimes, retained ObjectReflection destruction, per-instance reflection-facade teardown, and approved managed `int` enum indices reconciled
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
- [-] `Exception.h` - **EXCLUDED** · reason: use C# `RuntimeException` / `ArgumentException` or similar.
- [-] `StringUtils.h` — **EXCLUDED** · reason: string parsing and formatting utilities · replaced-by: .NET string APIs
- [-] `Signal.h` - **EXCLUDED** · reason: use C# events

## [x] A4. IO — depends on Container, Core

- [x] `AbstractFile.h` — extensible abstract non-POD class kind, public base construction, name ownership, explicit virtual destruction, and derived teardown dispatch reconciled
- [x] `Archive.h` / `Archive.cpp` / `ArchiveBase.h` / `ArchiveBase.cpp` — enum/class kinds, move-only block ownership, deterministic block teardown, abstract/helper API shape, context nullability, delayed-exception lifetime, hex decoding, and virtual destruction reconciled
- [x] `ArchiveSerialization.h` / `ArchiveSerialization.cpp` / serialization helper headers — native free-function/static utility shape, POD and enum storage, direct POD compound overloads, primitive/compound Variant formats, replacement lifetimes, container ownership, ResourceRef/List paths, generic optional/pair value helpers, and exception consumption reconciled
- [x] `TetrahedralMesh.h` / `TetrahedralMesh.cpp` archive serialization — POD tetrahedron layout/copy/default construction, primitive-array parsing, and object-versus-byte vector serialization reconciled
- [x] `Base64Archive.h` / `Base64Archive.cpp` — extensible non-POD class kinds, binary inheritance, owned VectorBuffer construction/reverse teardown, and direct encode/decode behavior reconciled
- [x] `BinaryArchive.h` / `BinaryArchive.cpp` — extensible non-POD archive/block kinds, input-block copy construction, borrowed streams, owned safe buffers, deterministic teardown, paths, guards, and delayed exceptions reconciled
- [x] `Compression.h` / `Compression.cpp` — stateless free-function shape, explicit buffer sizes, unsigned LZ4 results, temporary ownership, borrowed streams, and independent VectorBuffer returns reconciled
- [x] `MemoryBuffer.h` / `MemoryBuffer.cpp` — extensible copyable non-POD class kind, shallow clone semantics, borrowed backing storage, independent stream state and pin ownership, deterministic teardown, and by-value storage call sites reconciled
- [x] `MountPoint.h` / `MountPoint.cpp` — extensible abstract non-POD mount and watchable base kinds, POD watcher state, virtual teardown, default optional/value returns, and exact state-transition callback ordering reconciled
- [x] `MountedAliasRoot.h` / `MountedAliasRoot.cpp` — extensible noncopyable class kind, weak alias targets, copied alias metadata, expired-target handling, identifier temporaries, forwarding boundaries, and member-before-base teardown reconciled
- [x] `MountedRoot.h` / `MountedRoot.cpp` — extensible noncopyable class kind, fieldless implicit teardown, borrowed filesystem access, independent path/identifier values, and failed/successful file ownership boundaries reconciled
- [x] `NamedPipe.h` / `NamedPipe.cpp` — extensible noncopyable class kind, platform handle states, managed pipe/read-loop ownership, synchronous close/reopen ordering, cancellation teardown, and borrowed buffer boundaries reconciled
- [x] `PackageFile.h` / `PackageFile.cpp` — POD PackageEntry layout, extensible noncopyable PackageFile kind, reader scopes, entry value copies, owned storage teardown, unsigned offset truncation, and native read/update ordering reconciled
- [x] `VirtualFileSystem.h` / `VirtualFileSystem.cpp` — extensible non-POD subsystem and movable guard kinds, ordered mount ownership, alias reference lifetime, unmount/destruction teardown, failed package scope, and opened-file RAII reconciled
- [x] `Serializer.h` / `Serializer.cpp` — extensible abstract non-POD class kind, virtual disposal, primitive and POD single-write boundaries, compound success ordering, Variant/custom payloads, container aliasing, and temporary value lifetimes reconciled
- [x] `FileWatcher.h` / `FileWatcher.cpp` — POD event-kind enum, non-POD value change records, extensible noncopyable watcher, standard-library platform watcher mapping, queue timing/copy semantics, and deterministic teardown reconciled
- [x] `MultiFileWatcher.h` / `MultiFileWatcher.cpp` — extensible noncopyable class kind, owned watcher collection, failed-local and clear teardown, delay forwarding, and output preservation reconciled
- [x] `MountedDirectory.h` / `MountedDirectory.cpp` — extensible noncopyable mount kind, owned optional watcher teardown, file stream scopes, and update value semantics reconciled
- [x] `MountedExternalMemory.h` / `MountedExternalMemory.cpp` — extensible noncopyable mount kind, by-value MemoryBuffer storage and replacement lifetimes, wrapped borrowed views, and deterministic map teardown reconciled
- [x] `VectorBuffer.h` / `VectorBuffer.cpp` — extensible copyable non-POD class kind, deep clone semantics, borrowed byte/pointer views, owned storage teardown, and return-value lifetimes reconciled
- [x] `Deserializer.h` / `Deserializer.cpp` — extensible abstract non-POD class kind, public construction, scalar field lifetime, virtual disposal, raw and bulk read semantics, independent value returns, pointer/custom Variant handling, and unsigned relative seeking reconciled
- [x] `File.h` / `File.cpp` — extensible non-POD class kind, standalone POD `FileMode` enum, owned handle and buffer lifetimes, exact close/reset ordering, package metadata copy, deterministic transient-file teardown, and independent binary returns reconciled
- [x] `FileIdentifier.h` / `FileIdentifier.cpp` — mutable non-POD value-struct kind, owned string copy semantics, exact default/URI/path sanitization, operators, independent value returns, and canonical/resource-routing copies reconciled
- [-] `FileSystem.h` — **EXCLUDED** · reason: use standard library functionality
- [-] `MacFileWatcher.h` — **EXCLUDED** · reason: macOS FSEvents backend · replaced-by: `FileSystemWatcher`
- [-] `RWOpsWrapper.h` — **EXCLUDED** · reason: SDL_RWops C++ adapter · replaced-by: generated SDL interop

## [x] A5. Resource — depends on IO, Core

- [x] `BinaryFile.h` / `BinaryFile.cpp` — extensible non-POD class kind, factory construction, owned VectorBuffer teardown, callback/archive RAII, byte-copy semantics, borrowed stream views, and reflection registration reconciled
- [~] `Resource.h` / `Resource.cpp` — concrete Resource and ResourceWithMetadata class kinds and public constructor access synchronized; complete class-wide lifetime/copy audits remain pending
- [~] `ResourceCache.h` / `ResourceCache.cpp` — temporary-resource empty-name/null behavior and canonical VFS sanitization reconciled while auditing LightProbeGroup; complete class-wide lifetime/copy audit remains pending

## [~] A6. Scene — depends on Resource, Core

- [~] `Node.h` / `Node.cpp` — listener ownership and expired-listener cleanup reconciled while auditing CameraOperator; the complete Node lifetime/copy audit remains pending

## [x] A7. RenderAPI — depends on Core, Math (current effort)

- [x] `ConstantBufferCollection.h` — extensible collection class, mutable 12-byte POD block reference, native field/default order, reusable owned buffer storage, exact block allocation and parameter copies, full GPU-buffer upload range, approved managed `int` indices/sizes, and automatic lifetime reconciled
- [~] `DeviceObject.h` / `DeviceObject.cpp` — destructor unregisters from `RenderDevice` deterministically; complete class-wide lifetime/copy audit remains pending
- [~] `RawTexture.h` / `RawTexture.cpp` and `RenderAPIDefs.h` — SamplerStateDesc inline value type, native field order/defaults, independent assignment/return, and immutable-sampler value insertion reconciled; complete class-wide audits remain pending

## [x] A8. Shader — depends on Core, IO (feeds Graphics)

## [~] A9. Graphics — depends on RenderAPI, Scene, Resource, Shader

- [~] `AnimatedModel.h` / `AnimatedModel.cpp` — constructor, factory registration, weak animation-state ownership, animator replacement, and last-model bone-hierarchy destruction reconciled; final lifetime/copy closure depends on pending SoftwareModelAnimator, Model morph, StaticModel, and Node removal audits
- [~] `Animation.h` / `Animation.cpp` — constructor/factory and owned trigger/track teardown reconciled; trigger, transform-track, and variant-track copies are independent, while final closure depends on ResourceWithMetadata, Variant call sites, and AnimationController pointer mapping
- [~] `AnimationController.h` / `AnimationController.cpp` — constructor, controller-owned teardown, animation-instance value copies, deferred state synchronization, model/bone track connection, renderer statistics, and attribute subspan serialization reconciled; final lifetime/copy closure depends on AnimationStateSource/AnimationState, Variant, AnimatedModel, and borrowed trigger-pointer audits
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
- [x] `DrawableEvents.h` — all five struct event payloads preserve declaration order and borrowed object pointers; ParticleEffect typing and independent Variant trigger-data assignment reconciled
- [x] `Geometry.h` / `Geometry.cpp` — retained stream/dependency resize semantics, shared buffer and raw-array ownership, deterministic subscription and ID teardown, allocation-free shadow-data access, and non-indexed ray fallback reconciled
- [x] `Model.h` / `Model.cpp` — POD/descriptor value copies, packed morph-data aliasing, deep clone behavior, synchronous and staged loading, UMD3 saving, metadata sidecars, geometry/range shape, and default shared-reference teardown reconciled
- [x] `ModelView.h` / `ModelView.cpp` — container-backed value copies, metadata ownership/destruction, exact clear semantics, import/export staging, headless buffers, morph splitting, geometry transforms, and bone processing reconciled
- [x] `OcclusionBuffer.h` / `OcclusionBuffer.cpp` — construction defaults, reverse-order owned-buffer teardown, shared thread-buffer storage, borrowed batch data, value-copy boundaries, and logical buffer view reconciled
- [x] `Octree.h` / `Octree.cpp` — owned child-octant destruction and root relocation, recursive whole-tree detachment, borrowed drawable/zone aliases, delayed transform values, threaded updates, recursive queries, and deterministic reverse-order teardown reconciled
- [x] `OctreeQuery.h` / `OctreeQuery.cpp` — query-object class kinds, POD result structs, independent value copies, borrowed result/pointer aliases, and builtin ray-result storage ownership reconciled
- [x] `OutlineGroup.h` / `OutlineGroup.cpp` — component class kind, POD material-cache keys, independent VariantVector assignment, borrowed drawable/texture aliases, owned artificial materials, and reverse-order teardown reconciled
- [x] `ParticleEffect.h` / `ParticleEffect.cpp` — reference resource class, POD color/texture frame structs and defaults, independent frame-vector assignment and cloning, borrowed material aliasing, XML temporary lifetimes, and reverse-order teardown reconciled
- [x] `ParticleEmitter.h` / `ParticleEmitter.cpp` — non-copyable reference component, POD particle value type and zero initialization, owned particle-vector teardown, shared effect aliasing and reload observation, exact attributes, emission defaults, and event ordering reconciled
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
- [x] `Skeleton.h` / `Skeleton.cpp` — class kinds, non-POD Bone identity, independent Bone/Skeleton value copies, Define node-alias clearing, Model assignment/clone semantics, hierarchy ordering, and deterministic owned-Skeleton teardown reconciled
- [x] `Skybox.h` / `Skybox.cpp` — extensible noncopyable class kind, borrowed camera identity, stable per-camera transform aliases, insert-first frame semantics, reflected defaults, and deterministic map teardown reconciled
- [x] `SoftwareModelAnimator.h` / `SoftwareModelAnimator.cpp` — aggregate animation-data value type, independent owned-array copies, noncopyable animator identity, borrowed published collections, reduced-bone ordering, and deterministic cloned-resource teardown reconciled
- [x] `StaticModel.h` / `StaticModel.cpp` — POD geometry-data value type, extensible noncopyable model identity, stable borrowed transform alias, cached material attributes, model reload ownership, and deterministic reverse-order teardown reconciled
- [x] `StaticModelGroup.h` / `StaticModelGroup.cpp` — extensible noncopyable class kind, weak instance-node observations, VariantVector node-ID cache, contiguous transform aliases, ray/LOD behavior, listener invalidation, and deterministic teardown reconciled
- [x] `Tangent.h` / `Tangent.cpp` — namespace-function shape, borrowed raw-buffer spans, aliased temporary tangent halves, byte-width index reads, and in-place tangent generation verified
- [x] `Technique.h` / `Technique.cpp` — Pass/Technique extensible class kinds, noncopyable Pass semantics, exact state/hash/vector behavior, Technique deep-copy/cache ownership, XML RAII, sparse lookup, lazy pass indices, and deterministic teardown reconciled
- [x] `Terrain.h` / `Terrain.cpp` — extensible noncopyable class kind, weak patch/neighbor observations, shared index-buffer aliases, height-array ownership, nullable node paths, event teardown, and allocation-free propagation reconciled
- [x] `TerrainPatch.h` / `TerrainPatch.cpp` — extensible noncopyable class kind, weak owner/neighbor observations, shared geometry/buffer aliases, inherited transform storage, and derived-before-base teardown reconciled
- [~] `Texture.h` / `Texture.cpp` — concrete extensible noncopyable class kind, public Resource-chain constructors, pre-GPU POD sampler state, exact sampler mutations/XML lookup, max-LOD initialization, shared backup alias release, hardware-format block sizing/components, and created-format SRGB queries synchronized; complete field, body, and derived-texture audit remains pending
- [x] `Texture2D.h` / `Texture2D.cpp` — extensible noncopyable class kind, derived load-image ownership and reverse-order teardown, exact native flags/multisample API, RawTexture parameter forwarding, image temporary lifetime, and complete method bodies reconciled
- [x] `Texture2DArray.h` / `Texture2DArray.cpp` — extensible noncopyable class kind, exact field and teardown order, dependency-loading ownership, native flags-only array sizing, direct raw update/read semantics, image RAII, and complete method bodies reconciled
- [x] `Texture3D.h` / `Texture3D.cpp` — extensible noncopyable class kind, exact field and teardown order, dependency-loading ownership, native ignored-flags sizing, direct raw update/read semantics, image RAII, and complete method bodies reconciled
- [x] `TextureCube.h` / `TextureCube.cpp` — extensible noncopyable class kind, exact field and teardown order, ImageCube dependency-loading ownership, native flags/multisample sizing, direct raw update/read semantics, image RAII, and complete method bodies reconciled
- [x] `RibbonTrail.h` / `RibbonTrail.cpp` — POD TrailPoint value semantics, noncopyable Drawable identity, nullable point-link mapping, deterministic owned-buffer teardown, and allocation-free index-buffer mapping reconciled
- [x] `VertexBuffer.h` / `VertexBuffer.cpp` — complete VertexBuffer and DynamicVertexBuffer class shapes, extensible noncopyable identity, RawBuffer/DeviceObject composition forwarding, owned buffer replacement/destruction, shadow-data ownership, typed element-mask state, dynamic staging ownership, and affected call paths reconciled
- [x] `Viewport.h` / `Viewport.cpp` — extensible noncopyable reference-class kind, weak scene/camera/eye/cull/pipeline observations, retained pipeline-view ownership, nullable pointer APIs, automatic derived-pipeline selection, and deterministic teardown reconciled
- [x] `Zone.h` / `Zone.cpp` — extensible noncopyable reference-class kind, POD reflection-probe mapping, retained texture and weak gradient-zone ownership, cache storage/copies, resource subscription, octree invalidation, and deterministic teardown reconciled
- [~] `GlobalIllumination.h` / `GlobalIllumination.cpp` — owned mesh/baked-data reset semantics, world-origin mutation, debug geometry, reflected attributes, nullable resource loading, binary-compatible GI serialization, and default destruction reconciled; the reload editor action remains `TODO: port` until ObjectReflection supports actions
- [x] `Graphics.h` / `Graphics.cpp` — construction-context and SDL ownership, device-before-window close/destruction, frame and resize events, cached shader observation, independent window-settings copies, and managed native COM/unmanaged-storage teardown reconciled
- [x] `RenderBackend` in `RenderAPIDefs.h` — duplicate `GraphicsBackend` removed; exact enum values are used directly and the automatic backend request preserves C++ optional-value semantics through nullable `RenderBackend`
- [x] `GraphicsDefs.h` / `GraphicsDefs.cpp` — enum values and aliases, `VertexElement` default/parameterized construction and independent value copies, immutable shader hashes/vector constant, and const legacy-element/type-size tables reconciled
- [x] `GraphicsEvents.h` — all 16 event payload shapes and value/borrowed-pointer semantics, render-surface/texture sender selection, default and stereo view event ordering, and frame-level versus per-view rendering boundaries reconciled
- [x] `GraphicsUtils.h` / `GraphicsUtils.cpp` — fixed-capacity vertex-buffer/input-layout temporaries, borrowed geometry and resource aliases, draw-queue binding, backup-texture propagation, and null-texture diagnostics reconciled
- [x] `Image::Save`, `Image::SavePNG`, and `Image::SaveBMP` in `Image.h` / `Image.cpp` — non-source `ImageFileWriter` removed; generated stb encoding, original component count, compressed/empty guards, and deterministic native PNG-buffer release reconciled (complete `Image` class audit remains pending)
- [x] `IndexBuffer.h` / `IndexBuffer.cpp` — stable composed RawBuffer identity, device flags, pending shadow state, raw shadow-data access, offset-correct pack/update flow, pipeline hash, dynamic growth/commit behavior, and deterministic owned-buffer teardown reconciled
- [~] `Light.h` / `Light.cpp` — parameter POD value copies, constructor defaults, shared texture ownership, setter validation, dirty propagation, resource attributes, and virtual ray queries reconciled; the borrowed `LightBatchQueue` API remains `TODO: port` until that dependency exists
- [~] `LightBaker.h` / `LightBaker.cpp` — non-GLOW construction, settings value semantics, update subscription, task-data ownership, future waiting/release, and disabled-build control flow reconciled; GLOW baking fields and the dynamic reflection action remain `TODO: port`
- [x] `LightBakingSettings.h` — all eleven settings aggregates, defaults, constructors, independent nested value copies, immutable string mappings, and trivial destruction verified
- [x] `LightProbeGroup.h` / `LightProbeGroup.cpp` — probe POD values, deep collection copies, reflected attributes, collection/arrangement/bounds, binary/base64 serialization, baked-data load/save, resource aliases, and default derived teardown reconciled

## [x] A10. RenderPipeline — depends on Graphics, RenderAPI, Scene (current effort)

## [x] A11. Input — depends on Core, Graphics (window)

- [x] `InputEvents.h` — all 29 native event namespaces reconciled with the allowed struct-event format as ordered mutable value-type payloads; scalar, enum, immutable string, borrowed SDL pointer, mutable consumed flag, and `Vector2i` value-copy semantics verified, with native documentation restored
- [x] `InputConstants.h` — all eight native unsigned enum value types, declaration order, SDL-backed values, and trivial lifetime/copy semantics verified
- [x] `Input.h` / `Input.cpp` — extensible noncopyable subsystem and nested value-struct kinds reconciled; joystick initialization now performs the native reset, borrowed joystick wrappers have native temporary scope, joystick container copies are independent, acquired SDL state is release-safe across later initialization failure, and deterministic disposal releases the managed SDL resources corresponding to native destructor and automatic field teardown
- [x] `AxisAdapter.h` / `AxisAdapter.cpp` — extensible copyable non-POD class kind, independent scalar cloning at every native copy-construction site, optional archive defaults, and pedal/trigger local-copy flow reconciled
- [x] `DirectionAggregator.h` / `DirectionAggregator.cpp` — extensible noncopyable class and nested value-struct kinds, subsystem construction, incremental subscriptions, input aggregation, joystick filtering, touch filtering, and unsorted axis-state lifetime semantics reconciled
- [x] `DirectionalPadAdapter.h` / `DirectionalPadAdapter.cpp` — extensible noncopyable outer class, copyable non-POD aggregated-state cloning, exact source ordinals, subsystem construction, incremental subscriptions, and synthesized directional event lifetimes reconciled
- [x] `FreeFlyController.h` / `FreeFlyController.cpp` — extensible noncopyable component and nested value-struct kinds, factory registration, owned multitouch-adapter teardown, core-update subscriptions, raw attributes, device filtering, and movement value semantics reconciled
- [x] `InputMap.h` / `InputMap.cpp` — extensible noncopyable resource, helper POD structs, copyable non-POD action value, deep vector cloning at native value-copy sites, metadata ownership, reverse-order teardown, serialization, loading, and native mapping/evaluation quirks reconciled
- [x] `MoveAndOrbitComponent.h` / `MoveAndOrbitComponent.cpp` — extensible noncopyable logic component, value-only field semantics, factory registration, node rotation initialization, virtual setters, pitch clamp, distance limits, and default derived teardown reconciled
- [x] `MoveAndOrbitController.h` / `MoveAndOrbitController.cpp` — extensible noncopyable component, explicit empty destructor, retained input-map reference, borrowed component/UI aliases, input-end subscriptions, touch-state value flow, and movement/orbit evaluation reconciled; excluded built-in UI members remain exact TODOs
- [x] `MultitouchAdapter.h` / `MultitouchAdapter.cpp` — default-int event enum, nested POD touch value, extensible noncopyable adapter, inherited event teardown, subscription transitions, input-touch filtering, unsorted erasure, and gesture payload value flow reconciled
- [x] `PointerAdapter.h` / `PointerAdapter.cpp` — unsigned mask value enum, extensible noncopyable adapter, owned direction-adapter teardown, subsystem construction, incremental subscriptions, touch ownership, pointer value conversion, synthesized events, and native warp-only mouse positioning reconciled; excluded built-in UI members remain exact TODOs
- [x] `Log.h` / `Log.cpp` — enum and struct value kinds, copyable logger-wrapper cloning, noncopyable subsystem ownership and teardown, native logger call shape, threaded queue pumping, duplicate filtering, file parameters, and actual main-thread/end-frame `LogMessage` event emission reconciled
- [x] `IDearImGuiInputSink.cs` — accepted stateless assembly-boundary facade for native `SystemUI::OnRawEvent` under the allowed `SystemUI` → `DearImGui` replacement; no owned or copied state

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
