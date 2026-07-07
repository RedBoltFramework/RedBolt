using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

public readonly record struct ConstantBufferCollectionRef(uint Index, uint Offset, uint Size);

public sealed class ConstantBufferCollection
{
    private sealed class BufferEntry
    {
        public byte[] Data = [];
        public uint Used;
    }

    private readonly List<BufferEntry> buffers = [];
    private uint currentBufferIndex;
    private uint alignment = 1;
    private uint bufferSize = 16384;

    public void ClearAndInitialize(uint alignment)
    {
        this.alignment = Math.Max(alignment, 1u);
        currentBufferIndex = 0;
        foreach (var buffer in buffers)
            buffer.Used = 0;

        if (buffers.Count == 0)
            AllocateBuffer();
    }

    public (ConstantBufferCollectionRef Ref, Memory<byte> Data) AddBlock(uint size)
    {
        if (size > bufferSize)
            throw new ArgumentOutOfRangeException(nameof(size), "Requested block exceeds buffer capacity.");

        var alignedSize = Align(size, alignment);
        if (bufferSize - buffers[(int)currentBufferIndex].Used < alignedSize)
        {
            ++currentBufferIndex;
            if (buffers.Count <= currentBufferIndex)
                AllocateBuffer();
        }

        var currentBuffer = buffers[(int)currentBufferIndex];
        var offset = currentBuffer.Used;
        currentBuffer.Used += alignedSize;

        var memory = currentBuffer.Data.AsMemory((int)offset, (int)size);
        return (new ConstantBufferCollectionRef(currentBufferIndex, offset, size), memory);
    }

    public uint GetNumBuffers() => currentBufferIndex + 1;

    public uint GetBufferSize(uint index) => buffers[(int)index].Used;

    public uint GetGPUBufferSize(uint index) => Math.Max(512u, MathDefs.NextPowerOfTwo(GetBufferSize(index)));

    public ReadOnlyMemory<byte> GetBufferData(uint index) => buffers[(int)index].Data.AsMemory(0, (int)GetBufferSize(index));

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

    public static bool StoreParameter(Span<byte> destination, Matrix3 value)
    {
        var data = new Matrix3x4(value);
        return StoreParameter(destination, data);
    }

    public static bool StoreParameter(Span<byte> destination, Matrix3x4 value)
    {
        var matrix = value.Matrix4;
        return StoreParameter(destination, matrix);
    }

    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Vector4> values) => StoreSpan(destination, values);

    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Matrix3x4> values) => StoreSpan(destination, values);

    public static bool StoreParameter(Span<byte> destination, ReadOnlySpan<Matrix4> values) => StoreSpan(destination, values);

    private void AllocateBuffer()
    {
        buffers.Add(new BufferEntry
        {
            Data = new byte[bufferSize]
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
