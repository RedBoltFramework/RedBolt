using System.Text;
using System.Runtime.InteropServices;

namespace RedBolt;

public sealed unsafe class MemoryBuffer : AbstractFile, IDisposable
{
    private readonly byte[] _buffer;
    private readonly byte* _bufferPtr;
    private readonly int _bufferOffset;
    private readonly bool _usesUnmanagedBuffer;
    private readonly GCHandle _pinnedBuffer;

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

    public MemoryBuffer(string text)
        : this(Encoding.UTF8.GetBytes(text ?? string.Empty).AsMemory())
    {
    }

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

    public void Dispose()
    {
        if (_pinnedBuffer.IsAllocated)
            _pinnedBuffer.Free();

        GC.SuppressFinalize(this);
    }

    public bool IsReadOnly { get; }

    public nint GetData() => (nint)_bufferPtr;

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

    public override uint Seek(uint position)
    {
        Position = Math.Min(position, Size);
        return Position;
    }

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
