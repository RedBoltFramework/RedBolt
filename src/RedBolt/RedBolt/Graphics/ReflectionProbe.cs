using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Graphics;

/// <summary>Cached internal structure for reflection probe search.</summary>
public class InternalReflectionProbeData : ICloneable
{
    private Matrix3x4 _worldToLocal;
    private BoundingBox _localBoundingBox = new();
    private BoundingBox _worldBoundingBox = new();

    public InternalReflectionProbeData(ReflectionProbe probe)
    {
        Probe = probe;
        Data = probe.GetProbeDataStorage();
        Priority = probe.GetPriority();
        Update();
    }

    private InternalReflectionProbeData(InternalReflectionProbeData rhs)
    {
        Probe = rhs.Probe;
        Data = rhs.Data;
        Priority = rhs.Priority;
        _worldToLocal = rhs._worldToLocal;
        _localBoundingBox = rhs._localBoundingBox;
        _worldBoundingBox = rhs._worldBoundingBox;
    }

    public object Clone() => new InternalReflectionProbeData(this);

    public ReflectionProbe Probe { get; }

    public StrongBox<ReflectionProbeData> Data { get; }

    public int Priority { get; }

    public void Update()
    {
        var node = Probe.Node!;
        var worldTransform = node.GetWorldTransform();
        _worldToLocal = worldTransform.Inverse;
        _localBoundingBox = Probe.GetBoundingBox();
        _worldBoundingBox = Probe.GetBoundingBox().Transformed(worldTransform);
    }

    public float? GetIntersectionVolume(BoundingBox worldBoundingBox)
    {
        var localBoundingBox = worldBoundingBox.Transformed(_worldToLocal);
        var clippedBoundingBox = localBoundingBox.Clipped(_localBoundingBox);
        if (clippedBoundingBox.Defined)
            return clippedBoundingBox.Volume / SystemMath.Max(localBoundingBox.Volume, MathDefs.Epsilon);
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
[Reflected(ObjectCategory.Category_Subsystem)]
public partial class ReflectionProbeManager : TrackedComponentRegistryBase
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

    private CubemapRenderer? _cubemapRenderer;
    private float _queryPadding = DefaultQueryPadding;
    private uint _renderBudget = DefaultRenderBudget;
    private bool _filterCubemaps = true;
    private bool? _filteringSupported;
    private readonly HashSet<ReflectionProbe> _probesToUpdate = [];
    private readonly List<QueuedReflectionProbe> _updateQueue = [];
    private readonly List<QueuedReflectionProbe> _frameUpdates = [];
    private SpatialCache _spatial = new()
    {
        MovableProbes = [],
        ImmovableProbes = [],
        ImmovableProbesBvh = []
    };
    private AutoQueueCache _autoQueue = new() { RealtimeProbes = [] };
    private static readonly ThreadLocal<List<ReflectionProbeBVH>> TempIntersectedProbes = new(() => []);
    public ReflectionProbeManager()
        : base(typeof(ReflectionProbe).GetClassName())
    {
        SubscribeToEvent<GraphicsEvents.DeviceResetEvent>(
            (ref GraphicsEvents.DeviceResetEvent _) => RestoreCubemaps());
        // TODO: port URHO3D_ACTION_STATIC_LABEL("Bake!", QueueBakeAll, "Renders all baked reflection probes");
    }

    public override void Dispose()
    {
        _autoQueue.RealtimeProbes.Clear();
        _spatial.ImmovableProbesBvh.Clear();
        _spatial.ImmovableProbes.Clear();
        _spatial.MovableProbes.Clear();
        _frameUpdates.Clear();
        _updateQueue.Clear();
        _probesToUpdate.Clear();
        _cubemapRenderer?.Dispose();
        _cubemapRenderer = null;
        base.Dispose();
    }

    public override bool IsOnlyEnabledTracked => true;


    [Serialized("Query Padding")]
    public float QueryPadding
    {
        get => _queryPadding;
        set => _queryPadding = value;
    }

    [Serialized("Render Budget")]
    public uint RenderBudget
    {
        get => _renderBudget;
        set => _renderBudget = value;
    }

    [Serialized("Filter Cubemaps")]
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

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        foreach (var reflectionProbe in GetReflectionProbes())
            reflectionProbe.DrawDebugGeometry(debug, depthTest, compact: true);
    }

    /// <summary>Query two most important reflection probes.</summary>
    public void QueryDynamicProbes(BoundingBox worldBoundingBox, Span<ReflectionProbeReference> probes)
    {
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
        var intersections = TempIntersectedProbes.Value!;
        intersections.Clear();

        probes[0].Reset();
        probes[1].Reset();

        var padding = Vector3f.One * _queryPadding;
        var paddedBoundingBox = new BoundingBox(worldBoundingBox.Min - padding, worldBoundingBox.Max + padding);
        QueryBvh(intersections, _spatial.ImmovableProbesBvh, paddedBoundingBox);

        var cacheDistance = _queryPadding;
        foreach (var node in intersections)
        {
            var signedDistance = node.WorldBoundingBox.SignedDistanceToBoundingBox(worldBoundingBox);
            if (signedDistance > 0.0f)
                cacheDistance = SystemMath.Min(cacheDistance, signedDistance);
            else
            {
                var probeData = node.Data!;
                var volume = probeData.GetIntersectionVolume(worldBoundingBox);
                if (volume is not null)
                {
                    AppendReference(probes, new ReflectionProbeReference
                    {
                        Data = probeData.Data,
                        Priority = probeData.Priority,
                        Volume = volume.Value
                    });
                }

                cacheDistance = SystemMath.Min(cacheDistance, -signedDistance);
            }
        }
        cacheDistanceSquared = cacheDistance * cacheDistance;
    }

    public IEnumerable<ReflectionProbe> GetReflectionProbes()
    {
        foreach (var component in TrackedComponents)
        {
            if (component is ReflectionProbe probe)
                yield return probe;
        }
    }

    public uint GetRevision() => _spatial.Revision;

    public bool HasStaticProbes() => _spatial.ImmovableProbes.Count > 0;

    public bool HasDynamicProbes() => _spatial.MovableProbes.Count > 0;

    protected override void OnAddedToScene(SceneType scene)
    {
        _cubemapRenderer = new CubemapRenderer(scene);
    }

    protected override void OnRemovedFromScene()
    {
        _cubemapRenderer?.Dispose();
        _cubemapRenderer = null;
    }

    protected override void OnComponentAdded(TrackedComponentBase baseComponent)
    {
        if (baseComponent is not ReflectionProbe probe)
            return;
        MarkProbeDirty(probe);
        if (probe.IsRenderOnWake())
            probe.QueueRender();
    }

    protected override void OnComponentRemoved(TrackedComponentBase baseComponent)
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

        _spatial.Revision = SystemMath.Max(1u, _spatial.Revision + 1);
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
        _filteringSupported ??= SupportFiltering(renderDevice);
        var filterCubemapsEffective = _filterCubemaps && _filteringSupported.Value;

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

    private void RestoreCubemaps()
    {
        _filteringSupported = null;
        foreach (var reflectionProbe in GetReflectionProbes())
        {
            if (reflectionProbe.IsRenderOnWake())
                QueueProbeUpdate(reflectionProbe);
        }
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
                Log.GetLogger(), "Cannot save reflection probe texture to file");
            return string.Empty;
        }

        var textureFileName = $"{filePath}/{fileName}.xml";
        try
        {
            Directory.CreateDirectory(filePath);
            var xmlFile = new XMLFile();
            var rootElement = xmlFile.GetOrCreateRoot("cubemap");
            for (var face = 0; face < (int)CubeMapFace.MaxCubeMapFaces; ++face)
            {
                var faceFileName = $"{fileName}-{face}.png";
                var image = texture.GetImage((CubeMapFace)face);
                image?.SavePNG($"{filePath}/{faceFileName}");

                var faceElement = rootElement.CreateChild("face");
                faceElement.SetAttribute("name", faceFileName);
            }

            xmlFile.SaveFile(new FileIdentifier(textureFileName));
        }
        catch (Exception ex)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                Log.GetLogger(), ex, "Failed to save reflection probe texture");
            return string.Empty;
        }

        return textureFileName;
    }

    private static bool SupportFiltering(RenderDevice? device) =>
        device is not null
        && device.GetCaps().ComputeShaders
        && device.IsUnorderedAccessFormatSupported(TextureFormat.TexFormatRgba8Unorm);

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
        result.Capacity = SystemMath.Max(result.Capacity, nodeCount);
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
            var leaf = (InternalReflectionProbeData)probes[probesOffset].Clone();
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
[Reflected(ObjectCategory.Category_Scene)]
public partial class ReflectionProbe : TrackedComponent<ReflectionProbeManager>
{
    private static readonly string[] ReflectionProbeTypeNames = ["Baked", "Mixed", "Dynamic", "Custom Texture"];

    [Serialized(
        "Is Movable",
        Setter = nameof(SetMovable),
        Order = 1
    )]
    private bool _movable;
    [Serialized(
        "Probe Type",
        DefaultSource = nameof(GetProbeTypeAttributeDefault),
        EnumNamesSource = nameof(ReflectionProbeTypeNames),
        Setter = nameof(SetProbeType),
        Order = 2
    )]
    private ReflectionProbeType _probeType;
    [Serialized(
        "Is Realtime Update",
        Setter = nameof(SetRealtimeUpdate),
        Order = 3
    )]
    private bool _realtimeUpdate;
    [Serialized(
        "Is Sliced Update",
        Order = 4
    )]
    private bool _slicedUpdate;
    private BoundingBox _boundingBox = new(-Vector3f.One, Vector3f.One);
    [Serialized(
        "Priority",
        Setter = nameof(SetPriority),
        Order = 7
    )]
    private int _priority;
    [Serialized(
        "Use Box Projection",
        Setter = nameof(SetBoxProjectionUsed),
        Order = 9
    )]
    private bool _useBoxProjection;
    private BoundingBox _projectionBox = new(-Vector3f.One, Vector3f.One);
    private TextureCube? _texture;
    private CubemapRenderingSettings _cubemapRenderingSettings = new();
    private readonly StrongBox<ReflectionProbeData> _data = new();
    private CubemapRenderer? _dynamicProbeRenderer;
    private TextureCube? _mixedProbeTexture;

    public ReflectionProbe()
    {
        // TODO: port URHO3D_ACTION_STATIC_LABEL("Render!", QueueRender, "Renders cubemap for reflection probe");
    }

    public override void Dispose()
    {
        _mixedProbeTexture?.Dispose();
        _mixedProbeTexture = null;
        if (_dynamicProbeRenderer is not null)
        {
            _dynamicProbeRenderer.CubemapRendered -= OnDynamicCubemapRendered;
            _dynamicProbeRenderer.Dispose();
            _dynamicProbeRenderer = null;
        }
        _texture = null;
        base.Dispose();
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest) =>
        DrawDebugGeometry(debug, depthTest, compact: false);

    public void DrawDebugGeometry(DebugRenderer? debug, bool depthTest, bool compact)
    {
        if (debug is not null && IsEnabledEffective && TryGetNode() is not null)
        {
            debug.AddBoundingBox(_boundingBox, Node.GetWorldTransform(), Color.Blue, depthTest);
            if (!compact && _useBoxProjection)
                debug.AddBoundingBox(_data.Value.ProjectionBox, Matrix3x4.Identity, Color.Magenta, depthTest);
        }
    }

    /// <summary>Queue ReflectionProbe to be re-rendered as soon as possible.</summary>
    public void QueueRender() => Registry?.QueueProbeUpdate(this);

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

        if (IsRenderOnWake())
            QueueRender();
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
        if (_boundingBox.Equals(box))
            return;
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
        if (_probeType is ReflectionProbeType.Baked or ReflectionProbeType.CustomTexture)
            UpdateProbeTextureData(texture);
    }

    public TextureCube? GetTexture() => _texture;

    public void SetTextureAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>();
        SetTexture(cache?.GetResourceOrNull<TextureCube>(value.Name));
    }

    [Serialized(
        "Texture",
        DefaultSource = nameof(GetTextureAttributeDefault),
        Setter = nameof(SetTextureAttr),
        Order = 8
    )]
    public ResourceRef GetTextureAttr() =>
        ResourceType.GetResourceRef(_texture, new StringHash(nameof(TextureCube)));

    public void SetBoxProjectionUsed(bool useBoxProjection)
    {
        if (_useBoxProjection == useBoxProjection)
            return;
        _useBoxProjection = useBoxProjection;
        UpdateProbeBoxData();
    }

    public bool IsBoxProjectionUsed() => _useBoxProjection;

    public void SetProjectionBox(BoundingBox box)
    {
        _projectionBox = box;
    }

    public BoundingBox GetProjectionBox() => _projectionBox;

    public CubemapRenderingSettings GetCubemapRenderingSettings() => _cubemapRenderingSettings;

    public void SetTextureSize(uint value)
    {
        if (_cubemapRenderingSettings.TextureSize == value)
            return;
        _cubemapRenderingSettings.TextureSize = value;
        if (_mixedProbeTexture is not null)
        {
            CubemapRenderer.DefineTexture(_mixedProbeTexture, _cubemapRenderingSettings);
            UpdateProbeTextureData(_mixedProbeTexture);
        }
    }

    [Serialized(
        "Texture Size",
        DefaultSource = nameof(GetTextureSizeAttributeDefault),
        Setter = nameof(SetTextureSize),
        Order = 12
    )]
    public uint GetTextureSize() => _cubemapRenderingSettings.TextureSize;

    public void SetViewMask(uint value)
    {
        if (_cubemapRenderingSettings.ViewMask != value)
            _cubemapRenderingSettings.ViewMask = value;
    }

    [Serialized(
        "View Mask",
        DefaultSource = nameof(GetViewMaskAttributeDefault),
        Setter = nameof(SetViewMask),
        Order = 13
    )]
    public uint GetViewMask() => _cubemapRenderingSettings.ViewMask;

    public void SetNearClip(float value)
    {
        if (_cubemapRenderingSettings.NearClip != value)
            _cubemapRenderingSettings.NearClip = value;
    }

    [Serialized(
        "Near Clip",
        DefaultSource = nameof(GetNearClipAttributeDefault),
        Setter = nameof(SetNearClip),
        Order = 14
    )]
    public float GetNearClip() => _cubemapRenderingSettings.NearClip;

    public void SetFarClip(float value)
    {
        if (_cubemapRenderingSettings.FarClip != value)
            _cubemapRenderingSettings.FarClip = value;
    }

    [Serialized(
        "Far Clip",
        DefaultSource = nameof(GetFarClipAttributeDefault),
        Setter = nameof(SetFarClip),
        Order = 15
    )]
    public float GetFarClip() => _cubemapRenderingSettings.FarClip;

    public ref readonly ReflectionProbeData GetProbeData() => ref _data.Value;

    internal StrongBox<ReflectionProbeData> GetProbeDataStorage() => _data;

    public bool IsRenderOnWake() =>
        _probeType is ReflectionProbeType.Mixed or ReflectionProbeType.Dynamic;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (currentNode is not null)
        {
            currentNode.AddListener(this);
            MarkTransformDirty();
        }

        UpdateCubemapRenderer();
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        MarkTransformDirty();
    }

    private void MarkComponentDirty() => Registry?.MarkProbeDirty(this);

    private void MarkTransformDirty()
    {
        Registry?.MarkProbeTransformDirty(this);
        UpdateProbeBoxData();
    }

    private void MarkRealtimeDirty() => Registry?.MarkProbeRealtimeDirty(this);

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
                    _mixedProbeTexture = new TextureCube();
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
        _data.Value.ReflectionMap = texture;
        // C++: LogBaseTwo(texture->GetWidth()) when texture present
        _data.Value.RoughnessToLodFactor = texture is not null ? MathF.Log2(texture.Width) : 1.0f;
    }

    private void UpdateProbeBoxData()
    {
        if (!_useBoxProjection || TryGetNode() is null)
        {
            _data.Value.CubemapCenter = Vector4f.Zero;
            return;
        }

        var position = Node.GetWorldPosition();
        _data.Value.CubemapCenter = position.ToVector4(1.0f);
        _data.Value.ProjectionBox = new BoundingBox(position + _projectionBox.Min, position + _projectionBox.Max);
    }

    private static object? GetIsEnabledAttributeDefault() => true;


    private static object? GetProbeTypeAttributeDefault() => ReflectionProbeType.Baked;

    private static object? GetBoundingBoxMinAttributeDefault() => -Vector3f.One;

    [Serialized(
        "Bounding Box Min",
        typeof(Vector3f),
        DefaultSource = nameof(GetBoundingBoxMinAttributeDefault),
        Setter = nameof(SetSerializedBoundingBoxMin),
        Order = 5
    )]
    private object? GetSerializedBoundingBoxMin()
    {
        Serializable self = this;
        return ((ReflectionProbe)self)._boundingBox.Min;
    }

    private void SetSerializedBoundingBoxMin(object? value)
    {
        Serializable self = this;
        var probe = (ReflectionProbe)self;
        probe._boundingBox.Min = (Vector3f)value!;
        probe.MarkTransformDirty();
    }

    private static object? GetBoundingBoxMaxAttributeDefault() => Vector3f.One;

    [Serialized(
        "Bounding Box Max",
        typeof(Vector3f),
        DefaultSource = nameof(GetBoundingBoxMaxAttributeDefault),
        Setter = nameof(SetSerializedBoundingBoxMax),
        Order = 6
    )]
    private object? GetSerializedBoundingBoxMax()
    {
        Serializable self = this;
        return ((ReflectionProbe)self)._boundingBox.Max;
    }

    private void SetSerializedBoundingBoxMax(object? value)
    {
        Serializable self = this;
        var probe = (ReflectionProbe)self;
        probe._boundingBox.Max = (Vector3f)value!;
        probe.MarkTransformDirty();
    }


    private static object? GetTextureAttributeDefault() => new ResourceRef(new StringHash(nameof(TextureCube)));


    private static object? GetProjectionBoxMinAttributeDefault() => -Vector3f.One;

    [Serialized(
        "Projection Box Min",
        typeof(Vector3f),
        DefaultSource = nameof(GetProjectionBoxMinAttributeDefault),
        Setter = nameof(SetSerializedProjectionBoxMin),
        Order = 10
    )]
    private object? GetSerializedProjectionBoxMin()
    {
        Serializable self = this;
        return ((ReflectionProbe)self)._projectionBox.Min;
    }

    private void SetSerializedProjectionBoxMin(object? value)
    {
        Serializable self = this;
        var probe = (ReflectionProbe)self;
        probe._projectionBox.Min = (Vector3f)value!;
        probe.UpdateProbeBoxData();
    }

    private static object? GetProjectionBoxMaxAttributeDefault() => Vector3f.One;

    [Serialized(
        "Projection Box Max",
        typeof(Vector3f),
        DefaultSource = nameof(GetProjectionBoxMaxAttributeDefault),
        Setter = nameof(SetSerializedProjectionBoxMax),
        Order = 11
    )]
    private object? GetSerializedProjectionBoxMax()
    {
        Serializable self = this;
        return ((ReflectionProbe)self)._projectionBox.Max;
    }

    private void SetSerializedProjectionBoxMax(object? value)
    {
        Serializable self = this;
        var probe = (ReflectionProbe)self;
        probe._projectionBox.Max = (Vector3f)value!;
        probe.UpdateProbeBoxData();
    }

    private static object? GetTextureSizeAttributeDefault() => CubemapRenderingSettings.DefaultTextureSize;

    private static object? GetViewMaskAttributeDefault() => CubemapRenderingSettings.DefaultViewMask;

    private static object? GetNearClipAttributeDefault() => CubemapRenderingSettings.DefaultNearClip;

    private static object? GetFarClipAttributeDefault() => CubemapRenderingSettings.DefaultFarClip;


    [Serialized(
        "Is Enabled",
        DefaultSource = nameof(GetIsEnabledAttributeDefault),
        Order = 0
    )]
    private bool SerializedIsEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }
}
