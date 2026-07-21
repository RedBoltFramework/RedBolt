using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

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

    public void RenderBatches(BatchRenderingContext ctx, PipelineBatchGroup<PipelineBatchByState> batchGroup)
    {
        batchGroup.Flags = AdjustRenderFlags(batchGroup.Flags);
        RunCompositor(ctx, batchGroup.Flags, batchGroup.StartInstance, batchGroup.StartInstance + batchGroup.NumInstances,
            batchGroup.Batches.Span, static b => b.PipelineBatch, lightVolume: false);
    }

    public void RenderBatches(BatchRenderingContext ctx, PipelineBatchGroup<PipelineBatchBackToFront> batchGroup)
    {
        batchGroup.Flags = AdjustRenderFlags(batchGroup.Flags);
        RunCompositor(ctx, batchGroup.Flags, batchGroup.StartInstance, batchGroup.StartInstance + batchGroup.NumInstances,
            batchGroup.Batches.Span, static b => b.PipelineBatch, lightVolume: false);
    }

    public void RenderLightVolumeBatches(BatchRenderingContext ctx, ReadOnlySpan<PipelineBatchByState> batches)
    {
        RunCompositor(ctx, BatchRenderFlag.EnablePixelLights, 0, 0, batches, static b => b.PipelineBatch, lightVolume: true);
    }

    private void RunCompositor<T>(BatchRenderingContext ctx, BatchRenderFlag flags, int startInstance, int nextInstance,
        ReadOnlySpan<T> batches, Func<T, PipelineBatch?> select, bool lightVolume)
    {
        // Empty groups need no compositor work (and drawable processor may lack a scene in unit tests).
        if (batches.IsEmpty)
            return;

        // C++ always constructs DrawCommandCompositor with the InstancingBuffer pointer (may be disabled).
        // Do not skip scene draws when the buffer object is absent or disabled.
        var debuggerEnabled = RenderPipelineDebugger.IsSnapshotInProgress(_debugger);
        var compositor = new DrawCommandCompositor(ctx, _settings, debuggerEnabled ? _debugger : null,
            _drawableProcessor, _instancingBuffer, flags, startInstance, debuggerEnabled);
        foreach (var item in batches)
        {
            var pipelineBatch = select(item);
            if (pipelineBatch is null)
                continue;
            if (lightVolume)
                compositor.ProcessLightVolumeBatch(pipelineBatch);
            else
                compositor.ProcessSceneBatch(pipelineBatch);
        }
        compositor.FlushDrawCommands(nextInstance);
    }

    public void PrepareInstancingBuffer(ref PipelineBatchGroup<PipelineBatchByState> batches)
        => PrepareInstancingBufferImpl(ref batches);

    public void PrepareInstancingBuffer(ref PipelineBatchGroup<PipelineBatchBackToFront> batches)
        => PrepareInstancingBufferImpl(ref batches);

    private void PrepareInstancingBufferImpl<T>(ref PipelineBatchGroup<T> batches)
    {
        batches.Flags = AdjustRenderFlags(batches.Flags);
        batches.StartInstance = 0;
        batches.NumInstances = 0;
        if (_instancingBuffer is null)
            return;

        var objectParameterBuilder = new ObjectParameterBuilder(_settings, batches.Flags);
        if (!objectParameterBuilder.IsInstancingSupported())
            return;

        batches.StartInstance = _instancingBuffer.GetNextInstanceIndex();
        foreach (var sortedBatch in batches.Batches.Span)
        {
            PipelineBatch? pipelineBatch = sortedBatch switch
            {
                PipelineBatchByState byState => byState.PipelineBatch,
                PipelineBatchBackToFront backToFront => backToFront.PipelineBatch,
                _ => null
            };
            if (pipelineBatch is null || !objectParameterBuilder.IsBatchInstanced(pipelineBatch))
                continue;

            var sourceBatch = pipelineBatch.GetSourceBatch();
            if (objectParameterBuilder.IsAmbientEnabled())
                objectParameterBuilder.SetBatchAmbient(_drawableProcessor.GetGeometryLighting(pipelineBatch.DrawableIndex));

            var numWorldTransforms = checked((int)sourceBatch.NumWorldTransforms);
            for (var i = 0; i < numWorldTransforms; ++i)
                objectParameterBuilder.AddBatchesToInstancingBuffer(_instancingBuffer, sourceBatch, i);
            batches.NumInstances += numWorldTransforms;
        }
    }

    private BatchRenderFlag AdjustRenderFlags(BatchRenderFlag flags)
    {
        if (_instancingBuffer is null || !_instancingBuffer.IsEnabled())
            flags &= ~BatchRenderFlag.EnableInstancingForStaticGeometry;
        return flags;
    }
}
