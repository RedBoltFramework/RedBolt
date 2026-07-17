namespace RedBolt;

/// <summary>Cached internal structure for reflection probe search.</summary>
public class InternalReflectionProbeData
{
    private Matrix3x4 _worldToLocal;
    private BoundingBox _localBoundingBox;
    private BoundingBox _worldBoundingBox;

    public InternalReflectionProbeData(ReflectionProbe probe)
    {
        Probe = probe ?? throw new ArgumentNullException(nameof(probe));
        Data = probe.GetProbeData();
        Priority = probe.GetPriority();
        Update();
    }

    public ReflectionProbe Probe { get; }

    public ReflectionProbeData Data { get; private set; }

    public int Priority { get; }

    public void Update()
    {
        var node = Probe.Node;
        if (node is null)
            return;

        var worldTransform = node.GetWorldTransform();
        _worldToLocal = worldTransform.Inverse;
        _localBoundingBox = Probe.GetBoundingBox();
        _worldBoundingBox = Probe.GetBoundingBox().Transformed(worldTransform);
        Data = Probe.GetProbeData();
    }

    public float? GetIntersectionVolume(BoundingBox worldBoundingBox)
    {
        var localBoundingBox = worldBoundingBox.Transformed(_worldToLocal);
        var clippedBoundingBox = localBoundingBox.Clipped(_localBoundingBox);
        if (clippedBoundingBox.Defined)
            return clippedBoundingBox.Volume / Math.Max(localBoundingBox.Volume, MathDefs.Epsilon);
        return null;
    }

    public BoundingBox GetWorldBoundingBox() => _worldBoundingBox;
}

/// <summary>Node of static reflection probes tree.</summary>
public struct ReflectionProbeBVH
{
    public BoundingBox WorldBoundingBox;
    public InternalReflectionProbeData? Data;
}

/// <summary>Type of reflection probe.</summary>
public enum ReflectionProbeType
{
    Baked,
    Mixed,
    Dynamic,
    CustomTexture
}

/// <summary>Reflection probe manager.</summary>
public class ReflectionProbeManager : TrackedComponentRegistryBase
{
    public const float DefaultQueryPadding = 2.0f;
    public const uint DefaultRenderBudget = 6;
    private const uint MaxStaticUpdates = 1;

    private struct QueuedReflectionProbe
    {
        public ReflectionProbe? Probe;
        public CubemapRenderer? CubemapRenderer;
    }

    private struct SpatialCache
    {
        public bool Dirty;
        public uint Revision;
        public List<InternalReflectionProbeData> MovableProbes;
        public List<ReflectionProbe> ImmovableProbes;
        public List<ReflectionProbeBVH> ImmovableProbesBvh;
    }

    private struct AutoQueueCache
    {
        public bool Dirty;
        public List<ReflectionProbe> RealtimeProbes;
    }

    private SpatialCache _spatial = new()
    {
        MovableProbes = [],
        ImmovableProbes = [],
        ImmovableProbesBvh = []
    };

    private AutoQueueCache _autoQueue = new() { RealtimeProbes = [] };

    private CubemapRenderer? _cubemapRenderer;
    private float _queryPadding = DefaultQueryPadding;
    private uint _renderBudget = DefaultRenderBudget;
    private bool _filterCubemaps = true;
    private readonly HashSet<ReflectionProbe> _probesToUpdate = [];
    private readonly List<QueuedReflectionProbe> _updateQueue = [];

    public ReflectionProbeManager()
        : base(typeof(ReflectionProbe).GetClassName())
    {
    }

    public override bool IsOnlyEnabledTracked => true;

    public float QueryPadding
    {
        get => _queryPadding;
        set => _queryPadding = value;
    }

    public uint RenderBudget
    {
        get => _renderBudget;
        set => _renderBudget = value;
    }

    public bool FilterCubemaps
    {
        get => _filterCubemaps;
        set => _filterCubemaps = value;
    }

    /// <summary>Mark reflection probe as dirty, i.e. position or dimensions changed.</summary>
    public void MarkProbeDirty(ReflectionProbe reflectionProbe)
    {
        _spatial.Dirty = true;
        _autoQueue.Dirty = true;
    }

    public void MarkProbeTransformDirty(ReflectionProbe reflectionProbe)
    {
        if (!reflectionProbe.IsMovable())
        {
            _spatial.Dirty = true;
            _autoQueue.Dirty = true;
        }
    }

    public void MarkProbeRealtimeDirty(ReflectionProbe reflectionProbe) => _autoQueue.Dirty = true;

    /// <summary>Queue reflection probe rendering as soon as possible.</summary>
    public void QueueProbeUpdate(ReflectionProbe reflectionProbe)
    {
        if (reflectionProbe.GetProbeType() != ReflectionProbeType.CustomTexture)
            _probesToUpdate.Add(reflectionProbe);
    }

    /// <summary>Queue rendering of all Baked reflection probes.</summary>
    public void QueueBakeAll()
    {
        foreach (var reflectionProbe in GetReflectionProbes())
            QueueProbeUpdate(reflectionProbe);
    }

    /// <summary>Update reflection probes if dirty. Usually called internally once per frame.</summary>
    public void Update()
    {
        if (_spatial.Dirty)
            UpdateSpatialCache();

        if (_autoQueue.Dirty)
            UpdateAutoQueueCache();

        foreach (var probeData in _spatial.MovableProbes)
            probeData.Update();

        foreach (var probe in _autoQueue.RealtimeProbes)
            QueueProbeUpdate(probe);

        if (_updateQueue.Count == 0)
            FillUpdateQueue();

        ConsumeUpdateQueue();
    }

    /// <summary>Query two most important reflection probes.</summary>
    public void QueryDynamicProbes(BoundingBox worldBoundingBox, Span<ReflectionProbeReference> probes)
    {
        if (probes.Length < 2)
            return;

        if (_spatial.Dirty)
            UpdateSpatialCache();

        foreach (var probeData in _spatial.MovableProbes)
        {
            var volume = probeData.GetIntersectionVolume(worldBoundingBox);
            if (volume is null)
                continue;
            AppendReference(probes, new ReflectionProbeReference
            {
                Data = probeData.Data,
                Priority = probeData.Priority,
                Volume = volume.Value
            });
        }
    }

    public void QueryStaticProbes(BoundingBox worldBoundingBox, Span<ReflectionProbeReference> probes,
        ref float cacheDistanceSquared)
    {
        if (probes.Length < 2)
            return;

        if (_spatial.Dirty)
            UpdateSpatialCache();

        cacheDistanceSquared = MathDefs.LargeValue;
        var intersections = new List<ReflectionProbeBVH>();
        QueryBvh(intersections, _spatial.ImmovableProbesBvh, worldBoundingBox);

        foreach (var node in intersections)
        {
            if (node.Data is null)
                continue;
            var volume = node.Data.GetIntersectionVolume(worldBoundingBox);
            if (volume is null)
                continue;
            AppendReference(probes, new ReflectionProbeReference
            {
                Data = node.Data.Data,
                Priority = node.Data.Priority,
                Volume = volume.Value
            });
        }

        // Cache invalidation distance is half of query padding so probes re-query when moving far enough
        cacheDistanceSquared = _queryPadding * _queryPadding * 0.25f;
    }

    public IEnumerable<ReflectionProbe> GetReflectionProbes()
    {
        foreach (var component in GetTrackedComponents())
        {
            if (component is ReflectionProbe probe)
                yield return probe;
        }
    }

    public uint GetRevision() => _spatial.Revision;

    public bool HasStaticProbes() => _spatial.ImmovableProbes.Count > 0;

    public bool HasDynamicProbes() => _spatial.MovableProbes.Count > 0;

    protected internal override void OnAddedToScene(Scene scene)
    {
        _cubemapRenderer = new CubemapRenderer(scene);
    }

    protected internal override void OnRemovedFromScene()
    {
        _cubemapRenderer?.Dispose();
        _cubemapRenderer = null;
    }

    protected internal override void OnComponentAdded(TrackedComponentBase baseComponent)
    {
        if (baseComponent is not ReflectionProbe probe)
            return;
        MarkProbeDirty(probe);
        if (probe.IsRenderOnWake())
            probe.QueueRender();
    }

    protected internal override void OnComponentRemoved(TrackedComponentBase baseComponent)
    {
        if (baseComponent is ReflectionProbe probe)
            MarkProbeDirty(probe);
    }

    private void UpdateSpatialCache()
    {
        _spatial.ImmovableProbes.Clear();
        _spatial.MovableProbes.Clear();

        foreach (var reflectionProbe in GetReflectionProbes())
        {
            if (reflectionProbe.IsMovable())
                _spatial.MovableProbes.Add(new InternalReflectionProbeData(reflectionProbe));
            else
                _spatial.ImmovableProbes.Add(reflectionProbe);
        }

        _spatial.Revision = Math.Max(1u, _spatial.Revision + 1);
        _spatial.Dirty = false;

        BuildBvh(_spatial.ImmovableProbesBvh, _spatial.ImmovableProbes);
    }

    private void UpdateAutoQueueCache()
    {
        _autoQueue.RealtimeProbes.Clear();
        foreach (var reflectionProbe in GetReflectionProbes())
        {
            if (reflectionProbe.IsRealtimeUpdate())
                _autoQueue.RealtimeProbes.Add(reflectionProbe);
        }

        _autoQueue.Dirty = false;
    }

    private void FillUpdateQueue()
    {
        foreach (var probe in _probesToUpdate)
        {
            _updateQueue.Add(new QueuedReflectionProbe
            {
                Probe = probe,
                CubemapRenderer = probe.GetCubemapRenderer()
            });
        }

        _probesToUpdate.Clear();
    }

    private void ConsumeUpdateQueue()
    {
        // TODO: port full budgeted cubemap render loop from C++ ConsumeUpdateQueue
        _updateQueue.Clear();
        _ = MaxStaticUpdates;
        _ = _cubemapRenderer;
        _ = _filterCubemaps;
        _ = _renderBudget;
    }

    private static void AppendReference(Span<ReflectionProbeReference> result, ReflectionProbeReference newReference)
    {
        if (!result[0].IsValid || newReference.IsMoreImportantThan(result[0]))
        {
            // Insert first
            result[1] = result[0];
            result[0] = newReference;
        }
        else if (!result[1].IsValid || newReference.IsMoreImportantThan(result[1]))
        {
            // Insert second
            result[1] = newReference;
        }
        // Drop
    }

    private static void BuildBvh(List<ReflectionProbeBVH> result, List<ReflectionProbe> probes)
    {
        result.Clear();
        if (probes.Count == 0)
            return;

        // Simplified flat BVH: one leaf per probe (full power-of-two tree remains TODO for large counts)
        foreach (var probe in probes)
        {
            var data = new InternalReflectionProbeData(probe);
            result.Add(new ReflectionProbeBVH
            {
                Data = data,
                WorldBoundingBox = data.GetWorldBoundingBox()
            });
        }
    }

    private static void QueryBvh(List<ReflectionProbeBVH> intersections, List<ReflectionProbeBVH> bvh,
        BoundingBox worldBoundingBox)
    {
        foreach (var node in bvh)
        {
            if (node.WorldBoundingBox.IsInside(worldBoundingBox) == Intersection.Outside)
                continue;
            if (node.Data is not null)
                intersections.Add(node);
        }
    }
}

/// <summary>Reflection probe component that specifies reflection applied within a region.</summary>
public class ReflectionProbe : TrackedComponent<TrackedComponentBase, ReflectionProbeManager>
{
    private bool _movable;
    private ReflectionProbeType _probeType;
    private bool _realtimeUpdate;
    private bool _slicedUpdate;
    private BoundingBox _boundingBox = new(-Vector3f.One, Vector3f.One);
    private int _priority;
    private bool _useBoxProjection;
    private BoundingBox _projectionBox = new(-Vector3f.One, Vector3f.One);
    private TextureCube? _texture;
    private CubemapRenderingSettings _cubemapRenderingSettings = new();
    private ReflectionProbeData _data;
    private CubemapRenderer? _dynamicProbeRenderer;
    private TextureCube? _mixedProbeTexture;

    public ReflectionProbe()
    {
        _data.ProjectionBox = _projectionBox;
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest) =>
        DrawDebugGeometry(debug, depthTest, compact: false);

    public void DrawDebugGeometry(DebugRenderer? debug, bool depthTest, bool compact)
    {
        // TODO: port complete C++ ReflectionProbe::DrawDebugGeometry box drawing
        _ = debug;
        _ = depthTest;
        _ = compact;
    }

    /// <summary>Queue ReflectionProbe to be re-rendered as soon as possible.</summary>
    public void QueueRender() => GetRegistry()?.QueueProbeUpdate(this);

    /// <summary>Return cubemap renderer, available for dynamic probes.</summary>
    public CubemapRenderer? GetCubemapRenderer() => _dynamicProbeRenderer;

    /// <summary>Return writeable cubemap, available for mixed probes.</summary>
    public TextureCube? GetMixedProbeTexture() => _mixedProbeTexture;

    public void SetMovable(bool movable)
    {
        if (_movable == movable)
            return;
        _movable = movable;
        MarkComponentDirty();
    }

    public bool IsMovable() => _movable;

    public void SetProbeType(ReflectionProbeType probeType)
    {
        if (_probeType == probeType)
            return;
        _probeType = probeType;
        UpdateCubemapRenderer();
        MarkComponentDirty();
    }

    public ReflectionProbeType GetProbeType() => _probeType;

    public void SetRealtimeUpdate(bool realtimeUpdate)
    {
        if (_realtimeUpdate == realtimeUpdate)
            return;
        _realtimeUpdate = realtimeUpdate;
        MarkRealtimeDirty();
    }

    public bool IsRealtimeUpdate() => _realtimeUpdate;

    public void SetSlicedUpdate(bool slicedUpdate) => _slicedUpdate = slicedUpdate;

    public bool IsSlicedUpdate() => _slicedUpdate;

    public void SetBoundingBox(BoundingBox box)
    {
        _boundingBox = box;
        MarkTransformDirty();
    }

    public BoundingBox GetBoundingBox() => _boundingBox;

    public void SetPriority(int priority)
    {
        if (_priority == priority)
            return;
        _priority = priority;
        MarkComponentDirty();
    }

    public int GetPriority() => _priority;

    public void SetTexture(TextureCube? texture)
    {
        _texture = texture;
        UpdateProbeTextureData(texture);
        MarkComponentDirty();
    }

    public TextureCube? GetTexture() => _texture;

    public void SetBoxProjectionUsed(bool useBoxProjection)
    {
        _useBoxProjection = useBoxProjection;
        UpdateProbeBoxData();
        MarkComponentDirty();
    }

    public bool IsBoxProjectionUsed() => _useBoxProjection;

    public void SetProjectionBox(BoundingBox box)
    {
        _projectionBox = box;
        UpdateProbeBoxData();
    }

    public BoundingBox GetProjectionBox() => _projectionBox;

    public CubemapRenderingSettings GetCubemapRenderingSettings() => _cubemapRenderingSettings;

    public void SetTextureSize(uint value)
    {
        _cubemapRenderingSettings.TextureSize = value;
        UpdateCubemapRenderer();
    }

    public uint GetTextureSize() => _cubemapRenderingSettings.TextureSize;

    public void SetViewMask(uint value) => _cubemapRenderingSettings.ViewMask = value;

    public uint GetViewMask() => _cubemapRenderingSettings.ViewMask;

    public void SetNearClip(float value) => _cubemapRenderingSettings.NearClip = value;

    public float GetNearClip() => _cubemapRenderingSettings.NearClip;

    public void SetFarClip(float value) => _cubemapRenderingSettings.FarClip = value;

    public float GetFarClip() => _cubemapRenderingSettings.FarClip;

    public ReflectionProbeData GetProbeData() => _data;

    public bool IsRenderOnWake() =>
        _probeType is ReflectionProbeType.Mixed or ReflectionProbeType.Dynamic;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        UpdateCubemapRenderer();
        MarkTransformDirty();
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        base.OnMarkedDirty(node);
        MarkTransformDirty();
    }

    private void MarkComponentDirty() => GetRegistry()?.MarkProbeDirty(this);

    private void MarkTransformDirty() => GetRegistry()?.MarkProbeTransformDirty(this);

    private void MarkRealtimeDirty() => GetRegistry()?.MarkProbeRealtimeDirty(this);

    private void UpdateCubemapRenderer()
    {
        // TODO: port complete C++ ReflectionProbe::UpdateCubemapRenderer for dynamic/mixed probes
        if (_probeType == ReflectionProbeType.Dynamic && Scene is not null)
            _dynamicProbeRenderer ??= new CubemapRenderer(Scene);
        else
        {
            _dynamicProbeRenderer?.Dispose();
            _dynamicProbeRenderer = null;
        }
    }

    private void UpdateProbeTextureData(TextureCube? texture)
    {
        _data.ReflectionMap = texture;
        if (texture is not null && texture.Levels > 1)
            _data.RoughnessToLodFactor = MathF.Log2(texture.Levels - 1);
        else
            _data.RoughnessToLodFactor = 0;
    }

    private void UpdateProbeBoxData()
    {
        _data.ProjectionBox = _useBoxProjection ? _projectionBox : default;
        if (Node is not null)
            _data.CubemapCenter = new Vector4f(Node.GetWorldPosition(), _useBoxProjection ? 1.0f : 0.0f);
    }
}
