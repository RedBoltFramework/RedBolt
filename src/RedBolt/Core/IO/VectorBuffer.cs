namespace RedBolt.IO;

using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

/// <summary>Dynamically sized buffer that can be read and written to as a stream.</summary>
public class VectorBuffer : AbstractFile, ICloneable
{
    private const string VectorBufferName = "VectorBuffer";

    /// <summary>Construct an empty buffer.</summary>
    public VectorBuffer()
    {
    }

    /// <summary>Construct from another buffer.</summary>
    public VectorBuffer(byte[] data)
    {
        SetData(data);
    }

    /// <summary>Construct from a memory area.</summary>
    public VectorBuffer(nint data, uint size)
    {
        SetData(data, size);
    }

    /// <summary>Construct from a stream.</summary>
    public VectorBuffer(Deserializer source, uint size)
    {
        SetData(source, size);
    }

    public object Clone()
    {
        var clone = new VectorBuffer(_buffer);
        clone.SetName(base.GetName());
        clone.Position = Position;
        return clone;
    }

    public ReadOnlySpan<byte> Buffer => _buffer;

    public override string Name => base.Name.Length != 0 ? base.Name : VectorBufferName;

    /// <summary>Returns name of this object.</summary>
    public override string GetName()
    {
        var baseName = base.GetName();
        if (baseName.Length != 0)
            return baseName;
        return VectorBufferName;
    }

    /// <summary>Read bytes from the buffer. Return number of bytes actually read.</summary>
    public override uint Read(Span<byte> destination)
    {
        var available = (int)SystemMath.Min((uint)destination.Length, Size - Position);
        _buffer.AsSpan(checked((int)Position), available).CopyTo(destination);
        Position += (uint)available;
        return (uint)available;
    }

    /// <summary>Set position from the beginning of the buffer. Return actual new position.</summary>
    public override uint Seek(uint position)
    {
        Position = SystemMath.Min(position, Size);
        return Position;
    }

    /// <summary>Write bytes to the buffer. Return number of bytes actually written.</summary>
    public override uint Write(ReadOnlySpan<byte> data)
    {
        var end = checked(Position + (uint)data.Length);
        if (end > Size)
            Resize(end);

        data.CopyTo(_buffer.AsSpan(checked((int)Position), data.Length));
        Position = end;
        return (uint)data.Length;
    }

    /// <summary>Set data from another buffer.</summary>
    public void SetData(byte[] data)
    {
        Resize((uint)data.Length);
        data.AsSpan().CopyTo(_buffer);
        Position = 0;
    }

    public void SetData(ReadOnlySpan<byte> data)
    {
        Resize((uint)data.Length);
        data.CopyTo(_buffer);
        Position = 0;
    }

    /// <summary>Set data from a memory area.</summary>
    public void SetData(nint data, uint size)
    {
        if (data == 0)
            size = 0;

        Resize(size);
        if (size != 0)
            unsafe
            {
                new ReadOnlySpan<byte>((void*)data, checked((int)size)).CopyTo(_buffer);
            }
        Position = 0;
    }

    /// <summary>Set data from a stream.</summary>
    public void SetData(Deserializer source, uint size)
    {
        Resize(size);
        var actualSize = source.Read(_buffer);
        if (actualSize != size)
            Resize(actualSize);
        Position = 0;
    }

    /// <summary>Reset to zero size.</summary>
    public void Clear()
    {
        ReleaseBuffer();
        _buffer = [];
        Position = 0;
        Size = 0;
    }

    /// <summary>Set size.</summary>
    public void Resize(uint size)
    {
        if (size == Size)
            return;

        ReleaseBuffer();
        var oldSize = Size;
        Array.Resize(ref _buffer, checked((int)size));
        Size = size;
        Position = SystemMath.Min(Position, Size);
        if (size > oldSize)
            _buffer.AsSpan(checked((int)oldSize), checked((int)(size - oldSize))).Clear();
    }

    /// <summary>Return data.</summary>
    public nint GetData()
    {
        return PinBuffer();
    }

    /// <summary>Return non-const data.</summary>
    public nint GetModifiableData()
    {
        return PinBuffer();
    }

    /// <summary>Return the buffer.</summary>
    public byte[] GetBuffer()
    {
        return _buffer;
    }

    public override void Dispose()
    {
        ReleaseBuffer();
        _buffer = [];
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    ~VectorBuffer()
    {
        ReleaseBuffer();
    }

    private nint PinBuffer()
    {
        if (Size == 0)
            return nint.Zero;

        if (!_pinnedBuffer.IsAllocated)
            _pinnedBuffer = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        return _pinnedBuffer.AddrOfPinnedObject();
    }

    private void ReleaseBuffer()
    {
        if (_pinnedBuffer.IsAllocated)
            _pinnedBuffer.Free();
    }

    /// <summary>Dynamic data buffer.</summary>
    private byte[] _buffer = [];
    private GCHandle _pinnedBuffer;
}
