using System.Diagnostics;
using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Math;

namespace RedBolt.RenderAPI;

/// <summary>Reference to the region in constant buffer within collection. Plain old data.</summary>
public struct ConstantBufferCollectionRef
{
    /// <summary>Index of buffer in collection.</summary>
    public int Index;
    /// <summary>Offset in the buffer.</summary>
    public int Offset;
    /// <summary>Size of the chunk.</summary>
    public int Size;
}

/// <summary>Buffer of shader parameters ready to be uploaded.</summary>
public class ConstantBufferCollection
{
    /// <summary>Clear and/or initialize for work.</summary>
    public void ClearAndInitialize(int alignment)
    {
        _alignment = alignment;
        _currentBufferIndex = 0;
        for (var i = 0; i < _buffers.Count; ++i)
        {
            var buffer = _buffers[i];
            buffer.Used = 0;
            _buffers[i] = buffer;
        }

        if (_buffers.Count == 0)
            AllocateBuffer();
    }

    /// <summary>Allocate new block.</summary>
    public (ConstantBufferCollectionRef Ref, Memory<byte> Data) AddBlock(int size)
    {
        Debug.Assert(size <= _bufferSize);
        var alignedSize = (size + _alignment - 1) / _alignment * _alignment;

        if (_bufferSize - _buffers[_currentBufferIndex].Used < alignedSize)
        {
            ++_currentBufferIndex;
            if (_buffers.Count <= _currentBufferIndex)
                AllocateBuffer();
        }

        var currentBuffer = _buffers[_currentBufferIndex];
        var offset = currentBuffer.Used;
        currentBuffer.Used += alignedSize;
        _buffers[_currentBufferIndex] = currentBuffer;

        var data = currentBuffer.Data.AsMemory(offset, size);
        return (new ConstantBufferCollectionRef { Index = _currentBufferIndex, Offset = offset, Size = size }, data);
    }

    /// <summary>Return number of buffers.</summary>
    public int GetNumBuffers() => _currentBufferIndex + 1;

    /// <summary>Return used size of the CPU buffer.</summary>
    public int GetBufferSize(int index) => _buffers[index].Used;

    /// <summary>Return best size of GPU buffer. Round up to next power of two.</summary>
    public int GetGPUBufferSize(int index) => SystemMath.Max(512, checked((int)MathDefs.NextPowerOfTwo((uint)GetBufferSize(index))));

    /// <summary>Return buffer data.</summary>
    public ReadOnlyMemory<byte> GetBufferData(int index) => _buffers[index].Data;

    /// <summary>Copy variant parameter into storage.</summary>
    public static bool StoreParameter(Span<byte> destination, in Variant value)
    {
        switch (value.Type)
        {
            case VariantType.Bool:
                return StoreParameter(destination, value.GetBool() ? 1 : 0);

            case VariantType.Int:
                return StoreParameter(destination, value.GetInt());

            case VariantType.Float:
            case VariantType.Double:
                return StoreParameter(destination, value.GetFloat());

            case VariantType.Vector2:
                return StoreParameter(destination, value.GetVector2());

            case VariantType.Vector3:
                return StoreParameter(destination, value.GetVector3());

            case VariantType.Vector4:
                return StoreParameter(destination, value.GetVector4());

            case VariantType.Color:
                return StoreParameter(destination, value.GetColor());

            case VariantType.Matrix3:
                return StoreParameter(destination, value.GetMatrix3());

            case VariantType.Matrix3x4:
                return StoreParameter(destination, value.GetMatrix3x4());

            case VariantType.Matrix4:
                return StoreParameter(destination, value.GetMatrix4());

            default:
                // Unsupported parameter type, do nothing
                return false;
        }
    }

    /// <summary>Copy new simple parameter into storage. Trim if too much data provided.</summary>
    public static bool StoreParameter<T>(Span<byte> destination, in T value)
        where T : unmanaged
    {
        if (destination.Length > Unsafe.SizeOf<T>())
            return false;

        var source = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in value), 1));
        Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination),
            ref MemoryMarshal.GetReference(source), checked((uint)destination.Length));
        return true;
    }

    /// <summary>Copy new Matrix3 parameter into storage.</summary>
    public static bool StoreParameter(Span<byte> destination, in Matrix3 value)
    {
        if (destination.Length < Unsafe.SizeOf<Matrix3>())
            return false;

        var data = new Matrix3x4(value);
        var source = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref data, 1));
        Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination),
            ref MemoryMarshal.GetReference(source), checked((uint)source.Length));
        return true;
    }

    /// <summary>Copy new Matrix3x4 parameter into storage.</summary>
    public static bool StoreParameter(Span<byte> destination, in Matrix3x4 value)
    {
        if (destination.Length != 12 * sizeof(float) && destination.Length != 16 * sizeof(float))
            return false;

        var matrix = value.Matrix4;
        var source = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref matrix, 1));
        Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination),
            ref MemoryMarshal.GetReference(source), checked((uint)destination.Length));
        return true;
    }

    /// <summary>Add new Vector4f array parameter.</summary>
    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Vector4f> values)
    {
        if (destination.Length < 4 * sizeof(float) * values.Length)
            return false;

        var source = MemoryMarshal.AsBytes(values);
        Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination),
            ref MemoryMarshal.GetReference(source), checked((uint)source.Length));
        return true;
    }

    /// <summary>Add new Matrix3x4 array parameter.</summary>
    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Matrix3x4> values)
    {
        if (destination.Length < 12 * sizeof(float) * values.Length)
            return false;

        var source = MemoryMarshal.AsBytes(values);
        Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination),
            ref MemoryMarshal.GetReference(source), checked((uint)source.Length));
        return true;
    }

    /// <summary>Add new Matrix4 array parameter.</summary>
    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Matrix4> values)
    {
        if (destination.Length < 16 * sizeof(float) * values.Length)
            return false;

        var source = MemoryMarshal.AsBytes(values);
        Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination),
            ref MemoryMarshal.GetReference(source), checked((uint)source.Length));
        return true;
    }

    /// <summary>Allocate one more buffer.</summary>
    private void AllocateBuffer()
    {
        _buffers.Add((new byte[_bufferSize], 0));
    }

    /// <summary>Size of buffers.</summary>
    private int _bufferSize = 16384;
    /// <summary>Alignment of each block.</summary>
    private int _alignment = 1;
    /// <summary>Buffers.</summary>
    private readonly List<(byte[] Data, int Used)> _buffers = [];
    /// <summary>Current buffer index.</summary>
    private int _currentBufferIndex;
}
