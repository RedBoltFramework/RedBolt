namespace RedBolt;

public sealed class VectorBuffer : Deserializer
{
    private byte[] buffer;

    public VectorBuffer()
        : base(0)
    {
        buffer = [];
    }

    public VectorBuffer(IEnumerable<byte> data)
        : base((uint)(data?.Count() ?? 0))
    {
        ArgumentNullException.ThrowIfNull(data);
        buffer = data.ToArray();
        Size = (uint)buffer.Length;
    }

    public IReadOnlyList<byte> Buffer => buffer;

    public byte[] GetBuffer() => buffer;

    public byte[] GetData() => buffer;

    public byte[] GetModifiableData() => buffer;

    public void Clear()
    {
        buffer = [];
        Position = 0;
        Size = 0;
    }

    public override uint Read(Span<byte> destination)
    {
        var available = (int)Math.Min((uint)destination.Length, Size - Position);
        buffer.AsSpan((int)Position, available).CopyTo(destination);
        Position += (uint)available;
        return (uint)available;
    }

    public void Resize(uint size)
    {
        Array.Resize(ref buffer, checked((int)size));
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
        buffer = data.ToArray();
        Size = (uint)buffer.Length;
        Position = 0;
    }

    public void SetData(Deserializer source, uint size)
    {
        ArgumentNullException.ThrowIfNull(source);
        Resize(size);
        Position = 0;
        _ = source.Read(buffer.AsSpan(0, checked((int)size)));
        Position = 0;
    }

    public override uint Write(ReadOnlySpan<byte> data)
    {
        var end = checked(Position + (uint)data.Length);
        if (end > Size)
            Resize(end);

        data.CopyTo(buffer.AsSpan((int)Position, data.Length));
        Position = end;
        return (uint)data.Length;
    }
}
