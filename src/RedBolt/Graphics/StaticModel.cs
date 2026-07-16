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
    private static readonly uint ModelType = StringHash.Calculate("Model");
    /// <summary>Extra per-geometry data.</summary>
    protected readonly List<StaticModelGeometryData> _geometryData = [];
    /// <summary>All geometries.</summary>
    protected readonly List<List<Geometry?>> _geometries = [];
    /// <summary>Model.</summary>
    protected Model? _model;

    public StaticModel()
        : base(DrawableFlags.Geometry)
    {
    }

    public Material? Material
    {
        get => GetMaterial();
        set => SetMaterial(value);
    }

    public Model? Model => _model;

    public uint NumGeometries => (uint)_geometries.Count;

    public uint OcclusionLodLevel { get; set; } = uint.MaxValue;

    public bool BakeLightmap { get; set; }

    public float ScaleInLightmap { get; set; } = 1.0f;

    public uint LightmapIndex { get; set; }

    public Vector4f LightmapScaleOffset { get; set; } = new(1.0f, 1.0f, 0.0f, 0.0f);

    public virtual void SetModel(Model? model)
    {
        if (ReferenceEquals(_model, model))
            return;

        if (!IsAttached)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                Log.Logger, "Can not set model while model component is not attached to a scene node.");
            return;
        }

        if (_model is not null)
            _model.ReloadFinished -= HandleModelReloadFinished;

        _model = model;
        if (model is not null)
        {
            model.ReloadFinished += HandleModelReloadFinished;

            SetNumGeometries(model.GetNumGeometries());
            var geometries = model.GetGeometries();
            var worldTransform = new[] { Node.GetWorldTransform() };
            for (var i = 0; i < geometries.Count; ++i)
            {
                var batch = _batches[i];
                batch.WorldTransform = worldTransform;
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
            SetBoundingBox(default);
        }
    }

    public void SetBoundingBox(BoundingBox box)
    {
        SetLocalBoundingBox(box);
    }

    public void SetMaterial(Material? material)
    {
        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.Material = material;
            _batches[i] = batch;
        }
    }

    public bool SetMaterial(uint index, Material? material)
    {
        if (index >= _batches.Count)
            return false;

        var batch = _batches[(int)index];
        batch.Material = material;
        _batches[(int)index] = batch;
        return true;
    }

    public void SetOcclusionLodLevel(uint level)
    {
        OcclusionLodLevel = level;
    }

    public void ApplyMaterialList(string fileName = "")
    {
        var useFileName = fileName.Trim();
        if (string.IsNullOrEmpty(useFileName) && Model is not null)
            useFileName = Path.ChangeExtension(Model.Name, ".txt");

        if (string.IsNullOrEmpty(useFileName))
            return;

        var cache = GetSubsystem<ResourceCache>()!;
        BinaryFile? file;
        try
        {
            file = cache.GetTempResource<BinaryFile>(useFileName, false);
        }
        catch (InvalidOperationException)
        {
            file = null;
        }

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
        var camera = frame.Camera;
        if (camera is null)
            return;

        SetDistance(camera.GetDistance(worldBoundingBox.Center));
        if (_batches.Count == 1)
            _batches[0] = UpdateBatchDistance(_batches[0], GetDistance());
        else
        {
            var worldTransform = Node.GetWorldTransform();
            for (var i = 0; i < _batches.Count; ++i)
            {
                _batches[i] = UpdateBatchDistance(_batches[i],
                    camera.GetDistance(worldTransform * _geometryData[i].Center));
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

        UpdateBatchesLightmaps();
    }

    public Material? GetMaterial(uint index = 0)
    {
        return index < _batches.Count ? _batches[(int)index].Material : null;
    }

    public uint GetOcclusionLodLevel() => OcclusionLodLevel;

    public void SetBakeLightmap(bool bakeLightmap)
    {
        BakeLightmap = bakeLightmap;
        UpdateBatchesLightmaps();
    }

    public bool GetBakeLightmap() => BakeLightmap;

    public bool GetBakeLightmapEffective() => BakeLightmap && ScaleInLightmap > 0.0f;

    public void SetScaleInLightmap(float scale)
    {
        ScaleInLightmap = scale;
    }

    public float GetScaleInLightmap() => ScaleInLightmap;

    public void SetLightmapIndex(uint index)
    {
        LightmapIndex = index;
        UpdateBatchesLightmaps();
    }

    public uint GetLightmapIndex() => LightmapIndex;

    public void SetLightmapScaleOffset(Vector4f scaleOffset)
    {
        LightmapScaleOffset = scaleOffset;
        UpdateBatchesLightmaps();
    }

    public Vector4f GetLightmapScaleOffset() => LightmapScaleOffset;

    public void SetModelAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        SetModel(string.IsNullOrEmpty(value.Name) ? null : cache.GetResourceOrNull<Model>(value.Name));
    }

    public ResourceRef GetModelAttr() => Resource.GetResourceRef(Model, new StringHash(ModelType));

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
        var materials = new List<Material?>(_batches.Count);
        for (var i = 0; i < _batches.Count; ++i)
            materials.Add(GetMaterial((uint)i));
        return Resource.GetResourceRefList(materials);
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
        if (!IsAttached)
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
            var geometry = GetLodGeometry(index, 0);
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

    private void HandleModelReloadFinished(Resource resource)
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

    private void UpdateBatchesLightmaps()
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

    private static SourceBatch UpdateBatchDistance(SourceBatch batch, float distance)
    {
        batch.Distance = distance;
        return batch;
    }
}
