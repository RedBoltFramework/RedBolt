using System.Buffers.Binary;
using System.Text;

namespace RedBolt;

public abstract class Serializer
{
    public virtual string Name => string.Empty;

    public virtual string GetName() => Name;

    public abstract uint Write(ReadOnlySpan<byte> data);

    public bool WriteBool(bool value) => WriteUByte(value ? (byte)1 : (byte)0);

    public bool WriteBoundingBox(BoundingBox value) => WriteVector3(value.Min) && WriteVector3(value.Max);

    public bool WriteBuffer(IReadOnlyCollection<byte> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WriteVLE((uint)value.Count) && Write(value.ToArray()) == value.Count;
    }

    public bool WriteByte(sbyte value) => WriteUByte(unchecked((byte)value));

    public bool WriteColor(Color value) => WriteFloat(value.R) && WriteFloat(value.G) && WriteFloat(value.B) && WriteFloat(value.A);

    public bool WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteFileID(string value)
    {
        Span<byte> id = stackalloc byte[4];
        id.Fill((byte)' ');

        var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
        bytes.AsSpan(0, Math.Min(bytes.Length, id.Length)).CopyTo(id);
        return Write(id) == id.Length;
    }

    public bool WriteFloat(float value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteInt(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteIntRect(IntRect value) => WriteInt(value.Left) && WriteInt(value.Top) && WriteInt(value.Right) && WriteInt(value.Bottom);

    public bool WriteIntVector2(IntVector2 value) => WriteInt(value.X) && WriteInt(value.Y);

    public bool WriteIntVector3(IntVector3 value) => WriteInt(value.X) && WriteInt(value.Y) && WriteInt(value.Z);

    public bool WriteLine(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        return Write(bytes) == bytes.Length && WriteUByte((byte)'\r') && WriteUByte((byte)'\n');
    }

    public bool WriteQuaternion(Quaternion value) => WriteFloat(value.W) && WriteFloat(value.X) && WriteFloat(value.Y) && WriteFloat(value.Z);

    public bool WriteRect(Rect value) => WriteVector2(value.Min) && WriteVector2(value.Max);

    public bool WriteShort(short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        return Write(bytes) == bytes.Length && WriteUByte(0);
    }

    public bool WriteStringHash(StringHash value) => WriteUInt(value.Value);

    public bool WriteUByte(byte value)
    {
        Span<byte> buffer = stackalloc byte[] { value };
        return Write(buffer) == buffer.Length;
    }

    public bool WriteUInt(uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteUInt64(ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteUShort(ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    public bool WriteVector2(Vector2 value) => WriteFloat(value.X) && WriteFloat(value.Y);

    public bool WriteVector3(Vector3 value) => WriteFloat(value.X) && WriteFloat(value.Y) && WriteFloat(value.Z);

    public bool WriteVector4(Vector4 value) => WriteFloat(value.X) && WriteFloat(value.Y) && WriteFloat(value.Z) && WriteFloat(value.W);

    public bool WriteVLE(uint value)
    {
        while (value >= 0x80u)
        {
            if (!WriteUByte((byte)((value & 0x7fu) | 0x80u)))
                return false;
            value >>= 7;
        }

        return WriteUByte((byte)value);
    }
}
