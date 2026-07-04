namespace RedBolt;

public sealed class VertexBuffer
{
    public bool IsDynamic { get; private set; }

    public uint VertexCount { get; private set; }

    public uint VertexSize { get; private set; }

    public void SetSize(uint vertexCount, uint vertexSize, bool dynamic = false)
    {
        VertexCount = vertexCount;
        VertexSize = vertexSize;
        IsDynamic = dynamic;
    }
}
