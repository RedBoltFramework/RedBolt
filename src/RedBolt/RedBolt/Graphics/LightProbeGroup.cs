using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Graphics;

/// <summary>Light probe description.</summary>
public struct LightProbe
{
    /// <summary>Position in local space of light probe group.</summary>
    public Vector3f Position;
}

/// <summary>Light probe baked data.</summary>
public sealed class LightProbeCollectionBakedData : ICloneable
{
    /// <summary>Incoming light baked into spherical harmonics.</summary>
    public readonly List<SphericalHarmonicsDot9> SphericalHarmonics = [];
    /// <summary>Baked ambient light.</summary>
    public readonly List<Vector3f> Ambient = [];

    private LightProbeCollectionBakedData(LightProbeCollectionBakedData source)
    {
        SphericalHarmonics.AddRange(source.SphericalHarmonics);
        Ambient.AddRange(source.Ambient);
    }

    public LightProbeCollectionBakedData()
    {
    }

    /// <summary>Return whether the collection is empty.</summary>
    public bool Empty => SphericalHarmonics.Count == 0;
    /// <summary>Return total size.</summary>
    public uint Size => (uint)SphericalHarmonics.Count;

    /// <summary>Resize collection.</summary>
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

    /// <summary>Clear collection.</summary>
    public void Clear()
    {
        SphericalHarmonics.Clear();
        Ambient.Clear();
    }

    public object Clone() => new LightProbeCollectionBakedData(this);
}

/// <summary>Light probes from multiple light probe groups.</summary>
public sealed class LightProbeCollection : ICloneable
{
    /// <summary>World-space positions of light probes.</summary>
    public readonly List<Vector3f> WorldPositions = [];
    /// <summary>First light probe owned by corresponding group.</summary>
    public readonly List<uint> Offsets = [];
    /// <summary>Number of light probes owned by corresponding group.</summary>
    public readonly List<uint> Counts = [];
    /// <summary>Group names.</summary>
    public readonly List<string> Names = [];

    private LightProbeCollection(LightProbeCollection source)
    {
        WorldPositions.AddRange(source.WorldPositions);
        Offsets.AddRange(source.Offsets);
        Counts.AddRange(source.Counts);
        Names.AddRange(source.Names);
    }

    public LightProbeCollection()
    {
    }

    /// <summary>Return whether the collection is empty.</summary>
    public bool Empty => WorldPositions.Count == 0;
    /// <summary>Return total number of probes.</summary>
    public uint GetNumProbes() => (uint)WorldPositions.Count;
    /// <summary>Return number of groups.</summary>
    public uint GetNumGroups() => (uint)Offsets.Count;

    /// <summary>Calculate padded bounding box.</summary>
    public BoundingBox CalculateBoundingBox(Vector3f padding = default)
    {
        var boundingBox = new BoundingBox(WorldPositions);
        boundingBox.Min -= padding;
        boundingBox.Max += padding;
        return boundingBox;
    }

    /// <summary>Clear collection.</summary>
    public void Clear()
    {
        WorldPositions.Clear();
        Offsets.Clear();
        Counts.Clear();
        Names.Clear();
    }

    public object Clone() => new LightProbeCollection(this);
}

/// <summary>Light probe group.</summary>
public class LightProbeGroup : Component
{
    /// <summary>Auto placement limit: max grid size in one dimension.</summary>
    public const uint MaxAutoGridSize = 1024;
    /// <summary>Auto placement limit: max total number of probes generated.</summary>
    public const uint MaxAutoProbes = 65536;

    /// <summary>Construct.</summary>
    public LightProbeGroup()
    {
        RegisterAttribute("Is Enabled", typeof(bool), self => ((LightProbeGroup)self).Enabled,
            (self, value) => ((LightProbeGroup)self).SetEnabled(value is bool enabled && enabled), true);
        RegisterAttribute("Light Mask", typeof(uint), self => ((LightProbeGroup)self)._lightMask,
            (self, value) => ((LightProbeGroup)self)._lightMask = (uint)value!, Drawable.DefaultLightMask);
        RegisterAttribute("Zone Mask", typeof(uint), self => ((LightProbeGroup)self)._zoneMask,
            (self, value) => ((LightProbeGroup)self)._zoneMask = (uint)value!, Drawable.DefaultZoneMask);
        RegisterAttribute("Auto Placement", typeof(bool), self => ((LightProbeGroup)self).GetAutoPlacementEnabled(),
            (self, value) => ((LightProbeGroup)self).SetAutoPlacementEnabled(value is bool enabled && enabled), true);
        RegisterAttribute("Auto Placement Step", typeof(float), self => ((LightProbeGroup)self).GetAutoPlacementStep(),
            (self, value) => ((LightProbeGroup)self).SetAutoPlacementStep((float)value!), 1.0f);
        RegisterAttribute("Local Bounding Box Min", typeof(Vector3f), self => ((LightProbeGroup)self)._localBoundingBox.Min,
            (self, value) => ((LightProbeGroup)self)._localBoundingBox.Min = (Vector3f)value!, Vector3f.Zero,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute("Local Bounding Box Max", typeof(Vector3f), self => ((LightProbeGroup)self)._localBoundingBox.Max,
            (self, value) => ((LightProbeGroup)self)._localBoundingBox.Max = (Vector3f)value!, Vector3f.Zero,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute("Serialized Light Probes", typeof(string), self => ((LightProbeGroup)self).GetSerializedLightProbes(),
            (self, value) => ((LightProbeGroup)self).SetSerializedLightProbes(value as string ?? string.Empty), string.Empty,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute("Baked Data File", typeof(ResourceRef), self => ((LightProbeGroup)self).GetBakedDataFileRef(),
            (self, value) => ((LightProbeGroup)self).SetBakedDataFileRef((ResourceRef)value!),
            new ResourceRef(new StringHash(nameof(BinaryFile))), AttributeMode.Default | AttributeMode.NoEdit);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Register object factory. Drawable must be registered first.</summary>
    public static void RegisterObject(Context context) =>
        context.AddFactoryReflection<LightProbeGroup>(ObjectCategory.Category_Scene);

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        UpdateBakedData();

        var boundingBox = new BoundingBox(-Vector3f.One * 0.5f, Vector3f.One * 0.5f);
        debug!.AddBoundingBox(boundingBox, Node.GetWorldTransform(), Color.Green);

        for (var i = 0; i < _lightProbes.Count; ++i)
        {
            var probe = _lightProbes[i];
            var worldPosition = Node.LocalToWorld(probe.Position);
            var ambient = _bakedData.Ambient[i];
            debug.AddSphere(new Sphere(worldPosition, 0.1f), new Color(ambient.X, ambient.Y, ambient.Z));
        }
    }

    /// <summary>Collect all light probes from specified groups.</summary>
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

    /// <summary>Collect all light probes from all enabled groups in the scene.</summary>
    public static void CollectLightProbes(SceneType scene, LightProbeCollection collection,
        LightProbeCollectionBakedData? bakedData, bool reload = false)
    {
        var lightProbeGroups = new List<LightProbeGroup>();
        scene.GetComponents(lightProbeGroups);
        lightProbeGroups.RemoveAll(group => !group.IsEnabledEffective);
        CollectLightProbes(lightProbeGroups, collection, bakedData, reload);
    }

    /// <summary>Save light probes baked data for specific element. Return false if failed.</summary>
    public static bool SaveLightProbesBakedData(Context context, FileIdentifier fileName,
        LightProbeCollection collection, LightProbeCollectionBakedData bakedData, uint index)
    {
        if (index >= collection.GetNumGroups())
            return false;

        var offset = collection.Offsets[(int)index];
        var count = collection.Counts[(int)index];
        if (offset + count > bakedData.Size)
            return false;

        var copy = new LightProbeCollectionBakedData();
        copy.SphericalHarmonics.AddRange(bakedData.SphericalHarmonics.GetRange((int)offset, (int)count));
        copy.Ambient.AddRange(bakedData.Ambient.GetRange((int)offset, (int)count));

        using var bakedDataFile = new BinaryFile();

        if (!bakedDataFile.SaveObjectCallback(archive =>
            {
                var value = bakedData;
                ArchiveSerialization.SerializeValue(archive, "LightProbesBakedData", ref value);
            }))
            return false;

        var file = context.GetSubsystem<VirtualFileSystem>()!.OpenFile(fileName, FileMode.Write);
        if (file is null)
            return false;

        try
        {
            var data = bakedDataFile.GetMutableBuffer().Buffer;
            if (file.Write(data) != data.Length)
                return false;
        }
        finally
        {
            file.Close();
        }

        return true;
    }

    /// <summary>Return bounding box in local space.</summary>
    public BoundingBox GetLocalBoundingBox() => _localBoundingBox;

    /// <summary>Return bounding box in world space.</summary>
    public BoundingBox GetWorldBoundingBox() => _localBoundingBox.Transformed(Node.GetWorldTransform());

    /// <summary>Arrange light probes in scale.x*scale.y*scale.z volume around the node.</summary>
    public void ArrangeLightProbesInVolume()
    {
        _bakedDataDirty = true;
        _lightProbes.Clear();
        if (_autoPlacementStep <= MathDefs.LargeEpsilon)
            return;

        var volumeSize = Node.GetScale().Abs;
        var gridSize = Vector3i.Max(Vector3i.One * 2,
            Vector3i.One + Vector3f.RoundToInt(volumeSize / _autoPlacementStep));
        var maxGridSize = SystemMath.Max(gridSize.X, SystemMath.Max(gridSize.Y, gridSize.Z));

        if (maxGridSize >= (int)MaxAutoGridSize
            || gridSize.X * gridSize.Y * gridSize.Z >= (int)MaxAutoProbes)
        {
            Log.GetLogger().LogError("Automatic Light Probe Grid is too big");
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

    /// <summary>Reload baked light probes data.</summary>
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

    /// <summary>Set light probes.</summary>
    public void SetLightProbes(IReadOnlyList<LightProbe> lightProbes)
    {
        _lightProbes.Clear();
        _lightProbes.AddRange(lightProbes);
        _bakedDataDirty = true;
        UpdateLocalBoundingBox();
    }

    /// <summary>Return light probes.</summary>
    public IReadOnlyList<LightProbe> GetLightProbes() => _lightProbes;

    /// <summary>Serialize light probes.</summary>
    public void SerializeLightProbes(Archive archive)
    {
        using var block = archive.OpenUnorderedBlock("LightProbesData");
        const uint currentVersion = 2;
        var version = archive.SerializeVersion(currentVersion);
        if (version == currentVersion)
            ArchiveSerialization.SerializeValue(archive, "lightProbes", _lightProbes);
    }

    /// <summary>Set serialized light probes.</summary>
    public void SetSerializedLightProbes(string data)
    {
        using var buffer = new VectorBuffer(Convert.FromBase64String(data));
        using var archive = new BinaryInputArchive(Context.Instance, buffer);
        SerializeLightProbes(archive);
        _bakedDataDirty = true;
    }

    /// <summary>Return serialized light probes.</summary>
    public string GetSerializedLightProbes()
    {
        using var buffer = new VectorBuffer();
        using var archive = new BinaryOutputArchive(Context.Instance, buffer);
        SerializeLightProbes(archive);
        return Convert.ToBase64String(buffer.GetBuffer());
    }

    /// <summary>Set reference on file with baked data.</summary>
    public void SetBakedDataFileRef(ResourceRef fileRef)
    {
        if (_bakedDataRef != fileRef)
        {
            _bakedDataDirty = true;
            _bakedDataRef = fileRef;
        }
    }

    /// <summary>Return reference on file with baked data.</summary>
    public ResourceRef GetBakedDataFileRef() => _bakedDataRef;

    /// <summary>Handle scene node being assigned at creation.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        currentNode?.AddListener(this);
    }

    /// <summary>Handle scene node transform dirtied.</summary>
    protected internal override void OnMarkedDirty(Node node)
    {
        if (_autoPlacementEnabled && _lastNodeScale != node.GetScale())
        {
            _lastNodeScale = node.GetScale();
            ArrangeLightProbesInVolume();
        }
    }

    /// <summary>Update local bounding box.</summary>
    protected void UpdateLocalBoundingBox()
    {
        _localBoundingBox.Clear();
        foreach (var probe in _lightProbes)
            _localBoundingBox.Merge(probe.Position);
    }

    /// <summary>Update baked data.</summary>
    protected void UpdateBakedData()
    {
        if (!_bakedDataDirty)
            return;

        _bakedDataDirty = false;
        var cache = GetSubsystem<ResourceCache>()!;
        var bakedDataFile = cache.GetTempResource<BinaryFile>(_bakedDataRef.Name, false);

        var success = false;
        if (bakedDataFile is not null)
        {
            success = bakedDataFile.LoadObjectCallback(archive =>
            {
                var value = _bakedData;
                ArchiveSerialization.SerializeValue(archive, "LightProbesBakedData", ref value);
            }) && _bakedData.SphericalHarmonics.Count == _lightProbes.Count;
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

    /// <summary>Light probes.</summary>
    protected readonly List<LightProbe> _lightProbes = [];
    /// <summary>Bounding box in local space.</summary>
    protected BoundingBox _localBoundingBox = new();

    /// <summary>Light mask of light probe group.</summary>
    protected uint _lightMask = Drawable.DefaultLightMask;
    /// <summary>Zone mask of light probe group.</summary>
    protected uint _zoneMask = Drawable.DefaultZoneMask;
    /// <summary>Whether the auto placement is enabled.</summary>
    protected bool _autoPlacementEnabled = true;
    /// <summary>Automatic placement step.</summary>
    protected float _autoPlacementStep = 1.0f;
    /// <summary>Last node scale used during auto placement.</summary>
    protected Vector3f _lastNodeScale;

    /// <summary>Reference on file with baked data.</summary>
    protected ResourceRef _bakedDataRef = new(new StringHash(nameof(BinaryFile)));
    /// <summary>Whether the baked data is dirty.</summary>
    protected bool _bakedDataDirty;
    /// <summary>Baked data.</summary>
    protected readonly LightProbeCollectionBakedData _bakedData = new();
}
