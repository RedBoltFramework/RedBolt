namespace RedBolt;

/// <summary>Static model component.</summary>
/// <remarks>
// TODO: port - C++ code: bool StaticModel::DrawOcclusion(OcclusionBuffer* buffer) override
/// </remarks>
public class StaticModel : Drawable
{
    private static readonly uint ModelType = StringHash.Calculate("Model");
    private readonly List<Material?> _materials = [];

    public StaticModel()
        : base(DrawableFlags.Geometry)
    {
    }

    public Material? Material
    {
        get => GetMaterial();
        set => SetMaterial(value);
    }

    public Model? Model { get; private set; }

    public uint NumGeometries => Model?.NumGeometries ?? 0u;

    public uint OcclusionLodLevel { get; set; } = uint.MaxValue;

    public bool BakeLightmap { get; set; }

    public float ScaleInLightmap { get; set; } = 1.0f;

    public uint LightmapIndex { get; set; }

    public Vector4f LightmapScaleOffset { get; set; } = new(1.0f, 1.0f, 0.0f, 0.0f);

    public void SetModel(Model? model)
    {
        if (ReferenceEquals(Model, model))
            return;

        if (!IsAttached)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                Log.Logger, "Can not set model while model component is not attached to a scene node.");
            return;
        }

        if (Model is not null)
            Model.ReloadFinished -= HandleModelReloadFinished;

        Model = model;
        if (Model is not null)
        {
            Model.ReloadFinished += HandleModelReloadFinished;
            SetBoundingBox(Model.GetBoundingBox());
            ResizeMaterials(Model.NumGeometries);
        }
        else
        {
            ResizeMaterials(0u);
            SetBoundingBox(default);
        }
    }

    public void SetBoundingBox(BoundingBox box)
    {
        SetLocalBoundingBox(box);
    }

    public void SetMaterial(Material? material)
    {
        if (_materials.Count == 0)
            ResizeMaterials(1);

        for (var i = 0; i < _materials.Count; ++i)
            _materials[i] = material;
    }

    public bool SetMaterial(uint index, Material? material)
    {
        if (index >= _materials.Count)
            return false;

        _materials[(int)index] = material;
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

    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        if (query.Level == RayQueryLevel.RayAabb)
        {
            base.ProcessRayQuery(query, results);
            return;
        }

        var worldBoundingBox = GetWorldBoundingBox();
        if (query.Ray.HitDistance(worldBoundingBox) >= query.MaxDistance || !IsAttached)
            return;

        var localOrigin = Node.WorldToLocal(query.Ray.Origin);
        var localEnd = Node.WorldToLocal(query.Ray.Origin + query.Ray.Direction);
        var localDirection = localEnd - localOrigin;
        if (localDirection.LengthSquared <= MathDefs.Epsilon * MathDefs.Epsilon)
            return;

        var localRay = new Ray(localOrigin, localDirection);
        var distance = localRay.HitDistance(GetBoundingBox());
        if (float.IsPositiveInfinity(distance) || distance >= query.MaxDistance)
            return;

        if (query.Level == RayQueryLevel.RayTriangle)
        {
            distance = float.PositiveInfinity;
            for (var i = 0u; i < NumGeometries; ++i)
            {
                var geometry = GetLodGeometry(i, 0u);
                if (geometry is null)
                    continue;

                var geometryDistance = geometry.GetHitDistance(localRay);
                if (geometryDistance < distance)
                    distance = geometryDistance;
            }

            if (float.IsPositiveInfinity(distance) || distance >= query.MaxDistance)
                return;
        }

        var localHit = localRay.Origin + localRay.Direction * distance;
        var worldHit = Node.LocalToWorld(localHit);
        results.Add(new RayQueryResult
        {
            Drawable = this,
            Node = Node,
            Distance = (worldHit - query.Ray.Origin).Length,
            Position = worldHit,
            Normal = -query.Ray.Direction,
            TextureUV = Vector2f.Zero,
            SubObject = uint.MaxValue
        });
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
                var center = Model?.GetGeometryCenter((uint)i) ?? Vector3f.Zero;
                _batches[i] = UpdateBatchDistance(_batches[i], camera.GetDistance(worldTransform * center));
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
        return index < _materials.Count ? _materials[(int)index] : null;
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
        var count = Math.Min(value.Names.Count, _materials.Count);
        for (var i = 0; i < count; ++i)
        {
            var name = value.Names[i];
            _materials[i] = string.IsNullOrEmpty(name) ? null : cache.GetResourceOrNull<Material>(name);
        }

        for (var i = count; i < _materials.Count; ++i)
            _materials[i] = null;
    }

    public ResourceRefList GetMaterialsAttr() => Resource.GetResourceRefList(_materials);

    public override Geometry? GetLodGeometry(uint batchIndex, uint level)
    {
        var model = Model;
        if (model is null || batchIndex >= model.NumGeometries)
            return null;

        var geometries = model.GetGeometries()[(int)batchIndex];
        return level < geometries.Count ? geometries[(int)level] : model.GetGeometry(batchIndex);
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

    private void ResizeMaterials(uint count)
    {
        while (_materials.Count < count)
            _materials.Add(null);

        while (_materials.Count > count)
            _materials.RemoveAt(_materials.Count - 1);
    }

    private void HandleModelReloadFinished(Resource resource)
    {
        var currentModel = Model;
        Model = null;
        SetModel(currentModel);
    }

    private void CalculateLodLevels()
    {
        var model = Model;
        if (model is null)
            return;

        var geometries = model.GetGeometries();
        for (var i = 0; i < _batches.Count && i < geometries.Count; ++i)
        {
            var batchGeometries = geometries[i];
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
            var batch = _batches[i];
            batch.Geometry = batchGeometries[selectedLevel];
            _batches[i] = batch;
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
