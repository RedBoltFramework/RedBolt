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
        var renderDevice = GetSubsystem<RenderDevice>();
        var filterCubemapsEffective = _filterCubemaps && SupportFiltering(renderDevice);

        uint numStaticProbesRendered = 0;
        uint numRenderedFaces = 0;
        for (var queueIndex = 0; queueIndex < _updateQueue.Count; ++queueIndex)
        {
            if (numRenderedFaces >= _renderBudget && _renderBudget > 0)
                break;

            var queuedProbe = _updateQueue[queueIndex];
            var probe = queuedProbe.Probe;
            if (probe is null)
                continue;

            var position = probe.Node!.GetWorldPosition();

            // Skip custom texture probes
            if (probe.GetProbeType() == ReflectionProbeType.CustomTexture)
            {
                _updateQueue[queueIndex] = default;
                continue;
            }

            // Render dynamic probe
            if (queuedProbe.CubemapRenderer is { } probeRenderer)
            {
                var parameters = new CubemapUpdateParameters
                {
                    Settings = probe.GetCubemapRenderingSettings(),
                    Position = position,
                    SlicedUpdate = probe.IsSlicedUpdate(),
                    FilterResult = filterCubemapsEffective
                };

                var result = probeRenderer.Update(parameters);

                numRenderedFaces += result.NumRenderedFaces;
                if (result.IsComplete)
                    _updateQueue[queueIndex] = default;
                continue;
            }

            // Skip static probes if out of budget
            if (numStaticProbesRendered >= MaxStaticUpdates)
                continue;

            // Render mixed probe
            if (probe.GetMixedProbeTexture() is { } probeTexture)
            {
                // TODO: Create correct texture from the start
                var probeFlags = probeTexture.GetRawTexture()!.GetParams().Flags;
                if (filterCubemapsEffective && (probeFlags & TextureFlag.BindUnorderedAccess) == 0)
                {
                    probeTexture.SetSize(
                        probeTexture.Width,
                        probeTexture.Format,
                        probeFlags | TextureFlag.BindUnorderedAccess);
                }

                var parameters = new CubemapUpdateParameters
                {
                    Settings = probe.GetCubemapRenderingSettings(),
                    Position = position,
                    FilterResult = filterCubemapsEffective,
                    OverrideFinalTexture = probeTexture
                };

                var result = _cubemapRenderer!.Update(parameters);
                System.Diagnostics.Debug.Assert(result.IsComplete);
            }
            else
            {
                // Render baked probe
                var parameters = new CubemapUpdateParameters
                {
                    Settings = probe.GetCubemapRenderingSettings(),
                    Position = position,
                    FilterResult = filterCubemapsEffective
                };

                var result = _cubemapRenderer!.Update(parameters);
                System.Diagnostics.Debug.Assert(result.IsComplete);

                Action<TextureCube>? handler = null;
                var capturedProbe = probe;
                handler = texture =>
                {
                    var filePath = GetBakedProbeFilePath();
                    var fileName = $"ReflectionProbe-{capturedProbe.Id}";
                    var textureFileName = SaveTextureToFile(texture, filePath, fileName);
                    capturedProbe.SetTextureAttr(new ResourceRef(new StringHash(nameof(TextureCube)), textureFileName));
                    _cubemapRenderer!.CubemapRendered -= handler;
                };
                _cubemapRenderer.CubemapRendered += handler;
            }

            numRenderedFaces += (uint)CubeMapFace.MaxCubeMapFaces;
            _updateQueue[queueIndex] = default;
            ++numStaticProbesRendered;
        }

        _updateQueue.RemoveAll(queued => queued.Probe is null);
    }

    private string GetBakedProbeFilePath()
    {
        var scene = Scene;
        if (scene is null)
            return string.Empty;
        var sceneFileName = scene.GetFileName();
        if (string.IsNullOrEmpty(sceneFileName))
            return string.Empty;
        return $"{sceneFileName}.d/Textures";
    }

    private string SaveTextureToFile(TextureCube texture, string filePath, string fileName)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                Log.Logger, "Cannot save reflection probe texture to file");
            return string.Empty;
        }

        var textureFileName = $"{filePath}/{fileName}.xml";
        try
        {
            Directory.CreateDirectory(filePath);
            var xmlFile = new XMLFile("ReflectionProbeCubemap");
            var rootElement = xmlFile.GetOrCreateRoot("cubemap");
            for (var face = 0; face < (int)CubeMapFace.MaxCubeMapFaces; ++face)
            {
                var faceFileName = $"{fileName}-{face}.png";
                var image = texture.GetImage((CubeMapFace)face);
                image?.SavePNG($"{filePath}/{faceFileName}");

                var faceElement = rootElement.CreateChild("face");
                faceElement.SetAttribute("name", faceFileName);
            }

            xmlFile.SaveFile(textureFileName);
        }
        catch (Exception ex)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                Log.Logger, ex, "Failed to save reflection probe texture");
            return string.Empty;
        }

        return textureFileName;
    }

    private static bool SupportFiltering(RenderDevice? device) =>
        device is not null
        && device.GetCaps().ComputeShaders
        && device.IsUnorderedAccessFormatSupported(TextureFormat.RGBA8);

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

        var numProbes = probes.Count;

        var probesData = new List<InternalReflectionProbeData>(numProbes);
        foreach (var probe in probes)
            probesData.Add(new InternalReflectionProbeData(probe));

        var probesDataPtrs = new List<InternalReflectionProbeData>(numProbes);
        probesDataPtrs.AddRange(probesData);

        var nodeCount = (int)MathDefs.NextPowerOfTwo((uint)numProbes) * 2 - 1;
        result.Capacity = Math.Max(result.Capacity, nodeCount);
        for (var i = 0; i < nodeCount; ++i)
            result.Add(default);

        SplitBoundingVolumes(result, 0, nodeCount, probesDataPtrs, 0, probesDataPtrs.Count);
    }

    private static void SplitBoundingVolumes(
        List<ReflectionProbeBVH> result, int resultOffset, int capacity,
        List<InternalReflectionProbeData> probes, int probesOffset, int probesCount)
    {
        System.Diagnostics.Debug.Assert(probesCount > 0);
        System.Diagnostics.Debug.Assert(capacity > 0);
        System.Diagnostics.Debug.Assert(MathDefs.IsPowerOfTwo((uint)(capacity + 1)));

        if (probesCount == 1)
        {
            var leaf = probes[probesOffset];
            result[resultOffset] = new ReflectionProbeBVH
            {
                Data = leaf,
                WorldBoundingBox = leaf.GetWorldBoundingBox()
            };
            return;
        }

        var worldBox = new BoundingBox();
        for (var i = 0; i < probesCount; ++i)
            worldBox.Merge(probes[probesOffset + i].GetWorldBoundingBox());

        result[resultOffset] = new ReflectionProbeBVH
        {
            Data = null,
            WorldBoundingBox = worldBox
        };

        var size = worldBox.Size;
        var splitAxis = size.X > size.Y && size.X > size.Z ? 0 : size.Y > size.Z ? 1 : 2;

        // Sort probes in place for this span
        probes.Sort(probesOffset, probesCount, Comparer<InternalReflectionProbeData>.Create((lhs, rhs) =>
        {
            var lhsCenter = lhs.GetWorldBoundingBox().Center;
            var rhsCenter = rhs.GetWorldBoundingBox().Center;
            var l = splitAxis == 0 ? lhsCenter.X : splitAxis == 1 ? lhsCenter.Y : lhsCenter.Z;
            var r = splitAxis == 0 ? rhsCenter.X : splitAxis == 1 ? rhsCenter.Y : rhsCenter.Z;
            return l.CompareTo(r);
        }));

        var median = probesCount / 2;
        var halfCapacity = capacity / 2;
        SplitBoundingVolumes(result, resultOffset + 1, halfCapacity, probes, probesOffset, median);
        SplitBoundingVolumes(result, resultOffset + 1 + halfCapacity, halfCapacity, probes, probesOffset + median,
            probesCount - median);
    }

    private static void QueryBvh(List<ReflectionProbeBVH> intersections, List<ReflectionProbeBVH> bvh,
        BoundingBox worldBoundingBox)
    {
        if (bvh.Count == 0)
            return;
        QueryBvhRange(intersections, bvh, 0, bvh.Count, worldBoundingBox);
    }

    private static void QueryBvhRange(List<ReflectionProbeBVH> intersections, List<ReflectionProbeBVH> bvh,
        int offset, int count, BoundingBox worldBoundingBox)
    {
        // Early return if outside
        if (bvh[offset].WorldBoundingBox.IsInside(worldBoundingBox) == Intersection.Outside)
            return;

        // If node with probe, store and stop
        if (bvh[offset].Data is not null)
        {
            intersections.Add(bvh[offset]);
            return;
        }

        // Check children
        if (count > 1)
        {
            System.Diagnostics.Debug.Assert(count % 2 == 1);
            var stride = count / 2;
            QueryBvhRange(intersections, bvh, offset + 1, stride, worldBoundingBox);
            QueryBvhRange(intersections, bvh, offset + 1 + stride, stride, worldBoundingBox);
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
        if (debug is not null && IsEnabledEffective && Node is not null)
        {
            debug.AddBoundingBox(_boundingBox, Node.GetWorldTransform(), Color.Blue, depthTest);
            if (!compact && _useBoxProjection)
                debug.AddBoundingBox(_data.ProjectionBox, Matrix3x4.Identity, Color.Magenta, depthTest);
        }
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

    public void SetTextureAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>();
        SetTexture(cache?.GetResourceOrNull<TextureCube>(value.Name));
    }

    public ResourceRef GetTextureAttr() =>
        Resource.GetResourceRef(_texture, new StringHash(nameof(TextureCube)));

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
        var scene = Scene;
        if (scene is null
            || _probeType is ReflectionProbeType.Baked or ReflectionProbeType.CustomTexture)
        {
            _dynamicProbeRenderer?.Dispose();
            _dynamicProbeRenderer = null;
            _mixedProbeTexture = null;
            UpdateProbeTextureData(_texture);
            return;
        }

        switch (_probeType)
        {
            case ReflectionProbeType.Dynamic:
                if (_dynamicProbeRenderer is null)
                {
                    _dynamicProbeRenderer = new CubemapRenderer(scene);
                    _dynamicProbeRenderer.CubemapRendered += OnDynamicCubemapRendered;
                    _mixedProbeTexture = null;
                }
                break;
            case ReflectionProbeType.Mixed:
                if (_mixedProbeTexture is null)
                {
                    _mixedProbeTexture = new TextureCube("ReflectionProbe.Mixed");
                    CubemapRenderer.DefineTexture(_mixedProbeTexture, _cubemapRenderingSettings);
                    if (_dynamicProbeRenderer is not null)
                    {
                        _dynamicProbeRenderer.CubemapRendered -= OnDynamicCubemapRendered;
                        _dynamicProbeRenderer.Dispose();
                        _dynamicProbeRenderer = null;
                    }
                    UpdateProbeTextureData(_mixedProbeTexture);
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false, "Unsupported probe type");
                break;
        }
    }

    private void OnDynamicCubemapRendered(TextureCube texture) => UpdateProbeTextureData(texture);

    private void UpdateProbeTextureData(TextureCube? texture)
    {
        _data.ReflectionMap = texture;
        // C++: LogBaseTwo(texture->GetWidth()) when texture present
        _data.RoughnessToLodFactor = texture is not null ? MathF.Log2(texture.Width) : 1.0f;
    }

    private void UpdateProbeBoxData()
    {
        if (!_useBoxProjection || Node is null)
        {
            _data.CubemapCenter = Vector4f.Zero;
            return;
        }

        var position = Node.GetWorldPosition();
        _data.CubemapCenter = position.ToVector4(1.0f);
        _data.ProjectionBox = new BoundingBox(position + _projectionBox.Min, position + _projectionBox.Max);
    }
}
