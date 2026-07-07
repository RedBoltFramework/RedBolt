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
- `Graphics` now has a managed fullscreen-quad draw path with cached pipeline state, cached custom-uniform layout parsing, and generic shader-backed postprocess execution for render-path passes
- `ToneMappingPass` and generic `FullScreenShaderPass` now execute managed fullscreen postprocess draws instead of only contributing metadata and trait flags
- `RenderBufferManager` now honors `RenderBufferManagerSettings.ReadableDepth` by allocating a readable depth texture even on frames that do not need color ping-pong, and shared render-pass state now exposes that offscreen depth buffer through the normal viewport depth slot
- `Graphics.DrawFullscreenQuad` now has generic managed bindings for `sDepthBuffer` in addition to the existing color inputs, so upstream fullscreen shaders that sample depth can be ported without adding manual interop
- render-path trait plumbing now reaches farther into the managed runtime:
  - per-frame render-buffer requirements now carry readable-depth and filtered-color requests in addition to color read/write
  - `AmbientOcclusionPass` can now request depth-backed frames through that trait path instead of being permanently disabled by the base pipeline settings
- the regular scene-material path now shares the same managed custom-uniform plumbing as fullscreen passes:
  - `Graphics` parses `UNIFORM_BUFFER_BEGIN(6, Custom)` blocks from prepared material shaders, allocates a matching `Custom` constant buffer for geometry pipelines, and binds it on both shader stages when present
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
- focused unit coverage was expanded again for readable-depth trait aggregation, frame-derived depth allocation, bloom buffer sizing, dynamic intermediate-buffer resizing, managed custom-uniform parsing/packing, typed custom-uniform struct uploads, cubemap render-buffer creation, sliced render-target view resolution, outline-material rewriting, render-buffer rebinding, the normal-buffer shader/publication path, explicit output-target matching behavior, and the new partial-clear quad decision/state helpers; `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-build --no-restore` is green again with `328/328` passing after the latest render-target slice-view cleanup
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
  - focused validation for these RenderAPI slices used `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~ContextTests|FullyQualifiedName~RenderDeviceTests|FullyQualifiedName~RenderContextTests|FullyQualifiedName~PipelineStateCacheTests"` and `cmake --build --preset Debug --target redbolt-csharp`; both passed on `2026-07-07`
  - sample validation was rerun for the graphics-touching alias fix on `2026-07-07`; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-pipelinestatecache.png` completed successfully and still produced the expected mushroom field, outlines, ground plane, and black background (the sample still has no `Zone`/`Skybox`)

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

- Replace the still-placeholder shader part of the new pipeline-state surface with rbfx-style resource types:
  - `RawShader`
  - `ShaderBytecode`
  - `ShaderProgramReflection`
- Deepen the new RenderAPI slice by moving more transient draw-state/cache ownership behind `RenderContext`, `DrawCommandQueue`, and `PipelineStateCache` instead of leaving it in ad-hoc `Graphics` helpers.
- Start hooking the new managed `PipelineStateCache` to real pipeline creation paths once the shader/resource-layer slice is present, so `Graphics` stops being the only place where pipeline identity exists.
- Continue from the new pipeline-owned scene-render seam into real offscreen output selection and pass-to-pass data flow.
- Replace the current direct scene draw in `Graphics.RenderScene` with pipeline-owned processors and batching once the post-process/output plumbing is in place.
- Keep rebuilding with `cmake --build --preset Debug --target redbolt-csharp` after each slice.
