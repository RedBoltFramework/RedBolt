namespace RedBolt;

public sealed class IndexBuffer
{
    public uint IndexCount { get; private set; }

    public bool IsDynamic { get; private set; }

    public bool LargeIndices { get; private set; }

    public void SetSize(uint indexCount, bool largeIndices = false, bool dynamic = false)
    {
        IndexCount = indexCount;
        LargeIndices = largeIndices;
        IsDynamic = dynamic;
    }
}
