using System.Text;
using System.Runtime.InteropServices;

namespace RedBolt.IO;
using Math = global::System.Math;


/// <summary>Memory area that can be read and written to as a stream.</summary>
public unsafe class MemoryBuffer : AbstractFile, ICloneable
{
    /// <summary>Construct with a pointer and size.</summary>
    public MemoryBuffer(nint data, uint size, bool readOnly = false)
        : base(size)
    {
        _buffer = [];
        _bufferPtr = (byte*)data;
        _bufferOffset = 0;
        _usesUnmanagedBuffer = true;
        _pinnedBuffer = default;
        IsReadOnly = readOnly;

        if (_bufferPtr == null)
            Size = 0;
    }

    /// <summary>Construct from a vector, which must not go out of scope before MemoryBuffer.</summary>
    public MemoryBuffer(byte[] data)
        : base(GetBufferSize(data))
    {
        ArgumentNullException.ThrowIfNull(data);
        _buffer = data;
        _pinnedBuffer = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        _bufferPtr = (byte*)_pinnedBuffer.AddrOfPinnedObject();
        _bufferOffset = 0;
        _usesUnmanagedBuffer = false;
        IsReadOnly = false;
    }

    /// <summary>Construct from a read-only vector, which must not go out of scope before MemoryBuffer.</summary>
    public MemoryBuffer(ReadOnlyMemory<byte> data)
        : base((uint)data.Length)
    {
        if (!MemoryMarshal.TryGetArray(data, out var segment))
        {
            _buffer = data.ToArray();
            _bufferOffset = 0;
        }
        else
        {
            _buffer = segment.Array ?? [];
            _bufferOffset = segment.Offset;
        }

        _pinnedBuffer = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        _bufferPtr = (byte*)_pinnedBuffer.AddrOfPinnedObject() + _bufferOffset;
        _usesUnmanagedBuffer = false;
        IsReadOnly = true;
    }

    /// <summary>Construct as read-only from string.</summary>
    public MemoryBuffer(string text)
        : this(Encoding.UTF8.GetBytes(text ?? string.Empty).AsMemory())
    {
    }

    /// <summary>Construct from a vector buffer, which must not go out of scope before MemoryBuffer.</summary>
    public MemoryBuffer(VectorBuffer data)
        : base(GetBufferSize(data))
    {
        ArgumentNullException.ThrowIfNull(data);
        _buffer = data.GetBuffer();
        _pinnedBuffer = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        _bufferPtr = (byte*)_pinnedBuffer.AddrOfPinnedObject();
        _bufferOffset = 0;
        _usesUnmanagedBuffer = false;
        IsReadOnly = false;
    }

    private MemoryBuffer(MemoryBuffer rhs)
        : base(rhs.Size)
    {
        _buffer = rhs._buffer;
        _bufferOffset = rhs._bufferOffset;
        _usesUnmanagedBuffer = rhs._usesUnmanagedBuffer;
        IsReadOnly = rhs.IsReadOnly;

        if (_usesUnmanagedBuffer)
        {
            _bufferPtr = rhs._bufferPtr;
            _pinnedBuffer = default;
        }
        else
        {
            _pinnedBuffer = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferPtr = (byte*)_pinnedBuffer.AddrOfPinnedObject() + _bufferOffset;
        }

        _position = rhs._position;
        _name = rhs._name;
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        if (_pinnedBuffer.IsAllocated)
        {
            _pinnedBuffer.Free();
            _pinnedBuffer = default;
        }

        base.Dispose();
    }

    /// <summary>Read bytes from the memory area. Return number of bytes actually read.</summary>
    public override uint Read(Span<byte> destination)
    {
        var available = (int)Math.Min((uint)destination.Length, Size - Position);
        if (_usesUnmanagedBuffer)
        {
            new ReadOnlySpan<byte>(_bufferPtr + checked((int)Position), available).CopyTo(destination);
        }
        else
        {
            _buffer.AsSpan(_bufferOffset + checked((int)Position), available).CopyTo(destination);
        }

        Position += (uint)available;
        return (uint)available;
    }

    /// <summary>Set position from the beginning of the memory area. Return actual new position.</summary>
    public override uint Seek(uint position)
    {
        Position = Math.Min(position, Size);
        return Position;
    }

    /// <summary>Write bytes to the memory area. Return number of bytes actually written.</summary>
    public override uint Write(ReadOnlySpan<byte> data)
    {
        var available = (int)Math.Min((uint)data.Length, Size - Position);
        if (_usesUnmanagedBuffer)
        {
            data[..available].CopyTo(new Span<byte>(_bufferPtr + checked((int)Position), available));
        }
        else
        {
            data[..available].CopyTo(_buffer.AsSpan(_bufferOffset + checked((int)Position), available));
        }

        Position += (uint)available;
        return (uint)available;
    }

    /// <summary>Return direct pointer to the memory area.</summary>
    public nint GetData() => (nint)_bufferPtr;

    /// <summary>Return whether the buffer is read-only.</summary>
    public bool IsReadOnly { get; }

    public object Clone() => new MemoryBuffer(this);

    private readonly byte[] _buffer;
    private readonly byte* _bufferPtr;
    private readonly int _bufferOffset;
    private readonly bool _usesUnmanagedBuffer;
    private GCHandle _pinnedBuffer;

    private static uint GetBufferSize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return (uint)data.Length;
    }

    private static uint GetBufferSize(VectorBuffer data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Size;
    }
}
