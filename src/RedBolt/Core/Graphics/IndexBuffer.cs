namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.RenderAPI;

public enum IndexBufferType
{
    None,
    UInt16,
    UInt32
}

/// <summary>Hardware index buffer.</summary>
/// <remarks>
/// </remarks>
public sealed class IndexBuffer : PipelineStateTracker
{
    private sealed class IndexRawBuffer : RawBuffer
    {
        public IndexRawBuffer(DeviceObjectFlag flags)
            : base(flags)
        {
        }

        public bool CreateBuffer(RawBufferParams parameters) => Create(parameters);
    }

    private readonly IndexRawBuffer _rawBuffer;
    private uint _indexCount;
    private uint _indexSize;
    private bool _shadowedPending;

    public IndexBuffer(DeviceObjectFlag flags = DeviceObjectFlag.None)
    {
        _rawBuffer = new IndexRawBuffer(flags);
    }

    /// <summary>Enable shadowing in CPU memory. Shadowing is forced on if the graphics subsystem does not exist.</summary>
    public void SetShadowed(bool enable)
    {
        _shadowedPending = enable;
    }

    /// <summary>Set size and index format and dynamic mode. Previous data will be lost.</summary>
    public bool SetSize(uint indexCount, bool largeIndices = false, bool dynamic = false)
    {
        _indexCount = indexCount;
        _indexSize = largeIndices ? (uint)sizeof(uint) : sizeof(ushort);
        MarkPipelineStateHashDirty();

        return _rawBuffer.CreateBuffer(new RawBufferParams(
            BufferType.Index,
            checked((int)(indexCount * _indexSize)),
            checked((int)_indexSize),
            (_shadowedPending ? BufferFlag.Shadowed : BufferFlag.None)
                | (dynamic ? BufferFlag.Dynamic : BufferFlag.None)));
    }

    /// <summary>Return whether is dynamic.</summary>
    public bool IsDynamic() => (_rawBuffer.GetFlags() & BufferFlag.Dynamic) != 0;

    /// <summary>Return number of indices.</summary>
    public uint GetIndexCount() => _indexCount;

    /// <summary>Return index size in bytes.</summary>
    public uint GetIndexSize() => _indexSize;

    public bool IsShadowed() => _rawBuffer.IsShadowed();

    public byte[] GetShadowData() => _rawBuffer.GetShadowData();

    public void Update(ReadOnlySpan<byte> data, uint size = 0) => _rawBuffer.Update(data, checked((int)size));

    /// <summary>Return used vertex range from index range.</summary>
    public bool GetUsedVertexRange(uint start, uint count, out uint minVertex, out uint vertexCount)
    {
        var shadowData = _rawBuffer.GetShadowData();
        if (shadowData.Length == 0)
        {
            Log.GetLogger().LogError("Used vertex range can only be queried from an index buffer with shadow data");
            minVertex = 0;
            vertexCount = 0;
            return false;
        }

        if (unchecked(start + count) > _indexCount)
        {
            Log.GetLogger().LogError("Illegal index range for querying used vertices");
            minVertex = 0;
            vertexCount = 0;
            return false;
        }

        minVertex = uint.MaxValue;
        var maxVertex = 0u;
        if (_indexSize == sizeof(uint))
        {
            for (var i = 0u; i < count; ++i)
            {
                var index = BinaryPrimitives.ReadUInt32LittleEndian(shadowData.AsSpan(checked((int)((start + i) * _indexSize))));
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
                var index = BinaryPrimitives.ReadUInt16LittleEndian(shadowData.AsSpan(checked((int)((start + i) * _indexSize))));
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
        if (start >= _indexCount || count == 0 || !_rawBuffer.IsShadowed())
            return Array.Empty<uint>();

        // Clamp count to index buffer size.
        if (count == uint.MaxValue || unchecked(start + count) > _indexCount)
            count = _indexCount - start;

        var result = new uint[checked((int)count)];
        UnpackIndexData(_rawBuffer.GetShadowData(), _indexSize == 4, start, count, result);
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
        if (start >= _indexCount || count == 0)
            return;

        // Clamp count to index buffer size.
        if (count == uint.MaxValue || unchecked(start + count) > _indexCount)
            count = _indexCount - start;

        var buffer = new byte[checked((int)(count * _indexSize))];
        PackIndexData(data, buffer, _indexSize == 4, 0, count);
        _rawBuffer.UpdateRange(buffer, checked((int)(start * _indexSize)), checked((int)(count * _indexSize)));
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
    public RawBuffer GetRawBuffer() => _rawBuffer;

    /// <summary>Map buffer contents onto CPU-writeable memory. Old buffer data is discarded.</summary>
    public Span<byte> Map() => _rawBuffer.Map();

    /// <summary>Unmap the buffer. Should always be called before buffer is used or frame ends.</summary>
    public void Unmap() => _rawBuffer.Unmap();

    /// <summary>Update buffer data range without allocating.</summary>
    public void UpdateRange(ReadOnlySpan<byte> data, uint offset, uint size)
    {
        _rawBuffer.UpdateRange(data, checked((int)offset), checked((int)size));
    }

    protected override uint RecalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, _indexSize);
        return SystemMath.Max(1u, hash);
    }

    /// <summary>Set the debug name.</summary>
    public void SetDebugName(string debugName)
    {
        _rawBuffer.SetDebugName(debugName);
    }

    public string GetDebugName() => _rawBuffer.GetDebugName();

    public override void Dispose()
    {
        _rawBuffer.Destroy();
        _rawBuffer.Dispose();
        base.Dispose();
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}

/// <summary>Index Buffer of dynamic size. Resize policy is similar to standard vector.</summary>
public sealed class DynamicIndexBuffer : RedObject
{
    private readonly IndexBuffer _indexBuffer = new();
    private byte[] _shadowData = [];
    private bool _indexBufferNeedResize;

    private uint _indexSize;
    private uint _numIndices;
    private uint _maxNumIndices;

    public DynamicIndexBuffer()
    {
    }

    /// <summary>Initialize dynamic index buffer.</summary>
    public bool Initialize(uint indexCount, bool largeIndices)
    {
        _numIndices = 0;
        _maxNumIndices = indexCount;
        if (!_indexBuffer.SetSize(indexCount, largeIndices, true))
        {
            Log.GetLogger().LogError("Failed to create DynamicIndexBuffer");
            return false;
        }

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
            var largeIndices = _indexBuffer.GetIndexSize() == 4;
            if (!_indexBuffer.SetSize(_maxNumIndices, largeIndices, true))
            {
                Log.GetLogger().LogError(
                    "Failed to grow DynamicIndexBuffer to {MaxNumIndices} vertices with stride {IndexSize}",
                    _maxNumIndices,
                    _indexSize);
                return;
            }

        }

        _indexBuffer.UpdateRange(_shadowData.AsSpan(0, checked((int)(_numIndices * _indexSize))), 0,
            _numIndices * _indexSize);
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

    public override void Dispose()
    {
        _shadowData = [];
        _indexBuffer.Dispose();
        base.Dispose();
    }

    private void GrowBuffer(uint newMaxNumIndices)
    {
        _maxNumIndices = SystemMath.Max(newMaxNumIndices, _maxNumIndices > 0 ? 2 * _maxNumIndices : 128);
        Array.Resize(ref _shadowData, checked((int)(_maxNumIndices * _indexSize)));
        _indexBufferNeedResize = true;
    }
}
