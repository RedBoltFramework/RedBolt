namespace RedBolt;

/// <summary>
/// Utility to build pipeline states for RenderPipeline.
/// </summary>
public class PipelineStateBuilder : Object, BatchStateCacheCallback
{
    /// <summary>Objects whose settings contribute to pipeline states.</summary>
    private readonly SceneProcessor? _sceneProcessor;
    private readonly CameraProcessor? _cameraProcessor;
    private readonly ShadowMapAllocator? _shadowMapAllocator;
    private readonly InstancingBuffer? _instancingBuffer;

    private readonly Graphics? _graphics;
    private readonly RenderDevice? _renderDevice;
    private readonly PipelineStateCache? _pipelineStateCache;
    private readonly ShaderProgramCompositor _compositor;

    /// <summary>Re-used objects.</summary>
    private readonly GraphicsPipelineStateDesc _pipelineStateDesc = new();
    private readonly ShaderProgramDesc _shaderProgramDesc = new();

    public PipelineStateBuilder(SceneProcessor? sceneProcessor, CameraProcessor? cameraProcessor,
        ShadowMapAllocator? shadowMapAllocator, InstancingBuffer? instancingBuffer)
    {
        _sceneProcessor = sceneProcessor;
        _cameraProcessor = cameraProcessor;
        _shadowMapAllocator = shadowMapAllocator;
        _instancingBuffer = instancingBuffer;
        _graphics = GetSubsystem<Graphics>();
        _renderDevice = GetSubsystem<RenderDevice>();
        _pipelineStateCache = GetSubsystem<PipelineStateCache>();
        _compositor = new ShaderProgramCompositor();
    }

    public void SetSettings(ShaderProgramCompositorSettings settings) => _compositor.SetSettings(settings);

    public void UpdateFrameSettings(bool linearColorSpace)
    {
        if (_cameraProcessor is not null)
            _compositor.SetFrameSettings(_cameraProcessor, linearColorSpace);
    }

    /// <summary>Implement BatchStateCacheCallback.</summary>
    public PipelineState? CreateBatchPipelineState(
        BatchStateCreateKey key, BatchStateCreateContext ctx, PipelineStateOutputDesc outputDesc)
    {
        if (_sceneProcessor is null || _pipelineStateCache is null || _renderDevice is null)
            return null;

        var caps = _renderDevice.GetCaps();

        var light = key.PixelLight?.GetLight();
        var hasShadow = key.PixelLight is not null && key.PixelLight.HasShadow();

        var batchCompositorPass = _sceneProcessor.GetUserPass(ctx.Pass);
        var isShadowPass = batchCompositorPass is null && ctx.SubpassIndex == BatchCompositor.ShadowSubpass;
        var isLightVolumePass = batchCompositorPass is null && ctx.SubpassIndex == BatchCompositor.LitVolumeSubpass;
        var isRefractionPass =
            batchCompositorPass is not null && batchCompositorPass.IsFlagSet(DrawableProcessorPassFlag.RefractionPass);
        var isStereoPass =
            batchCompositorPass is not null && batchCompositorPass.IsFlagSet(DrawableProcessorPassFlag.StereoInstancing);

        ClearState();

        _pipelineStateDesc.Output.DepthStencilFormat = outputDesc.DepthStencilFormat;
        _pipelineStateDesc.Output.MultiSample = outputDesc.MultiSample;
        _pipelineStateDesc.Output.SetRenderTargetFormats(outputDesc.RenderTargetFormats);

        if (isShadowPass)
        {
            if (key.PixelLight is null || key.Geometry is null || key.Material is null || key.Pass is null)
                return null;

            _compositor.ProcessShadowBatch(_shaderProgramDesc,
                key.Geometry, key.GeometryType, key.Material, key.Pass, light);
            SetupShadowPassState(ctx.ShadowSplitIndex, key.PixelLight, key.Material, key.Pass);

            SetupSamplersForUserOrShadowPass(key.Material, false, false, false);
            SetupInputLayoutAndPrimitiveType(_pipelineStateDesc, _shaderProgramDesc, key.Geometry, false);
            SetupShaders(_pipelineStateDesc, _shaderProgramDesc);
        }
        else if (isLightVolumePass)
        {
            if (key.PixelLight is null || key.Geometry is null || key.Pass is null)
                return null;

            _compositor.ProcessLightVolumeBatch(_shaderProgramDesc,
                key.Geometry, key.GeometryType, key.Pass, light!, hasShadow);
            SetupLightVolumePassState(key.PixelLight);

            SetupLightSamplers(key.PixelLight);
            SetupGeometryBufferSamplers();
            SetupInputLayoutAndPrimitiveType(_pipelineStateDesc, _shaderProgramDesc, key.Geometry, false);
            SetupShaders(_pipelineStateDesc, _shaderProgramDesc);

            _pipelineStateDesc.ReadOnlyDepth = true;
        }
        else if (batchCompositorPass is not null
                 && batchCompositorPass.IsFlagSet(DrawableProcessorPassFlag.PipelineStateCallback))
        {
            batchCompositorPass.CreatePipelineState(_pipelineStateDesc, this, key, ctx);
        }
        else if (batchCompositorPass is not null)
        {
            if (key.Drawable is null || key.Geometry is null || key.Material is null || key.Pass is null)
                return null;

            var flags = batchCompositorPass.GetFlags();
            var subpass = (BatchCompositorSubpass)ctx.SubpassIndex;
            var lightMaskToStencil = subpass == BatchCompositorSubpass.Deferred
                && (flags & DrawableProcessorPassFlag.DeferredLightMaskToStencil) != 0;
            var hasAmbient = (flags & DrawableProcessorPassFlag.HasAmbientLighting) != 0;
            var hasLightmap = key.Drawable.GetGlobalIlluminationType() == GlobalIlluminationType.UseLightMap;

            _compositor.ProcessUserBatch(_shaderProgramDesc, flags, key.Drawable, key.Geometry, key.GeometryType,
                key.Material, key.Pass, light, hasShadow, subpass);
            SetupUserPassState(key.Drawable, key.Material, key.Pass, lightMaskToStencil);

            // Support negative lights
            if (light is not null && light.IsNegative())
            {
                System.Diagnostics.Debug.Assert(subpass == BatchCompositorSubpass.Light);
                if (_pipelineStateDesc.BlendMode == BlendMode.Add)
                    _pipelineStateDesc.BlendMode = BlendMode.Subtract;
                else if (_pipelineStateDesc.BlendMode == BlendMode.AddAlpha)
                    _pipelineStateDesc.BlendMode = BlendMode.SubtractAlpha;
            }

            // Mark depth as read-only if requested and supported
            if (caps.ReadOnlyDepth && (flags & DrawableProcessorPassFlag.ReadOnlyDepth) != 0)
                _pipelineStateDesc.ReadOnlyDepth = true;

            SetupLightSamplers(key.PixelLight);
            SetupSamplersForUserOrShadowPass(key.Material, hasLightmap, hasAmbient, isRefractionPass);
            SetupInputLayoutAndPrimitiveType(_pipelineStateDesc, _shaderProgramDesc, key.Geometry, isStereoPass);
            SetupShaders(_pipelineStateDesc, _shaderProgramDesc);
        }

        return _pipelineStateCache.GetGraphicsPipelineState(_pipelineStateDesc);
    }

    public PipelineState? CreateBatchPipelineStatePlaceholder(
        uint vertexStride, PipelineStateOutputDesc outputDesc)
    {
        if (_graphics is null || _pipelineStateCache is null)
            return null;

        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = $"Pipeline State Placeholder for vertex stride {vertexStride}",
            ColorWriteEnabled = true,
            DepthWriteEnabled = true,
            DepthCompareFunction = CompareMode.Always
        };

        desc.InputLayout.SetElements(
        [
            new InputLayoutElementDesc(
                0,
                vertexStride,
                0,
                0,
                VertexElementType.Vector3,
                VertexElementSemantic.Position,
                0)
        ]);

        desc.Output.DepthStencilFormat = outputDesc.DepthStencilFormat;
        desc.Output.MultiSample = outputDesc.MultiSample;
        desc.Output.SetRenderTargetFormats(outputDesc.RenderTargetFormats);

        desc.VertexShader = _graphics.GetShader(ShaderType.VS, "v2/X_PlaceholderShader", string.Empty);
        desc.PixelShader = _graphics.GetShader(ShaderType.PS, "v2/X_PlaceholderShader", string.Empty);

        return _pipelineStateCache.GetGraphicsPipelineState(desc);
    }

    /// <summary>Helpers for passes that override pipeline state creation.</summary>
    public void SetupInputLayoutAndPrimitiveType(GraphicsPipelineStateDesc pipelineStateDesc,
        ShaderProgramDesc shaderProgramDesc, Geometry geometry, bool isStereoPass)
    {
        if (shaderProgramDesc.IsInstancingUsed)
        {
            GraphicsUtils.InitializeInputLayoutAndPrimitiveType(pipelineStateDesc, geometry,
                _instancingBuffer?.GetVertexBuffer());

            if (isStereoPass)
            {
                // Patch step rates for stereo rendering.
                // TODO: Do we want to have something nicer?
                var elements = pipelineStateDesc.InputLayout.Elements.ToArray();
                for (var i = 0; i < elements.Length; ++i)
                {
                    if (elements[i].InstanceStepRate != 0)
                    {
                        elements[i] = elements[i] with { InstanceStepRate = 2 };
                    }
                }

                pipelineStateDesc.InputLayout.SetElements(elements);
            }
        }
        else
        {
            GraphicsUtils.InitializeInputLayoutAndPrimitiveType(pipelineStateDesc, geometry);
        }
    }

    public void SetupShaders(GraphicsPipelineStateDesc pipelineStateDesc, ShaderProgramDesc shaderProgramDesc)
    {
        if (_graphics is null)
            return;

        for (var i = 0; i < shaderProgramDesc.ShaderDefines.Length; ++i)
            shaderProgramDesc.ShaderDefines[i] += shaderProgramDesc.CommonShaderDefines;

        pipelineStateDesc.VertexShader = _graphics.GetShader(
            ShaderType.VS, shaderProgramDesc.ShaderName[(int)ShaderType.VS],
            shaderProgramDesc.ShaderDefines[(int)ShaderType.VS]);
        pipelineStateDesc.PixelShader = _graphics.GetShader(
            ShaderType.PS, shaderProgramDesc.ShaderName[(int)ShaderType.PS],
            shaderProgramDesc.ShaderDefines[(int)ShaderType.PS]);
    }

    public ShaderProgramCompositor GetShaderProgramCompositor() => _compositor;

    /// <summary>State builder helpers.</summary>
    private void ClearState()
    {
        _pipelineStateDesc.DebugName = string.Empty;
        _pipelineStateDesc.ColorWriteEnabled = false;
        _pipelineStateDesc.BlendMode = default;
        _pipelineStateDesc.AlphaToCoverageEnabled = false;
        _pipelineStateDesc.FillMode = default;
        _pipelineStateDesc.CullMode = default;
        _pipelineStateDesc.ConstantDepthBias = 0;
        _pipelineStateDesc.SlopeScaledDepthBias = 0;
        _pipelineStateDesc.ScissorTestEnabled = false;
        _pipelineStateDesc.LineAntiAlias = false;
        _pipelineStateDesc.DepthWriteEnabled = false;
        _pipelineStateDesc.StencilTestEnabled = false;
        _pipelineStateDesc.DepthCompareFunction = default;
        _pipelineStateDesc.StencilCompareFunction = default;
        _pipelineStateDesc.StencilOperationOnPassed = default;
        _pipelineStateDesc.StencilOperationOnStencilFailed = default;
        _pipelineStateDesc.StencilOperationOnDepthFailed = default;
        _pipelineStateDesc.StencilCompareMask = 0;
        _pipelineStateDesc.StencilWriteMask = 0;
        _pipelineStateDesc.InputLayout.Clear();
        _pipelineStateDesc.PrimitiveType = default;
        _pipelineStateDesc.Output.ClearRenderTargetFormats();
        _pipelineStateDesc.Output.DepthStencilFormat = TextureFormat.Unknown;
        _pipelineStateDesc.Output.MultiSample = 1;
        _pipelineStateDesc.Samplers.Clear();
        _pipelineStateDesc.ReadOnlyDepth = false;
        _pipelineStateDesc.VertexShader = null;
        _pipelineStateDesc.PixelShader = null;
        _pipelineStateDesc.DomainShader = null;
        _pipelineStateDesc.HullShader = null;
        _pipelineStateDesc.GeometryShader = null;
        _shaderProgramDesc.Clear();
    }

    private void SetupShadowPassState(uint splitIndex, LightProcessor lightProcessor,
        Material material, Pass pass)
    {
        if (_shadowMapAllocator is null)
            return;

        var settings = _shadowMapAllocator.Settings;
        ref readonly var lightParams = ref lightProcessor.GetParams();
        var biasMultiplier = lightParams.ShadowDepthBiasMultiplier![splitIndex] * settings.DepthBiasScale;
        var biasParameters = lightProcessor.GetLight().GetShadowBias();

        _pipelineStateDesc.DebugName = $"Shadow Pass for material '{material.Name}'";

        if (settings.EnableVarianceShadowMaps)
        {
            _pipelineStateDesc.ColorWriteEnabled = true;
            _pipelineStateDesc.ConstantDepthBias = 0.0f;
            _pipelineStateDesc.SlopeScaledDepthBias = 0.0f;
        }
        else
        {
            _pipelineStateDesc.ColorWriteEnabled = false;
            _pipelineStateDesc.ConstantDepthBias =
                biasMultiplier * biasParameters.ConstantBias + settings.DepthBiasOffset;
            _pipelineStateDesc.SlopeScaledDepthBias = biasMultiplier * biasParameters.SlopeScaledBias;
        }

        _pipelineStateDesc.DepthWriteEnabled = pass.DepthWrite;
        _pipelineStateDesc.DepthCompareFunction = pass.DepthTestMode;

        _pipelineStateDesc.CullMode = GetEffectiveCullMode(pass.CullMode, material.GetShadowCullMode(), false);
    }

    private void SetupLightVolumePassState(LightProcessor lightProcessor)
    {
        var light = lightProcessor.GetLight();
        _pipelineStateDesc.DebugName = "Light Volume Pass";
        _pipelineStateDesc.ColorWriteEnabled = true;
        _pipelineStateDesc.BlendMode = light.IsNegative() ? BlendMode.Subtract : BlendMode.Add;

        if (light.GetLightType() != LightType.Directional)
        {
            if (lightProcessor.DoesOverlapCamera())
            {
                _pipelineStateDesc.CullMode = GetEffectiveCullMode(CullMode.Clockwise, _cameraProcessor!.IsCameraReversed());
                _pipelineStateDesc.DepthCompareFunction = CompareMode.Greater;
            }
            else
            {
                _pipelineStateDesc.CullMode = GetEffectiveCullMode(CullMode.CounterClockwise, _cameraProcessor!.IsCameraReversed());
                _pipelineStateDesc.DepthCompareFunction = CompareMode.LessEqual;
            }
        }
        else
        {
            _pipelineStateDesc.CullMode = CullMode.None;
            _pipelineStateDesc.DepthCompareFunction = CompareMode.Always;
        }

        _pipelineStateDesc.StencilTestEnabled = true;
        _pipelineStateDesc.StencilCompareFunction = CompareMode.NotEqual;
        _pipelineStateDesc.StencilCompareMask = light.GetLightMaskEffective() & Drawable.PortableLightMask;
    }

    private void SetupUserPassState(Drawable drawable,
        Material material, Pass pass, bool lightMaskToStencil)
    {
        _ = drawable;
        _pipelineStateDesc.DebugName = $"User Pass '{pass.Name}' for material '{material.Name}'";

        _pipelineStateDesc.DepthWriteEnabled = pass.DepthWrite;
        _pipelineStateDesc.DepthCompareFunction = pass.DepthTestMode;

        _pipelineStateDesc.ColorWriteEnabled = pass.ColorWrite;
        _pipelineStateDesc.BlendMode = pass.BlendMode;
        _pipelineStateDesc.AlphaToCoverageEnabled = pass.AlphaToCoverage || material.GetAlphaToCoverage();
        var depthBias = material.GetDepthBias();
        _pipelineStateDesc.ConstantDepthBias = depthBias.ConstantBias;
        _pipelineStateDesc.SlopeScaledDepthBias = depthBias.SlopeScaledBias;

        var cameraFill = _cameraProcessor?.GetCameraFillMode() ?? FillMode.Solid;
        _pipelineStateDesc.FillMode = (FillMode)Math.Max((int)cameraFill, (int)material.GetFillMode());
        _pipelineStateDesc.CullMode = GetEffectiveCullMode(pass.CullMode,
            material.GetCullMode(), _cameraProcessor?.IsCameraReversed() ?? false);

        if (lightMaskToStencil)
        {
            _pipelineStateDesc.StencilTestEnabled = true;
            _pipelineStateDesc.StencilOperationOnPassed = StencilOp.Ref;
            _pipelineStateDesc.StencilWriteMask = Drawable.PortableLightMask;
        }
    }

    private void SetupLightSamplers(LightProcessor? lightProcessor)
    {
        var light = lightProcessor?.GetLight();
        if (light is not null)
        {
            if (light.GetRampTexture() is { } rampTexture)
                _pipelineStateDesc.Samplers.Add(ShaderResources.LightRamp, rampTexture.GetSamplerStateDesc());
            if (light.GetShapeTexture() is { } shapeTexture)
                _pipelineStateDesc.Samplers.Add(ShaderResources.LightShape, shapeTexture.GetSamplerStateDesc());
        }
        if (lightProcessor is not null && lightProcessor.HasShadow() && _shadowMapAllocator is not null)
            _pipelineStateDesc.Samplers.Add(ShaderResources.ShadowMap, _shadowMapAllocator.SamplerStateDesc);
    }

    private void SetupSamplersForUserOrShadowPass(
        Material material, bool hasLightmap, bool hasAmbient, bool isRefractionPass)
    {
        // TODO: Make configurable
        var lightMapSampler = SamplerStateDesc.Default();
        var reflectionMapSampler = SamplerStateDesc.Trilinear();
        var refractionMapSampler = SamplerStateDesc.Trilinear();

        var materialHasEnvironmentMap = false;
        foreach (var (nameHash, texture) in material.GetTextures())
        {
            if (texture.Texture is not null)
            {
                if (nameHash == (StringHash)ShaderResources.Emission && hasLightmap)
                    continue;
                if (nameHash == (StringHash)ShaderResources.Reflection0)
                    materialHasEnvironmentMap = true;
                _pipelineStateDesc.Samplers.Add(nameHash, texture.Texture.GetSamplerStateDesc());
            }
        }

        if (hasLightmap)
            _pipelineStateDesc.Samplers.Add(ShaderResources.Emission, lightMapSampler);

        if (hasAmbient)
        {
            if (!materialHasEnvironmentMap)
                _pipelineStateDesc.Samplers.Add(ShaderResources.Reflection0, reflectionMapSampler);
            _pipelineStateDesc.Samplers.Add(ShaderResources.Reflection1, reflectionMapSampler);
        }

        if (isRefractionPass)
            _pipelineStateDesc.Samplers.Add(ShaderResources.Emission, refractionMapSampler);

        _pipelineStateDesc.Samplers.Add(ShaderResources.DepthBuffer, SamplerStateDesc.Nearest());
    }

    private void SetupGeometryBufferSamplers()
    {
        _pipelineStateDesc.Samplers.Add(ShaderResources.Albedo, SamplerStateDesc.Nearest());
        _pipelineStateDesc.Samplers.Add(ShaderResources.Properties, SamplerStateDesc.Nearest());
        _pipelineStateDesc.Samplers.Add(ShaderResources.Normal, SamplerStateDesc.Nearest());
        _pipelineStateDesc.Samplers.Add(ShaderResources.DepthBuffer, SamplerStateDesc.Nearest());
    }

    private static CullMode GetEffectiveCullMode(CullMode mode, bool isCameraReversed)
    {
        if (mode == CullMode.None || !isCameraReversed)
            return mode;

        return mode == CullMode.Clockwise ? CullMode.CounterClockwise : CullMode.Clockwise;
    }

    private static CullMode GetEffectiveCullMode(CullMode passCullMode, CullMode materialCullMode, bool isCameraReversed)
    {
        var cullMode = passCullMode != CullMode.Max ? passCullMode : materialCullMode;
        return GetEffectiveCullMode(cullMode, isCameraReversed);
    }
}
