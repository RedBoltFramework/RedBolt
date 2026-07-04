namespace RedBolt;

public enum IndexBufferType
{
    None,
    UInt16,
    UInt32
}

public sealed class IndexBuffer
{
    private uint[] unpackedData = [];

    public uint IndexCount { get; private set; }

    public uint IndexSize => LargeIndices ? 4u : 2u;

    public bool IsDynamic { get; private set; }

    public bool LargeIndices { get; private set; }

    public bool Shadowed { get; private set; }

    public uint GetIndexCount() => IndexCount;

    public uint GetIndexSize() => IndexSize;

    public bool GetUsedVertexRange(uint start, uint count, out uint minVertex, out uint vertexCount)
    {
        minVertex = 0;
        vertexCount = 0;
        if (start >= unpackedData.Length)
            return false;

        var actualCount = Math.Min(count, (uint)unpackedData.Length - start);
        if (actualCount == 0)
            return false;

        var min = uint.MaxValue;
        var max = 0u;
        for (var i = 0u; i < actualCount; ++i)
        {
            var index = unpackedData[(int)(start + i)];
            min = Math.Min(min, index);
            max = Math.Max(max, index);
        }

        minVertex = min;
        vertexCount = max - min + 1;
        return true;
    }

    public IReadOnlyList<uint> GetUnpackedData(uint start = 0, uint count = uint.MaxValue)
    {
        if (start >= unpackedData.Length)
            return Array.Empty<uint>();

        var actualCount = Math.Min(count, (uint)unpackedData.Length - start);
        return unpackedData.Skip((int)start).Take((int)actualCount).ToArray();
    }

    public bool IsDynamicBuffer() => IsDynamic;

    public void SetSize(uint indexCount, bool largeIndices = false, bool dynamic = false)
    {
        IndexCount = indexCount;
        LargeIndices = largeIndices;
        IsDynamic = dynamic;
        if (unpackedData.Length != indexCount)
            unpackedData = new uint[indexCount];
    }

    public void SetShadowed(bool enable)
    {
        Shadowed = enable;
    }

    public void SetUnpackedData(IEnumerable<uint> data, uint start = 0, uint count = uint.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(data);
        var source = count == uint.MaxValue ? data.ToArray() : data.Take((int)count).ToArray();
        var requiredSize = checked(start + (uint)source.Length);
        if (requiredSize > IndexCount)
            SetSize(requiredSize, LargeIndices, IsDynamic);

        Array.Copy(source, 0, unpackedData, start, source.Length);
    }

    public static IndexBufferType GetIndexBufferType(IndexBuffer? indexBuffer)
    {
        if (indexBuffer is null)
            return IndexBufferType.None;

        return indexBuffer.LargeIndices ? IndexBufferType.UInt32 : IndexBufferType.UInt16;
    }
}
