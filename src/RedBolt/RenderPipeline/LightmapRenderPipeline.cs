namespace RedBolt;

/// <summary>
/// RenderPipeline used to render geometry buffer for lightmap baking.
/// </summary>
public class LightmapRenderPipelineView : Object, IRenderPipelineInterface
{
    private RenderBuffer? _depthBuffer;
    private RenderBuffer? _positionBuffer;
    private RenderBuffer? _smoothPositionBuffer;
    private RenderBuffer? _faceNormalBuffer;
    private RenderBuffer? _smoothNormalBuffer;
    private RenderBuffer? _albedoBuffer;
    private RenderBuffer? _emissionBuffer;

    public LightmapRenderPipelineView()
    {
    }

    /// <summary>Render geometry buffer. May be called only once.</summary>
    public void RenderGeometryBuffer(Viewport viewport, int textureSize)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        var renderBufferManager = new RenderBufferManager(this);
        var shadowMapAllocator = new ShadowMapAllocator();
        var instancingBuffer = new InstancingBuffer();
        var sceneProcessor = new SceneProcessor(this, string.Empty, shadowMapAllocator, instancingBuffer);

        var pass = new UnorderedScenePass(this, sceneProcessor.GetDrawableProcessor(),
            sceneProcessor.GetPipelineStateBuilder(), DrawableProcessorPassFlag.None,
            "deferred", string.Empty, string.Empty, string.Empty);

        var size = Vector2f.One * textureSize;
        var flags = RenderBufferFlag.FixedTextureSize | RenderBufferFlag.Persistent;
        var depthFormat = TextureFormat.TexFormatD24UnormS8Uint;
        var colorFormat = TextureFormat.TexFormatRgba32Float;
        _depthBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(depthFormat, 1, flags), size);
        _positionBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(colorFormat, 1, flags), size);
        _smoothPositionBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(colorFormat, 1, flags), size);
        _faceNormalBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(colorFormat, 1, flags), size);
        _smoothNormalBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(colorFormat, 1, flags), size);
        _albedoBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(colorFormat, 1, flags), size);
        _emissionBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(colorFormat, 1, flags), size);

        var outputDesc = new PipelineStateOutputDesc
        {
            DepthStencilFormat = depthFormat
        };
        outputDesc.NumRenderTargets = 6;
        for (var i = 0; i < outputDesc.NumRenderTargets; ++i)
            outputDesc.RenderTargetFormats[i] = colorFormat;
        pass.SetDeferredOutputDesc(outputDesc);

        // Use main viewport as render target because it's not used anyway
        var frameInfo = new CommonFrameInfo
        {
            Viewport = viewport,
            RenderTarget = null,
            ViewportRect = viewport.GetEffectiveRect(null),
        };
        frameInfo.ViewportSize = frameInfo.ViewportRect.Size;

        sceneProcessor.SetPasses([pass]);
        sceneProcessor.Define(frameInfo);
        sceneProcessor.SetRenderCamera(viewport.GetCamera());

        UpdateBegin?.Invoke(frameInfo);
        sceneProcessor.Update();
        UpdateEnd?.Invoke(frameInfo);

        RenderBegin?.Invoke(frameInfo);

        var graphics = GetSubsystem<Graphics>();
        if (graphics is null)
            return;

        var invalidPosition = new Color(-100000000.0f, -100000000.0f, -100000000.0f, 0.0f);
        // C# Clear* APIs require Graphics; pass graphics for managed mapping of C++ overloads.
        renderBufferManager.ClearDepthStencil(graphics, _depthBuffer, ClearTarget.Depth, 1.0f, 0);
        renderBufferManager.ClearColor(graphics, _positionBuffer, invalidPosition);
        renderBufferManager.ClearColor(graphics, _smoothPositionBuffer, invalidPosition);
        renderBufferManager.ClearColor(graphics, _faceNormalBuffer, Color.TransparentBlack);
        renderBufferManager.ClearColor(graphics, _smoothNormalBuffer, Color.TransparentBlack);
        renderBufferManager.ClearColor(graphics, _albedoBuffer, Color.TransparentBlack);
        renderBufferManager.ClearColor(graphics, _emissionBuffer, Color.TransparentBlack);

        var renderDevice = GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        var renderContext = renderDevice.GetRenderContext();
        var drawQueue = renderDevice.GetDefaultQueue();
        var batchRenderer = sceneProcessor.GetBatchRenderer();
        var camera = viewport.GetCamera();
        if (renderContext is null || drawQueue is null || camera is null)
            return;

        using var renderScope = new RenderScope(renderContext, "LightmapRenderPipelineView::RenderGeometryBuffer");

        RenderBuffer[] gBuffer =
        [
            _positionBuffer,
            _smoothPositionBuffer,
            _faceNormalBuffer,
            _smoothNormalBuffer,
            _albedoBuffer,
            _emissionBuffer
        ];
        renderBufferManager.SetRenderTargets(graphics, _depthBuffer, gBuffer);

        drawQueue.Reset();

        instancingBuffer.Begin();
        batchRenderer.RenderBatches(new BatchRenderingContext(drawQueue, camera), pass.GetDeferredBatches());
        instancingBuffer.End();

        renderContext.Execute(drawQueue);

        // Do not end the renderer so textures are available outside
        // OnRenderEnd(this, frameInfo);
    }

    /// <summary>Return geometry buffer textures.</summary>
    public RawTexture? GetPositionBuffer() => _positionBuffer?.GetTexture()?.GetRawTexture();

    public RawTexture? GetSmoothPositionBuffer() => _smoothPositionBuffer?.GetTexture()?.GetRawTexture();

    public RawTexture? GetFaceNormalBuffer() => _faceNormalBuffer?.GetTexture()?.GetRawTexture();

    public RawTexture? GetSmoothNormalBuffer() => _smoothNormalBuffer?.GetTexture()?.GetRawTexture();

    public RawTexture? GetAlbedoBuffer() => _albedoBuffer?.GetTexture()?.GetRawTexture();

    public RawTexture? GetEmissionBuffer() => _emissionBuffer?.GetTexture()?.GetRawTexture();

    /// <summary>Implement RenderPipelineInterface.</summary>
    public Context? GetContext() => Context.Instance;

    public RenderPipelineDebugger GetDebugger() => null!;

    public bool IsLinearColorSpace => true;

    public event Action<CommonFrameInfo>? UpdateBegin;

    public event Action<CommonFrameInfo>? UpdateEnd;

    public event Action<CommonFrameInfo>? RenderBegin;

    public event Action<CommonFrameInfo>? RenderEnd;

    public event Action? PipelineStatesInvalidated;

    public event RenderPipelineStatsHandler? CollectStatistics;
}
