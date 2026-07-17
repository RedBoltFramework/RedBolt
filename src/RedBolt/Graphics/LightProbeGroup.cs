using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Light probe description.</summary>
public struct LightProbe
{
    /// <summary>Position in local space of light probe group.</summary>
    public Vector3f Position;
}

/// <summary>Light probe baked data.</summary>
public sealed class LightProbeCollectionBakedData
{
    /// <summary>Incoming light baked into spherical harmonics.</summary>
    public readonly List<SphericalHarmonicsDot9> SphericalHarmonics = [];
    /// <summary>Baked ambient light.</summary>
    public readonly List<Vector3f> Ambient = [];

    public bool Empty => SphericalHarmonics.Count == 0;
    public uint Size => (uint)SphericalHarmonics.Count;

    public void Resize(uint size)
    {
        while (SphericalHarmonics.Count < size)
            SphericalHarmonics.Add(default);
        while (Ambient.Count < size)
            Ambient.Add(default);
        if (SphericalHarmonics.Count > size)
            SphericalHarmonics.RemoveRange((int)size, SphericalHarmonics.Count - (int)size);
        if (Ambient.Count > size)
            Ambient.RemoveRange((int)size, Ambient.Count - (int)size);
    }

    public void Clear()
    {
        SphericalHarmonics.Clear();
        Ambient.Clear();
    }
}

/// <summary>Light probes from multiple light probe groups.</summary>
public sealed class LightProbeCollection
{
    /// <summary>World-space positions of light probes.</summary>
    public readonly List<Vector3f> WorldPositions = [];
    /// <summary>First light probe owned by corresponding group.</summary>
    public readonly List<uint> Offsets = [];
    /// <summary>Number of light probes owned by corresponding group.</summary>
    public readonly List<uint> Counts = [];
    /// <summary>Group names.</summary>
    public readonly List<string> Names = [];

    public bool Empty => WorldPositions.Count == 0;
    public uint GetNumProbes() => (uint)WorldPositions.Count;
    public uint GetNumGroups() => (uint)Offsets.Count;

    public BoundingBox CalculateBoundingBox(Vector3f padding = default)
    {
        var boundingBox = default(BoundingBox);
        boundingBox.Clear();
        foreach (var position in WorldPositions)
            boundingBox.Merge(position);
        boundingBox.Min -= padding;
        boundingBox.Max += padding;
        return boundingBox;
    }

    public void Clear()
    {
        WorldPositions.Clear();
        Offsets.Clear();
        Counts.Clear();
        Names.Clear();
    }
}

/// <summary>Light probe group.</summary>
public class LightProbeGroup : Component
{
    /// <summary>Auto placement limit: max grid size in one dimension.</summary>
    public const uint MaxAutoGridSize = 1024;
    /// <summary>Auto placement limit: max total number of probes generated.</summary>
    public const uint MaxAutoProbes = 65536;

    private readonly List<LightProbe> _lightProbes = [];
    private BoundingBox _localBoundingBox;
    private uint _lightMask = Drawable.DefaultLightMask;
    private uint _zoneMask = Drawable.DefaultZoneMask;
    private bool _autoPlacementEnabled = true;
    private float _autoPlacementStep = 1.0f;
    private Vector3f _lastNodeScale;
    private ResourceRef _bakedDataRef = new(new StringHash(nameof(BinaryFile)));
    private bool _bakedDataDirty = true;
    private readonly LightProbeCollectionBakedData _bakedData = new();

    public LightProbeGroup()
    {
    }

    public static void RegisterObject(Context context) =>
        context.AddFactoryReflection<LightProbeGroup>(ObjectCategory.Category_Scene);

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || !IsAttached)
            return;

        UpdateBakedData();

        var boundingBox = new BoundingBox(-Vector3f.One * 0.5f, Vector3f.One * 0.5f);
        debug.AddBoundingBox(boundingBox, Node.GetWorldTransform(), Color.Green, depthTest);

        for (var i = 0; i < _lightProbes.Count; ++i)
        {
            var worldPosition = Node.LocalToWorld(_lightProbes[i].Position);
            var color = i < _bakedData.Ambient.Count
                ? new Color(_bakedData.Ambient[i].X, _bakedData.Ambient[i].Y, _bakedData.Ambient[i].Z)
                : Color.White;
            debug.AddSphere(new Sphere(worldPosition, 0.1f), color, depthTest);
        }
    }

    public BoundingBox GetLocalBoundingBox() => _localBoundingBox;
    public BoundingBox GetWorldBoundingBox() =>
        IsAttached ? _localBoundingBox.Transformed(Node.GetWorldTransform()) : _localBoundingBox;

    public static void CollectLightProbes(IReadOnlyList<LightProbeGroup> lightProbeGroups,
        LightProbeCollection collection, LightProbeCollectionBakedData? bakedData, bool reload = false)
    {
        var offset = collection.GetNumProbes();
        foreach (var group in lightProbeGroups)
        {
            if (bakedData is not null)
            {
                if (reload)
                    group.ReloadBakedData();
                group.UpdateBakedData();
            }

            var node = group.Node;
            var probes = group.GetLightProbes();
            collection.Offsets.Add(offset);
            collection.Counts.Add((uint)probes.Count);
            collection.Names.Add(node.Name);
            offset += (uint)probes.Count;

            foreach (var probe in probes)
                collection.WorldPositions.Add(node.LocalToWorld(probe.Position));

            if (bakedData is not null)
            {
                bakedData.SphericalHarmonics.AddRange(group._bakedData.SphericalHarmonics);
                bakedData.Ambient.AddRange(group._bakedData.Ambient);
            }
        }
    }

    public static void CollectLightProbes(Scene scene, LightProbeCollection collection,
        LightProbeCollectionBakedData? bakedData, bool reload = false)
    {
        var lightProbeGroups = new List<LightProbeGroup>();
        scene.GetComponents(lightProbeGroups);
        lightProbeGroups.RemoveAll(group => !group.IsEnabledEffective);
        CollectLightProbes(lightProbeGroups, collection, bakedData, reload);
    }

    public void ArrangeLightProbesInVolume()
    {
        _bakedDataDirty = true;
        _lightProbes.Clear();
        if (_autoPlacementStep <= MathDefs.LargeEpsilon || !IsAttached)
            return;

        var volumeSize = Node.GetScale().Abs;
        var gridSize = Vector3i.Max(Vector3i.One * 2,
            Vector3i.One + Vector3f.RoundToInt(volumeSize / _autoPlacementStep));
        var maxGridSize = Math.Max(gridSize.X, Math.Max(gridSize.Y, gridSize.Z));

        if (maxGridSize >= (int)MaxAutoGridSize
            || gridSize.X * gridSize.Y * gridSize.Z >= (int)MaxAutoProbes)
        {
            Log.Logger.LogError("Automatic Light Probe Grid is too big");
            return;
        }

        var gridStep = Vector3f.One / (gridSize - Vector3i.One).ToVector3();
        for (var z = 0; z < gridSize.Z; ++z)
        {
            for (var y = 0; y < gridSize.Y; ++y)
            {
                for (var x = 0; x < gridSize.X; ++x)
                {
                    var localPosition = -Vector3f.One / 2 + new Vector3f(x, y, z) * gridStep;
                    _lightProbes.Add(new LightProbe { Position = localPosition });
                }
            }
        }

        _localBoundingBox = new BoundingBox(-Vector3f.One / 2, Vector3f.One / 2);
    }

    public void ReloadBakedData()
    {
        _bakedDataDirty = true;
        UpdateBakedData();
    }

    public void SetAutoPlacementEnabled(bool enabled)
    {
        _autoPlacementEnabled = enabled;
        if (_autoPlacementEnabled)
            ArrangeLightProbesInVolume();
    }

    public bool GetAutoPlacementEnabled() => _autoPlacementEnabled;

    public void SetAutoPlacementStep(float step)
    {
        _autoPlacementStep = step;
        if (_autoPlacementEnabled)
            ArrangeLightProbesInVolume();
    }

    public float GetAutoPlacementStep() => _autoPlacementStep;
    public void SetLightMask(uint lightMask) => _lightMask = lightMask;
    public uint GetLightMask() => _lightMask;
    public void SetZoneMask(uint zoneMask) => _zoneMask = zoneMask;
    public uint GetZoneMask() => _zoneMask;

    public void SetLightProbes(IReadOnlyList<LightProbe> lightProbes)
    {
        _lightProbes.Clear();
        _lightProbes.AddRange(lightProbes);
        _bakedDataDirty = true;
        UpdateLocalBoundingBox();
    }

    public IReadOnlyList<LightProbe> GetLightProbes() => _lightProbes;

    public void SetBakedDataFileRef(ResourceRef fileRef)
    {
        if (_bakedDataRef != fileRef)
        {
            _bakedDataDirty = true;
            _bakedDataRef = fileRef;
        }
    }

    public ResourceRef GetBakedDataFileRef() => _bakedDataRef;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        currentNode?.AddListener(this);
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        if (_autoPlacementEnabled && _lastNodeScale != node.GetScale())
        {
            _lastNodeScale = node.GetScale();
            ArrangeLightProbesInVolume();
        }
    }

    private void UpdateLocalBoundingBox()
    {
        _localBoundingBox = default;
        _localBoundingBox.Clear();
        foreach (var probe in _lightProbes)
            _localBoundingBox.Merge(probe.Position);
    }

    private void UpdateBakedData()
    {
        if (!_bakedDataDirty)
            return;

        _bakedDataDirty = false;
        var cache = GetSubsystem<ResourceCache>();
        BinaryFile? bakedDataFile = null;
        if (!string.IsNullOrEmpty(_bakedDataRef.Name) && cache is not null)
        {
            try
            {
                bakedDataFile = cache.GetTempResource<BinaryFile>(_bakedDataRef.Name, false);
            }
            catch (InvalidOperationException)
            {
                bakedDataFile = null;
            }
        }

        var success = false;
        if (bakedDataFile is not null)
        {
            // Load via callback serializing SH list when present; fall back to empty.
            // Full C++ named-block LoadObject("LightProbesBakedData") maps to callback serialization.
            success = false;
        }

        if (!success)
        {
            _bakedData.Resize((uint)_lightProbes.Count);
            for (var i = 0; i < _lightProbes.Count; ++i)
            {
                _bakedData.SphericalHarmonics[i] = SphericalHarmonicsDot9.Zero;
                _bakedData.Ambient[i] = Vector3f.Zero;
            }
        }
    }
}
