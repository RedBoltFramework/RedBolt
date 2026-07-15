namespace RedBolt;

public sealed class VectorBuffer : AbstractFile
{
    private const string VectorBufferName = "VectorBuffer";
    private byte[] _buffer = [];

    public VectorBuffer()
    {
    }

    public VectorBuffer(byte[] data)
    {
        SetData(data);
    }

    public VectorBuffer(nint data, uint size)
    {
        SetData(data, size);
    }

    public VectorBuffer(Deserializer source, uint size)
    {
        ArgumentNullException.ThrowIfNull(source);
        SetData(source, size);
    }

    public ReadOnlySpan<byte> Buffer => _buffer;

    // C++: GetName() checks AbstractFile::GetName() first; if empty, returns "VectorBuffer"
    public override string Name => base.Name.Length != 0 ? base.Name : VectorBufferName;

    public byte[] GetBuffer()
    {
        return _buffer;
    }

    // C++: const unsigned char* GetData() const { return size_ ? &buffer_[0] : nullptr; }
    public nint GetData()
    {
        return 0;
    }

    // C++: unsigned char* GetModifiableData() { return size_ ? &buffer_[0] : nullptr; }
    public nint GetModifiableData()
    {
        return 0;
    }

    public void Clear()
    {
        _buffer = [];
        Position = 0;
        Size = 0;
    }

    public override uint Read(Span<byte> destination)
    {
        var available = (int)Math.Min((uint)destination.Length, Size - Position);
        _buffer.AsSpan(checked((int)Position), available).CopyTo(destination);
        Position += (uint)available;
        return (uint)available;
    }

    public void Resize(uint size)
    {
        if (size == Size)
            return;

        var oldSize = Size;
        Array.Resize(ref _buffer, checked((int)size));
        Size = size;
        Position = Math.Min(Position, Size);
        if (size > oldSize)
            _buffer.AsSpan(checked((int)oldSize), checked((int)(size - oldSize))).Clear();
    }

    public override uint Seek(uint position)
    {
        Position = Math.Min(position, Size);
        return Position;
    }

    public void SetData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
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

    public void SetData(Deserializer source, uint size)
    {
        ArgumentNullException.ThrowIfNull(source);
        Resize(size);
        var actualSize = source.Read(_buffer);
        if (actualSize != size)
            Resize(actualSize);
        Position = 0;
    }

    public override uint Write(ReadOnlySpan<byte> data)
    {
        var end = checked(Position + (uint)data.Length);
        if (end > Size)
            Resize(end);

        data.CopyTo(_buffer.AsSpan(checked((int)Position), data.Length));
        Position = end;
        return (uint)data.Length;
    }
}
