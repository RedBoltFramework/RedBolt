namespace RedBolt;

/// <summary>Static model per-geometry extra data.</summary>
public struct StaticModelGeometryData
{
    /// <summary>Geometry center.</summary>
    public Vector3f Center;
    /// <summary>Current LOD level.</summary>
    public uint LodLevel;
}

/// <summary>Static model component.</summary>
public class StaticModel : Drawable
{
    /// <summary>Construct.</summary>
    public StaticModel()
        : base(DrawableFlags.Geometry)
    {
        RegisterAttribute("Is Enabled", typeof(bool), self => ((StaticModel)self).Enabled,
            (self, value) => ((StaticModel)self).SetEnabled(value is bool enabled && enabled), true);
        _materialsAttr = new ResourceRefList(nameof(Material));
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _materialsAttr.Names.Clear();
        _model = null;
        _geometries.Clear();
        _geometryData.Clear();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<StaticModel>(ObjectCategory.Category_Geometry);
    }

    public Material? Material
    {
        get => GetMaterial();
        set => SetMaterial(value);
    }

    public Model? Model => _model;

    public uint NumGeometries => (uint)_geometries.Count;

    public uint OcclusionLodLevel
    {
        get => _occlusionLodLevel;
        set => SetOcclusionLodLevel(value);
    }

    public bool BakeLightmap
    {
        get => _bakeLightmap;
        set => SetBakeLightmap(value);
    }

    public float ScaleInLightmap
    {
        get => _scaleInLightmap;
        set => SetScaleInLightmap(value);
    }

    public uint LightmapIndex
    {
        get => _lightmapIndex;
        set => SetLightmapIndex(value);
    }

    public Vector4f LightmapScaleOffset
    {
        get => _lightmapScaleOffset;
        set => SetLightmapScaleOffset(value);
    }

    public virtual void SetModel(Model? model)
    {
        if (ReferenceEquals(_model, model))
            return;

        if (Node is null)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                Log.GetLogger(), "Can not set model while model component is not attached to a scene node.");
            return;
        }

        if (_model is not null)
            UnsubscribeFromEvent<ResourceEvents.ReloadFinished>(_model);

        _model = model;
        if (model is not null)
        {
            SubscribeToEvent<ResourceEvents.ReloadFinished>(model, HandleModelReloadFinished);

            SetNumGeometries(model.GetNumGeometries());
            var geometries = model.GetGeometries();
            _worldTransform[0] = Node.GetWorldTransform();
            for (var i = 0; i < geometries.Count; ++i)
            {
                var batch = _batches[i];
                batch.WorldTransform = _worldTransform;
                _batches[i] = batch;
                _geometries[i].Clear();
                _geometries[i].AddRange(geometries[i]);
                _geometryData[i] = new StaticModelGeometryData { Center = model.GetGeometryCenter((uint)i) };
            }

            SetBoundingBox(model.GetBoundingBox());
            ResetLodLevels();
        }
        else
        {
            SetNumGeometries(0);
            SetBoundingBox(new BoundingBox());
        }
    }

    public void SetBoundingBox(BoundingBox box)
    {
        _boundingBox = box;
        OnMarkedDirty(Node);
    }

    public virtual void SetMaterial(Material? material)
    {
        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.Material = material;
            _batches[i] = batch;
        }
    }

    public virtual bool SetMaterial(uint index, Material? material)
    {
        if (index >= _batches.Count)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                Log.GetLogger(), "Material index {Index} is out of bounds", index);
            return false;
        }

        var batch = _batches[(int)index];
        batch.Material = material;
        _batches[(int)index] = batch;
        return true;
    }

    public void SetOcclusionLodLevel(uint level)
    {
        _occlusionLodLevel = level;
    }

    public void ApplyMaterialList(string fileName = "")
    {
        var useFileName = fileName.Trim();
        if (string.IsNullOrEmpty(useFileName) && Model is not null)
            useFileName = Path.ChangeExtension(Model.Name, ".txt");

        var cache = GetSubsystem<ResourceCache>()!;
        using var file = cache.GetTempResource<BinaryFile>(useFileName, false);
        if (file is null)
            return;

        SetMaterialsAttr(file.AsDeserializer().ReadResourceRefList());
    }

    public void ResetLodLevels()
    {
        for (var i = 0; i < _batches.Count; ++i)
        {
            if (_geometries[i].Count == 0)
                _geometries[i].Add(null);
            var batch = _batches[i];
            batch.Geometry = GetGeometryIfNotEmpty(_geometries[i][0]);
            _batches[i] = batch;
            var geometryData = _geometryData[i];
            geometryData.LodLevel = 0;
            _geometryData[i] = geometryData;
        }

        SetLodDistance(float.PositiveInfinity);
    }

    public Model? GetModel() => Model;

    public uint GetNumGeometries() => NumGeometries;

    public override uint GetNumOccluderTriangles()
    {
        var triangles = 0u;
        for (var i = 0u; i < NumGeometries; ++i)
        {
            var geometry = GetLodGeometry(i, OcclusionLodLevel);
            if (geometry is null)
                continue;

            var material = GetMaterial(i);
            if (material is not null && !material.GetOcclusion())
                continue;

            triangles += geometry.GetIndexCount() / 3u;
        }

        return triangles;
    }

    /// <summary>Draw to occlusion buffer. Return true if did not run out of triangles.</summary>
    public override bool DrawOcclusion(OcclusionBuffer buffer)
    {
        for (var i = 0; i < _batches.Count; ++i)
        {
            var geometry = GetLodGeometry((uint)i, OcclusionLodLevel);
            if (geometry is null)
                continue;

            var material = _batches[i].Material;
            if (material is not null)
            {
                if (!material.GetOcclusion())
                    continue;
                buffer.SetCullMode(material.GetCullMode());
            }
            else
                buffer.SetCullMode(CullMode.CounterClockwise);

            geometry.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize,
                out var elements);
            if (vertexData is null || indexData is null || elements is null)
                continue;
            var positionElement = VertexBuffer.GetElement(elements, VertexElementType.Vector3,
                VertexElementSemantic.Position);
            if (positionElement is null || positionElement.Value.Offset != 0)
                continue;

            if (!buffer.AddTriangles(Node.GetWorldTransform(), vertexData, vertexSize, indexData, indexSize,
                    geometry.GetIndexStart(), geometry.GetIndexCount()))
                return false;
        }

        return true;
    }

    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        ProcessCustomRayQuery(query, GetWorldBoundingBox(), Node.GetWorldTransform(), results);
    }

    /// <summary>Process raycast with custom transform.</summary>
    public void ProcessCustomRayQuery(RayOctreeQuery query, BoundingBox worldBoundingBox,
        Matrix3x4 worldTransform, List<RayQueryResult> results)
    {
        var level = query.Level;

        if (level == RayQueryLevel.RayAabb)
        {
            base.ProcessCustomRayQuery(query, worldBoundingBox, results);
            return;
        }

        var inverse = worldTransform.Inverse;
        var localRay = query.Ray.Transformed(inverse);
        var distanceAndNormal = localRay.HitDistanceAndNormal(GetBoundingBox());
        var distance = distanceAndNormal.Distance;
        var normal = (worldTransform.Matrix3 * distanceAndNormal.Normal).Normalized;
        var geometryUv = Vector2f.Zero;
        var hitBatch = uint.MaxValue;

        if (level >= RayQueryLevel.RayTriangle && distance < query.MaxDistance)
        {
            distance = float.PositiveInfinity;

            for (var i = 0; i < _batches.Count; ++i)
            {
                var geometry = _batches[i].Geometry;
                if (geometry is null)
                    continue;

                float geometryDistance;
                Vector3f geometryNormal;
                if (level == RayQueryLevel.RayTriangle)
                    geometryDistance = geometry.GetHitDistance(localRay, out geometryNormal);
                else
                    geometryDistance = geometry.GetHitDistance(localRay, out geometryNormal, out geometryUv);
                if (geometryDistance < query.MaxDistance && geometryDistance < distance)
                {
                    distance = geometryDistance;
                    normal = (worldTransform.Matrix3 * geometryNormal).Normalized;
                    hitBatch = checked((uint)i);
                }
            }
        }

        if (distance < query.MaxDistance)
        {
            results.Add(new RayQueryResult
            {
                Position = query.Ray.Origin + distance * query.Ray.Direction,
                Normal = normal,
                TextureUV = geometryUv,
                Distance = distance,
                Drawable = this,
                Node = Node,
                SubObject = hitBatch
            });
        }
    }

    public override void UpdateBatches(CommonFrameInfo frame)
    {
        var worldBoundingBox = GetWorldBoundingBox();
        var camera = frame.Camera!;
        _worldTransform[0] = Node.GetWorldTransform();

        SetDistance(camera.GetDistance(worldBoundingBox.Center));
        if (_batches.Count == 1)
        {
            var batch = _batches[0];
            batch.Distance = GetDistance();
            _batches[0] = batch;
        }
        else
        {
            for (var i = 0; i < _batches.Count; ++i)
            {
                var batch = _batches[i];
                batch.Distance = camera.GetDistance(_worldTransform[0] * _geometryData[i].Center);
                _batches[i] = batch;
            }
        }

        var size = worldBoundingBox.Size;
        var scale = (size.X + size.Y + size.Z) / 3.0f;
        var newLodDistance = camera.GetLodDistance(GetDistance(), scale, LodBias);
        if (newLodDistance != GetLodDistance())
        {
            SetLodDistance(newLodDistance);
            CalculateLodLevels();
        }

    }

    public virtual Material? GetMaterial()
    {
        return GetMaterial(0);
    }

    public virtual Material? GetMaterial(uint index)
    {
        return index < _batches.Count ? _batches[(int)index].Material : null;
    }

    public uint GetOcclusionLodLevel() => OcclusionLodLevel;

    public void SetBakeLightmap(bool bakeLightmap)
    {
        _bakeLightmap = bakeLightmap;
        UpdateBatchesLightmaps();
    }

    public bool GetBakeLightmap() => BakeLightmap;

    public bool GetBakeLightmapEffective() => BakeLightmap && ScaleInLightmap > 0.0f;

    public void SetScaleInLightmap(float scale)
    {
        _scaleInLightmap = scale;
    }

    public float GetScaleInLightmap() => ScaleInLightmap;

    public void SetLightmapIndex(uint index)
    {
        _lightmapIndex = index;
        UpdateBatchesLightmaps();
    }

    public uint GetLightmapIndex() => LightmapIndex;

    public void SetLightmapScaleOffset(Vector4f scaleOffset)
    {
        _lightmapScaleOffset = scaleOffset;
        UpdateBatchesLightmaps();
    }

    public Vector4f GetLightmapScaleOffset() => LightmapScaleOffset;

    public void SetModelAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        SetModel(string.IsNullOrEmpty(value.Name) ? null : cache.GetResourceOrNull<Model>(value.Name));
    }

    public ResourceRef GetModelAttr() => Resource.GetResourceRef(Model, ObjectReflection<Model>.TypeId);

    public void SetMaterialsAttr(ResourceRefList value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var count = Math.Min(value.Names.Count, _batches.Count);
        for (var i = 0; i < count; ++i)
        {
            var name = value.Names[i];
            var batch = _batches[i];
            batch.Material = string.IsNullOrEmpty(name) ? null : cache.GetResourceOrNull<Material>(name);
            _batches[i] = batch;
        }

        for (var i = count; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.Material = null;
            _batches[i] = batch;
        }
    }

    public ResourceRefList GetMaterialsAttr()
    {
        while (_materialsAttr.Names.Count < _batches.Count)
            _materialsAttr.Names.Add(string.Empty);
        while (_materialsAttr.Names.Count > _batches.Count)
            _materialsAttr.Names.RemoveAt(_materialsAttr.Names.Count - 1);
        for (var i = 0; i < _batches.Count; ++i)
            _materialsAttr.Names[i] = GetMaterial((uint)i)?.Name ?? string.Empty;
        return _materialsAttr;
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        _worldBoundingBox = _boundingBox.Transformed(Node.GetWorldTransform());
    }

    public override Geometry? GetLodGeometry(uint batchIndex, uint level)
    {
        if (batchIndex >= _geometries.Count)
            return null;

        var geometries = _geometries[(int)batchIndex];
        return level < geometries.Count ? geometries[(int)level] : _batches[(int)batchIndex].Geometry;
    }

    public bool IsInside(Vector3f point)
    {
        if (Node is null)
            return false;

        return IsInsideLocal(Node.WorldToLocal(point));
    }

    public bool IsInsideLocal(Vector3f point)
    {
        // Early-out if point is not inside bounding box
        if (GetBoundingBox().IsInside(point) == Intersection.Outside)
            return false;

        var localRay = new Ray(point, new Vector3f(1.0f, -1.0f, 1.0f));
        for (var index = 0u; index < NumGeometries; ++index)
        {
            var geometry = _batches[(int)index].Geometry;
            if (geometry is not null && geometry.IsInside(localRay))
                return true;
        }

        return false;
    }

    protected void SetNumGeometries(uint count)
    {
        while (_batches.Count < count)
        {
            _batches.Add(new SourceBatch());
            _geometries.Add([]);
            _geometryData.Add(default);
        }

        while (_batches.Count > count)
        {
            _batches.RemoveAt(_batches.Count - 1);
            _geometries.RemoveAt(_geometries.Count - 1);
            _geometryData.RemoveAt(_geometryData.Count - 1);
        }

        ResetLodLevels();
        UpdateBatchesLightmaps();
    }

    private void HandleModelReloadFinished(ref ResourceEvents.ReloadFinished eventData)
    {
        var currentModel = _model;
        _model = null;
        SetModel(currentModel);
    }

    protected void CalculateLodLevels()
    {
        for (var i = 0; i < _batches.Count; ++i)
        {
            var batchGeometries = _geometries[i];
            if (batchGeometries.Count <= 1)
                continue;

            var level = 1;
            for (; level < batchGeometries.Count; ++level)
            {
                var geometry = batchGeometries[level];
                if (geometry is not null && GetLodDistance() <= geometry.GetLodDistance())
                    break;
            }

            var selectedLevel = level - 1;
            var geometryData = _geometryData[i];
            if (geometryData.LodLevel != selectedLevel)
            {
                geometryData.LodLevel = checked((uint)selectedLevel);
                _geometryData[i] = geometryData;
                var batch = _batches[i];
                batch.Geometry = GetGeometryIfNotEmpty(batchGeometries[selectedLevel]);
                _batches[i] = batch;
            }
        }
    }

    protected void UpdateBatchesLightmaps()
    {
        var useLightmap = GetBakeLightmapEffective();
        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            if (useLightmap)
            {
                batch.LightmapIndex = LightmapIndex;
                batch.GeometryType = GeometryType.StaticNoInstancing;
                batch.LightmapScaleOffset = LightmapScaleOffset;
            }
            else
            {
                batch.GeometryType = GeometryType.Static;
                batch.LightmapScaleOffset = null;
            }

            _batches[i] = batch;
        }
    }

    /// <summary>Extra per-geometry data.</summary>
    protected readonly List<StaticModelGeometryData> _geometryData = [];
    /// <summary>All geometries.</summary>
    protected readonly List<List<Geometry?>> _geometries = [];
    /// <summary>Model.</summary>
    protected Model? _model;
    /// <summary>Occlusion geometry LOD level.</summary>
    protected uint _occlusionLodLevel = uint.MaxValue;
    /// <summary>Material resource attribute.</summary>
    protected ResourceRefList _materialsAttr;
    /// <summary>Whether to bake lightmap.</summary>
    protected bool _bakeLightmap;
    /// <summary>Scale in lightmap.</summary>
    protected float _scaleInLightmap = 1.0f;
    /// <summary>Lightmap index.</summary>
    protected uint _lightmapIndex;
    /// <summary>Lightmap scale and offset.</summary>
    protected Vector4f _lightmapScaleOffset = new(1.0f, 1.0f, 0.0f, 0.0f);
}
