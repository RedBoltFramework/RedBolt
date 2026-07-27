namespace RedBolt;

/// <summary>Common parameters of batch rendering.</summary>
public ref struct BatchRenderingContext
{
    public DrawCommandQueue DrawQueue { get; }
    public Camera Camera { get; }
    public ShadowSplitProcessor? OutputShadowSplit { get; set; }
    public int InstanceMultiplier { get; set; }

    public ReadOnlySpan<ShaderResourceDesc> GlobalResources { get; set; }
    public ReadOnlySpan<ShaderParameterDesc> FrameParameters { get; set; }
    public ReadOnlySpan<ShaderParameterDesc> CameraParameters { get; set; }

    public BatchRenderingContext(DrawCommandQueue drawQueue, Camera camera)
    {
        DrawQueue = drawQueue ?? throw new ArgumentNullException(nameof(drawQueue));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        OutputShadowSplit = null;
        InstanceMultiplier = 1;
        GlobalResources = default;
        FrameParameters = default;
        CameraParameters = default;
    }

    public BatchRenderingContext(DrawCommandQueue drawQueue, ShadowSplitProcessor outputShadowSplit)
    {
        DrawQueue = drawQueue ?? throw new ArgumentNullException(nameof(drawQueue));
        OutputShadowSplit = outputShadowSplit ?? throw new ArgumentNullException(nameof(outputShadowSplit));
        Camera = outputShadowSplit.ShadowCamera;
        InstanceMultiplier = 1;
        GlobalResources = default;
        FrameParameters = default;
        CameraParameters = default;
    }
}

/// <summary>Utility class to convert pipeline batches into sequence of draw commands.</summary>
public class BatchRenderer : RedObject
{
    private readonly Renderer? _renderer;
    private readonly RenderPipelineDebugger? _debugger;
    private readonly DrawableProcessor? _drawableProcessor;
    private readonly InstancingBuffer? _instancingBuffer;
    private BatchRendererSettings _settings = new();

    public BatchRenderer(IRenderPipelineInterface? renderPipeline, DrawableProcessor? drawableProcessor,
        InstancingBuffer? instancingBuffer)
    {
        _drawableProcessor = drawableProcessor;
        _instancingBuffer = instancingBuffer;
        _debugger = renderPipeline!.GetDebugger();
        _renderer = GetSubsystem<Renderer>()!;
    }

    public void SetSettings(BatchRendererSettings settings) => _settings = settings;

    public void RenderBatches(BatchRenderingContext ctx, PipelineBatchGroup<PipelineBatchByState> batchGroup)
    {
        batchGroup.Flags = AdjustRenderFlags(batchGroup.Flags);
        var debuggerEnabled = RenderPipelineDebugger.IsSnapshotInProgress(_debugger);
        var compositor = new DrawCommandCompositor(ctx, _settings, debuggerEnabled ? _debugger : null,
            _drawableProcessor!, _instancingBuffer!, batchGroup.Flags, batchGroup.StartInstance, debuggerEnabled);
        try
        {
            if (batchGroup.Batches is not null)
            {
                var batches = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(batchGroup.Batches);
                for (var i = 0; i < batches.Length; ++i)
                    compositor.ProcessSceneBatch(in batches[i].PipelineBatch);
            }
            compositor.FlushDrawCommands(batchGroup.StartInstance + batchGroup.NumInstances);
        }
        finally
        {
            compositor.Dispose();
        }
    }

    public void RenderBatches(BatchRenderingContext ctx, PipelineBatchGroup<PipelineBatchBackToFront> batchGroup)
    {
        batchGroup.Flags = AdjustRenderFlags(batchGroup.Flags);
        var debuggerEnabled = RenderPipelineDebugger.IsSnapshotInProgress(_debugger);
        var compositor = new DrawCommandCompositor(ctx, _settings, debuggerEnabled ? _debugger : null,
            _drawableProcessor!, _instancingBuffer!, batchGroup.Flags, batchGroup.StartInstance, debuggerEnabled);
        try
        {
            if (batchGroup.Batches is not null)
            {
                foreach (ref readonly var sortedBatch in
                    System.Runtime.InteropServices.CollectionsMarshal.AsSpan(batchGroup.Batches))
                    compositor.ProcessSceneBatch(in sortedBatch.PipelineBatch);
            }
            compositor.FlushDrawCommands(batchGroup.StartInstance + batchGroup.NumInstances);
        }
        finally
        {
            compositor.Dispose();
        }
    }

    public void RenderLightVolumeBatches(BatchRenderingContext ctx, ReadOnlySpan<PipelineBatchByState> batches)
    {
        var debuggerEnabled = RenderPipelineDebugger.IsSnapshotInProgress(_debugger);
        var compositor = new DrawCommandCompositor(ctx, _settings, debuggerEnabled ? _debugger : null,
            _drawableProcessor!, _instancingBuffer!, BatchRenderFlag.EnablePixelLights, 0, debuggerEnabled);
        try
        {
            foreach (ref readonly var sortedBatch in batches)
                compositor.ProcessLightVolumeBatch(in sortedBatch.PipelineBatch);
            compositor.FlushDrawCommands(0);
        }
        finally
        {
            compositor.Dispose();
        }
    }

    public void PrepareInstancingBuffer(ref PipelineBatchGroup<PipelineBatchByState> batches)
    {
        batches.Flags = AdjustRenderFlags(batches.Flags);
        batches.StartInstance = 0;
        batches.NumInstances = 0;

        var objectParameterBuilder = new ObjectParameterBuilder(_settings, batches.Flags);
        if (!objectParameterBuilder.IsInstancingSupported())
            return;

        batches.StartInstance = _instancingBuffer!.NextInstanceIndex;
        if (batches.Batches is null)
            return;
        foreach (ref readonly var sortedBatch in
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(batches.Batches))
        {
            ref readonly var pipelineBatch = ref sortedBatch.PipelineBatch;
            if (!objectParameterBuilder.IsBatchInstanced(pipelineBatch))
                continue;

            var sourceBatch = pipelineBatch.GetSourceBatch();
            if (objectParameterBuilder.IsAmbientEnabled())
                objectParameterBuilder.SetBatchAmbient(_drawableProcessor!.GetGeometryLighting(pipelineBatch.DrawableIndex));

            var numWorldTransforms = checked((int)sourceBatch.NumWorldTransforms);
            for (var i = 0; i < numWorldTransforms; ++i)
                objectParameterBuilder.AddBatchesToInstancingBuffer(
                    _instancingBuffer, sourceBatch, sourceBatch.WorldTransform, i);
            batches.NumInstances += numWorldTransforms;
        }
    }

    public void PrepareInstancingBuffer(ref PipelineBatchGroup<PipelineBatchBackToFront> batches)
    {
        batches.Flags = AdjustRenderFlags(batches.Flags);
        batches.StartInstance = 0;
        batches.NumInstances = 0;
        var objectParameterBuilder = new ObjectParameterBuilder(_settings, batches.Flags);
        if (!objectParameterBuilder.IsInstancingSupported())
            return;

        batches.StartInstance = _instancingBuffer!.NextInstanceIndex;
        if (batches.Batches is null)
            return;
        foreach (ref readonly var sortedBatch in
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(batches.Batches))
        {
            ref readonly var pipelineBatch = ref sortedBatch.PipelineBatch;
            if (!objectParameterBuilder.IsBatchInstanced(pipelineBatch))
                continue;

            var sourceBatch = pipelineBatch.GetSourceBatch();
            if (objectParameterBuilder.IsAmbientEnabled())
                objectParameterBuilder.SetBatchAmbient(_drawableProcessor!.GetGeometryLighting(pipelineBatch.DrawableIndex));

            var numWorldTransforms = checked((int)sourceBatch.NumWorldTransforms);
            for (var i = 0; i < numWorldTransforms; ++i)
                objectParameterBuilder.AddBatchesToInstancingBuffer(
                    _instancingBuffer, sourceBatch, sourceBatch.WorldTransform, i);
            batches.NumInstances += numWorldTransforms;
        }
    }

    private BatchRenderFlag AdjustRenderFlags(BatchRenderFlag flags)
    {
        if (!_instancingBuffer!.IsEnabled)
            flags &= ~BatchRenderFlag.EnableInstancingForStaticGeometry;
        return flags;
    }
}
