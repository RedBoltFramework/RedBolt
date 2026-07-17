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
        _sceneZRange = default;
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
