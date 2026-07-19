using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Abstract stream for reading.</summary>
public abstract class Deserializer : Serializer
{
    /// <summary>Construct with zero size.</summary>
    public Deserializer()
    {
    }

    /// <summary>Construct with defined size.</summary>
    public Deserializer(uint size)
    {
        _size = size;
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
    }

    /// <summary>Read bytes from the stream. Return number of bytes actually read.</summary>
    public abstract uint Read(Span<byte> destination);

    /// <summary>Set position from the beginning of the stream. Return actual new position.</summary>
    public abstract uint Seek(uint position);

    /// <summary>Return name of the stream.</summary>
    public override string Name => string.Empty;

    /// <summary>Return name of the stream.</summary>
    public override string GetName() => Name;

    /// <summary>Return a checksum if applicable.</summary>
    public virtual uint GetChecksum() => 0;

    /// <summary>Return whether the end of stream has been reached.</summary>
    public bool Eof => IsEof();

    /// <summary>Return whether the end of stream has been reached.</summary>
    public virtual bool IsEof() => _position >= _size;

    /// <summary>Set position relative to current position. Return actual new position.</summary>
    public uint SeekRelative(int delta)
    {
        return Seek(unchecked(GetPosition() + (uint)delta));
    }

    /// <summary>Return current position.</summary>
    public uint Position
    {
        get => _position;
        protected set => _position = value;
    }

    /// <summary>Return current position.</summary>
    public uint GetPosition() => _position;

    /// <summary>Return current position.</summary>
    public uint Tell() => _position;

    /// <summary>Return size.</summary>
    public uint Size
    {
        get => _size;
        protected set => _size = value;
    }

    /// <summary>Return size.</summary>
    public uint GetSize() => _size;

    public bool ReadBool() => ReadUByte() != 0;

    public BoundingBox ReadBoundingBox()
    {
        Span<byte> buffer = stackalloc byte[6 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new BoundingBox(new Vector3f(data[0], data[1], data[2]), new Vector3f(data[3], data[4], data[5]));
    }

    public byte[] ReadBuffer()
    {
        var size = ReadVLE();
        var buffer = new byte[size];
        if (buffer.Length != 0)
            _ = Read(buffer);
        return buffer;
    }

    public void ReadBuffer(List<byte> byteVector)
    {
        var size = checked((int)ReadVLE());
        byteVector.Clear();
        if (size == 0)
            return;

        var buffer = new byte[size];
        _ = Read(buffer);
        byteVector.AddRange(buffer);
    }

    public sbyte ReadByte() => unchecked((sbyte)ReadUByte());

    public Color ReadColor()
    {
        Span<byte> buffer = stackalloc byte[4 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Color(data[0], data[1], data[2], data[3]);
    }

    public Vector3d ReadPackedVector3(VectorBinaryEncoding encoding, float param = 1.0f)
    {
        switch (encoding)
        {
            case VectorBinaryEncoding.Float:
            {
                var data = ReadVector3();
                return new Vector3d(data.X, data.Y, data.Z);
            }

            case VectorBinaryEncoding.Double:
            {
                Span<byte> buffer = stackalloc byte[3 * sizeof(double)];
                _ = Read(buffer);
                var data = MemoryMarshal.Cast<byte, double>(buffer);
                return new Vector3d(data[0], data[1], data[2]);
            }

            case VectorBinaryEncoding.Int32:
            {
                var intValue = ReadIntVector3();
                return new Vector3d(intValue.X, intValue.Y, intValue.Z) / MathDefs.MaxInt * param;
            }

            case VectorBinaryEncoding.Int16:
            {
                Span<byte> buffer = stackalloc byte[3 * sizeof(short)];
                _ = Read(buffer);
                var intValue = MemoryMarshal.Cast<byte, short>(buffer);
                return new Vector3d(intValue[0], intValue[1], intValue[2]) / MathDefs.MaxShort * param;
            }

            default:
                return Vector3d.ZERO;
        }
    }

    public double ReadDouble()
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        _ = Read(buffer);
        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    public string ReadFileID()
    {
        Span<byte> buffer = stackalloc byte[4];
        _ = Read(buffer);
        return Encoding.ASCII.GetString(buffer);
    }

    public float ReadFloat()
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        _ = Read(buffer);
        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    public int ReadInt()
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        _ = Read(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public long ReadInt64()
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        _ = Read(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    public IntRect ReadIntRect()
    {
        Span<byte> buffer = stackalloc byte[4 * sizeof(int)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, int>(buffer);
        return new IntRect(data[0], data[1], data[2], data[3]);
    }

    public Vector2i ReadIntVector2()
    {
        Span<byte> buffer = stackalloc byte[2 * sizeof(int)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, int>(buffer);
        return new Vector2i(data[0], data[1]);
    }

    public Vector3i ReadIntVector3()
    {
        Span<byte> buffer = stackalloc byte[3 * sizeof(int)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, int>(buffer);
        return new Vector3i(data[0], data[1], data[2]);
    }

    public Matrix3 ReadMatrix3()
    {
        Span<byte> buffer = stackalloc byte[9 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Matrix3(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7], data[8]);
    }

    public Matrix3x4 ReadMatrix3x4()
    {
        Span<byte> buffer = stackalloc byte[12 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Matrix3x4(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7],
            data[8], data[9], data[10], data[11]);
    }

    public Matrix4 ReadMatrix4()
    {
        Span<byte> buffer = stackalloc byte[16 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Matrix4(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7],
            data[8], data[9], data[10], data[11], data[12], data[13], data[14], data[15]);
    }

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

    public Quaternion ReadQuaternion()
    {
        Span<byte> buffer = stackalloc byte[4 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Quaternion(data[0], data[1], data[2], data[3]);
    }

    public Quaternion ReadPackedQuaternion(VectorBinaryEncoding encoding)
    {
        switch (encoding)
        {
            case VectorBinaryEncoding.Double:
                // Not implemented
            case VectorBinaryEncoding.Int32:
                // Not implemented
            case VectorBinaryEncoding.Float:
                return ReadQuaternion();

            case VectorBinaryEncoding.Int16:
            {
                Span<byte> buffer = stackalloc byte[4 * sizeof(short)];
                _ = Read(buffer);
                var coords = MemoryMarshal.Cast<byte, short>(buffer);
                var factor = (float)MathDefs.MaxShort;
                var value = new Quaternion(coords[0] * factor, coords[1] * factor, coords[2] * factor,
                    coords[3] * factor);
                value.Normalize();
                return value;
            }

            default:
                return Quaternion.IDENTITY;
        }
    }

    public Rect ReadRect()
    {
        Span<byte> buffer = stackalloc byte[4 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Rect(new Vector2f(data[0], data[1]), new Vector2f(data[2], data[3]));
    }

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
        _ = Read(buffer);
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
        _ = Read(buffer);
        return buffer[0];
    }

    public uint ReadUInt()
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        _ = Read(buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    public ulong ReadUInt64()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        _ = Read(buffer);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    public ushort ReadUShort()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        _ = Read(buffer);
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    public Vector2f ReadVector2()
    {
        Span<byte> buffer = stackalloc byte[2 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Vector2f(data[0], data[1]);
    }

    public Vector3f ReadVector3()
    {
        Span<byte> buffer = stackalloc byte[3 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Vector3f(data[0], data[1], data[2]);
    }

    public Vector4f ReadVector4()
    {
        Span<byte> buffer = stackalloc byte[4 * sizeof(float)];
        _ = Read(buffer);
        var data = MemoryMarshal.Cast<byte, float>(buffer);
        return new Vector4f(data[0], data[1], data[2], data[3]);
    }

    public Variant ReadVariant()
    {
        var type = (VariantType)ReadUByte();
        return ReadVariant(type);
    }

    public Variant ReadVariant(VariantType type, Context? context = null)
    {
        switch (type)
        {
            case VariantType.Int:
                return new Variant(ReadInt());
            case VariantType.Int64:
                return new Variant(ReadInt64());
            case VariantType.Bool:
                return new Variant(ReadBool());
            case VariantType.Float:
                return new Variant(ReadFloat());
            case VariantType.Vector2:
                return new Variant(ReadVector2());
            case VariantType.DoubleVector2:
            {
                Span<byte> buffer = stackalloc byte[2 * sizeof(double)];
                _ = Read(buffer);
                var data = MemoryMarshal.Cast<byte, double>(buffer);
                return new Variant(new Vector2d(data[0], data[1]));
            }
            case VariantType.Vector3:
                return new Variant(ReadVector3());
            case VariantType.DoubleVector3:
            {
                Span<byte> buffer = stackalloc byte[3 * sizeof(double)];
                _ = Read(buffer);
                var data = MemoryMarshal.Cast<byte, double>(buffer);
                return new Variant(new Vector3d(data[0], data[1], data[2]));
            }
            case VariantType.Vector4:
                return new Variant(ReadVector4());
            case VariantType.Quaternion:
                return new Variant(ReadQuaternion());
            case VariantType.Color:
                return new Variant(ReadColor());
            case VariantType.String:
                return new Variant(ReadString());
            case VariantType.Buffer:
                return new Variant(ReadBuffer());

            // Deserializing pointers is not supported. Return null
            case VariantType.VoidPtr:
            case VariantType.Ptr:
                ReadUInt();
                return new Variant(IntPtr.Zero);

            case VariantType.ResourceRef:
                return new Variant(ReadResourceRef());
            case VariantType.ResourceRefList:
                return new Variant(ReadResourceRefList());
            case VariantType.VariantVector:
                return new Variant(ReadVariantVector());
            case VariantType.StringVector:
                return new Variant(ReadStringVector());
            case VariantType.Rect:
                return new Variant(ReadRect());
            case VariantType.VariantMap:
                return new Variant(ReadVariantMap());
            case VariantType.IntRect:
                return new Variant(ReadIntRect());
            case VariantType.Vector2i:
                return new Variant(ReadIntVector2());
            case VariantType.Vector3i:
                return new Variant(ReadIntVector3());
            case VariantType.Matrix3:
                return new Variant(ReadMatrix3());
            case VariantType.Matrix3x4:
                return new Variant(ReadMatrix3x4());
            case VariantType.Matrix4:
                return new Variant(ReadMatrix4());
            case VariantType.Double:
                return new Variant(ReadDouble());
            case VariantType.StringVariantMap:
                return new Variant(ReadStringVariantMap());

            case VariantType.Custom:
            {
                var typeName = new StringHash(ReadUInt());
                if (typeName.Value == 0)
                    return default;

                if (context is null)
                {
                    Log.GetLogger().LogError("Context must not be null for SharedPtr<Serializable>");
                    return default;
                }

                if (context.CreateObject(typeName) is not Serializable serializable)
                {
                    Log.GetLogger().LogError("Creation of type '{TypeName:X8}' failed because it has no factory registered",
                        typeName.Value);
                    return default;
                }

                if (serializable.Load(this))
                    return Variant.MakeCustomValue(serializable);

                Log.GetLogger().LogError("Deserialization of '{TypeName:X8}' failed", typeName.Value);
                serializable.Dispose();
                return default;
            }

            default:
                return default;
        }
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

    /// <summary>Stream position.</summary>
    protected uint _position;
    /// <summary>Stream size.</summary>
    protected uint _size;
}
