using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>Reference to the region in constant buffer within collection. Plain old data.</summary>
/// <param name="Index">Index of buffer in collection.</param>
/// <param name="Offset">Offset in the buffer.</param>
/// <param name="Size">Size of the chunk.</param>
public readonly record struct ConstantBufferCollectionRef(uint Index, uint Offset, uint Size);

/// <summary>Buffer of shader parameters ready to be uploaded.</summary>
public sealed class ConstantBufferCollection
{
    private sealed class BufferEntry
    {
        public byte[] Data = [];
        public uint Used;
    }

    /// <summary>Buffers.</summary>
    private readonly List<BufferEntry> _buffers = [];
    /// <summary>Current buffer index.</summary>
    private uint _currentBufferIndex;
    /// <summary>Alignment of each block.</summary>
    private uint _alignment = 1;
    /// <summary>Size of buffers.</summary>
    private uint _bufferSize = 16384;

    /// <summary>Clear and/or initialize for work.</summary>
    public void ClearAndInitialize(uint alignment)
    {
        this._alignment = Math.Max(alignment, 1u);
        _currentBufferIndex = 0;
        foreach (var buffer in _buffers)
            buffer.Used = 0;

        if (_buffers.Count == 0)
            AllocateBuffer();
    }

    /// <summary>Allocate new block.</summary>
    public (ConstantBufferCollectionRef Ref, Memory<byte> Data) AddBlock(uint size)
    {
        if (size > _bufferSize)
            throw new ArgumentOutOfRangeException(nameof(size), "Requested block exceeds buffer capacity.");

        var alignedSize = Align(size, _alignment);
        if (_bufferSize - _buffers[(int)_currentBufferIndex].Used < alignedSize)
        {
            ++_currentBufferIndex;
            if (_buffers.Count <= _currentBufferIndex)
                AllocateBuffer();
        }

        var currentBuffer = _buffers[(int)_currentBufferIndex];
        var offset = currentBuffer.Used;
        currentBuffer.Used += alignedSize;

        var memory = currentBuffer.Data.AsMemory((int)offset, (int)size);
        return (new ConstantBufferCollectionRef(_currentBufferIndex, offset, size), memory);
    }

    /// <summary>Return number of buffers.</summary>
    public uint GetNumBuffers() => _currentBufferIndex + 1;

    /// <summary>Return used size of the CPU buffer.</summary>
    public uint GetBufferSize(uint index) => _buffers[(int)index].Used;

    /// <summary>Return best size of GPU buffer. Round up to next power of two.</summary>
    public uint GetGPUBufferSize(uint index) => Math.Max(512u, MathDefs.NextPowerOfTwo(GetBufferSize(index)));

    /// <summary>Return buffer data.</summary>
    public ReadOnlyMemory<byte> GetBufferData(uint index) => _buffers[(int)index].Data.AsMemory(0, (int)GetBufferSize(index));

    /// <summary>Copy variant parameter into storage.</summary>
    public static bool StoreParameter(Span<byte> destination, Variant value)
    {
        return value.Type switch
        {
            VariantType.Bool => StoreParameter(destination, value.GetBool() ? 1 : 0),
            VariantType.Int => StoreParameter(destination, value.GetInt()),
            VariantType.Float => StoreParameter(destination, value.GetFloat()),
            VariantType.Vector2 => StoreParameter(destination, value.GetVector2()),
            VariantType.Vector3 => StoreParameter(destination, value.GetVector3()),
            VariantType.Vector4 => StoreParameter(destination, value.GetVector4()),
            VariantType.Color => StoreParameter(destination, value.GetColor()),
            _ => false
        };
    }

    /// <summary>Copy new simple parameter into storage. Trim if too much data provided.</summary>
    public static bool StoreParameter<T>(Span<byte> destination, T value)
        where T : unmanaged
    {
        var valueSize = (uint)Unsafe.SizeOf<T>();
        if (destination.Length > valueSize)
            return false;

        Span<byte> source = stackalloc byte[(int)valueSize];
        MemoryMarshal.Write(source, in value);
        source[..destination.Length].CopyTo(destination);
        return true;
    }

    /// <summary>Copy new Matrix3 parameter into storage.</summary>
    public static bool StoreParameter(Span<byte> destination, Matrix3 value)
    {
        var data = new Matrix3x4(value);
        return StoreParameter(destination, data);
    }

    /// <summary>Copy new Matrix3x4 parameter into storage.</summary>
    public static bool StoreParameter(Span<byte> destination, Matrix3x4 value)
    {
        var matrix = value.Matrix4;
        return StoreParameter(destination, matrix);
    }

    /// <summary>Add new Vector4 array parameter.</summary>
    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Vector4> values) => StoreSpan(destination, values);

    /// <summary>Add new Matrix3x4 array parameter.</summary>
    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Matrix3x4> values) => StoreSpan(destination, values);

    /// <summary>Add new Matrix4 array parameter.</summary>
    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Matrix4> values) => StoreSpan(destination, values);

    /// <summary>Allocate one more buffer.</summary>
    private void AllocateBuffer()
    {
        _buffers.Add(new BufferEntry
        {
            Data = new byte[_bufferSize]
        });
    }

    private static uint Align(uint size, uint alignment) => ((size + alignment - 1) / alignment) * alignment;

    private static bool StoreSpan<T>(Span<byte> destination, ReadOnlySpan<T> values)
        where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(values);
        if (destination.Length < bytes.Length)
            return false;

        bytes.CopyTo(destination);
        return true;
    }
}
