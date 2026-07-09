namespace RedBolt;

public class StaticModel : Drawable
{
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

    public Vector4 LightmapScaleOffset { get; set; } = new(1.0f, 1.0f, 0.0f, 0.0f);

    public void SetModel(Model? model)
    {
        Model = model;
        SetBoundingBox(model?.GetBoundingBox() ?? default);
        ResizeMaterials(model?.NumGeometries ?? 0u);
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

    public void SetLightmapScaleOffset(Vector4 scaleOffset)
    {
        LightmapScaleOffset = scaleOffset;
    }

    public Vector4 GetLightmapScaleOffset() => LightmapScaleOffset;

    public Geometry? GetLodGeometry(uint batchIndex, uint level)
    {
        return Model?.GetGeometry(batchIndex, level);
    }

    public bool IsInside(Vector3 point) => IsInsideLocal(Node.WorldToLocal(point));

    public bool IsInsideLocal(Vector3 point) => GetBoundingBox().IsInside(point) != Intersection.Outside;

    private void ResizeMaterials(uint count)
    {
        while (_materials.Count < count)
            _materials.Add(null);

        while (_materials.Count > count)
            _materials.RemoveAt(_materials.Count - 1);
    }
}
