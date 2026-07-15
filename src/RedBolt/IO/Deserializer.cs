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

    public virtual uint GetChecksum() => 0;

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

    public void ReadBuffer(List<byte> byteVector)
    {
        ArgumentNullException.ThrowIfNull(byteVector);

        var size = checked((int)ReadVLE());
        byteVector.Clear();
        if (size == 0)
            return;

        var buffer = new byte[size];
        _ = Read(buffer);
        byteVector.AddRange(buffer);
    }

    public sbyte ReadByte() => unchecked((sbyte)ReadUByte());

    public Color ReadColor() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public Vector3d ReadPackedVector3(VectorBinaryEncoding encoding, float param = 1.0f)
    {
        return encoding switch
        {
            VectorBinaryEncoding.Float =>
                new Vector3d(
                    double.CreateChecked(ReadFloat()),
                    double.CreateChecked(ReadFloat()),
                    double.CreateChecked(ReadFloat())),
            VectorBinaryEncoding.Double => ReadDoubleVector3(),
            VectorBinaryEncoding.Int32 => ReadIntVector3() is var intVector
                ? new Vector3d(intVector.X, intVector.Y, intVector.Z) / MathDefs.MaxInt * param
                : Vector3d.ZERO,
            VectorBinaryEncoding.Int16 => new Vector3d(ReadShort(), ReadShort(), ReadShort()) / MathDefs.MaxShort * param,
            _ => Vector3d.ZERO
        };
    }

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

    public Vector2i ReadIntVector2() => new(ReadInt(), ReadInt());

    public Vector3i ReadIntVector3() => new(ReadInt(), ReadInt(), ReadInt());

    public Matrix3 ReadMatrix3() => new(
        ReadFloat(), ReadFloat(), ReadFloat(),
        ReadFloat(), ReadFloat(), ReadFloat(),
        ReadFloat(), ReadFloat(), ReadFloat());

    public Matrix3x4 ReadMatrix3x4() => new(
        ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
        ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
        ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public Matrix4 ReadMatrix4() => new(
        ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
        ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
        ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat(),
        ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public string ReadLine()
    {
        var bytes = new List<byte>();
        while (!IsEof())
        {
            var value = ReadUByte();
            if (value == '\n')
                break;
            if (value == '\r')
            {
                if (!IsEof())
                {
                    var next = ReadUByte();
                    if (next != '\n')
                        Seek(Position - 1);
                }
                break;
            }

            bytes.Add(value);
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public Quaternion ReadQuaternion() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public Quaternion ReadPackedQuaternion(VectorBinaryEncoding encoding)
    {
        switch (encoding)
        {
            case VectorBinaryEncoding.Double:
            case VectorBinaryEncoding.Int32:
            case VectorBinaryEncoding.Float:
                return ReadQuaternion();

            case VectorBinaryEncoding.Int16:
            {
                var factor = (float)MathDefs.MaxShort;
                var value = new Quaternion(
                    ReadShort() * factor,
                    ReadShort() * factor,
                    ReadShort() * factor,
                    ReadShort() * factor);
                value.Normalize();
                return value;
            }

            default:
                return Quaternion.IDENTITY;
        }
    }

    public Rect ReadRect() => new(ReadVector2(), ReadVector2());

    public ResourceRef ReadResourceRef() => new(ReadStringHash(), ReadString());

    public ResourceRefList ReadResourceRefList()
    {
        var type = ReadStringHash().Value;
        var size = ReadVLE();
        var names = new StringVector();
        for (var i = 0u; i < size; ++i)
            names.Add(ReadString());
        return new ResourceRefList(type, names);
    }

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

    public StringVariantMap ReadStringVariantMap()
    {
        var result = new StringVariantMap();
        var count = ReadVLE();
        for (var i = 0u; i < count; ++i)
            result[ReadString()] = ReadVariant();
        return result;
    }

    public StringVector ReadStringVector()
    {
        var result = new StringVector();
        var count = ReadVLE();
        for (var i = 0u; i < count; ++i)
            result.Add(ReadString());
        return result;
    }

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

    public Vector2f ReadVector2() => new(ReadFloat(), ReadFloat());

    public Vector2d ReadDoubleVector2() => new(ReadDouble(), ReadDouble());

    public Vector3f ReadVector3() => new(ReadFloat(), ReadFloat(), ReadFloat());

    public Vector3d ReadDoubleVector3() => new(ReadDouble(), ReadDouble(), ReadDouble());

    public Vector4f ReadVector4() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    public Variant ReadVariant()
    {
        var type = (VariantType)ReadUByte();
        return ReadVariant(type);
    }

    public Variant ReadVariant(VariantType type)
    {
        return type switch
        {
            VariantType.None => default,
            VariantType.Int => new Variant(ReadInt()),
            VariantType.Bool => new Variant(ReadBool()),
            VariantType.Float => new Variant(ReadFloat()),
            VariantType.Vector2 => new Variant(ReadVector2()),
            VariantType.DoubleVector2 => new Variant(ReadDoubleVector2()),
            VariantType.Vector3 => new Variant(ReadVector3()),
            VariantType.DoubleVector3 => new Variant(ReadDoubleVector3()),
            VariantType.Vector4 => new Variant(ReadVector4()),
            VariantType.Quaternion => new Variant(ReadQuaternion()),
            VariantType.Color => new Variant(ReadColor()),
            VariantType.String => new Variant(ReadString()),
            VariantType.Buffer => new Variant(ReadBuffer()),
            VariantType.ResourceRef => new Variant(ReadResourceRef()),
            VariantType.ResourceRefList => new Variant(ReadResourceRefList()),
            VariantType.VariantVector => new Variant(ReadVariantVector()),
            VariantType.VariantMap => new Variant(ReadVariantMap()),
            VariantType.IntRect => new Variant(ReadIntRect()),
            VariantType.Vector2i => new Variant(ReadIntVector2()),
            VariantType.Matrix3 => new Variant(ReadMatrix3()),
            VariantType.Matrix3x4 => new Variant(ReadMatrix3x4()),
            VariantType.Matrix4 => new Variant(ReadMatrix4()),
            VariantType.Double => new Variant(ReadDouble()),
            VariantType.StringVector => new Variant(ReadStringVector()),
            VariantType.Rect => new Variant(ReadRect()),
            VariantType.Vector3i => new Variant(ReadIntVector3()),
            VariantType.Int64 => new Variant(ReadInt64()),
            VariantType.StringVariantMap => new Variant(ReadStringVariantMap()),
            _ => default
        };
    }

    public VariantMap ReadVariantMap()
    {
        var result = new VariantMap();
        var count = ReadVLE();
        for (var i = 0u; i < count; ++i)
            result[ReadStringHash()] = ReadVariant();
        return result;
    }

    public VariantVector ReadVariantVector()
    {
        var result = new VariantVector();
        var count = ReadVLE();
        for (var i = 0u; i < count; ++i)
            result.Add(ReadVariant());
        return result;
    }

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
