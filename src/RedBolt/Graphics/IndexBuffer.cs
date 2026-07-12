namespace RedBolt;

public enum IndexBufferType
{
    None,
    UInt16,
    UInt32
}

public sealed class IndexBuffer : PipelineStateTracker
{
    private uint[] _unpackedData = [];

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
        if (start >= _unpackedData.Length)
            return false;

        var actualCount = Math.Min(count, (uint)_unpackedData.Length - start);
        if (actualCount == 0)
            return false;

        var min = uint.MaxValue;
        var max = 0u;
        for (var i = 0u; i < actualCount; ++i)
        {
            var index = _unpackedData[(int)(start + i)];
            min = Math.Min(min, index);
            max = Math.Max(max, index);
        }

        minVertex = min;
        vertexCount = max - min + 1;
        return true;
    }

    public IReadOnlyList<uint> GetUnpackedData(uint start = 0, uint count = uint.MaxValue)
    {
        if (start >= _unpackedData.Length)
            return Array.Empty<uint>();

        var actualCount = Math.Min(count, (uint)_unpackedData.Length - start);
        return _unpackedData.Skip((int)start).Take((int)actualCount).ToArray();
    }

    public bool IsDynamicBuffer() => IsDynamic;

    public void SetSize(uint indexCount, bool largeIndices = false, bool dynamic = false)
    {
        IndexCount = indexCount;
        LargeIndices = largeIndices;
        IsDynamic = dynamic;
        if (_unpackedData.Length != indexCount)
            _unpackedData = new uint[indexCount];
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

        Array.Copy(source, 0, _unpackedData, start, source.Length);
    }

    public static IndexBufferType GetIndexBufferType(IndexBuffer? indexBuffer)
    {
        if (indexBuffer is null)
            return IndexBufferType.None;

        return indexBuffer.LargeIndices ? IndexBufferType.UInt32 : IndexBufferType.UInt16;
    }

    protected override uint RecalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, IndexSize);
        return Math.Max(1u, hash);
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
