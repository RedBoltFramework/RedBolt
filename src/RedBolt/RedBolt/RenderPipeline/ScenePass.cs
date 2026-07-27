using Microsoft.Extensions.Logging;

using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;
namespace RedBolt.RenderPipeline;
using Scene = global::RedBolt.Scene.Scene;


/// <summary>Base type for scene pass.</summary>
public abstract class ScenePass : BatchCompositorPass
{
    /// <summary>Construct pass with forward lighting.</summary>
    protected ScenePass(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, DrawableProcessorPassFlag flags, string deferredPass,
        string unlitBasePass, string litBasePass, string lightPass)
        : base(renderPipeline, drawableProcessor, callback, flags,
            Technique.GetPassIndex(deferredPass),
            Technique.GetPassIndex(unlitBasePass),
            Technique.GetPassIndex(litBasePass),
            Technique.GetPassIndex(lightPass))
    {
    }

    /// <summary>Construct pass without forward lighting.</summary>
    protected ScenePass(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, DrawableProcessorPassFlag flags, string pass)
        : base(renderPipeline, drawableProcessor, callback, flags, int.MaxValue,
            Technique.GetPassIndex(pass), int.MaxValue, int.MaxValue)
    {
    }

    /// <summary>Prepare instancing buffer for scene pass.</summary>
    public abstract void PrepareInstancingBuffer(BatchRenderer batchRenderer);
}

/// <summary>Scene pass with batches sorted by render order and pipeline state.</summary>
public class UnorderedScenePass : ScenePass
{
    private readonly List<PipelineBatchByState> _sortedDeferredBatches = [];
    private readonly List<PipelineBatchByState> _sortedBaseBatches = [];
    private readonly List<PipelineBatchByState> _sortedLightBatches = [];

    private PipelineBatchGroup<PipelineBatchByState> _deferredBatchGroup;
    private PipelineBatchGroup<PipelineBatchByState> _baseBatchGroup;
    private PipelineBatchGroup<PipelineBatchByState> _lightBatchGroup;

    public UnorderedScenePass(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, DrawableProcessorPassFlag flags, string deferredPass,
        string unlitBasePass, string litBasePass, string lightPass)
        : base(renderPipeline, drawableProcessor, callback, flags, deferredPass, unlitBasePass, litBasePass, lightPass)
    {
    }

    public UnorderedScenePass(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, DrawableProcessorPassFlag flags, string pass)
        : base(renderPipeline, drawableProcessor, callback, flags, pass)
    {
    }

    public override void PrepareInstancingBuffer(BatchRenderer batchRenderer)
    {
        batchRenderer.PrepareInstancingBuffer(ref _deferredBatchGroup);
        batchRenderer.PrepareInstancingBuffer(ref _baseBatchGroup);
        batchRenderer.PrepareInstancingBuffer(ref _lightBatchGroup);
    }

    public ref readonly PipelineBatchGroup<PipelineBatchByState> GetDeferredBatches() => ref _deferredBatchGroup;
    public ref readonly PipelineBatchGroup<PipelineBatchByState> GetBaseBatches() => ref _baseBatchGroup;
    public ref readonly PipelineBatchGroup<PipelineBatchByState> GetLightBatches() => ref _lightBatchGroup;

    public override void Dispose()
    {
        _lightBatchGroup = default;
        _baseBatchGroup = default;
        _deferredBatchGroup = default;
        _sortedLightBatches.Clear();
        _sortedBaseBatches.Clear();
        _sortedDeferredBatches.Clear();
        base.Dispose();
    }

    protected override void OnBatchesReady()
    {
        BatchCompositor.FillSortKeys(_sortedDeferredBatches, DeferredBatches);
        BatchCompositor.FillSortKeys(_sortedBaseBatches, BaseBatches);
        BatchCompositor.FillSortKeys(_sortedLightBatches, LightBatches, NegativeLightBatches);

        _sortedDeferredBatches.Sort();
        _sortedBaseBatches.Sort();

        var numNegativeLightBatches = (int)NegativeLightBatches.Size();
        var lightSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_sortedLightBatches);
        lightSpan[..^numNegativeLightBatches].Sort();
        lightSpan[^numNegativeLightBatches..].Sort();

        _deferredBatchGroup = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = _sortedDeferredBatches
        };
        _baseBatchGroup = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = _sortedBaseBatches
        };
        _lightBatchGroup = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = _sortedLightBatches
        };

        if (!IsFlagSet(DrawableProcessorPassFlag.DisableInstancing))
        {
            _deferredBatchGroup.Flags |= BatchRenderFlag.EnableInstancingForStaticGeometry;
            _baseBatchGroup.Flags |= BatchRenderFlag.EnableInstancingForStaticGeometry;
            _lightBatchGroup.Flags |= BatchRenderFlag.EnableInstancingForStaticGeometry;
        }

        if (HasLightPass)
        {
            _baseBatchGroup.Flags |= BatchRenderFlag.EnablePixelLights;
            _lightBatchGroup.Flags |= BatchRenderFlag.EnablePixelLights;
        }

        if (IsFlagSet(DrawableProcessorPassFlag.HasAmbientLighting))
        {
            _deferredBatchGroup.Flags |= BatchRenderFlag.EnableAmbientLighting;
            _baseBatchGroup.Flags |= BatchRenderFlag.EnableAmbientAndVertexLighting;
        }

        if (IsFlagSet(DrawableProcessorPassFlag.DepthOnlyPass))
        {
            _deferredBatchGroup.Flags |= BatchRenderFlag.DisableColorOutput;
            _baseBatchGroup.Flags |= BatchRenderFlag.DisableColorOutput;
        }

        if (IsFlagSet(DrawableProcessorPassFlag.DeferredLightMaskToStencil))
            _deferredBatchGroup.Flags |= BatchRenderFlag.LightMaskToStencil;

        if (LinearColorSpace)
        {
            _deferredBatchGroup.Flags |= BatchRenderFlag.LinearColorSpace;
            _baseBatchGroup.Flags |= BatchRenderFlag.LinearColorSpace;
            _lightBatchGroup.Flags |= BatchRenderFlag.LinearColorSpace;
        }
    }
}

/// <summary>Scene pass with batches sorted by render order and distance back to front.</summary>
public class BackToFrontScenePass : ScenePass
{
    private readonly List<PipelineBatchBackToFront> _sortedBatches = [];
    private bool _hasRefractionBatches;
    private PipelineBatchGroup<PipelineBatchBackToFront> _batchGroup;

    public BackToFrontScenePass(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, DrawableProcessorPassFlag flags, string deferredPass,
        string unlitBasePass, string litBasePass, string lightPass)
        : base(renderPipeline, drawableProcessor, callback, flags, deferredPass, unlitBasePass, litBasePass, lightPass)
    {
    }

    public BackToFrontScenePass(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, DrawableProcessorPassFlag flags, string pass)
        : base(renderPipeline, drawableProcessor, callback, flags, pass)
    {
    }

    public override void PrepareInstancingBuffer(BatchRenderer batchRenderer)
    {
        batchRenderer.PrepareInstancingBuffer(ref _batchGroup);
    }

    public ref readonly PipelineBatchGroup<PipelineBatchBackToFront> GetBatches() => ref _batchGroup;

    public bool HasRefractionBatches() => _hasRefractionBatches;

    public override void Dispose()
    {
        _batchGroup = default;
        _sortedBatches.Clear();
        base.Dispose();
    }

    protected override void OnBatchesReady()
    {
        BatchCompositor.FillSortKeys(_sortedBatches, BaseBatches, LightBatches, NegativeLightBatches);

        // When rendering back-to-front, it's still important to render batches for each object in order:
        // - Base batch;
        // - Additive light batches;
        // - Subtractive light batches.
        // Multiply distance by some factor close to 1 if distance is greater than 0, ignore otherwise.
        var subtractiveLightBatchesBegin = _sortedBatches.Count - (int)NegativeLightBatches.Size();
        var subtractiveLightBatchesEnd = _sortedBatches.Count;
        var additiveLightBatchesBegin = subtractiveLightBatchesBegin - (int)LightBatches.Size();
        var additiveLightBatchesEnd = subtractiveLightBatchesBegin;

        const float additiveDistanceFactor = 1 - MathDefs.Epsilon;
        const float subtractiveDistanceFactor = 1 - 2 * MathDefs.Epsilon;

        // Validate distances before sorting, NaN may corrupt sort
        var sortedBatchesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_sortedBatches);
        for (var i = 0; i < sortedBatchesSpan.Length; ++i)
        {
            ref var sortedBatch = ref sortedBatchesSpan[i];
            if (float.IsFinite(sortedBatch.Distance))
                continue;

            ref readonly var batch = ref sortedBatch.PipelineBatch;
            Log.GetLogger().LogError("Drawable batch [{Drawable}].{Index} has NaN distance",
                batch.Drawable!.GetFullNameDebug(), batch.SourceBatchIndex);
            sortedBatch.Distance = MathDefs.LargeValue;
        }

        for (var i = additiveLightBatchesBegin; i < additiveLightBatchesEnd; ++i)
            sortedBatchesSpan[i].Distance *= additiveDistanceFactor;

        for (var i = subtractiveLightBatchesBegin; i < subtractiveLightBatchesEnd; ++i)
            sortedBatchesSpan[i].Distance *= subtractiveDistanceFactor;

        _sortedBatches.Sort();

        if (IsFlagSet(DrawableProcessorPassFlag.RefractionPass))
        {
            _hasRefractionBatches = false;
            foreach (ref readonly var sortedBatch in
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_sortedBatches))
            {
                // Assume refraction if blending is disabled
                if (sortedBatch.PipelineBatch.PipelineState!.GetDesc().AsGraphics()!.Value.BlendMode == BlendMode.Replace)
                {
                    _hasRefractionBatches = true;
                    break;
                }
            }
        }

        _batchGroup = new PipelineBatchGroup<PipelineBatchBackToFront>
        {
            Batches = _sortedBatches
        };
        if (IsFlagSet(DrawableProcessorPassFlag.HasAmbientLighting))
            _batchGroup.Flags |= BatchRenderFlag.EnableAmbientAndVertexLighting;
        if (HasLightPass)
            _batchGroup.Flags |= BatchRenderFlag.EnablePixelLights;
        if (!IsFlagSet(DrawableProcessorPassFlag.DisableInstancing))
            _batchGroup.Flags |= BatchRenderFlag.EnableInstancingForStaticGeometry;
        if (LinearColorSpace)
            _batchGroup.Flags |= BatchRenderFlag.LinearColorSpace;
    }
}
