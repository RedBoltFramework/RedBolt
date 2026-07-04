namespace RedBolt;

public enum PrimitiveType
{
    TriangleList,
    LineList,
    PointList,
    TriangleStrip,
    LineStrip
}

public enum GeometryType
{
    Static,
    Skinned,
    Instanced,
    Billboard,
    DirectionalBillboard,
    TrailFaceCamera,
    TrailBone,
    StaticNoInstancing
}

public sealed class Geometry
{
    private readonly List<VertexBuffer?> vertexBuffers = [null];

    public IReadOnlyList<VertexBuffer?> VertexBuffers => vertexBuffers;

    public IndexBuffer? IndexBuffer { get; private set; }

    public PrimitiveType PrimitiveType { get; private set; } = PrimitiveType.TriangleList;

    public uint IndexStart { get; private set; }

    public uint IndexCount { get; private set; }

    public uint VertexStart { get; private set; }

    public uint VertexCount { get; private set; }
    public float LodDistance { get; set; }
    public bool IsEmpty => IndexCount == 0 && VertexCount == 0;
    public uint EffectiveIndexCount => IndexBuffer is not null ? IndexCount : VertexCount;

    public uint GetEffectiveIndexCount() => EffectiveIndexCount;

    public IndexBuffer? GetIndexBuffer() => IndexBuffer;

    public uint GetIndexCount() => IndexCount;

    public uint GetIndexStart() => IndexStart;

    public float GetLodDistance() => LodDistance;

    public uint GetNumVertexBuffers() => (uint)vertexBuffers.Count;

    public PrimitiveType GetPrimitiveType() => PrimitiveType;

    public uint GetPrimitiveCount()
    {
        var count = EffectiveIndexCount;
        return PrimitiveType switch
        {
            PrimitiveType.TriangleList => count / 3u,
            PrimitiveType.LineList => count / 2u,
            PrimitiveType.PointList => count,
            PrimitiveType.TriangleStrip => count >= 3u ? count - 2u : 0u,
            PrimitiveType.LineStrip => count >= 2u ? count - 1u : 0u,
            _ => 0u
        };
    }

    public uint GetVertexCount() => VertexCount;

    public uint GetVertexStart() => VertexStart;

    public IReadOnlyList<VertexBuffer?> GetVertexBuffers() => VertexBuffers;

    public bool IsHeadless() => true;

    public bool IsInstanced(GeometryType geometryType) =>
        (geometryType == GeometryType.Static || geometryType == GeometryType.Instanced) && IndexBuffer is not null;

    public bool SetNumVertexBuffers(uint count)
    {
        vertexBuffers.Clear();
        for (var index = 0; index < count; ++index)
            vertexBuffers.Add(null);
        return true;
    }

    public bool SetVertexBuffer(uint index, VertexBuffer? buffer)
    {
        if (index >= vertexBuffers.Count)
            return false;

        vertexBuffers[(int)index] = buffer;
        return true;
    }

    public void SetVertexBuffers(IEnumerable<VertexBuffer?> buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers);
        vertexBuffers.Clear();
        vertexBuffers.AddRange(buffers);
    }

    public VertexBuffer? GetVertexBuffer(uint index)
    {
        return index < vertexBuffers.Count ? vertexBuffers[(int)index] : null;
    }

    public void SetIndexBuffer(IndexBuffer? buffer)
    {
        IndexBuffer = buffer;
    }

    public void SetLodDistance(float distance)
    {
        LodDistance = distance;
    }

    public bool SetDrawRange(PrimitiveType type, uint indexStart, uint indexCount, bool getUsedVertexRange = true)
    {
        return SetDrawRange(type, indexStart, indexCount, vertexStart: 0, vertexCount: indexCount);
    }

    public bool SetDrawRange(PrimitiveType type, uint indexStart, uint indexCount, uint vertexStart, uint vertexCount, bool checkIllegal = true)
    {
        PrimitiveType = type;
        IndexStart = indexStart;
        IndexCount = indexCount;
        VertexStart = vertexStart;
        VertexCount = vertexCount;
        return true;
    }
}
