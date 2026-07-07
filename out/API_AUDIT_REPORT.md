# rbfx ↔ RedBolt Render API Audit Report

Generated: 2026-07-07

## Goal

RedBolt should match rbfx RenderAPI and RenderPipeline surface area as closely as possible. Idiomatic C# substitutions are acceptable, but missing rbfx types or materially different public APIs are not.

## Scope Of This Pass

This report is a verified snapshot, not a historical summary.

- Verified against current rbfx headers in `Source/Urho3D/RenderAPI` and `Source/Urho3D/RenderPipeline`.
- Verified against the current RedBolt working tree after commit `3788ecc` (`Port RawShader and ShaderBytecode groundwork`).
- Old percentage tables and stale class totals were removed instead of carried forward.

## Executive Summary

- The RenderAPI object model is no longer missing its foundational device/context layer, but that slice is still thin:
  - `DeviceObject`
  - `RenderDevice`
  - `RenderContext`
  - `RenderScope`
  - `RenderPool`
  - `DrawCommandQueue`
- The shader/resource identity layer is no longer entirely absent:
  - `ShaderBytecode`
  - `RawShader`
- Some of the previously missing support scaffolding is now present too:
  - `ConstantBufferCollection`
  - `ConstantBufferCollectionRef`
  - `RenderDeviceSettingsVulkan`
  - `RenderDeviceSettingsD3D12`
  - `RenderPoolSettings`
- The pipeline-state descriptor/cache layer now exists and now keys on `RawShader`, but it is still a managed surface layer, not a real backend-integrated RenderAPI implementation:
  - `PipelineStateDesc`
  - `PipelineStateCache`
  - `GraphicsPipelineStateDesc`
  - `ComputePipelineStateDesc`
- `Graphics` still absorbs work that rbfx splits across `RenderDevice`, `RenderContext`, `RenderPool`, `DrawCommandQueue`, shader reflection/resource types, and pipeline-state/cache infrastructure. That decomposition remains mandatory for parity.
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
| `PipelineStateDesc` | Present, incomplete | Wrapper exists with cloning/hash semantics and now keys on `RawShader`, but it still lacks real backend PSO/reflection integration |
| `PipelineStateCache` | Present, incomplete | Subsystem exists and deduplicates managed pipeline-state objects, but it is not connected to Diligent pipeline-state cache or real backend PSO creation |
| `GraphicsPipelineStateDesc` | Present, incomplete | Value surface exists and now accepts `RawShader`, but it still carries only managed equality/hash semantics |
| `ComputePipelineStateDesc` | Present, incomplete | Value surface exists and now accepts `RawShader`, but it still carries only managed equality/hash semantics |
| `ConstantBufferCollection` | Present, incomplete | Packing/allocation helper exists, but it is not wired into a real draw-command recording path yet |
| `RawTexture` | Missing | Texture work is still in `Texture` / `Graphics` |
| `RawBuffer` | Missing | Buffer work is still in `VertexBuffer`, `IndexBuffer`, and `Graphics` |
| `RawShader` | Present, incomplete | Exists as a `DeviceObject` that owns bytecode and native shader handles, but reflection/loading/integration are still sparse |
| `RenderTargetView` | Present, incomplete | See detailed deviations below |
| `RenderPool` | Present, incomplete | Type exists, but lifecycle methods are no-op placeholders |
| `ShaderProgramReflection` | Missing | No equivalent type exists |
| `DrawCommandQueue` | Present, incomplete | Type exists, but currently carries only clip-plane state and context execution glue |
| `ShaderBytecode` | Present, incomplete | Exists with MIME/bytecode/vertex-attribute serialization, but the wider reflection/program pipeline is still absent |

## Verified RenderAPI Supporting-Type Gaps

### Missing structs / aliases / helper types

- `RenderDeviceSettingsVulkan`
- `RenderDeviceSettingsD3D12`
- `RawTextureParams`
- `RawTextureUAVKey`
- `RawBufferParams`
- `DrawCommandDescription`
- `UniformBufferReflection`
- `UniformReflection`
- `ShaderResourceReflection`
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
- The current managed implementation now uses `RawShader` as the shader identity surface, with special-case equality/hash behavior for `ShaderVariation`.
- That closes the previously verified type-shape gap, but it is still not rbfx parity because upstream also carries backend-facing pipeline-state creation, shader-program reflection, and richer resource-binding metadata.

### `PipelineStateCache`

Verified current state:

- The cache now exists as a registered engine subsystem and deduplicates `PipelineState` objects by `PipelineStateDesc`.
- Cache invalidation/restore currently only forwards managed lifecycle events to cached `PipelineState` objects.
- The cache does not yet create or own a Diligent GPU pipeline-state cache blob, and it is not wired into RedBolt’s live pipeline creation paths in `Graphics`.

### `RawShader`

Verified current state:

- `RawShader` now exists as a `DeviceObject` and owns:
  - shader type
  - `ShaderBytecode`
  - optional native Diligent `IShader` handle
- `CreateFromBinary(...)`, `GetBytecode()`, `GetShaderType()`, and `GetHandle()` now exist.
- Current GPU creation is still thin:
  - it reaches into `Graphics` for the native device instead of being owned by a fuller RenderAPI backend layer
  - it supports current shader creation from stored GLSL source or binary blobs, but there is still no upstream-style shader reflection/program reflection surface around it

### `ShaderBytecode`

Verified current state:

- `ShaderBytecode` now exists with:
  - shader type
  - MIME
  - raw bytecode payload
  - vertex-attribute metadata
  - stream save/load helpers
  - equality/hash behavior
- This closes the earlier “missing type” finding, but the surrounding reflection/resource pipeline is still incomplete because upstream also couples bytecode to richer shader-program reflection and cache integration.

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
- `RenderPoolSettings` now exists and the managed `RenderPool` exposes default settings through a property.
- `Invalidate()`, `Restore()`, and `OnFrameEnd()` are still empty placeholders.

### `ConstantBufferCollection`

Verified current state:

- `ConstantBufferCollection` and `ConstantBufferCollectionRef` now exist.
- The current managed surface supports:
  - alignment-aware block allocation
  - multi-buffer growth
  - typed parameter packing into CPU-side byte buffers
- The larger rbfx integration is still missing:
  - `DrawCommandQueue` does not yet consume it
  - there is no real backend upload/command-recording path tied to these buffers yet

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
- Present rbfx-style sub-objects:
  - `Vulkan`
  - `D3D12`
- These settings carriers exist as managed shape only; they are not yet consumed by live backend creation.

### `ImmutableSamplersDesc`

Verified current state:

- Backing name and descriptor arrays exist internally.
- Public access is closer to rbfx now because names and sampler-descriptor spans are exposed and `NamedSamplerStateDesc` exists.
- The remaining gap is that the current surface is still a managed helper class rather than a direct port of the exact upstream alias/container shape.

### `NamedSamplerStateDesc`

Verified current state:

- RedBolt does expose `NamedSamplerStateDesc` now, as a managed `record struct`.
- That is close enough for intent, so this should no longer be reported as missing.
- The remaining parity work is container-level (`ImmutableSamplersDesc` and descriptor ownership), not the existence of the named sampler pair itself.

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
   - `ShaderProgramReflection`
2. Deepen the new shader/resource slice so it stops at more than bytecode + native-handle ownership:
   - `ShaderProgramReflection`
   - `UniformBufferReflection`
   - `UniformReflection`
   - `ShaderResourceReflection`
3. Deepen the new device/context layer so it stops being mostly pass-through glue:
   - move more bind/cache/state ownership from `Graphics` into `RenderContext`
   - flesh out `RenderPool`
   - flesh out `DrawCommandQueue`
4. Connect the new pipeline-state cache layer to real backend pipeline creation instead of leaving all PSO ownership inside `Graphics`.
5. Add resource-layer types:
   - `RawTexture`
   - `RawBuffer`
6. Connect the new support scaffolding to real recording/binding paths:
   - `ConstantBufferCollection`
   - `DrawCommandDescription`
   - `DrawCommandQueue`

## Working Rule

Do not extend the `Graphics` monolith as the long-term solution. New rendering work should move behavior into rbfx-named types until `Graphics` becomes a thin integration point instead of a substitute for the missing RenderAPI layer.
