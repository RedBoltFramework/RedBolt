using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Global illumination manager.</summary>
public class GlobalIllumination : Component
{
    [ThreadStatic]
    private static List<(uint First, uint Second)>? _edges;

    /// <summary>Emission indirect brightness.</summary>
    private float _emissionBrightness = 1.0f;
    /// <summary>Reference on file with GI data.</summary>
    private ResourceRef _fileRef = new(new StringHash(nameof(BinaryFile)));
    /// <summary>Offset.</summary>
    private Vector3f _offset;
    /// <summary>Light probes mesh.</summary>
    private TetrahedralMesh _lightProbesMesh = new();
    /// <summary>Baked light probes data.</summary>
    private LightProbeCollectionBakedData _lightProbesBakedData = new();

    /// <summary>Construct.</summary>
    public GlobalIllumination()
    {
        // TODO: port URHO3D_ACTION_STATIC_LABEL("Reload!", ReloadData, "Reload data from the file");
        RegisterAttribute("Emission Brightness", typeof(float), self => ((GlobalIllumination)self)._emissionBrightness,
            (self, value) => ((GlobalIllumination)self)._emissionBrightness = (float)value!, 1.0f);
        RegisterAttribute("Offset", typeof(Vector3f), self => ((GlobalIllumination)self)._offset,
            (self, value) => ((GlobalIllumination)self)._offset = (Vector3f)value!, Vector3f.Zero);
        RegisterAttribute("Data File", typeof(ResourceRef), self => ((GlobalIllumination)self).GetFileRef(),
            (self, value) => ((GlobalIllumination)self).SetFileRef((ResourceRef)value!),
            new ResourceRef(new StringHash(nameof(BinaryFile))), AttributeMode.Default | AttributeMode.NoEdit);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Register object factory. Drawable must be registered first.</summary>
    public static void RegisterObject(Context context) =>
        context.AddFactoryReflection<GlobalIllumination>(ObjectCategory.Category_Subsystem);

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var edges = _edges ??= [];
        _lightProbesMesh.CollectEdges(edges);

        foreach (var edge in edges)
        {
            var startPos = _lightProbesMesh.Vertices[(int)edge.First];
            var endPos = _lightProbesMesh.Vertices[(int)edge.Second];
            debug!.AddLine(startPos, endPos, Color.Yellow);
        }

        foreach (var ignoredVertex in _lightProbesMesh.IgnoredVertices)
        {
            var position = _lightProbesMesh.Vertices[(int)ignoredVertex];
            var boundingBox = new BoundingBox(position - Vector3f.One * 0.1f, position + Vector3f.One * 0.1f);
            debug!.AddBoundingBox(boundingBox, Color.Red);
        }

        foreach (var highlightEdge in _lightProbesMesh.DebugHighlightEdges)
        {
            var startPos = _lightProbesMesh.Vertices[(int)highlightEdge.First];
            var endPos = _lightProbesMesh.Vertices[(int)highlightEdge.Second];
            debug!.AddLine(startPos, endPos, Color.Red);
        }
    }

    /// <summary>Handle scene set for event subscription.</summary>
    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (previousScene is not null)
            UnsubscribeFromEvent<SceneEvents.WorldOriginPostUpdateEvent>(previousScene);

        if (scene is not null)
            SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);
    }

    /// <summary>Reset light probes.</summary>
    public void ResetLightProbes()
    {
        _lightProbesBakedData.Clear();
        _lightProbesMesh = new TetrahedralMesh();
    }

    /// <summary>Compile all enabled light probe groups in the scene.</summary>
    public void CompileLightProbes()
    {
        ResetLightProbes();

        // Collect light probes
        var collection = new LightProbeCollection();
        LightProbeGroup.CollectLightProbes(Scene!, collection, _lightProbesBakedData, true);
        if (collection.Empty)
            return;

        // Add padding to avoid vertex collision
        _lightProbesMesh.Define(collection.WorldPositions);

        // Store in file
        var cache = GetSubsystem<ResourceCache>()!;
        var file = cache.GetTempResource(new StringHash(nameof(BinaryFile)), _fileRef.Name) as BinaryFile;

        if (file is null)
        {
            Log.GetLogger().LogError("Cannot save Global Illumination data: output file doesn't exist");
            return;
        }

        file.Clear();
        if (!file.SaveObjectCallback(archive => SerializeData(archive)))
        {
            Log.GetLogger().LogError("Cannot save Global Illumination data: serialization failed");
            return;
        }

        if (!file.SaveFile(file.AbsoluteFileName))
        {
            Log.GetLogger().LogError("Cannot save Global Illumination data: cannot save file");
            return;
        }
    }

    /// <summary>Sample ambient spherical harmonics.</summary>
    public SphericalHarmonicsDot9 SampleAmbientSH(Vector3f position, ref uint hint) =>
        _lightProbesMesh.Sample(_lightProbesBakedData.SphericalHarmonics, position, ref hint);

    /// <summary>Sample average ambient lighting.</summary>
    public Vector3f SampleAverageAmbient(Vector3f position, ref uint hint) =>
        _lightProbesMesh.Sample(_lightProbesBakedData.Ambient, position, ref hint);

    /// <summary>Set emission brightness.</summary>
    public void SetEmissionBrightness(float emissionBrightness) => _emissionBrightness = emissionBrightness;
    /// <summary>Return emission brightness.</summary>
    public float GetEmissionBrightness() => _emissionBrightness;

    /// <summary>Set reference on file with baked data.</summary>
    public void SetFileRef(ResourceRef fileRef)
    {
        if (_fileRef != fileRef)
        {
            _fileRef = fileRef;
            ReloadData();
        }
    }

    /// <summary>Return reference on file with baked data.</summary>
    public ResourceRef GetFileRef() => _fileRef;

    /// <summary>Set offset.</summary>
    public void SetOffset(Vector3f offset) => _offset = offset;
    /// <summary>Return offset.</summary>
    public Vector3f GetOffset() => _offset;

    /// <summary>Serialize GI data. May throw ArchiveException.</summary>
    public void SerializeData(Archive archive)
    {
        using var block = archive.OpenUnorderedBlock("lightProbes");
        const uint currentVersion = 2;
        var version = archive.SerializeVersion(currentVersion);
        if (version == currentVersion)
        {
            ArchiveSerialization.SerializeValue(archive, "mesh", ref _lightProbesMesh);
            ArchiveSerialization.SerializeValue(archive, "data", ref _lightProbesBakedData);
        }
    }

    /// <summary>Handle world origin post-update.</summary>
    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        var delta = eventData.Delta.ToVector3();

        _offset -= delta;
        for (var i = 0; i < _lightProbesMesh.Vertices.Count; ++i)
            _lightProbesMesh.Vertices[i] -= delta;
    }

    /// <summary>Reload GI data.</summary>
    private void ReloadData()
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var file = cache.GetTempResource(new StringHash(nameof(BinaryFile)), _fileRef.Name) as BinaryFile;

        if (file is null || !file.LoadObjectCallback(archive => SerializeData(archive)))
        {
            _lightProbesMesh = new TetrahedralMesh();
            _lightProbesBakedData.Clear();
        }
        else
        {
            for (var i = 0; i < _lightProbesMesh.Vertices.Count; ++i)
                _lightProbesMesh.Vertices[i] += _offset;
        }
    }
}
