# Render Port Progress

Keep this file concise. Remove stale guesses, completed low-value notes, and superseded plans.

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
- focused unit coverage was expanded again for readable-depth trait aggregation, frame-derived depth allocation, bloom buffer sizing, managed custom-uniform parsing/packing, outline-material rewriting, render-buffer rebinding, the normal-buffer shader/publication path, and the new explicit output-target matching behavior; `dotnet test tests/RedBolt.Tests/RedBolt.Tests.csproj --configuration Debug --no-restore` is green again with `309/309` passing after the latest `ReadableColor` parity cleanup
- sample validation for this checkpoint used `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-outputtarget-2.png`, and the captured frame shows the scene geometry rendering correctly again after restoring the swap-chain resolve path
- sample validation was rerun again after the `SwapColorBuffers` cleanup using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-swapbuffers-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after allowing matching texture outputs to remain primary for color read/write frames using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-direct-output-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the readable-depth partial-output fix using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-readabledepth-1.png`, and the captured frame remained visually stable
- sample validation was rerun again after the `ReadableColor` buffer-allocation cleanup using `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-readablecolor-1.png`, and the captured frame remained visually stable
- SharpCpp fixed-size array support now uses a shared `SharpCpp.Support.NativeArray<T>` plus generated per-field proxy accessors for handle-backed and nested fixed arrays, so bindings no longer emit duplicate `NativeArray<>` types per assembly
- fixed-array fields like Diligent `BlendStateDesc.RenderTargets[8]` can now be configured from managed code through item getter/setter thunks instead of raw memory aliasing
- `cmake --build --preset Debug --target redbolt-generate-native-bindings` and `cmake --build --preset Debug --target redbolt-csharp` are green again after the array-support slice
- sample verification was rerun against the current render-pipeline path on `2026-07-06`; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-outline-3.png` completed successfully after the managed outline-producer slice and produced a valid `1280x720` PNG with the expected mushroom field, explicit foreground outline test geometry, and black background (the sample does not create a `Zone` or `Skybox`)
- sample verification was rerun again on `2026-07-06` after the normal-buffer slice; `out/build/dev/bin/Debug/RedBolt.Sample.StaticScene --timeout 2 --screenshot-on-exit /tmp/redbolt-staticscene-normalbuffer-1.png` completed successfully and still produced a valid `1280x720` frame with the expected mushroom field and ground plane, confirming that the added normal prepass did not regress the visible sample output

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

- Continue from the new pipeline-owned scene-render seam into real offscreen output selection and pass-to-pass data flow.
- Replace the current direct scene draw in `Graphics.RenderScene` with pipeline-owned processors and batching once the post-process/output plumbing is in place.
- Replace the current `Graphics.RenderScene`/`RenderOutlineGroups` direct loops with a closer port of rbfx drawable processing and scene-pass orchestration once more of the higher-level pipeline stack is in place.
- Expand the custom-uniform path toward typed parameter structs for built-in passes so reflection-based field layout can be cached once and reused without hot-path reflection.
- Continue replacing the remaining substitute-depth and multisample gaps with closer ports of upstream `RenderBuffer` / `RenderBufferManager` semantics now that explicit output-target matching and color-space-aware resolves are in place.
- Keep rebuilding with `cmake --build --preset Debug --target redbolt-csharp` after each slice.
