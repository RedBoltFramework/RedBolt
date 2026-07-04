namespace RedBolt;

public sealed class VertexBuffer
{
    private byte[] data = [];

    public bool IsDynamic { get; private set; }

    public bool Shadowed { get; private set; }

    public IReadOnlyList<byte> Data => data;

    public uint VertexCount { get; private set; }

    public uint VertexSize { get; private set; }

    public IReadOnlyList<byte> GetData() => data;

    public uint GetVertexCount() => VertexCount;

    public uint GetVertexSize() => VertexSize;

    public bool IsDynamicBuffer() => IsDynamic;

    public void SetData(IEnumerable<byte> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        this.data = data.ToArray();
    }

    public void SetShadowed(bool enable)
    {
        Shadowed = enable;
    }

    public void SetSize(uint vertexCount, uint vertexSize, bool dynamic = false)
    {
        VertexCount = vertexCount;
        VertexSize = vertexSize;
        IsDynamic = dynamic;
        var size = checked((int)(vertexCount * vertexSize));
        if (data.Length != size)
            data = new byte[size];
    }
}
