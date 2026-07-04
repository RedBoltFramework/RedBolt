namespace RedBolt;

public sealed class StaticModel : Drawable
{
    private readonly List<Material?> materials = [];

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

    public void SetModel(Model? model)
    {
        Model = model;
        ResizeMaterials(model?.NumGeometries ?? 0u);
    }

    public void SetMaterial(Material? material)
    {
        if (materials.Count == 0)
            ResizeMaterials(1);

        for (var i = 0; i < materials.Count; ++i)
            materials[i] = material;
    }

    public bool SetMaterial(uint index, Material? material)
    {
        if (index >= materials.Count)
            return false;

        materials[(int)index] = material;
        return true;
    }

    public Material? GetMaterial(uint index = 0)
    {
        return index < materials.Count ? materials[(int)index] : null;
    }

    public Geometry? GetLodGeometry(uint batchIndex, uint level)
    {
        return Model?.GetGeometry(batchIndex, level);
    }

    private void ResizeMaterials(uint count)
    {
        while (materials.Count < count)
            materials.Add(null);

        while (materials.Count > count)
            materials.RemoveAt(materials.Count - 1);
    }
}
