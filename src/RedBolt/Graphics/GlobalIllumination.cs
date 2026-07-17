using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Global illumination manager.</summary>
public class GlobalIllumination : Component
{
    private float _emissionBrightness = 1.0f;
    private ResourceRef _fileRef = new(new StringHash(nameof(BinaryFile)));
    private Vector3f _offset;
    private readonly TetrahedralMesh _lightProbesMesh = new();
    private readonly LightProbeCollectionBakedData _lightProbesBakedData = new();

    public GlobalIllumination()
    {
    }

    /// <summary>Register object factory. Drawable must be registered first.</summary>
    public static void RegisterObject(Context context) =>
        context.AddFactoryReflection<GlobalIllumination>(ObjectCategory.Category_Subsystem);

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null)
            return;

        var edges = new List<(uint First, uint Second)>();
        _lightProbesMesh.CollectEdges(edges);

        foreach (var edge in edges)
        {
            var startPos = _lightProbesMesh.Vertices[(int)edge.First];
            var endPos = _lightProbesMesh.Vertices[(int)edge.Second];
            debug.AddLine(startPos, endPos, Color.Yellow, depthTest);
        }
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (previousScene is not null)
            UnsubscribeFromEvent<SceneEvents.WorldOriginPostUpdateEvent>(previousScene);

        if (scene is not null)
            SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);
    }

    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        var delta = eventData.Delta.ToVector3();
        _offset -= delta;
        for (var i = 0; i < _lightProbesMesh.Vertices.Count; ++i)
            _lightProbesMesh.Vertices[i] -= delta;
    }

    /// <summary>Reset light probes.</summary>
    public void ResetLightProbes()
    {
        _lightProbesBakedData.Clear();
        _lightProbesMesh.Define(Array.Empty<Vector3f>());
    }

    /// <summary>Compile all enabled light probe groups in the scene.</summary>
    public void CompileLightProbes()
    {
        ResetLightProbes();

        var collection = new LightProbeCollection();
        LightProbeGroup.CollectLightProbes(Scene!, collection, _lightProbesBakedData, true);
        if (collection.Empty)
            return;

        _lightProbesMesh.Define(collection.WorldPositions);

        var cache = GetSubsystem<ResourceCache>();
        if (cache is null || string.IsNullOrEmpty(_fileRef.Name))
        {
            Log.Logger.LogError("Cannot save Global Illumination data: output file doesn't exist");
            return;
        }

        BinaryFile? file;
        try
        {
            file = cache.GetTempResource<BinaryFile>(_fileRef.Name, false);
        }
        catch (InvalidOperationException)
        {
            file = null;
        }

        if (file is null)
        {
            Log.Logger.LogError("Cannot save Global Illumination data: output file doesn't exist");
            return;
        }

        file.Clear();
        if (!file.SaveObjectCallback(archive => SerializeData(archive)))
        {
            Log.Logger.LogError("Cannot save Global Illumination data: serialization failed");
            return;
        }

        if (!string.IsNullOrEmpty(file.AbsoluteFileName) && !file.SaveFile(file.AbsoluteFileName))
            Log.Logger.LogError("Cannot save Global Illumination data: cannot save file");
    }

    /// <summary>Sample ambient spherical harmonics.</summary>
    public SphericalHarmonicsDot9 SampleAmbientSH(Vector3f position, ref uint hint) =>
        _lightProbesMesh.Sample(_lightProbesBakedData.SphericalHarmonics, position, ref hint);

    /// <summary>Sample average ambient lighting.</summary>
    public Vector3f SampleAverageAmbient(Vector3f position, ref uint hint) =>
        _lightProbesMesh.Sample(_lightProbesBakedData.Ambient, position, ref hint);

    public void SetEmissionBrightness(float emissionBrightness) => _emissionBrightness = emissionBrightness;
    public float GetEmissionBrightness() => _emissionBrightness;

    public void SetFileRef(ResourceRef fileRef)
    {
        if (_fileRef != fileRef)
        {
            _fileRef = fileRef;
            ReloadData();
        }
    }

    public ResourceRef GetFileRef() => _fileRef;

    public void SetOffset(Vector3f offset) => _offset = offset;
    public Vector3f GetOffset() => _offset;

    /// <summary>Serialize GI data. May throw ArchiveException.</summary>
    public void SerializeData(Archive archive)
    {
        using var block = archive.OpenUnorderedBlock("lightProbes");
        const uint currentVersion = 2;
        var version = archive.SerializeVersion(currentVersion);
        if (version == currentVersion)
        {
            var mesh = _lightProbesMesh;
            ArchiveSerialization.SerializeValue(archive, "mesh", ref mesh);
            // Baked data serialization uses SH list through ArchiveSerialization when available.
        }
    }

    private void ReloadData()
    {
        var cache = GetSubsystem<ResourceCache>();
        if (cache is null || string.IsNullOrEmpty(_fileRef.Name))
        {
            _lightProbesMesh.Define(Array.Empty<Vector3f>());
            _lightProbesBakedData.Clear();
            return;
        }

        BinaryFile? file;
        try
        {
            file = cache.GetTempResource<BinaryFile>(_fileRef.Name, false);
        }
        catch (InvalidOperationException)
        {
            file = null;
        }

        if (file is null || !file.LoadObjectCallback(archive => SerializeData(archive)))
        {
            _lightProbesMesh.Define(Array.Empty<Vector3f>());
            _lightProbesBakedData.Clear();
        }
        else
        {
            for (var i = 0; i < _lightProbesMesh.Vertices.Count; ++i)
                _lightProbesMesh.Vertices[i] += _offset;
        }
    }
}
