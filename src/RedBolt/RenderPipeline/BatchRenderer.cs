namespace RedBolt;

/// <summary>Common parameters of batch rendering.</summary>
public sealed class BatchRenderingContext
{
    public DrawCommandQueue DrawQueue { get; }
    public Camera Camera { get; }
    public ShadowSplitProcessor? OutputShadowSplit { get; set; }
    public uint InstanceMultiplier { get; set; } = 1u;

    public ReadOnlyMemory<ShaderResourceDesc> GlobalResources { get; set; }
    public ReadOnlyMemory<ShaderParameterDesc> FrameParameters { get; set; }
    public ReadOnlyMemory<ShaderParameterDesc> CameraParameters { get; set; }

    public BatchRenderingContext(DrawCommandQueue drawQueue, Camera camera)
    {
        DrawQueue = drawQueue ?? throw new ArgumentNullException(nameof(drawQueue));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
    }

    public BatchRenderingContext(DrawCommandQueue drawQueue, ShadowSplitProcessor outputShadowSplit)
    {
        DrawQueue = drawQueue ?? throw new ArgumentNullException(nameof(drawQueue));
        OutputShadowSplit = outputShadowSplit ?? throw new ArgumentNullException(nameof(outputShadowSplit));
        Camera = outputShadowSplit.GetShadowCamera()
            ?? throw new InvalidOperationException("Shadow split has no camera.");
    }
}

/// <summary>Utility class to convert pipeline batches into sequence of draw commands.</summary>
public class BatchRenderer : Object
{
    private readonly Renderer? _renderer;
    private readonly RenderPipelineDebugger? _debugger;
    private readonly DrawableProcessor _drawableProcessor;
    private readonly InstancingBuffer? _instancingBuffer;
    private BatchRendererSettings _settings = new();

    public BatchRenderer(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        InstancingBuffer? instancingBuffer)
    {
        ArgumentNullException.ThrowIfNull(renderPipeline);
        _drawableProcessor = drawableProcessor ?? throw new ArgumentNullException(nameof(drawableProcessor));
        _instancingBuffer = instancingBuffer;
        _debugger = renderPipeline.GetDebugger();
        _renderer = Context.Instance?.GetSubsystem<Renderer>();
    }

    public void SetSettings(BatchRendererSettings settings) => _settings = settings;

    public BatchRendererSettings GetSettings() => _settings;

    /// <summary>Render batches sorted by pipeline state.</summary>
    public void RenderBatches(BatchRenderingContext ctx, PipelineBatchGroup<PipelineBatchByState> batchGroup)
    {
        ReportBatches(batchGroup.Batches.Span, static batch => batch.PipelineBatch);
    }

    /// <summary>Render batches sorted back to front.</summary>
    public void RenderBatches(BatchRenderingContext ctx, PipelineBatchGroup<PipelineBatchBackToFront> batchGroup)
    {
        ReportBatches(batchGroup.Batches.Span, static batch => batch.PipelineBatch);
    }

    /// <summary>Render light volume batches.</summary>
    public void RenderLightVolumeBatches(BatchRenderingContext ctx, ReadOnlySpan<PipelineBatchByState> batches)
    {
        ReportBatches(batches, static batch => batch.PipelineBatch);
    }

    /// <summary>Store instancing data for state-sorted batches.</summary>
    public void PrepareInstancingBuffer(ref PipelineBatchGroup<PipelineBatchByState> batches)
    {
        PrepareInstancingBufferImpl(ref batches);
    }

    /// <summary>Store instancing data for back-to-front batches.</summary>
    public void PrepareInstancingBuffer(ref PipelineBatchGroup<PipelineBatchBackToFront> batches)
    {
        PrepareInstancingBufferImpl(ref batches);
    }

    private void PrepareInstancingBufferImpl<T>(ref PipelineBatchGroup<T> batches)
    {
        // Full instancing buffer fill is ported with the C++ DrawCommandCompositor path.
        // Keep group metadata consistent for callers until that path lands.
        if (_instancingBuffer is null)
        {
            batches.StartInstance = 0;
            batches.NumInstances = 0;
            return;
        }

        batches.StartInstance = 0;
        batches.NumInstances = 0;
    }

    private void ReportBatches<T>(ReadOnlySpan<T> batches, Func<T, PipelineBatch?> selectBatch)
    {
        if (!RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            return;

        var resetInstancingGroup = true;
        foreach (var item in batches)
        {
            var pipelineBatch = selectBatch(item);
            if (pipelineBatch is null || pipelineBatch.PipelineState is null)
                continue;

            _debugger!.ReportSceneBatch(new DebugFrameSnapshotBatch(_drawableProcessor, pipelineBatch, resetInstancingGroup));
            resetInstancingGroup = false;
        }
    }
}

/// <summary>
/// Placeholder for shadow-split processor until the full C++ class is ported.
/// BatchRenderingContext only needs a camera reference from the split.
/// </summary>
public class ShadowSplitProcessor
{
    private Camera? _shadowCamera;

    public void SetShadowCamera(Camera? camera) => _shadowCamera = camera;

    public Camera? GetShadowCamera() => _shadowCamera;
}
