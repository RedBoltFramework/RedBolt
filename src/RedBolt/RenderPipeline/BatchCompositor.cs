namespace RedBolt;

/// <summary>Batch compositor for single scene pass.</summary>
public class BatchCompositorPass : DrawableProcessorPass
{
    private readonly WorkQueue? _workQueue;
    private readonly Material? _defaultMaterial;
    private readonly DrawableProcessor _drawableProcessor;
    private readonly BatchStateCacheCallback? _batchStateCacheCallback;

    protected readonly WorkQueueVector<PipelineBatch> DeferredBatches = new();
    protected readonly WorkQueueVector<PipelineBatch> BaseBatches = new();
    protected readonly WorkQueueVector<PipelineBatch> LightBatches = new();
    protected readonly WorkQueueVector<PipelineBatch> NegativeLightBatches = new();

    private readonly BatchStateCache _deferredCache = new();
    private readonly BatchStateCache _unlitBaseCache = new();
    private readonly BatchStateCache _litBaseCache = new();
    private readonly BatchStateCache _lightCache = new();

    private readonly WorkQueueVector<PipelineBatchDesc> _delayedDeferredBatches = new();
    private readonly WorkQueueVector<PipelineBatchDesc> _delayedUnlitBaseBatches = new();
    private readonly WorkQueueVector<PipelineBatchDesc> _delayedLitBaseBatches = new();
    private readonly WorkQueueVector<PipelineBatchDesc> _delayedLightBatches = new();
    private readonly WorkQueueVector<PipelineBatchDesc> _delayedNegativeLightBatches = new();

    public BatchCompositorPass(
        IRenderPipelineInterface renderPipeline,
        DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback,
        DrawableProcessorPassFlag flags,
        uint deferredPassIndex,
        uint unlitBasePassIndex,
        uint litBasePassIndex,
        uint lightPassIndex)
        : base(renderPipeline, flags, deferredPassIndex, unlitBasePassIndex, litBasePassIndex, lightPassIndex)
    {
        _drawableProcessor = drawableProcessor ?? throw new ArgumentNullException(nameof(drawableProcessor));
        _batchStateCacheCallback = callback;
        _workQueue = GetSubsystem<WorkQueue>();
        _defaultMaterial = GetSubsystem<Renderer>()?.GetDefaultMaterial();
        renderPipeline.PipelineStatesInvalidated += OnPipelineStatesInvalidated;
    }

    public override void Dispose()
    {
        _delayedNegativeLightBatches.Clear();
        _delayedLightBatches.Clear();
        _delayedLitBaseBatches.Clear();
        _delayedUnlitBaseBatches.Clear();
        _delayedDeferredBatches.Clear();
        _lightCache.Dispose();
        _litBaseCache.Dispose();
        _unlitBaseCache.Dispose();
        _deferredCache.Dispose();
        NegativeLightBatches.Clear();
        LightBatches.Clear();
        BaseBatches.Clear();
        DeferredBatches.Clear();
        RenderPipeline.PipelineStatesInvalidated -= OnPipelineStatesInvalidated;
        base.Dispose();
    }

    /// <summary>Callback for pipeline state initialization.</summary>
    public virtual bool CreatePipelineState(ref GraphicsPipelineStateDesc desc, PipelineStateBuilder? builder,
        BatchStateCreateKey key, BatchStateCreateContext ctx) => false;

    public void SetForwardOutputDesc(PipelineStateOutputDesc desc)
    {
        _unlitBaseCache.SetOutputDesc(desc);
        _litBaseCache.SetOutputDesc(desc);
        _lightCache.SetOutputDesc(desc);
    }

    public void SetDeferredOutputDesc(PipelineStateOutputDesc desc) => _deferredCache.SetOutputDesc(desc);

    public void ComposeBatches()
    {
        // Try to process batches in worker threads
        if (_workQueue is not null)
        {
            var geometryBatchList = new List<GeometryBatch>();
            GeometryBatches.CopyTo(geometryBatchList);
            WorkQueue.ForEachParallel(_workQueue, geometryBatchList,
                (_, geometryBatch) => ProcessGeometryBatch(geometryBatch));
        }
        else
        {
            foreach (var geometryBatch in GeometryBatches)
                ProcessGeometryBatch(geometryBatch);
        }

        // Create missing pipeline states from main thread
        if (_batchStateCacheCallback is not null)
        {
            ResolveDelayedBatches(BatchCompositorSubpass.Deferred, _delayedDeferredBatches, _deferredCache, DeferredBatches);
            ResolveDelayedBatches(BatchCompositorSubpass.Base, _delayedUnlitBaseBatches, _unlitBaseCache, BaseBatches);
            ResolveDelayedBatches(BatchCompositorSubpass.Base, _delayedLitBaseBatches, _litBaseCache, BaseBatches);
            ResolveDelayedBatches(BatchCompositorSubpass.Light, _delayedLightBatches, _lightCache, LightBatches);
            ResolveDelayedBatches(BatchCompositorSubpass.Light, _delayedNegativeLightBatches, _lightCache, NegativeLightBatches);
        }

        OnBatchesReady();
    }

    public bool HasBatches() =>
        DeferredBatches.Size() > 0 || BaseBatches.Size() > 0 || LightBatches.Size() > 0;

    protected override void OnUpdateBegin(CommonFrameInfo frameInfo)
    {
        base.OnUpdateBegin(frameInfo);

        DeferredBatches.Clear();
        BaseBatches.Clear();
        LightBatches.Clear();
        NegativeLightBatches.Clear();

        _delayedDeferredBatches.Clear();
        _delayedUnlitBaseBatches.Clear();
        _delayedLitBaseBatches.Clear();
        _delayedLightBatches.Clear();
        _delayedNegativeLightBatches.Clear();
    }

    protected virtual void OnPipelineStatesInvalidated()
    {
        _deferredCache.Invalidate();
        _unlitBaseCache.Invalidate();
        _litBaseCache.Invalidate();
        _lightCache.Invalidate();
    }

    /// <summary>Called when batches are ready.</summary>
    protected virtual void OnBatchesReady()
    {
    }

    private void ProcessGeometryBatch(GeometryBatch geometryBatch)
    {
        // Skip invalid batches. It may happen if UpdateGeometry removed some source batches.
        var desc = new PipelineBatchDesc(geometryBatch.Drawable!, checked((int)geometryBatch.SourceBatchIndex),
            geometryBatch.DeferredPass, geometryBatch.UserData);
        if (desc.Geometry is null)
            return;

        if (desc.Material is null)
            desc.Material = _defaultMaterial;

        // Always add deferred batch if possible.
        if (desc.Pass is not null)
        {
            AddPipelineBatch(desc, _deferredCache, DeferredBatches, _delayedDeferredBatches);
            return;
        }

        // Process forward lighting if applicable
        var litBaseLightIndex = int.MaxValue;
        if (geometryBatch.LightPass is not null)
        {
            var drawableIndex = desc.Drawable!.GetDrawableIndex();
            ref readonly var lightAccumulator = ref _drawableProcessor.GetGeometryLighting(drawableIndex);
            var pixelLights = lightAccumulator.PixelLights;

            // Add light batches
            desc.Pass = geometryBatch.LightPass;
            for (var i = 0; i < pixelLights.Length; ++i)
            {
                var lightIndex = pixelLights[i].LightIndex;
                var lightProcessor = _drawableProcessor.GetLightProcessor(checked((uint)lightIndex));
                if (lightProcessor is null)
                    continue;
                var light = lightProcessor.Light;

                // Combine first directional additive light with base pass, if possible
                if (geometryBatch.LitBasePass is not null && litBaseLightIndex == int.MaxValue
                    && light.GetLightType() == LightType.Directional && !light.IsNegative())
                {
                    litBaseLightIndex = lightIndex;
                    continue;
                }

                desc.InitializeLitBatch(lightProcessor, lightIndex, lightProcessor.ForwardLitHash);

                if (light.IsNegative())
                    AddPipelineBatch(desc, _lightCache, NegativeLightBatches, _delayedNegativeLightBatches);
                else
                    AddPipelineBatch(desc, _lightCache, LightBatches, _delayedLightBatches);
            }

            // Initialize vertex lights after all light batches
            desc.VertexLightsHash = lightAccumulator.VertexLightsHash;
        }

        // Add base pass if needed
        if (litBaseLightIndex != int.MaxValue)
        {
            var light = _drawableProcessor.GetLightProcessor(checked((uint)litBaseLightIndex))!;
            desc.InitializeLitBatch(light, litBaseLightIndex, light.ForwardLitHash);
            desc.Pass = geometryBatch.LitBasePass;
            AddPipelineBatch(desc, _litBaseCache, BaseBatches, _delayedLitBaseBatches);
        }
        else
        {
            desc.InitializeLitBatch(null, int.MaxValue, 0);
            desc.Pass = geometryBatch.UnlitBasePass;
            AddPipelineBatch(desc, _unlitBaseCache, BaseBatches, _delayedUnlitBaseBatches);
        }
    }

    private void ResolveDelayedBatches(BatchCompositorSubpass subpass,
        WorkQueueVector<PipelineBatchDesc> delayedBatches,
        BatchStateCache cache, WorkQueueVector<PipelineBatch> batches)
    {
        if (_batchStateCacheCallback is null)
            return;

        var ctx = new BatchStateCreateContext(this, (int)subpass, 0);
        foreach (var desc in delayedBatches)
        {
            var pipelineState = cache.GetOrCreatePipelineState(desc.GetKey(), ctx, _batchStateCacheCallback);
            if (pipelineState is null)
                continue;

            ref var pipelineBatch = ref batches.Emplace(() => new PipelineBatch(desc));
            if (pipelineState.IsValid)
                pipelineBatch.PipelineState = pipelineState;
            else if (GetPlaceholderPipelineState(cache, pipelineState) is { } placeholder)
            {
                pipelineBatch.PipelineState = placeholder;
                pipelineBatch.GeometryType = GeometryType.StaticNoInstancing;
            }
            else
            {
                // This should never happen
                batches.PopBack(WorkQueue.GetThreadIndex());
            }
        }
    }

    private void AddPipelineBatch(PipelineBatchDesc desc, BatchStateCache cache,
        WorkQueueVector<PipelineBatch> batches, WorkQueueVector<PipelineBatchDesc> delayedBatches)
    {
        var pipelineState = cache.GetPipelineState(desc.GetKey().LookupKey);
        if (pipelineState is not null && pipelineState.IsValid)
        {
            ref var pipelineBatch = ref batches.Emplace(() => new PipelineBatch(desc));
            pipelineBatch.PipelineState = pipelineState;
        }
        else
            delayedBatches.Insert(desc);
    }

    private PipelineState? GetPlaceholderPipelineState(BatchStateCache cache, PipelineState original)
    {
        if (_batchStateCacheCallback is null)
            return null;
        var desc = original.GetDesc();
        var graphicsDesc = desc.AsGraphics();
        if (graphicsDesc is not { } graphics || graphics.InputLayout.Size == 0)
            return null;

        var vertexSize = graphics.InputLayout.Elements[0].BufferStride;
        return cache.GetOrCreatePlaceholderPipelineState(vertexSize, _batchStateCacheCallback);
    }

}

/// <summary>Batch composition manager.</summary>
public class BatchCompositor : Object
{
    public const int ShadowSubpass = 0;
    public const int LitVolumeSubpass = 1;

    private readonly IRenderPipelineInterface _renderPipeline;
    private readonly int _shadowPassIndex;
    private readonly WorkQueue? _workQueue;
    private readonly Renderer? _renderer;
    private readonly DrawableProcessor _drawableProcessor;
    private readonly Material? _defaultMaterial;
    private readonly BatchStateCacheCallback? _batchStateCacheCallback;

    private readonly List<BatchCompositorPass> _allPasses = [];
    private readonly List<BatchCompositorPass> _passes = [];
    private MaterialQuality _shadowMaterialQuality;
    private readonly Material? _lightVolumeMaterial;
    private readonly Material? _negativeLightVolumeMaterial;
    private readonly Pass _lightVolumePass;

    private readonly BatchStateCache _shadowCache = new();
    private readonly BatchStateCache _lightVolumeCache = new();

    private readonly WorkQueueVector<(ShadowSplitProcessor Split, PipelineBatchDesc Desc)> _delayedShadowBatches = new();
    private readonly List<PipelineBatch> _lightVolumeBatches = [];
    private readonly List<PipelineBatchByState> _sortedLightVolumeBatches = [];

    public BatchCompositor(IRenderPipelineInterface renderPipeline, DrawableProcessor drawableProcessor,
        BatchStateCacheCallback? callback, int shadowPassIndex)
    {
        ArgumentNullException.ThrowIfNull(renderPipeline);
        _renderPipeline = renderPipeline;
        _drawableProcessor = drawableProcessor ?? throw new ArgumentNullException(nameof(drawableProcessor));
        _batchStateCacheCallback = callback;
        _shadowPassIndex = shadowPassIndex;
        _workQueue = GetSubsystem<WorkQueue>();
        _renderer = GetSubsystem<Renderer>();
        _defaultMaterial = _renderer?.GetDefaultMaterial();

        if (_defaultMaterial is not null)
        {
            _lightVolumeMaterial = _defaultMaterial.Clone("[Internal]/LightVolume");
            _negativeLightVolumeMaterial = _defaultMaterial.Clone("[Internal]/NegativeLightVolume");
            _negativeLightVolumeMaterial.SetRenderOrder((byte)(128 + 1));
        }

        _lightVolumePass = new Pass("lightvolume");
        _lightVolumePass.SetVertexShader("DeferredLight");
        _lightVolumePass.SetPixelShader("DeferredLight");

        renderPipeline.UpdateBegin += OnUpdateBegin;
        renderPipeline.PipelineStatesInvalidated += OnPipelineStatesInvalidated;
    }

    public override void Dispose()
    {
        _sortedLightVolumeBatches.Clear();
        _lightVolumeBatches.Clear();
        _delayedShadowBatches.Clear();
        _lightVolumeCache.Dispose();
        _shadowCache.Dispose();
        _lightVolumePass.Dispose();
        _negativeLightVolumeMaterial?.Dispose();
        _lightVolumeMaterial?.Dispose();
        _passes.Clear();
        _allPasses.Clear();
        _renderPipeline.UpdateBegin -= OnUpdateBegin;
        _renderPipeline.PipelineStatesInvalidated -= OnPipelineStatesInvalidated;
        base.Dispose();
    }

    public void SetPasses(IReadOnlyList<BatchCompositorPass> passes)
    {
        _allPasses.Clear();
        _allPasses.AddRange(passes);
    }

    public void SetShadowMaterialQuality(MaterialQuality materialQuality) =>
        _shadowMaterialQuality = materialQuality;

    public void SetShadowOutputDesc(PipelineStateOutputDesc desc) => _shadowCache.SetOutputDesc(desc);

    public void SetLightVolumesOutputDesc(PipelineStateOutputDesc desc) => _lightVolumeCache.SetOutputDesc(desc);

    public void ComposeShadowBatches()
    {
        // Collect shadow caster batches in worker threads
        var lightProcessors = _drawableProcessor.LightProcessors;
        for (var lightIndex = 0; lightIndex < lightProcessors.Count; ++lightIndex)
        {
            var lightProcessor = lightProcessors[lightIndex];
            if (lightProcessor is null)
                continue;
            var numSplits = lightProcessor.NumSplits;
            for (var splitIndex = 0; splitIndex < numSplits; ++splitIndex)
            {
                var capturedLightIndex = lightIndex;
                var split = lightProcessor.GetMutableSplit(splitIndex);
                if (_workQueue is not null)
                {
                    _workQueue.PostTask(_ =>
                    {
                        BeginShadowBatchesComposition(capturedLightIndex, split);
                    }, TaskPriority.Immediate);
                }
                else
                    BeginShadowBatchesComposition(capturedLightIndex, split);
            }
        }

        _workQueue?.CompleteImmediateForThisThread();

        // Finalize shadow batches
        FinalizeShadowBatchesComposition();
    }

    public void ComposeSceneBatches()
    {
        foreach (var pass in _passes)
            pass.ComposeBatches();
    }

    public void ComposeLightVolumeBatches()
    {
        if (_batchStateCacheCallback is null || _renderer is null)
            return;

        var ctx = new BatchStateCreateContext(this, LitVolumeSubpass, 0);

        var lightProcessors = _drawableProcessor.LightProcessors;
        for (var lightIndex = 0; lightIndex < lightProcessors.Count; ++lightIndex)
        {
            var lightProcessor = lightProcessors[lightIndex];
            if (lightProcessor is null || !lightProcessor.HasLitGeometries)
                continue;

            var light = lightProcessor.Light;

            var desc = new PipelineBatchDesc();
            desc.InitializeLitBatch(lightProcessor, lightIndex, lightProcessor.LightVolumeHash);
            desc.DrawableHash = 0;
            desc.SourceBatchIndex = int.MaxValue;
            desc.GeometryType = GeometryType.StaticNoInstancing;
            desc.Geometry = _renderer.GetLightGeometry(light);
            desc.Material = light.IsNegative() ? _negativeLightVolumeMaterial : _lightVolumeMaterial;
            desc.Pass = _lightVolumePass;
            desc.Drawable = light;
            desc.VertexLightsHash = 0;

            if (desc.Geometry is null || desc.Material is null)
                continue;

            var pipelineState = _lightVolumeCache.GetOrCreatePipelineState(desc.GetKey(), ctx, _batchStateCacheCallback);
            if (pipelineState is not null && pipelineState.IsValid)
            {
                var pipelineBatch = new PipelineBatch(desc);
                pipelineBatch.PipelineState = pipelineState;
                _lightVolumeBatches.Add(pipelineBatch);
            }
        }

        FillSortKeys(_sortedLightVolumeBatches, _lightVolumeBatches);
        _sortedLightVolumeBatches.Sort();
    }

    public IReadOnlyList<PipelineBatchByState> GetLightVolumeBatches() => _sortedLightVolumeBatches;

    /// <summary>Prepare vector of sorted batches w/o actual sorting.</summary>
    public static void FillSortKeys<T>(List<T> sortedBatches, params IEnumerable<PipelineBatch>[] pipelineBatches)
        where T : struct
    {
        var totalSize = 0;
        foreach (var batches in pipelineBatches)
        {
            if (batches is ICollection<PipelineBatch> collection)
                totalSize += collection.Count;
            else if (batches is MultiVector<PipelineBatch> multi)
                totalSize += (int)multi.Size();
            else
                totalSize += batches.Count();
        }

        sortedBatches.Clear();
        if (sortedBatches.Capacity < totalSize)
            sortedBatches.Capacity = totalSize;

        foreach (var batches in pipelineBatches)
        {
            foreach (var pipelineBatch in batches)
            {
                if (typeof(T) == typeof(PipelineBatchByState))
                    sortedBatches.Add((T)(object)new PipelineBatchByState(pipelineBatch));
                else if (typeof(T) == typeof(PipelineBatchBackToFront))
                    sortedBatches.Add((T)(object)new PipelineBatchBackToFront(pipelineBatch));
            }
        }
    }

    /// <summary>Overload accepting WorkQueueVector sources (matches C++ variadic FillSortKeys).</summary>
    public static void FillSortKeys<T>(List<T> sortedBatches, params MultiVector<PipelineBatch>[] pipelineBatches)
        where T : struct
    {
        var arrays = new IEnumerable<PipelineBatch>[pipelineBatches.Length];
        for (var i = 0; i < pipelineBatches.Length; ++i)
            arrays[i] = pipelineBatches[i];
        FillSortKeys(sortedBatches, arrays);
    }

    protected virtual void OnUpdateBegin(CommonFrameInfo frameInfo)
    {
        _ = frameInfo;
        _delayedShadowBatches.Clear();
        _lightVolumeBatches.Clear();
        _sortedLightVolumeBatches.Clear();

        _passes.Clear();
        foreach (var pass in _allPasses)
        {
            if (pass.IsEnabled)
                _passes.Add(pass);
        }
    }

    protected virtual void OnPipelineStatesInvalidated()
    {
        _shadowCache.Invalidate();
        _lightVolumeCache.Invalidate();
    }

    /// <summary>Safe to call from worker thread.</summary>
    protected void BeginShadowBatchesComposition(int lightIndex, ShadowSplitProcessor splitProcessor)
    {
        var lightProcessor = splitProcessor.LightProcessor;
        var lightHash = lightProcessor.GetShadowHash(splitProcessor.SplitIndex);

        var threadIndex = WorkQueue.GetThreadIndex();
        var shadowCasters = splitProcessor.ShadowCasters;
        var shadowBatches = splitProcessor.MutableUnsortedShadowBatches;
        var lightMask = splitProcessor.Light.GetLightMask();

        foreach (var drawable in shadowCasters)
        {
            // Check shadow mask now when zone is ready
            if ((drawable.GetShadowMaskInZone() & lightMask) == 0)
                continue;

            // Check shadow distance
            var maxShadowDistance = drawable.GetShadowDistance();
            var drawDistance = drawable.GetDrawDistance();
            if (drawDistance > 0.0f && (maxShadowDistance <= 0.0f || drawDistance < maxShadowDistance))
                maxShadowDistance = drawDistance;
            if (maxShadowDistance > 0.0f && drawable.GetDistance() > maxShadowDistance)
                continue;

            // Add batches
            var sourceBatches = drawable.GetBatches();
            for (var j = 0; j < sourceBatches.Count; ++j)
            {
                var sourceBatch = sourceBatches[j];
                if (sourceBatch.Geometry is null || sourceBatch.NumWorldTransforms == 0)
                    continue;

                var material = sourceBatch.Material ?? _defaultMaterial;
                if (material is null)
                    continue;
                var tech = material.FindTechnique(drawable, _shadowMaterialQuality);
                if (tech is null)
                    continue;
                var pass = tech.GetPass(checked((uint)_shadowPassIndex));
                if (pass is null)
                    continue;

                var desc = new PipelineBatchDesc(drawable, j, pass);
                desc.Material = material;
                desc.InitializeShadowBatch(lightProcessor, lightIndex, lightHash);

                var pipelineState = _shadowCache.GetPipelineState(desc.GetKey().LookupKey);
                if (pipelineState is not null)
                {
                    if (pipelineState.IsValid)
                    {
                        var pipelineBatch = new PipelineBatch(desc);
                        pipelineBatch.PipelineState = pipelineState;
                        shadowBatches.Add(pipelineBatch);
                    }
                }
                else
                    _delayedShadowBatches.PushBack(threadIndex, (splitProcessor, desc));
            }
        }
    }

    /// <summary>Should be called from main thread.</summary>
    protected void FinalizeShadowBatchesComposition()
    {
        if (_batchStateCacheCallback is null)
            return;

        var ctx = new BatchStateCreateContext(this, ShadowSubpass, 0);

        foreach (var (split, desc) in _delayedShadowBatches)
        {
            ctx = new BatchStateCreateContext(this, ShadowSubpass, split.SplitIndex);
            var pipelineState = _shadowCache.GetOrCreatePipelineState(desc.GetKey(), ctx, _batchStateCacheCallback);
            if (pipelineState is not null && pipelineState.IsValid)
            {
                var pipelineBatch = new PipelineBatch(desc);
                pipelineBatch.PipelineState = pipelineState;
                split.MutableUnsortedShadowBatches.Add(pipelineBatch);
            }
        }

        // Finalize shadow batches
        var lightProcessors = _drawableProcessor.LightProcessors;
        for (var lightIndex = 0; lightIndex < lightProcessors.Count; ++lightIndex)
        {
            var lightProcessor = lightProcessors[lightIndex];
            if (lightProcessor is null)
                continue;
            var numSplits = lightProcessor.NumSplits;
            for (var splitIndex = 0; splitIndex < numSplits; ++splitIndex)
            {
                var split = lightProcessor.GetMutableSplit(splitIndex);
                if (_workQueue is not null)
                {
                    _workQueue.PostTask(_ => { split.FinalizeShadowBatches(); }, TaskPriority.Immediate);
                }
                else
                    split.FinalizeShadowBatches();
            }
        }

        _workQueue?.CompleteImmediateForThisThread();
    }

}
