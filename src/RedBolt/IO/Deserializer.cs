using System.Buffers.Binary;
using System.Text;

namespace RedBolt;

public abstract class Deserializer : Serializer
{
    protected Deserializer(uint size = 0)
    {
        Size = size;
    }

    public bool Eof => IsEof();

    public uint Position { get; protected set; }

    public uint Size { get; protected set; }

    public abstract uint Read(Span<byte> destination);

    public abstract uint Seek(uint position);

    public uint GetPosition() => Position;

    public uint GetSize() => Size;

    public virtual bool IsEof() => Position >= Size;

    public uint SeekRelative(int delta)
    {
        var position = delta < 0 && (uint)-delta > Position ? 0u : (uint)Math.Max(0L, (long)Position + delta);
        return Seek(position);
    }

    public uint Tell() => Position;

    public bool ReadBool() => ReadUByte() != 0;

    public BoundingBox ReadBoundingBox() => new(ReadVector3(), ReadVector3());

    public byte[] ReadBuffer()
    {
        var size = ReadVLE();
        var buffer = new byte[size];
        _ = Read(buffer);
        return buffer;
    }

    public sbyte ReadByte() => unchecked((sbyte)ReadUByte());

    public Color ReadColor() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public double ReadDouble()
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    public string ReadFileID()
    {
        Span<byte> buffer = stackalloc byte[4];
        ReadPadded(buffer);
        return Encoding.ASCII.GetString(buffer);
    }

    public float ReadFloat()
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    public int ReadInt()
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public long ReadInt64()
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    public IntRect ReadIntRect() => new(ReadInt(), ReadInt(), ReadInt(), ReadInt());

    public IntVector2 ReadIntVector2() => new(ReadInt(), ReadInt());

    public IntVector3 ReadIntVector3() => new(ReadInt(), ReadInt(), ReadInt());

    public string ReadLine()
    {
        var bytes = new List<byte>();
        while (!IsEof())
        {
            var value = ReadUByte();
            if (value == '\n')
                break;
            if (value != '\r')
                bytes.Add(value);
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public Quaternion ReadQuaternion() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public Rect ReadRect() => new(ReadVector2(), ReadVector2());

    public short ReadShort()
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    public string ReadString()
    {
        var bytes = new List<byte>();
        while (!IsEof())
        {
            var value = ReadUByte();
            if (value == 0)
                break;
            bytes.Add(value);
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public StringHash ReadStringHash() => new(ReadUInt());

    public byte ReadUByte()
    {
        Span<byte> buffer = stackalloc byte[1];
        ReadPadded(buffer);
        return buffer[0];
    }

    public uint ReadUInt()
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    public ulong ReadUInt64()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    public ushort ReadUShort()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        ReadPadded(buffer);
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    public Vector2 ReadVector2() => new(ReadFloat(), ReadFloat());

    public Vector3 ReadVector3() => new(ReadFloat(), ReadFloat(), ReadFloat());

    public Vector4 ReadVector4() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public uint ReadVLE()
    {
        var result = 0u;
        var shift = 0;
        while (shift < 32)
        {
            var value = ReadUByte();
            result |= (uint)(value & 0x7f) << shift;
            if ((value & 0x80) == 0)
                break;
            shift += 7;
        }

        return result;
    }

    private void ReadPadded(Span<byte> buffer)
    {
        var read = Read(buffer);
        if (read < buffer.Length)
            buffer[(int)read..].Clear();
    }
}
