namespace RedBolt;

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

    public Material? GetMaterial(uint index = 0)
    {
        return index < _materials.Count ? _materials[(int)index] : null;
    }

    public uint GetOcclusionLodLevel() => OcclusionLodLevel;

    public void SetBakeLightmap(bool bakeLightmap)
    {
        BakeLightmap = bakeLightmap;
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
    }

    public uint GetLightmapIndex() => LightmapIndex;

    public void SetLightmapScaleOffset(Vector4f scaleOffset)
    {
        LightmapScaleOffset = scaleOffset;
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

    public Geometry? GetLodGeometry(uint batchIndex, uint level)
    {
        return Model?.GetGeometry(batchIndex, level);
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
}
