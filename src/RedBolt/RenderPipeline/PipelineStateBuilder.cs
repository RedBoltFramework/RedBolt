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
        // TODO: port complete C++ PipelineStateBuilder::CreateBatchPipelineState (needs full SceneProcessor)
        _ = key;
        _ = ctx;
        _ = outputDesc;
        _ = _sceneProcessor;
        _ = _renderDevice;
        _ = _pipelineStateCache;
        _ = _pipelineStateDesc;
        _ = _shaderProgramDesc;
        return null;
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
}
