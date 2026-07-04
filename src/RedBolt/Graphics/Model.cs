namespace RedBolt;

public sealed class Model : Resource
{
    private readonly List<List<Geometry>> geometries = [];

    internal Model(string name, uint vertexCount)
        : base(name)
    {
        SetNumGeometries(1);
        SetNumGeometryLodLevels(0, 1);
        var geometry = new Geometry();
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, vertexCount, 0, vertexCount);
        SetGeometry(0, 0, geometry);
    }

    public IReadOnlyList<IReadOnlyList<Geometry>> Geometries => geometries;

    public uint NumGeometries => (uint)geometries.Count;

    public uint VertexCount => geometries.Count == 0 || geometries[0].Count == 0 ? 0u : geometries[0][0].VertexCount;

    public void SetNumGeometries(uint num)
    {
        while (geometries.Count < num)
            geometries.Add([]);

        while (geometries.Count > num)
            geometries.RemoveAt(geometries.Count - 1);
    }

    public bool SetNumGeometryLodLevels(uint index, uint num)
    {
        if (index >= geometries.Count)
            return false;

        var lodLevels = geometries[(int)index];
        while (lodLevels.Count < num)
            lodLevels.Add(new Geometry());

        while (lodLevels.Count > num)
            lodLevels.RemoveAt(lodLevels.Count - 1);

        return true;
    }

    public bool SetGeometry(uint index, uint lodLevel, Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (index >= geometries.Count)
            return false;

        var lodLevels = geometries[(int)index];
        if (lodLevel >= lodLevels.Count)
            return false;

        lodLevels[(int)lodLevel] = geometry;
        return true;
    }

    public Geometry? GetGeometry(uint index, uint lodLevel = 0)
    {
        if (index >= geometries.Count)
            return null;

        var lodLevels = geometries[(int)index];
        if (lodLevels.Count == 0)
            return null;

        return lodLevels[(int)Math.Min(lodLevel, (uint)lodLevels.Count - 1)];
    }

    public uint GetNumGeometryLodLevels(uint index)
    {
        return index < geometries.Count ? (uint)geometries[(int)index].Count : 0u;
    }
}
