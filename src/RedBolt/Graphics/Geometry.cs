namespace RedBolt;

public enum PrimitiveType
{
    TriangleList,
    LineList,
    PointList,
    TriangleStrip,
    LineStrip
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

    public VertexBuffer? GetVertexBuffer(uint index)
    {
        return index < vertexBuffers.Count ? vertexBuffers[(int)index] : null;
    }

    public void SetIndexBuffer(IndexBuffer? buffer)
    {
        IndexBuffer = buffer;
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
