using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

namespace RedBolt.RenderPipeline;

/// <summary>
/// RenderPipeline used to render geometry buffer for lightmap baking.
/// </summary>
public class LightmapRenderPipelineView : RedObject, IRenderPipelineInterface
{
    public LightmapRenderPipelineView()
    {
    }

    public override void Dispose()
    {
        _emissionBuffer?.Dispose();
        _albedoBuffer?.Dispose();
        _smoothNormalBuffer?.Dispose();
        _faceNormalBuffer?.Dispose();
        _smoothPositionBuffer?.Dispose();
        _positionBuffer?.Dispose();
        _depthBuffer?.Dispose();
        _emissionBuffer = null;
        _albedoBuffer = null;
        _smoothNormalBuffer = null;
        _faceNormalBuffer = null;
        _smoothPositionBuffer = null;
        _positionBuffer = null;
        _depthBuffer = null;
        _collectStatistics = null;
        _pipelineStatesInvalidated = null;
        _renderEnd = null;
        RenderBegin = null;
        UpdateEnd = null;
        UpdateBegin = null;
        base.Dispose();
    }

    /// <summary>Render geometry buffer. May be called only once.</summary>
    public void RenderGeometryBuffer(Viewport viewport, int textureSize)
    {
        using var renderBufferManager = new RenderBufferManager(this);
        using var shadowMapAllocator = new ShadowMapAllocator();
        using var instancingBuffer = new InstancingBuffer();
        using var sceneProcessor = new SceneProcessor(this, string.Empty, shadowMapAllocator, instancingBuffer);

        using var pass = new UnorderedScenePass(this, sceneProcessor.GetDrawableProcessor(),
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

        var invalidPosition = new Color(-100000000.0f, -100000000.0f, -100000000.0f, 0.0f);
        renderBufferManager.ClearDepthStencil(_depthBuffer, ClearTarget.Depth, 1.0f, 0);
        renderBufferManager.ClearColor(_positionBuffer, invalidPosition);
        renderBufferManager.ClearColor(_smoothPositionBuffer, invalidPosition);
        renderBufferManager.ClearColor(_faceNormalBuffer, Color.TransparentBlack);
        renderBufferManager.ClearColor(_smoothNormalBuffer, Color.TransparentBlack);
        renderBufferManager.ClearColor(_albedoBuffer, Color.TransparentBlack);
        renderBufferManager.ClearColor(_emissionBuffer, Color.TransparentBlack);

        var renderDevice = GetSubsystem<RenderDevice>()!;
        var renderContext = renderDevice.GetRenderContext();
        var drawQueue = renderDevice.GetDefaultQueue();
        var batchRenderer = sceneProcessor.GetBatchRenderer();
        var camera = viewport.GetCamera()!;

        using var renderScope = RenderScope.Acquire(renderContext, "LightmapRenderPipelineView::RenderGeometryBuffer");

        RenderBuffer[] gBuffer =
        [
            _positionBuffer,
            _smoothPositionBuffer,
            _faceNormalBuffer,
            _smoothNormalBuffer,
            _albedoBuffer,
            _emissionBuffer
        ];
        renderBufferManager.SetRenderTargets(_depthBuffer, gBuffer);

        drawQueue.Reset();

        instancingBuffer.Begin();
        batchRenderer.RenderBatches(new BatchRenderingContext(drawQueue, camera), pass.GetDeferredBatches());
        instancingBuffer.End();

        renderContext.Execute(drawQueue);

        // Do not end the renderer so textures are available outside
        // OnRenderEnd(this, frameInfo);
    }

    /// <summary>Return geometry buffer textures.</summary>
    public RawTexture? PositionBuffer => _positionBuffer!.GetTexture();

    public RawTexture? SmoothPositionBuffer => _smoothPositionBuffer!.GetTexture();

    public RawTexture? FaceNormalBuffer => _faceNormalBuffer!.GetTexture();

    public RawTexture? SmoothNormalBuffer => _smoothNormalBuffer!.GetTexture();

    public RawTexture? AlbedoBuffer => _albedoBuffer!.GetTexture();

    public RawTexture? EmissionBuffer => _emissionBuffer!.GetTexture();

    /// <summary>Implement RenderPipelineInterface.</summary>
    public Context? GetContext() => Context.Instance;

    public RenderPipelineDebugger? GetDebugger() => null;

    public bool IsLinearColorSpace => true;

    public event Action<CommonFrameInfo>? UpdateBegin;

    public event Action<CommonFrameInfo>? UpdateEnd;

    public event Action<CommonFrameInfo>? RenderBegin;

    public event Action<CommonFrameInfo>? RenderEnd
    {
        add => _renderEnd += value;
        remove => _renderEnd -= value;
    }

    public event Action? PipelineStatesInvalidated
    {
        add => _pipelineStatesInvalidated += value;
        remove => _pipelineStatesInvalidated -= value;
    }

    public event RenderPipelineStatsHandler? CollectStatistics
    {
        add => _collectStatistics += value;
        remove => _collectStatistics -= value;
    }

    private Action<CommonFrameInfo>? _renderEnd;
    private Action? _pipelineStatesInvalidated;
    private RenderPipelineStatsHandler? _collectStatistics;
    private RenderBuffer? _depthBuffer;
    private RenderBuffer? _positionBuffer;
    private RenderBuffer? _smoothPositionBuffer;
    private RenderBuffer? _faceNormalBuffer;
    private RenderBuffer? _smoothNormalBuffer;
    private RenderBuffer? _albedoBuffer;
    private RenderBuffer? _emissionBuffer;
}
