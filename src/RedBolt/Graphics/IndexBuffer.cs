using System.Buffers.Binary;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public enum IndexBufferType
{
    None,
    UInt16,
    UInt32
}

/// <summary>Hardware index buffer.</summary>
public sealed class IndexBuffer : PipelineStateTracker
{
    private sealed class IndexRawBuffer : RawBuffer
    {
        public bool CreateBuffer(RawBufferParams parameters) => Create(parameters);
    }

    private byte[] _data = [];
    private IndexRawBuffer? _rawBuffer;
    private bool _shadowedPending;

    /// <summary>Number of indices.</summary>
    public uint IndexCount { get; private set; }

    /// <summary>Index size.</summary>
    public uint IndexSize => LargeIndices ? 4u : 2u;

    /// <summary>Whether is dynamic.</summary>
    public bool IsDynamic { get; private set; }

    /// <summary>Whether indices use 32-bit values.</summary>
    public bool LargeIndices { get; private set; }

    /// <summary>Whether the buffer has CPU shadow data.</summary>
    public bool Shadowed { get; private set; }

    /// <summary>Enable shadowing in CPU memory.</summary>
    public void SetShadowed(bool enable)
    {
        _shadowedPending = enable;
        Shadowed = enable;
    }

    /// <summary>Set size and index format and dynamic mode. Previous data will be lost.</summary>
    public bool SetSize(uint indexCount, bool largeIndices = false, bool dynamic = false)
    {
        IndexCount = indexCount;
        LargeIndices = largeIndices;
        IsDynamic = dynamic;
        MarkPipelineStateHashDirty();

        var indexSize = IndexSize;
        var size = checked((int)(indexCount * indexSize));
        if (_data.Length != size)
            _data = new byte[size];

        _rawBuffer?.Destroy();
        _rawBuffer = new IndexRawBuffer();
        return _rawBuffer.CreateBuffer(new RawBufferParams(
            BufferType.Index,
            checked((uint)size),
            indexSize,
            (_shadowedPending ? BufferFlag.Shadowed : BufferFlag.None)
                | (dynamic ? BufferFlag.Dynamic : BufferFlag.None)));
    }

    /// <summary>Return whether is dynamic.</summary>
    public bool IsDynamicBuffer() => IsDynamic;

    /// <summary>Return number of indices.</summary>
    public uint GetIndexCount() => IndexCount;

    /// <summary>Return index size in bytes.</summary>
    public uint GetIndexSize() => IndexSize;

    /// <summary>Return used vertex range from index range.</summary>
    public bool GetUsedVertexRange(uint start, uint count, out uint minVertex, out uint vertexCount)
    {
        if (!Shadowed)
        {
            Log.Logger.LogError("Used vertex range can only be queried from an index buffer with shadow data");
            minVertex = 0;
            vertexCount = 0;
            return false;
        }

        if (unchecked(start + count) > IndexCount)
        {
            Log.Logger.LogError("Illegal index range for querying used vertices");
            minVertex = 0;
            vertexCount = 0;
            return false;
        }

        minVertex = uint.MaxValue;
        var maxVertex = 0u;
        if (IndexSize == sizeof(uint))
        {
            for (var i = 0u; i < count; ++i)
            {
                var index = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(checked((int)((start + i) * IndexSize))));
                if (index < minVertex)
                    minVertex = index;
                if (index > maxVertex)
                    maxVertex = index;
            }
        }
        else
        {
            for (var i = 0u; i < count; ++i)
            {
                var index = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(checked((int)((start + i) * IndexSize))));
                if (index < minVertex)
                    minVertex = index;
                if (index > maxVertex)
                    maxVertex = index;
            }
        }

        vertexCount = unchecked(maxVertex - minVertex + 1);
        return true;
    }

    /// <summary>Return unpacked buffer data as plain array of indices.</summary>
    public IReadOnlyList<uint> GetUnpackedData(uint start = 0, uint count = uint.MaxValue)
    {
        if (start >= IndexCount || count == 0 || !Shadowed)
            return Array.Empty<uint>();

        // Clamp count to index buffer size.
        if (count == uint.MaxValue || unchecked(start + count) > IndexCount)
            count = IndexCount - start;

        var result = new uint[checked((int)count)];
        UnpackIndexData(_data, LargeIndices, start, count, result);
        return result;
    }

    /// <summary>Set data in the buffer from unpacked data. Data should contain at least count elements.</summary>
    public void SetUnpackedData(IEnumerable<uint> data, uint start = 0, uint count = uint.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(data);
        SetUnpackedData(data.ToArray().AsSpan(), start, count);
    }

    /// <summary>Set data in the buffer from unpacked data. Data should contain at least count elements.</summary>
    public void SetUnpackedData(ReadOnlySpan<uint> data, uint start = 0, uint count = uint.MaxValue)
    {
        if (start >= IndexCount || count == 0)
            return;

        // Clamp count to index buffer size.
        if (count == uint.MaxValue || unchecked(start + count) > IndexCount)
            count = IndexCount - start;

        PackIndexData(data, _data, LargeIndices, start, count);
        _rawBuffer?.UpdateRange(_data.AsSpan(checked((int)(start * IndexSize)), checked((int)(count * IndexSize))),
            start * IndexSize, count * IndexSize);
    }

    /// <summary>Unpack index data from index buffer into unsigned int array.</summary>
    public static void UnpackIndexData(ReadOnlySpan<byte> source, bool largeIndices, uint start, uint count, Span<uint> dest)
    {
        var stride = largeIndices ? 4u : 2u;
        var sourceBytes = source.Slice(checked((int)(start * stride)));

        if (largeIndices)
        {
            for (var i = 0u; i < count; ++i)
                dest[checked((int)i)] = BinaryPrimitives.ReadUInt32LittleEndian(sourceBytes.Slice(checked((int)(i * stride))));
        }
        else
        {
            for (var i = 0u; i < count; ++i)
                dest[checked((int)i)] = BinaryPrimitives.ReadUInt16LittleEndian(sourceBytes.Slice(checked((int)(i * stride))));
        }
    }

    /// <summary>Pack index data from unsigned int array into index buffer.</summary>
    public static void PackIndexData(ReadOnlySpan<uint> source, Span<byte> dest, bool largeIndices, uint start, uint count,
        uint offset = 0)
    {
        var stride = largeIndices ? 4u : 2u;
        var destBytes = dest.Slice(checked((int)(start * stride)));

        if (largeIndices)
        {
            for (var i = 0u; i < count; ++i)
                BinaryPrimitives.WriteUInt32LittleEndian(
                    destBytes.Slice(checked((int)(i * stride))), unchecked(source[checked((int)i)] + offset));
        }
        else
        {
            for (var i = 0u; i < count; ++i)
                BinaryPrimitives.WriteUInt16LittleEndian(
                    destBytes.Slice(checked((int)(i * stride))), unchecked((ushort)(source[checked((int)i)] + offset)));
        }
    }

    /// <summary>Return type of index buffer. Null is allowed.</summary>
    public static IndexBufferType GetIndexBufferType(IndexBuffer? indexBuffer)
    {
        if (indexBuffer is null)
            return IndexBufferType.None;

        return indexBuffer.GetIndexSize() == 4 ? IndexBufferType.UInt32 : IndexBufferType.UInt16;
    }

    /// <summary>Return the underlying raw buffer.</summary>
    public RawBuffer? GetRawBuffer() => _rawBuffer;

    protected override uint RecalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, IndexSize);
        return Math.Max(1u, hash);
    }

    /// <summary>Set the debug name.</summary>
    public void SetDebugName(string debugName)
    {
        _rawBuffer?.SetDebugName(debugName);
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}

/// <summary>Index Buffer of dynamic size. Resize policy is similar to standard vector.</summary>
public sealed class DynamicIndexBuffer
{
    private readonly IndexBuffer _indexBuffer = new();
    private byte[] _shadowData = [];
    private bool _indexBufferNeedResize;

    private bool _largeIndices;
    private uint _indexSize;
    private uint _numIndices;
    private uint _maxNumIndices;

    /// <summary>Initialize dynamic index buffer.</summary>
    public bool Initialize(uint indexCount, bool largeIndices)
    {
        _numIndices = 0;
        _maxNumIndices = indexCount;
        _largeIndices = largeIndices;

        if (!_indexBuffer.SetSize(indexCount, largeIndices, true))
        {
            Log.Logger.LogError("Failed to create DynamicIndexBuffer");
            return false;
        }

        _indexBuffer.SetShadowed(true);
        _indexSize = _indexBuffer.GetIndexSize();
        _shadowData = new byte[checked((int)(_indexSize * _maxNumIndices))];
        return true;
    }

    /// <summary>Set the debug name.</summary>
    public void SetDebugName(string debugName)
    {
        _indexBuffer.SetDebugName(debugName);
    }

    /// <summary>Discard existing content of the buffer.</summary>
    public void Discard()
    {
        _numIndices = 0;
    }

    /// <summary>Commit all added data to GPU.</summary>
    public void Commit()
    {
        if (_numIndices == 0)
            return;

        if (_indexBufferNeedResize)
        {
            _indexBufferNeedResize = false;
            if (!_indexBuffer.SetSize(_maxNumIndices, _largeIndices, true))
            {
                Log.Logger.LogError(
                    "Failed to grow DynamicIndexBuffer to {MaxNumIndices} vertices with stride {IndexSize}",
                    _maxNumIndices,
                    _indexSize);
                return;
            }

            _indexBuffer.SetShadowed(true);
        }

        var unpackedData = new uint[checked((int)_numIndices)];
        IndexBuffer.UnpackIndexData(_shadowData, _largeIndices, 0, _numIndices, unpackedData);
        _indexBuffer.SetUnpackedData(unpackedData, 0, _numIndices);
    }

    /// <summary>Allocate indices. Returns index of first index and writeable buffer of sufficient size.</summary>
    public (uint StartIndex, Memory<byte> Data) AddIndices(uint count)
    {
        var startIndex = _numIndices;
        var newMaxNumIndices = unchecked(startIndex + count);
        if (newMaxNumIndices > _maxNumIndices)
            GrowBuffer(newMaxNumIndices);

        _numIndices += count;
        var start = checked((int)(startIndex * _indexSize));
        var size = checked((int)(count * _indexSize));
        return (startIndex, _shadowData.AsMemory(start, size));
    }

    /// <summary>Store indices. Returns index of first vertex.</summary>
    public uint AddIndices(uint count, ReadOnlySpan<byte> data)
    {
        var indexAndData = AddIndices(count);
        data[..checked((int)(count * _indexSize))].CopyTo(indexAndData.Data.Span);
        return indexAndData.StartIndex;
    }

    /// <summary>Return the index buffer.</summary>
    public IndexBuffer GetIndexBuffer() => _indexBuffer;

    private void GrowBuffer(uint newMaxNumIndices)
    {
        _maxNumIndices = Math.Max(newMaxNumIndices, _maxNumIndices > 0 ? 2 * _maxNumIndices : 128);
        Array.Resize(ref _shadowData, checked((int)(_maxNumIndices * _indexSize)));
        _indexBufferNeedResize = true;
    }
}
