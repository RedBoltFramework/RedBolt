using System.Text;

namespace RedBolt;

public sealed class MemoryBuffer : Deserializer
{
    private readonly byte[] _buffer;

    public MemoryBuffer(byte[] data, bool readOnly = true)
        : base((uint)(data?.Length ?? 0))
    {
        ArgumentNullException.ThrowIfNull(data);
        _buffer = data;
        IsReadOnly = readOnly;
    }

    public MemoryBuffer(IEnumerable<byte> data, bool readOnly = true)
        : this(data?.ToArray() ?? throw new ArgumentNullException(nameof(data)), readOnly)
    {
    }

    public MemoryBuffer(string text)
        : this(Encoding.UTF8.GetBytes(text ?? string.Empty))
    {
    }

    public MemoryBuffer(uint size)
        : this(new byte[size], readOnly: false)
    {
    }

    public bool IsReadOnly { get; }

    public byte[] GetData() => _buffer;

    public override uint Read(Span<byte> destination)
    {
        var available = (int)Math.Min((uint)destination.Length, Size - Position);
        _buffer.AsSpan((int)Position, available).CopyTo(destination);
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
        if (IsReadOnly)
            return 0;

        var available = (int)Math.Min((uint)data.Length, Size - Position);
        data[..available].CopyTo(_buffer.AsSpan((int)Position, available));
        Position += (uint)available;
        return (uint)available;
    }
}
