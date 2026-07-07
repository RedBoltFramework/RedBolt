# rbfx ↔ RedBolt Render API Audit Report

Generated: 2026-07-07

## Goal

RedBolt should match rbfx RenderAPI and RenderPipeline surface area as closely as possible. Idiomatic C# substitutions are acceptable, but missing rbfx types or materially different public APIs are not.

## Scope Of This Pass

This report is a verified snapshot, not a historical summary.

- Verified against current rbfx headers in `Source/Urho3D/RenderAPI` and `Source/Urho3D/RenderPipeline`.
- Verified against the current RedBolt working tree after the pipeline-state-cache groundwork slice (post-`090daea` working tree).
- Old percentage tables and stale class totals were removed instead of carried forward.

## Executive Summary

- The RenderAPI object model is no longer missing its foundational device/context layer, but that new slice is still thin:
  - `DeviceObject`
  - `RenderDevice`
  - `RenderContext`
  - `RenderScope`
  - `RenderPool`
  - `DrawCommandQueue`
- The pipeline-state descriptor/cache layer now exists, but it is still a managed surface layer, not a real backend-integrated RenderAPI implementation:
  - `PipelineStateDesc`
  - `PipelineStateCache`
  - `GraphicsPipelineStateDesc`
  - `ComputePipelineStateDesc`
- `Graphics` still absorbs work that rbfx splits across `RenderDevice`, `RenderContext`, `RenderPool`, `DrawCommandQueue`, and pipeline-state/cache infrastructure. That decomposition remains mandatory for parity.
- `RenderAPIDefs.cs` is farther along than the class layer, but it still has verified enum and descriptor gaps.
- RenderPipeline settings/types are mostly present, but there are still verified API deviations in shared definitions.

## Verified RenderAPI Class Inventory

| rbfx type | RedBolt status | Notes |
|---|---|---|
| `DeviceObject` | Present, incomplete | Shared lifecycle base exists, but the wider object graph above it is still sparse |
| `RenderDevice` | Present, incomplete | Subsystem exists, but most backend behavior remains in `Graphics` |
| `RenderContext` | Present, incomplete | Core bind/viewport/clear getters exist, but it is still a thin `Graphics` adapter |
| `RenderScope` | Present, incomplete | Helper exists, but it is effectively a no-op while debug scopes are disabled |
| `PipelineState` | Present, incomplete | Now carries descriptor/type access and cache ownership, but still has no real GPU/reflection state |
| `PipelineStateDesc` | Present, incomplete | Wrapper exists with cloning/hash semantics, but it still uses RedBolt shader variations instead of rbfx `RawShader` |
| `PipelineStateCache` | Present, incomplete | Subsystem exists and deduplicates managed pipeline-state objects, but it is not connected to Diligent pipeline-state cache or real backend PSO creation |
| `GraphicsPipelineStateDesc` | Present, incomplete | Value surface exists, but it still uses RedBolt shader variations instead of rbfx `RawShader` |
| `ComputePipelineStateDesc` | Present, incomplete | Value surface exists, but it still uses RedBolt shader variations instead of rbfx `RawShader` |
| `ConstantBufferCollection` | Missing | No class exists |
| `RawTexture` | Missing | Texture work is still in `Texture` / `Graphics` |
| `RawBuffer` | Missing | Buffer work is still in `VertexBuffer`, `IndexBuffer`, and `Graphics` |
| `RawShader` | Missing | No equivalent type exists |
| `RenderTargetView` | Present, incomplete | See detailed deviations below |
| `RenderPool` | Present, incomplete | Type exists, but lifecycle methods are no-op placeholders |
| `ShaderProgramReflection` | Missing | No equivalent type exists |
| `DrawCommandQueue` | Present, incomplete | Type exists, but currently carries only clip-plane state and context execution glue |
| `ShaderBytecode` | Missing | No equivalent type exists |

## Verified RenderAPI Supporting-Type Gaps

### Missing structs / aliases / helper types

- `RenderDeviceSettingsVulkan`
- `RenderDeviceSettingsD3D12`
- `ConstantBufferCollectionRef`
- `RawTextureParams`
- `RawTextureUAVKey`
- `RawBufferParams`
- `RenderPoolSettings`
- `DrawCommandDescription`
- `UniformBufferReflection`
- `UniformReflection`
- `ShaderResourceReflection`
- `ShaderBytecode`
- `OptionalRawTextureRTV`

## Verified Deviations In Existing RenderAPI Types

### `PipelineState`

Current RedBolt `PipelineState` still only exposes:

- `DebugName`
- `IsValid`
- descriptor/type/cache-owner access

It now at least inherits `DeviceObject`, so invalidate/restore/destroy routing is no longer missing entirely. However, the rbfx class also carries:

- real pipeline-state construction from `PipelineStateDesc`
- `DeviceObject` lifecycle (`Invalidate`, `Restore`, `Destroy`)
- descriptor accessors
- reflection access
- GPU handle access
- shader-resource-binding access

This is still a placeholder, not a port.

### `PipelineStateDesc` / `GraphicsPipelineStateDesc` / `ComputePipelineStateDesc`

Verified current state:

- All three descriptor surfaces now exist with hash/equality behavior and defensive cloning of nested mutable descriptor state.
- The current managed implementation uses `ShaderVariation` references as the shader identity surface.
- That is enough for managed cache identity work, but it is still not rbfx parity because upstream uses `RawShader` plus backend-facing shader handles/reflection metadata.

### `PipelineStateCache`

Verified current state:

- The cache now exists as a registered engine subsystem and deduplicates `PipelineState` objects by `PipelineStateDesc`.
- Cache invalidation/restore currently only forwards managed lifecycle events to cached `PipelineState` objects.
- The cache does not yet create or own a Diligent GPU pipeline-state cache blob, and it is not wired into RedBolt’s live pipeline creation paths in `Graphics`.

### `RenderContext`

Verified current state:

- `SetSwapChainRenderTargets()`, `SetRenderTargets(...)`, `SetFullViewport()`, `SetViewport(...)`, `ClearDepthStencil(...)`, `ClearRenderTarget(...)`, `Execute(...)`, and the rbfx-style current-target getters now exist.
- Current render-target size, viewport, and output-desc queries are delegated to `Graphics`, not owned by a real backend-facing RenderContext cache yet.
- Only a single color render target is currently supported; multi-render-target binding still throws.
- Debug groups exist only as no-op shells because `IsDebugScopeEnabled()` is hardcoded `false`.

### `RenderScope`

Verified current state:

- The helper type now exists with both `string` and formatted-name constructors.
- Because `RenderContext.IsDebugScopeEnabled()` is currently `false`, the type is effectively a no-op RAII wrapper rather than a real debug-group scope.

### `RenderPool`

Verified current state:

- The type now exists and is reachable from `RenderDevice` and `RenderContext`.
- `Invalidate()`, `Restore()`, and `OnFrameEnd()` are still empty placeholders.

### `DrawCommandQueue`

Verified current state:

- The type now exists and is reachable from `RenderDevice`.
- Current behavior is limited to storing a clip-plane mask and replaying that mask into `RenderContext.Execute(...)`.
- rbfx command recording/execution surfaces are still otherwise missing.

### `RenderTargetView`

Verified current state:

- `GetMultiSample()` now correctly follows the backing texture multisample count.
- `GetView()` is still missing.
- The static constructors still accept `Texture` or no argument, while rbfx uses `RawTexture*` / `RenderDevice*`.
- `OptionalRawTextureRTV` is still missing.

### `RenderDeviceSettings`

Verified current state:

- Present fields:
  - `Backend`
  - `ExternalWindowHandle`
  - `GpuDebug`
  - `AdapterId`
- Missing rbfx sub-objects:
  - `Vulkan`
  - `D3D12`

### `ImmutableSamplersDesc`

Verified current state:

- Backing name and descriptor arrays exist internally.
- Public access is closer to rbfx now because names and sampler-descriptor spans are exposed and `NamedSamplerStateDesc` exists.
- The remaining gap is that the current surface is still a managed helper class rather than a direct port of the exact upstream alias/container shape.

## Verified RenderPipeline Definition Deviations

### `RenderBufferManagerFrameSettings`

rbfx currently has two fields:

- `readableColor_`
- `supportColorReadWrite_`

RedBolt currently exposes four properties:

- `ReadableColor`
- `SupportColorReadWrite`
- `FilteredColor`
- `ReadableDepth`

The extra `FilteredColor` and `ReadableDepth` fields need justification from upstream parity work or they should be folded into a different rbfx-aligned surface.

### `NamedSamplerStateDesc`

rbfx defines:

- `using NamedSamplerStateDesc = ea::pair<ea::string, SamplerStateDesc>;`

RedBolt does not currently expose an equivalent alias or record.

## Types Verified As Already Present

These were stale false positives in the old audit and should not be reported missing:

- `BlendMode`
- `CompareMode`
- `CullMode`
- `FillMode`
- `PrimitiveType`
- `TextureAddressMode.Border`

## Priority Order For The Next Port Slices

1. Replace the current descriptor-layer shader identity with real rbfx shader/resource types:
   - `RawShader`
   - `ShaderBytecode`
   - `ShaderProgramReflection`
2. Deepen the new device/context layer so it stops being mostly pass-through glue:
   - move more bind/cache/state ownership from `Graphics` into `RenderContext`
   - flesh out `RenderPool`
   - flesh out `DrawCommandQueue`
3. Connect the new pipeline-state cache layer to real backend pipeline creation instead of leaving all PSO ownership inside `Graphics`.
4. Add resource-layer types:
   - `RawTexture`
   - `RawBuffer`
5. Add command/reflection infrastructure:
   - `ConstantBufferCollection`

## Working Rule

Do not extend the `Graphics` monolith as the long-term solution. New rendering work should move behavior into rbfx-named types until `Graphics` becomes a thin integration point instead of a substitute for the missing RenderAPI layer.
