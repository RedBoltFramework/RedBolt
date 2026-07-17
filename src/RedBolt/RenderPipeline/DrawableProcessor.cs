namespace RedBolt;

/// <summary>
/// Flags related to geometry rendering.
/// Use old enum for simplified flag manipulation, avoid FlagSet for quick reset.
/// </summary>
[Flags]
public enum GeometryRenderFlag : byte
{
    VisibleInCullCamera = 1 << 0,
    Lit = 1 << 1,
    ForwardLit = 1 << 2
}

public struct SortedOccluder : IComparable<SortedOccluder>
{
    public float SortValue;
    public Drawable? Drawable;

    public readonly int CompareTo(SortedOccluder other) => SortValue.CompareTo(other.SortValue);
}

/// <summary>
/// Reference to SourceBatch of Drawable geometry, with resolved material passes.
/// </summary>
public struct GeometryBatch
{
    public static GeometryBatch Deferred(Drawable drawable, uint sourceBatchIndex, Pass deferredPass, object? userData = null)
    {
        GeometryBatch batch = default;
        batch.Drawable = drawable;
        batch.SourceBatchIndex = sourceBatchIndex;
        batch.DeferredPass = deferredPass;
        batch.UserData = userData;
        return batch;
    }

    public static GeometryBatch Forward(Drawable drawable, uint sourceBatchIndex,
        Pass unlitBasePass, Pass? litBasePass, Pass? lightPass, object? userData = null)
    {
        GeometryBatch batch = default;
        batch.Drawable = drawable;
        batch.SourceBatchIndex = sourceBatchIndex;
        batch.UnlitBasePass = unlitBasePass;
        batch.LitBasePass = litBasePass;
        batch.LightPass = lightPass;
        batch.UserData = userData;
        return batch;
    }

    public Drawable? Drawable;
    public uint SourceBatchIndex;
    public object? UserData;

    /// <summary>
    /// If deferred pass is present, unlit base, lit base and light passes are ignored.
    /// </summary>
    public Pass? DeferredPass;
    /// <summary>
    /// Unlit base pass (no per-pixel lighting, optional ambient lighting).
    /// </summary>
    public Pass? UnlitBasePass;
    /// <summary>
    /// Lit base pass (per-pixel lighting from one light source and ambient lighting).
    /// </summary>
    public Pass? LitBasePass;
    /// <summary>
    /// Additive light pass (per-pixel lighting from one light source).
    /// </summary>
    public Pass? LightPass;
}

/// <summary>
/// Interface of scene pass used by drawable processor.
///
/// There are 4 types of batches:
/// 1) Deferred Pass: if present, used unconditionally. Unlit base, lit base and light passes are ignored.
/// 2) Unlit Base + Lit Base + Light:
///    Batch is rendered in N passes if the first per-pixel light can be rendered together with ambient.
///    Batch is rendered in N + 1 passes otherwise.
///    N is equal to the number per-pixel lights affecting object.
/// 3) Unlit Base + Light: batch is rendered with per-pixel forward lighting in N + 1 passes.
/// 4) Unlit Base: batch is rendered once, per-pixel lighting is not applied.
///
/// Other pass combinations are invalid.
/// </summary>
public class DrawableProcessorPass : Object
{
    public readonly record struct AddBatchResult(bool Added, bool ForwardLitAdded);

    private readonly DrawableProcessorPassFlag _flags;
    private readonly bool _useBatchCallback;

    private readonly uint _deferredPassIndex;
    private readonly uint _unlitBasePassIndex;
    private readonly uint _litBasePassIndex;
    private readonly uint _lightPassIndex;

    private bool _enabled = true;

    protected readonly IRenderPipelineInterface RenderPipeline;
    protected readonly WorkQueueVector<GeometryBatch> GeometryBatches = new();

    protected bool LinearColorSpace;

    public DrawableProcessorPass(IRenderPipelineInterface renderPipeline, DrawableProcessorPassFlag flags,
        uint deferredPassIndex, uint unlitBasePassIndex, uint litBasePassIndex, uint lightPassIndex)
    {
        RenderPipeline = renderPipeline;
        _flags = flags;
        _useBatchCallback = IsFlagSet(DrawableProcessorPassFlag.BatchCallback);
        _deferredPassIndex = deferredPassIndex;
        _unlitBasePassIndex = unlitBasePassIndex;
        _litBasePassIndex = litBasePassIndex;
        _lightPassIndex = lightPassIndex;

        renderPipeline.UpdateBegin += OnUpdateBegin;
    }

    public override void Dispose()
    {
        RenderPipeline.UpdateBegin -= OnUpdateBegin;
        base.Dispose();
    }

    public void SetEnabled(bool enabled) => _enabled = enabled;

    public bool IsEnabled() => _enabled;

    /// <summary>
    /// Custom callback for adding batches.
    /// </summary>
    public virtual AddBatchResult AddCustomBatch(uint threadIndex, Drawable drawable, uint sourceBatchIndex, Technique technique) => default;

    public AddBatchResult AddBatch(uint threadIndex, Drawable drawable, uint sourceBatchIndex, Technique technique)
    {
        if (_useBatchCallback)
            return AddCustomBatch(threadIndex, drawable, sourceBatchIndex, technique);

        var deferredPass = technique.GetPass(_deferredPassIndex);
        if (deferredPass is not null)
        {
            GeometryBatches.PushBack(threadIndex, GeometryBatch.Deferred(drawable, sourceBatchIndex, deferredPass));
            return new AddBatchResult(true, false);
        }

        var unlitBasePass = technique.GetPass(_unlitBasePassIndex);
        var lightPass = technique.GetPass(_lightPassIndex);
        var litBasePass = lightPass is not null ? technique.GetPass(_litBasePassIndex) : null;

        if (unlitBasePass is null)
            return default;

        GeometryBatches.PushBack(threadIndex,
            GeometryBatch.Forward(drawable, sourceBatchIndex, unlitBasePass, litBasePass, lightPass));
        return new AddBatchResult(true, lightPass is not null);
    }

    public DrawableProcessorPassFlag GetFlags() => _flags;

    public bool IsFlagSet(DrawableProcessorPassFlag flag) => (_flags & flag) != 0;

    public bool HasLightPass() => _lightPassIndex != uint.MaxValue;

    /// <summary>
    /// RenderPipeline callbacks
    /// </summary>
    protected virtual void OnUpdateBegin(CommonFrameInfo frameInfo)
    {
        GeometryBatches.Clear();
        LinearColorSpace = RenderPipeline.IsLinearColorSpace;
    }
}

/// <summary>Utility used to update and process visible or shadow caster Drawables.</summary>
public class DrawableProcessor : Object
{
    private readonly IRenderPipelineInterface _renderPipeline;
    private readonly List<DrawableProcessorPass> _passes = [];
    private readonly List<Light?> _lights = [];
    private readonly List<LightProcessor?> _lightProcessors = [];
    private readonly List<Drawable?> _geometries = [];
    private readonly List<SortedOccluder> _sortedOccluders = [];
    private readonly Dictionary<uint, LightAccumulator> _geometryLighting = [];
    private readonly Dictionary<uint, GeometryRenderFlag> _geometryRenderFlags = [];
    private readonly Dictionary<uint, FloatRange> _geometryZRanges = [];
    private readonly HashSet<uint> _isDrawableUpdated = [];
    private readonly WorkQueueVector<Drawable> _queuedDrawableUpdates = new();
    private readonly WorkQueueVector<Drawable> _threadedGeometryUpdates = new();
    private readonly WorkQueueVector<Drawable> _nonThreadedGeometryUpdates = new();
    private DrawableProcessorSettings _settings = new();
    private CommonFrameInfo _frameInfo = new();
    private FloatRange _sceneZRange;

    public DrawableProcessor(IRenderPipelineInterface renderPipeline)
    {
        _renderPipeline = renderPipeline ?? throw new ArgumentNullException(nameof(renderPipeline));
        _renderPipeline.UpdateBegin += OnUpdateBegin;
        _renderPipeline.CollectStatistics += OnCollectStatistics;
    }

    public override void Dispose()
    {
        _renderPipeline.UpdateBegin -= OnUpdateBegin;
        _renderPipeline.CollectStatistics -= OnCollectStatistics;
        base.Dispose();
    }

    public void SetPasses(IReadOnlyList<DrawableProcessorPass> passes)
    {
        _passes.Clear();
        _passes.AddRange(passes);
    }

    public void SetSettings(DrawableProcessorSettings settings) => _settings = settings;

    public CommonFrameInfo GetFrameInfo() => _frameInfo;

    public DrawableProcessorSettings GetSettings() => _settings;

    public bool HasOccluders() => _sortedOccluders.Count != 0;

    public IReadOnlyList<SortedOccluder> GetOccluders() => _sortedOccluders;

    public IReadOnlyList<Drawable?> GetGeometries() => _geometries;

    public FloatRange GetSceneZRange() => _sceneZRange;

    public IReadOnlyList<Light?> GetLights() => _lights;

    public Light? GetLight(uint lightIndex) =>
        lightIndex < (uint)_lights.Count ? _lights[(int)lightIndex] : null;

    public IReadOnlyList<LightProcessor?> GetLightProcessors() => _lightProcessors;

    public LightProcessor? GetLightProcessor(uint lightIndex) =>
        lightIndex < (uint)_lightProcessors.Count ? _lightProcessors[(int)lightIndex] : null;

    public LightAccumulator GetGeometryLighting(uint drawableIndex) =>
        _geometryLighting.TryGetValue(drawableIndex, out var lighting) ? lighting : new LightAccumulator();

    public void SetGeometryLighting(uint drawableIndex, LightAccumulator lighting) =>
        _geometryLighting[drawableIndex] = lighting;

    public GeometryRenderFlag GetGeometryRenderFlags(uint drawableIndex) =>
        _geometryRenderFlags.TryGetValue(drawableIndex, out var flags) ? flags : default;

    public void SetGeometryRenderFlags(uint drawableIndex, GeometryRenderFlag flags) =>
        _geometryRenderFlags[drawableIndex] = flags;

    public FloatRange GetGeometryZRange(uint drawableIndex) =>
        _geometryZRanges.TryGetValue(drawableIndex, out var range) ? range : default;

    public void SetGeometryZRange(uint drawableIndex, FloatRange range) =>
        _geometryZRanges[drawableIndex] = range;

    public void SetSceneZRange(FloatRange range) => _sceneZRange = range;

    /// <summary>Replace the per-frame light list used by pipeline batches and debugger reporting.</summary>
    public void SetLights(IReadOnlyList<Light?> lights)
    {
        _lights.Clear();
        _lights.AddRange(lights);
        _lightProcessors.Clear();
        foreach (var light in lights)
            _lightProcessors.Add(light is null ? null : new LightProcessor(light));
    }

    /// <summary>Replace the per-frame geometry list used by scene processing.</summary>
    public void SetGeometries(IReadOnlyList<Drawable?> geometries)
    {
        _geometries.Clear();
        _geometries.AddRange(geometries);
    }

    public void OnUpdateBegin(CommonFrameInfo frameInfo)
    {
        _frameInfo = frameInfo;
        _sortedOccluders.Clear();
        _geometries.Clear();
        _lights.Clear();
        _lightProcessors.Clear();
        _geometryLighting.Clear();
        _geometryRenderFlags.Clear();
        _geometryZRanges.Clear();
        _isDrawableUpdated.Clear();
        _queuedDrawableUpdates.Clear();
        _threadedGeometryUpdates.Clear();
        _nonThreadedGeometryUpdates.Clear();
        _sceneZRange = default;
    }

    public void PreprocessShadowCasters(List<Drawable> shadowCasters,
        IReadOnlyList<Drawable> candidates, FloatRange frustumSubRange, Light light, Camera shadowCamera)
    {
        shadowCasters.Clear();

        var cullCamera = _frameInfo.Camera;
        if (cullCamera is null)
            return;

        var shadowCameraFrustum = shadowCamera.GetFrustum();
        var worldToLightSpace = shadowCamera.GetView();
        var lightType = light.GetLightType();

        // Convert frustum (or sub-frustum) to shadow camera space
        var splitZRange = lightType != LightType.Directional ? _sceneZRange : _sceneZRange & frustumSubRange;
        var frustum = cullCamera.GetSplitFrustum(splitZRange.First, splitZRange.Second);
        var lightSpaceFrustum = frustum.Transformed(worldToLightSpace);
        var lightSpaceFrustumBoundingBox = new BoundingBox(lightSpaceFrustum);

        // Check for degenerate split frustum: in that case there is no need to get shadow casters
        if (lightSpaceFrustum.Vertices[0] == lightSpaceFrustum.Vertices[4])
            return;

        foreach (var drawable in candidates)
        {
            // For point light, check that this drawable is inside the split shadow camera frustum
            if (lightType == LightType.Point
                && shadowCameraFrustum.IsInsideFast(drawable.GetWorldBoundingBox()) == Intersection.Outside)
                continue;

            // Queue shadow caster if it's visible
            var lightSpaceBoundingBox = drawable.GetWorldBoundingBox().Transformed(worldToLightSpace);
            var isDrawableVisible = (GetGeometryRenderFlags(drawable.GetDrawableIndex())
                & GeometryRenderFlag.VisibleInCullCamera) != 0;
            if (isDrawableVisible
                || IsShadowCasterVisible(lightSpaceBoundingBox, shadowCamera, lightSpaceFrustum, lightSpaceFrustumBoundingBox))
            {
                QueueDrawableUpdate(drawable);
                shadowCasters.Add(drawable);
            }
        }
    }

    public void QueueDrawableUpdate(Drawable drawable)
    {
        var drawableIndex = drawable.GetDrawableIndex();
        if (!_isDrawableUpdated.Add(drawableIndex))
            return;
        _queuedDrawableUpdates.Insert(drawable);
    }

    public void ProcessShadowCasters()
    {
        var workQueue = GetSubsystem<WorkQueue>();
        if (workQueue is not null)
        {
            var list = new List<Drawable>();
            _queuedDrawableUpdates.CopyTo(list);
            WorkQueue.ForEachParallel(workQueue, list, (_, drawable) => ProcessQueuedDrawable(drawable));
        }
        else
        {
            foreach (var drawable in _queuedDrawableUpdates)
                ProcessQueuedDrawable(drawable);
        }

        _queuedDrawableUpdates.Clear();
    }

    public void ProcessQueuedDrawable(Drawable drawable)
    {
        drawable.UpdateBatches(_frameInfo);
        drawable.MarkInView(_frameInfo);

        var boundingBox = drawable.GetWorldBoundingBox();
        UpdateDrawableZone(boundingBox, drawable);
        QueueDrawableGeometryUpdate(WorkQueue.GetThreadIndex(), drawable);
    }

    public void UpdateDrawableZone(BoundingBox boundingBox, Drawable drawable)
    {
        var drawableCenter = boundingBox.Center;
        ref var cachedZone = ref drawable.GetMutableCachedZone();
        var drawableCacheDistanceSquared = (cachedZone.CachePosition - drawableCenter).LengthSquared;

        // Force update if bounding box is invalid
        var forcedUpdate = !float.IsFinite(drawableCacheDistanceSquared);
        // TODO: Do we want strict compare here too?
        if (forcedUpdate || drawableCacheDistanceSquared >= cachedZone.CacheInvalidationDistanceSquared)
        {
            if (_frameInfo.Octree is not null)
            {
                cachedZone = _frameInfo.Octree.QueryZone(drawableCenter, drawable.GetZoneMask());
                drawable.MarkPipelineStateHashDirty();
            }
        }
    }

    public void QueueDrawableGeometryUpdate(uint threadIndex, Drawable drawable)
    {
        var updateGeometryType = drawable.GetUpdateGeometryType();
        if (updateGeometryType == UpdateGeometryType.MainThread)
            _nonThreadedGeometryUpdates.PushBack(threadIndex, drawable);
        else if (updateGeometryType == UpdateGeometryType.WorkerThread)
            _threadedGeometryUpdates.PushBack(threadIndex, drawable);
    }

    private static bool IsBoundingBoxShadowInOrthoFrustum(BoundingBox boundingBox,
        Frustum frustum, BoundingBox frustumBoundingBox)
    {
        // Extrude the bounding box up to the far edge of the frustum's light space bounding box
        var extrudedBoundingBox = boundingBox;
        extrudedBoundingBox.Max = new Vector3f(
            extrudedBoundingBox.Max.X,
            extrudedBoundingBox.Max.Y,
            Math.Max(extrudedBoundingBox.Max.Z, frustumBoundingBox.Max.Z));
        return frustum.IsInsideFast(extrudedBoundingBox) != Intersection.Outside;
    }

    private static bool IsBoundingBoxShadowInPerspectiveFrustum(BoundingBox boundingBox,
        Frustum frustum, float extrusionDistance)
    {
        // Extrusion direction depends on the position of the shadow caster
        var center = boundingBox.Center;
        var size = boundingBox.Size;

        // Because of the perspective, the bounding box must also grow when it is extruded to the distance
        var originalDistance = MathDefs.Clamp(center.Z - size.Z * 0.5f, MathDefs.Epsilon, extrusionDistance);
        var sizeFactor = extrusionDistance / originalDistance;

        // Calculate the endpoint box and merge it to the original. Because it's axis-aligned, it will be larger
        // than necessary, so the test will be conservative
        var newCenter = center * sizeFactor;
        var newHalfSize = size * sizeFactor * 0.5f;

        var extrudedBox = new BoundingBox(newCenter - newHalfSize, newCenter + newHalfSize);
        extrudedBox.Merge(boundingBox);

        return frustum.IsInsideFast(extrudedBox) != Intersection.Outside;
    }

    private static bool IsShadowCasterVisible(BoundingBox lightSpaceBoundingBox, Camera shadowCamera,
        Frustum lightSpaceFrustum, BoundingBox lightSpaceFrustumBoundingBox)
    {
        if (shadowCamera.IsOrthographic())
            return IsBoundingBoxShadowInOrthoFrustum(lightSpaceBoundingBox, lightSpaceFrustum, lightSpaceFrustumBoundingBox);
        return IsBoundingBoxShadowInPerspectiveFrustum(lightSpaceBoundingBox, lightSpaceFrustum, shadowCamera.GetFarClip());
    }

    public void OnCollectStatistics(RenderPipelineStats stats)
    {
        stats.NumGeometries += (uint)_geometries.Count;
        stats.NumLights += (uint)_lights.Count;
    }

    public void ProcessOccluders(IReadOnlyList<Drawable> occluders, float sizeThreshold)
    {
        _sortedOccluders.Clear();
        foreach (var drawable in occluders)
        {
            if (!drawable.Occluder)
                continue;
            _sortedOccluders.Add(new SortedOccluder
            {
                SortValue = drawable.GetDistance(),
                Drawable = drawable
            });
        }
        _sortedOccluders.Sort();
        _ = sizeThreshold;
    }
}
