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
    public PrimitiveType PrimitiveType { get; private set; } = PrimitiveType.TriangleList;

    public uint IndexStart { get; private set; }

    public uint IndexCount { get; private set; }

    public uint VertexStart { get; private set; }

    public uint VertexCount { get; private set; }
    public float LodDistance { get; set; }
    public bool IsEmpty => IndexCount == 0 && VertexCount == 0;
    public uint EffectiveIndexCount => IndexCount != 0 ? IndexCount : VertexCount;

    public bool SetDrawRange(PrimitiveType type, uint indexStart, uint indexCount, bool getUsedVertexRange = true)
    {
        return SetDrawRange(type, indexStart, indexCount, vertexStart: 0, vertexCount: indexCount);
    }

    public bool SetDrawRange(PrimitiveType type, uint indexStart, uint indexCount, uint vertexStart, uint vertexCount)
    {
        PrimitiveType = type;
        IndexStart = indexStart;
        IndexCount = indexCount;
        VertexStart = vertexStart;
        VertexCount = vertexCount;
        return true;
    }
}
