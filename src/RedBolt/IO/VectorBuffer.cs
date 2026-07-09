namespace RedBolt;

public sealed class VectorBuffer : Deserializer
{
    private byte[] _buffer;

    public VectorBuffer()
        : base(0)
    {
        _buffer = [];
    }

    public VectorBuffer(IEnumerable<byte> data)
        : base((uint)(data?.Count() ?? 0))
    {
        ArgumentNullException.ThrowIfNull(data);
        _buffer = data.ToArray();
        Size = (uint)_buffer.Length;
    }

    public IReadOnlyList<byte> Buffer => _buffer;

    public byte[] GetBuffer() => _buffer;

    public byte[] GetData() => _buffer;

    public byte[] GetModifiableData() => _buffer;

    public void Clear()
    {
        _buffer = [];
        Position = 0;
        Size = 0;
    }

    public override uint Read(Span<byte> destination)
    {
        var available = (int)Math.Min((uint)destination.Length, Size - Position);
        _buffer.AsSpan((int)Position, available).CopyTo(destination);
        Position += (uint)available;
        return (uint)available;
    }

    public void Resize(uint size)
    {
        Array.Resize(ref _buffer, checked((int)size));
        Size = size;
        if (Position > Size)
            Position = Size;
    }

    public override uint Seek(uint position)
    {
        Position = Math.Min(position, Size);
        return Position;
    }

    public void SetData(IEnumerable<byte> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _buffer = data.ToArray();
        Size = (uint)_buffer.Length;
        Position = 0;
    }

    public void SetData(Deserializer source, uint size)
    {
        ArgumentNullException.ThrowIfNull(source);
        Resize(size);
        Position = 0;
        _ = source.Read(_buffer.AsSpan(0, checked((int)size)));
        Position = 0;
    }

    public override uint Write(ReadOnlySpan<byte> data)
    {
        var end = checked(Position + (uint)data.Length);
        if (end > Size)
            Resize(end);

        data.CopyTo(_buffer.AsSpan((int)Position, data.Length));
        Position = end;
        return (uint)data.Length;
    }
}
