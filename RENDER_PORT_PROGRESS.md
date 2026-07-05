# Render Port Progress

Keep this file concise. Remove stale guesses, completed low-value notes, and superseded plans.

## Goal

Incrementally port rbfx `Source/Urho3D/RenderAPI` and `Source/Urho3D/RenderPipeline` into RedBolt without losing session context.

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

- Start replacing the legacy scene draw embedded in `Graphics.RenderViewport` with pipeline-owned scene-processing and pass execution stages.
- Continue from the minimal `RenderBufferManager` into real offscreen render-buffer allocation and pass-to-pass data flow.
- Port concrete render passes or scene-processing stages so `RenderPath` content does more than no-op bookkeeping.
- Keep rebuilding with `cmake --build --preset Debug --target redbolt-csharp` after each slice.
