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

    public bool WritePackedVector3(Vector3d value, VectorBinaryEncoding encoding, float param = 1.0f)
    {
        switch (encoding)
        {
            case VectorBinaryEncoding.Float:
                return WriteVector3(new Vector3f(float.CreateChecked(value.X), float.CreateChecked(value.Y), float.CreateChecked(value.Z)));

            case VectorBinaryEncoding.Double:
                return WriteVector3(value);

            case VectorBinaryEncoding.Int32:
            {
                var normalizedValue = ClampVector3(value / param, -Vector3d.ONE, Vector3d.ONE);
                var intValue = new Vector3i(
                    (int)(normalizedValue.X * MathDefs.MaxInt + 0.5),
                    (int)(normalizedValue.Y * MathDefs.MaxInt + 0.5),
                    (int)(normalizedValue.Z * MathDefs.MaxInt + 0.5));
                return WriteIntVector3(intValue);
            }

            case VectorBinaryEncoding.Int16:
            {
                var normalizedValue = ClampVector3(value / param, -Vector3d.ONE, Vector3d.ONE);
                return WriteShort((short)(normalizedValue.X * MathDefs.MaxShort + 0.5))
                    && WriteShort((short)(normalizedValue.Y * MathDefs.MaxShort + 0.5))
                    && WriteShort((short)(normalizedValue.Z * MathDefs.MaxShort + 0.5));
            }

            default:
                return false;
        }
    }

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

    public bool WriteIntVector2(Vector2i value) => WriteInt(value.X) && WriteInt(value.Y);

    public bool WriteIntVector3(Vector3i value) => WriteInt(value.X) && WriteInt(value.Y) && WriteInt(value.Z);

    public bool WriteLine(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        return Write(bytes) == bytes.Length && WriteUByte((byte)'\r') && WriteUByte((byte)'\n');
    }

    public bool WriteQuaternion(Quaternion value) => WriteFloat(value.W) && WriteFloat(value.X) && WriteFloat(value.Y) && WriteFloat(value.Z);

    public bool WritePackedQuaternion(Quaternion value, VectorBinaryEncoding encoding)
    {
        switch (encoding)
        {
            case VectorBinaryEncoding.Double:
            case VectorBinaryEncoding.Int32:
            case VectorBinaryEncoding.Float:
                return WriteQuaternion(value);

            case VectorBinaryEncoding.Int16:
            {
                var normalized = value.Normalized;
                return WriteShort((short)MathDefs.Round(MathDefs.Clamp(normalized.W, -1.0f, 1.0f) * MathDefs.MaxShort))
                    && WriteShort((short)MathDefs.Round(MathDefs.Clamp(normalized.X, -1.0f, 1.0f) * MathDefs.MaxShort))
                    && WriteShort((short)MathDefs.Round(MathDefs.Clamp(normalized.Y, -1.0f, 1.0f) * MathDefs.MaxShort))
                    && WriteShort((short)MathDefs.Round(MathDefs.Clamp(normalized.Z, -1.0f, 1.0f) * MathDefs.MaxShort));
            }

            default:
                return false;
        }
    }

    public bool WriteRect(Rect value) => WriteVector2(value.Min) && WriteVector2(value.Max);

    public bool WriteResourceRef(ResourceRef value) => WriteStringHash(value.Type) && WriteString(value.Name);

    public bool WriteResourceRefList(ResourceRefList value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var success = true;
        success &= WriteStringHash(new StringHash(value.Type));
        success &= WriteVLE((uint)value.Names.Count);
        for (var i = 0; i < value.Names.Count; ++i)
            success &= WriteString(value.Names[i]);
        return success;
    }

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

    public bool WriteStringVector(StringVector value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var success = WriteVLE((uint)value.Count);
        for (var i = 0; i < value.Count; ++i)
            success &= WriteString(value[i]);
        return success;
    }

    public bool WriteStringHash(StringHash value) => WriteUInt(value.Value);

    public bool WriteStringVariantMap(StringVariantMap value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var success = WriteVLE((uint)value.Count);
        foreach (var pair in value)
        {
            success &= WriteString(pair.Key);
            success &= WriteVariant(pair.Value);
        }

        return success;
    }

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

    public bool WriteVector2(Vector2f value) => WriteFloat(value.X) && WriteFloat(value.Y);

    public bool WriteVector2(Vector2d value) => WriteDouble(value.X) && WriteDouble(value.Y);

    public bool WriteVector3(Vector3f value) => WriteFloat(value.X) && WriteFloat(value.Y) && WriteFloat(value.Z);

    public bool WriteVector3(Vector3d value) => WriteDouble(value.X) && WriteDouble(value.Y) && WriteDouble(value.Z);

    public bool WriteVector4(Vector4f value) => WriteFloat(value.X) && WriteFloat(value.Y) && WriteFloat(value.Z) && WriteFloat(value.W);

    public bool WriteVariant(Variant value) => WriteUByte((byte)value.Type) && WriteVariantData(value);

    public bool WriteVariantData(Variant value)
    {
        return value.Type switch
        {
            VariantType.None => true,
            VariantType.Int => WriteInt(value.GetInt()),
            VariantType.Bool => WriteBool(value.GetBool()),
            VariantType.Float => WriteFloat(value.GetFloat()),
            VariantType.Vector2 => WriteVector2(value.GetVector2()),
            VariantType.DoubleVector2 => WriteVector2(value.GetDoubleVector2()),
            VariantType.Vector3 => WriteVector3(value.GetVector3()),
            VariantType.DoubleVector3 => WriteVector3(value.GetDoubleVector3()),
            VariantType.Vector4 => WriteVector4(value.GetVector4()),
            VariantType.Quaternion => WriteQuaternion(value.GetQuaternion()),
            VariantType.Color => WriteColor(value.GetColor()),
            VariantType.String => WriteString(value.GetString()),
            VariantType.Buffer => WriteBuffer(value.GetBuffer()),
            VariantType.ResourceRef => WriteResourceRef(value.GetResourceRef()),
            VariantType.ResourceRefList => WriteResourceRefList(value.GetResourceRefList()),
            VariantType.VariantVector => WriteVariantVector(value.GetVariantVector()),
            VariantType.VariantMap => WriteVariantMap(value.GetVariantMap()),
            VariantType.IntRect => WriteIntRect(value.GetIntRect()),
            VariantType.Vector2i => WriteIntVector2(value.GetIntVector2()),
            VariantType.Matrix3 => WriteMatrix3(value.GetMatrix3()),
            VariantType.Matrix3x4 => WriteMatrix3x4(value.GetMatrix3x4()),
            VariantType.Matrix4 => WriteMatrix4(value.GetMatrix4()),
            VariantType.Double => WriteDouble(value.GetDouble()),
            VariantType.StringVector => WriteStringVector(value.GetStringVector()),
            VariantType.Rect => WriteRect(value.GetRect()),
            VariantType.Vector3i => WriteIntVector3(value.GetIntVector3()),
            VariantType.Int64 => WriteInt64(value.GetInt64()),
            VariantType.StringVariantMap => WriteStringVariantMap(value.GetStringVariantMap()),
            _ => false
        };
    }

    public bool WriteVariantMap(VariantMap value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var success = WriteVLE((uint)value.Count);
        foreach (var pair in value)
        {
            success &= WriteStringHash(pair.Key);
            success &= WriteVariant(pair.Value);
        }

        return success;
    }

    public bool WriteVariantVector(VariantVector value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var success = WriteVLE((uint)value.Count);
        for (var i = 0; i < value.Count; ++i)
            success &= WriteVariant(value[i]);
        return success;
    }

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

    public bool WriteMatrix3(Matrix3 value) =>
        WriteFloat(value.M00) && WriteFloat(value.M01) && WriteFloat(value.M02) &&
        WriteFloat(value.M10) && WriteFloat(value.M11) && WriteFloat(value.M12) &&
        WriteFloat(value.M20) && WriteFloat(value.M21) && WriteFloat(value.M22);

    public bool WriteMatrix3x4(Matrix3x4 value) =>
        WriteFloat(value.M00) && WriteFloat(value.M01) && WriteFloat(value.M02) && WriteFloat(value.M03) &&
        WriteFloat(value.M10) && WriteFloat(value.M11) && WriteFloat(value.M12) && WriteFloat(value.M13) &&
        WriteFloat(value.M20) && WriteFloat(value.M21) && WriteFloat(value.M22) && WriteFloat(value.M23);

    public bool WriteMatrix4(Matrix4 value) =>
        WriteFloat(value.M00) && WriteFloat(value.M01) && WriteFloat(value.M02) && WriteFloat(value.M03) &&
        WriteFloat(value.M10) && WriteFloat(value.M11) && WriteFloat(value.M12) && WriteFloat(value.M13) &&
        WriteFloat(value.M20) && WriteFloat(value.M21) && WriteFloat(value.M22) && WriteFloat(value.M23) &&
        WriteFloat(value.M30) && WriteFloat(value.M31) && WriteFloat(value.M32) && WriteFloat(value.M33);

    private static Vector3d ClampVector3(Vector3d value, Vector3d min, Vector3d max)
    {
        return new Vector3d(
            Math.Clamp(value.X, min.X, max.X),
            Math.Clamp(value.Y, min.Y, max.Y),
            Math.Clamp(value.Z, min.Z, max.Z));
    }
}
