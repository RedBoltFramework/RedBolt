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
- real sample verification exposed two regressions introduced during the managed pipeline work and both are now fixed:
  - CPU-backed textures were created on GPU with full rbfx mip counts but only uploaded level 0, which made mipmapped sampling go black; managed GPU uploads now keep CPU-backed textures single-mip until full subresource upload is implemented
  - render-pipeline offscreen color buffers were being paired with the viewport depth target; managed `RenderBufferManager` now allocates a substitute depth buffer for read/write offscreen frames, matching the upstream rbfx substitute-buffer path more closely
- `RedBolt.Sample.FirstCube` and `RedBolt.Sample.StaticScene` now both exit cleanly again and produce non-black screenshots with `--screenshot-on-exit`
- core event/object parity moved closer to rbfx:
  - `Object`/`Context` now support sender-scoped bulk unsubscribe, block-events gating, and context global vars
  - named-event dispatch no longer snapshots subscriptions with `ToArray()` on every send; subscription removal is deferred and compacted after dispatch instead
- render-pipeline mutation behavior is closer to upstream rbfx:
  - `RenderPipeline` now raises managed settings/parameters/render-path change events, ignores no-op render-path parameter updates, and no longer dirties revision state for frame shader parameter writes
  - `RenderPath.RepairEnabledRenderPasses` now preserves already-matching pass arrays without copying, and `PipelineStateOutputDesc.RenderTargetFormats` no longer allocates on access
- low-level managed hot-path cleanup in this slice also includes reusing `CommonFrameInfo` objects across `Define()` calls and making `RenderBufferManagerFrameSettings` a value type
- SharpCpp fixed-size array support now uses a shared `SharpCpp.Support.NativeArray<T>` plus generated per-field proxy accessors for handle-backed and nested fixed arrays, so bindings no longer emit duplicate `NativeArray<>` types per assembly
- fixed-array fields like Diligent `BlendStateDesc.RenderTargets[8]` can now be configured from managed code through item getter/setter thunks instead of raw memory aliasing
- `cmake --build --preset Debug --target redbolt-generate-native-bindings` and `cmake --build --preset Debug --target redbolt-csharp` are green again after the array-support slice
- sample runtime is currently blocked in this sandbox by `SDL_Init` failing before window/context creation, so render verification still needs a working local graphics session outside the restricted shell

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
- Port the execution side of the remaining built-in passes (`BloomPass`, `AmbientOcclusionPass`, `OutlineRenderPass`) on top of the new fullscreen-quad path.
- Replace the current direct scene draw in `Graphics.RenderScene` with pipeline-owned processors and batching once the post-process/output plumbing is in place.
- Expand the custom-uniform path toward typed parameter structs for built-in passes so reflection-based field layout can be cached once and reused without hot-path reflection.
- Replace the current substitute-depth and fullscreen-copy implementation with a closer port of upstream `RenderBuffer` / `RenderBufferManager` semantics, including proper depth/readback formats and `DrawTextureRegion`-style color-space-aware copies.
- Keep rebuilding with `cmake --build --preset Debug --target redbolt-csharp` after each slice.
