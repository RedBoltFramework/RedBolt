namespace RedBolt;

/// <summary>
/// Scene processor for RenderPipeline.
/// Full C++ update/render bodies remain to be ported; this shell provides the type
/// graph used by PipelineStateBuilder and future scene-pass orchestration.
/// </summary>
// TODO: port complete C++ SceneProcessor implementation
public class SceneProcessor : Object, LightProcessorCallback
{
    private readonly IRenderPipelineInterface _renderPipeline;
    private readonly ShadowMapAllocator? _shadowMapAllocator;
    private readonly InstancingBuffer? _instancingBuffer;
    private readonly CameraProcessor _cameraProcessor;
    private readonly PipelineStateBuilder _pipelineStateBuilder;
    private readonly DrawableProcessor _drawableProcessor;
    private readonly BatchCompositor _batchCompositor;
    private readonly BatchRenderer _batchRenderer;
    private readonly List<ScenePass> _passes = [];
    private SceneProcessorSettings _settings = new();

    public SceneProcessor(IRenderPipelineInterface renderPipeline, string shadowTechnique,
        ShadowMapAllocator? shadowMapAllocator, InstancingBuffer? instancingBuffer)
    {
        ArgumentNullException.ThrowIfNull(renderPipeline);
        _renderPipeline = renderPipeline;
        _shadowMapAllocator = shadowMapAllocator;
        _instancingBuffer = instancingBuffer;

        _cameraProcessor = new CameraProcessor();
        _drawableProcessor = new DrawableProcessor(renderPipeline);
        _pipelineStateBuilder = new PipelineStateBuilder(this, _cameraProcessor, shadowMapAllocator, instancingBuffer);
        _batchCompositor = new BatchCompositor(renderPipeline, _drawableProcessor, _pipelineStateBuilder,
            Technique.GetPassIndex(shadowTechnique));
        _batchRenderer = new BatchRenderer(renderPipeline, _drawableProcessor, instancingBuffer);
    }

    /// <summary>Setup. Slow, called rarely.</summary>
    public void SetPasses(IReadOnlyList<ScenePass?> passes)
    {
        _passes.Clear();
        foreach (var pass in passes)
        {
            if (pass is not null)
                _passes.Add(pass);
        }
    }

    public void SetSettings(ShaderProgramCompositorSettings settings)
    {
        // TODO: port complete SceneProcessor::SetSettings
        _pipelineStateBuilder.SetSettings(settings);
        _ = _settings;
    }

    public bool Define(CommonFrameInfo frameInfo)
    {
        // TODO: port complete SceneProcessor::Define
        _ = frameInfo;
        return true;
    }

    public void SetRenderCamera(Camera? camera)
    {
        // TODO: port complete SceneProcessor::SetRenderCamera
        _ = camera;
    }

    public void SetRenderCameras(ReadOnlySpan<Camera?> cameras)
    {
        // TODO: port complete SceneProcessor::SetRenderCameras
        _ = cameras;
    }

    public virtual void Update()
    {
        // TODO: port complete SceneProcessor::Update
    }

    public void PrepareInstancingBuffer()
    {
        // TODO: port complete SceneProcessor::PrepareInstancingBuffer
        foreach (var pass in _passes)
            pass.PrepareInstancingBuffer(_batchRenderer);
    }

    public void PrepareDrawablesBeforeRendering()
    {
        // TODO: port complete SceneProcessor::PrepareDrawablesBeforeRendering
    }

    public void RenderShadowMaps()
    {
        // TODO: port complete SceneProcessor::RenderShadowMaps
    }

    public void RenderSceneBatches(string debugName, Camera? camera,
        PipelineBatchGroup<PipelineBatchByState> batchGroup,
        ReadOnlySpan<ShaderResourceDesc> globalResources = default,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters = default,
        ReadOnlySpan<ShaderParameterDesc> frameParameters = default,
        uint instanceMultiplier = 1u)
    {
        // TODO: port complete SceneProcessor::RenderSceneBatches
        _ = debugName;
        _ = camera;
        _ = batchGroup;
        _ = globalResources;
        _ = cameraParameters;
        _ = frameParameters;
        _ = instanceMultiplier;
        _ = _renderPipeline;
        _ = _shadowMapAllocator;
        _ = _instancingBuffer;
    }

    public void RenderSceneBatches(string debugName, Camera? camera,
        PipelineBatchGroup<PipelineBatchBackToFront> batchGroup,
        ReadOnlySpan<ShaderResourceDesc> globalResources = default,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters = default,
        ReadOnlySpan<ShaderParameterDesc> frameParameters = default,
        uint instanceMultiplier = 1u)
    {
        // TODO: port complete SceneProcessor::RenderSceneBatches
        _ = debugName;
        _ = camera;
        _ = batchGroup;
        _ = globalResources;
        _ = cameraParameters;
        _ = frameParameters;
        _ = instanceMultiplier;
    }

    public void RenderLightVolumeBatches(string debugName, Camera? camera,
        ReadOnlySpan<ShaderResourceDesc> globalResources,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters,
        ReadOnlySpan<ShaderParameterDesc> frameParameters = default,
        uint instanceMultiplier = 1u)
    {
        // TODO: port complete SceneProcessor::RenderLightVolumeBatches
        _ = debugName;
        _ = camera;
        _ = globalResources;
        _ = cameraParameters;
        _ = frameParameters;
        _ = instanceMultiplier;
    }

    public SceneProcessorSettings GetSettings() => _settings;

    /// <summary>
    /// Takes pass object from BatchStateCreateContext.
    /// Returns user-configured pass (inherited from BatchCompositorPass) if possible.
    /// Returns null if internal pass from BatchCompositor itself (e.g. shadow or light volume pass).
    /// </summary>
    public BatchCompositorPass? GetUserPass(Object? pass) => pass as BatchCompositorPass;

    public IReadOnlyList<PipelineBatchByState> GetLightVolumeBatches() => _batchCompositor.GetLightVolumeBatches();

    public CameraProcessor GetCameraProcessor() => _cameraProcessor;

    public PipelineStateBuilder GetPipelineStateBuilder() => _pipelineStateBuilder;

    public DrawableProcessor GetDrawableProcessor() => _drawableProcessor;

    public BatchCompositor GetBatchCompositor() => _batchCompositor;

    public BatchRenderer GetBatchRenderer() => _batchRenderer;

    protected virtual void DrawOccluders()
    {
        // TODO: port complete SceneProcessor::DrawOccluders
    }

    public bool IsLightShadowed(Light light)
    {
        // TODO: port complete SceneProcessor::IsLightShadowed
        _ = light;
        return false;
    }

    public uint GetShadowMapSize(Light light, uint numActiveSplits)
    {
        // TODO: port complete SceneProcessor::GetShadowMapSize
        _ = light;
        _ = numActiveSplits;
        return 0;
    }

    public ShadowMapRegion AllocateTransientShadowMap(Vector2i size)
    {
        // TODO: port complete SceneProcessor::AllocateTransientShadowMap
        return _shadowMapAllocator?.AllocateShadowMap(size) ?? default;
    }
}
