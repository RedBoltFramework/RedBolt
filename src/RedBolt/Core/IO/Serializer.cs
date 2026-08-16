// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IO;

using System.Buffers.Binary;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Abstract stream for writing.</summary>
public abstract class Serializer : IDisposable
{
    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Write bytes to the stream. Return number of bytes actually written.</summary>
    public abstract uint Write(ReadOnlySpan<byte> data);

    /// <summary>Return name of the stream.</summary>
    public virtual string Name => string.Empty;

    /// <summary>Return name of the stream.</summary>
    public virtual string GetName() => Name;

    /// <summary>Write a 64-bit integer.</summary>
    public bool WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write a 32-bit integer.</summary>
    public bool WriteInt(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write a 16-bit integer.</summary>
    public bool WriteShort(short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write an 8-bit integer.</summary>
    public bool WriteByte(sbyte value) => WriteUByte(unchecked((byte)value));

    /// <summary>Write a 64-bit unsigned integer.</summary>
    public bool WriteUInt64(ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write a 32-bit unsigned integer.</summary>
    public bool WriteUInt(uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write a 16-bit unsigned integer.</summary>
    public bool WriteUShort(ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write an 8-bit unsigned integer.</summary>
    public bool WriteUByte(byte value)
    {
        Span<byte> buffer = stackalloc byte[] { value };
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write a bool.</summary>
    public bool WriteBool(bool value) => WriteUByte(value ? (byte)1 : (byte)0);

    /// <summary>Write a float.</summary>
    public bool WriteFloat(float value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write a double.</summary>
    public bool WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write an IntRect.</summary>
    public bool WriteIntRect(IntRect value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write an IntVector2.</summary>
    public bool WriteIntVector2(Vector2i value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write an IntVector3.</summary>
    public bool WriteIntVector3(Vector3i value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a Rect.</summary>
    public bool WriteRect(Rect value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a Vector2.</summary>
    public bool WriteVector2(Vector2f value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a Vector3.</summary>
    public bool WriteVector3(Vector3f value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a DoubleVector3 packed with specified encoding.</summary>
    public bool WritePackedVector3(Vector3d value, VectorBinaryEncoding encoding, float param = 1.0f)
    {
        switch (encoding)
        {
            case VectorBinaryEncoding.Float:
            {
                var data = new Vector3f(float.CreateChecked(value.X), float.CreateChecked(value.Y), float.CreateChecked(value.Z));
                return WriteVector3(data);
            }

            case VectorBinaryEncoding.Double:
            {
                var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
                return Write(data) == data.Length;
            }

            case VectorBinaryEncoding.Int32:
            {
                var normalizedValue = value / param;
                normalizedValue = new Vector3d(
                    SystemMath.Clamp(normalizedValue.X, -1.0, 1.0),
                    SystemMath.Clamp(normalizedValue.Y, -1.0, 1.0),
                    SystemMath.Clamp(normalizedValue.Z, -1.0, 1.0));
                var intValue = new Vector3i(
                    (int)(normalizedValue.X * MathDefs.MaxInt + 0.5),
                    (int)(normalizedValue.Y * MathDefs.MaxInt + 0.5),
                    (int)(normalizedValue.Z * MathDefs.MaxInt + 0.5));
                var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref intValue, 1));
                return Write(data) == data.Length;
            }

            case VectorBinaryEncoding.Int16:
            {
                var normalizedValue = value / param;
                normalizedValue = new Vector3d(
                    SystemMath.Clamp(normalizedValue.X, -1.0, 1.0),
                    SystemMath.Clamp(normalizedValue.Y, -1.0, 1.0),
                    SystemMath.Clamp(normalizedValue.Z, -1.0, 1.0));
                Span<short> intValue =
                [
                    (short)(normalizedValue.X * MathDefs.MaxShort + 0.5),
                    (short)(normalizedValue.Y * MathDefs.MaxShort + 0.5),
                    (short)(normalizedValue.Z * MathDefs.MaxShort + 0.5)
                ];
                var data = MemoryMarshal.AsBytes(intValue);
                return Write(data) == data.Length;
            }

            default:
                return false;
        }
    }

    /// <summary>Write a Vector4.</summary>
    public bool WriteVector4(Vector4f value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a quaternion.</summary>
    public bool WriteQuaternion(Quaternion value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a quaternion with each component packed in 16 bits.</summary>
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
                Span<short> coords =
                [
                    (short)MathDefs.Round(MathDefs.Clamp(normalized.W, -1.0f, 1.0f) * MathDefs.MaxShort),
                    (short)MathDefs.Round(MathDefs.Clamp(normalized.X, -1.0f, 1.0f) * MathDefs.MaxShort),
                    (short)MathDefs.Round(MathDefs.Clamp(normalized.Y, -1.0f, 1.0f) * MathDefs.MaxShort),
                    (short)MathDefs.Round(MathDefs.Clamp(normalized.Z, -1.0f, 1.0f) * MathDefs.MaxShort)
                ];
                var data = MemoryMarshal.AsBytes(coords);
                return Write(data) == data.Length;
            }

            default:
                return false;
        }
    }

    /// <summary>Write a Matrix3.</summary>
    public bool WriteMatrix3(Matrix3 value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a Matrix3x4.</summary>
    public bool WriteMatrix3x4(Matrix3x4 value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a Matrix4.</summary>
    public bool WriteMatrix4(Matrix4 value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a color.</summary>
    public bool WriteColor(Color value)
    {
        var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        return Write(data) == data.Length;
    }

    /// <summary>Write a bounding box.</summary>
    public bool WriteBoundingBox(BoundingBox value)
    {
        var success = true;
        success &= WriteVector3(value.Min);
        success &= WriteVector3(value.Max);
        return success;
    }

    /// <summary>Write a null-terminated string.</summary>
    public bool WriteString(string value)
    {
        var zeroPosition = value.IndexOf('\0');
        var text = zeroPosition >= 0 ? value.AsSpan(0, zeroPosition) : value.AsSpan();
        var buffer = new byte[Encoding.UTF8.GetByteCount(text) + 1];
        Encoding.UTF8.GetBytes(text, buffer);
        return Write(buffer) == buffer.Length;
    }

    /// <summary>Write a four-letter file ID. If the string is not long enough, spaces will be appended.</summary>
    public bool WriteFileID(string value)
    {
        var success = true;
        var bytes = Encoding.UTF8.GetBytes(value);
        var length = SystemMath.Min(bytes.Length, 4);

        success &= Write(bytes.AsSpan(0, length)) == length;
        for (var i = bytes.Length; i < 4; ++i)
            success &= WriteByte((sbyte)' ');
        return success;
    }

    /// <summary>Write a 32-bit StringHash.</summary>
    public bool WriteStringHash(StringHash value) => WriteUInt(value.Value);

    /// <summary>Write a buffer, with size encoded as VLE.</summary>
    public bool WriteBuffer(ReadOnlySpan<byte> value)
    {
        var success = true;
        success &= WriteVLE((uint)value.Length);
        if (!value.IsEmpty)
            success &= Write(value) == value.Length;
        return success;
    }

    /// <summary>Write a resource reference.</summary>
    public bool WriteResourceRef(ResourceRef value)
    {
        var success = true;
        success &= WriteStringHash(value.Type);
        success &= WriteString(value.Name);
        return success;
    }

    /// <summary>Write a resource reference list.</summary>
    public bool WriteResourceRefList(ResourceRefList value)
    {
        var success = true;
        success &= WriteStringHash(new StringHash(value.Type));
        success &= WriteVLE((uint)value.Names.Count);
        for (var i = 0; i < value.Names.Count; ++i)
            success &= WriteString(value.Names[i]);
        return success;
    }

    /// <summary>Write a variant.</summary>
    public bool WriteVariant(Variant value)
    {
        var success = true;
        success &= WriteUByte((byte)value.Type);
        success &= WriteVariantData(value);
        return success;
    }

    /// <summary>Write a variant without the type information.</summary>
    public bool WriteVariantData(Variant value)
    {
        switch (value.Type)
        {
            case VariantType.None:
                return true;
            case VariantType.Int:
                return WriteInt(value.GetInt());
            case VariantType.Int64:
                return WriteInt64(value.GetInt64());
            case VariantType.Bool:
                return WriteBool(value.GetBool());
            case VariantType.Float:
                return WriteFloat(value.GetFloat());
            case VariantType.Vector2:
                return WriteVector2(value.GetVector2());
            case VariantType.DoubleVector2:
            {
                var item = value.GetDoubleVector2();
                var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref item, 1));
                return Write(data) == data.Length;
            }
            case VariantType.Vector3:
                return WriteVector3(value.GetVector3());
            case VariantType.DoubleVector3:
            {
                var item = value.GetDoubleVector3();
                var data = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref item, 1));
                return Write(data) == data.Length;
            }
            case VariantType.Vector4:
                return WriteVector4(value.GetVector4());
            case VariantType.Quaternion:
                return WriteQuaternion(value.GetQuaternion());
            case VariantType.Color:
                return WriteColor(value.GetColor());
            case VariantType.String:
                return WriteString(value.GetString());
            case VariantType.Buffer:
                return WriteBuffer(value.GetBuffer());
            case VariantType.Custom:
                if (value.TryGetCustom<Serializable>(out var serializable) && serializable is not null)
                {
                    WriteUInt(serializable.GetTypeNameHash().Value);
                    return serializable.Save(this);
                }
                goto case VariantType.VoidPtr;
            case VariantType.VoidPtr:
            case VariantType.Ptr:
                return WriteUInt(0);
            case VariantType.ResourceRef:
                return WriteResourceRef(value.GetResourceRef());
            case VariantType.ResourceRefList:
                return WriteResourceRefList(value.GetResourceRefList());
            case VariantType.VariantVector:
                return WriteVariantVector(value.GetVariantVector());
            case VariantType.StringVector:
                return WriteStringVector(value.GetStringVector());
            case VariantType.Rect:
                return WriteRect(value.GetRect());
            case VariantType.VariantMap:
                return WriteVariantMap(value.GetVariantMap());
            case VariantType.IntRect:
                return WriteIntRect(value.GetIntRect());
            case VariantType.Vector2i:
                return WriteIntVector2(value.GetIntVector2());
            case VariantType.Vector3i:
                return WriteIntVector3(value.GetIntVector3());
            case VariantType.Matrix3:
                return WriteMatrix3(value.GetMatrix3());
            case VariantType.Matrix3x4:
                return WriteMatrix3x4(value.GetMatrix3x4());
            case VariantType.Matrix4:
                return WriteMatrix4(value.GetMatrix4());
            case VariantType.Double:
                return WriteDouble(value.GetDouble());
            case VariantType.StringVariantMap:
                return WriteStringVariantMap(value.GetStringVariantMap());
            case VariantType.CString:
            {
                var item = value.GetCString();
                return Write(item.Bytes) == item.ByteLength + 1;
            }
            default:
                return false;
        }
    }

    /// <summary>Write a variant vector.</summary>
    public bool WriteVariantVector(VariantVector value)
    {
        var success = true;
        success &= WriteVLE((uint)value.Count);
        for (var i = 0; i < value.Count; ++i)
            success &= WriteVariant(value[i]);
        return success;
    }

    /// <summary>Write a variant vector.</summary>
    public bool WriteStringVector(StringVector value)
    {
        var success = true;
        success &= WriteVLE((uint)value.Count);
        for (var i = 0; i < value.Count; ++i)
            success &= WriteString(value[i]);
        return success;
    }

    /// <summary>Write a variant map.</summary>
    public bool WriteVariantMap(VariantMap value)
    {
        var success = true;
        success &= WriteVLE((uint)value.Count);
        foreach (var pair in value)
        {
            success &= WriteStringHash(pair.Key);
            success &= WriteVariant(pair.Value);
        }
        return success;
    }

    /// <summary>Write a string variant map.</summary>
    public bool WriteStringVariantMap(StringVariantMap value)
    {
        var success = true;
        success &= WriteVLE((uint)value.Count);
        foreach (var pair in value)
        {
            success &= WriteString(pair.Key);
            success &= WriteVariant(pair.Value);
        }
        return success;
    }

    /// <summary>Write a variable-length encoded unsigned integer.</summary>
    public bool WriteVLE(uint value)
    {
        Span<byte> data = stackalloc byte[5];
        var numBytes = 0;
        while (value >= 0x80u)
        {
            data[numBytes++] = (byte)((value & 0x7fu) | 0x80u);
            value >>= 7;
        }

        data[numBytes++] = (byte)value;
        return Write(data[..numBytes]) == numBytes;
    }

    /// <summary>Write a text line. Char codes 13 &amp; 10 will be automatically appended.</summary>
    public bool WriteLine(string value)
    {
        var success = true;
        var bytes = Encoding.UTF8.GetBytes(value);
        success &= Write(bytes) == bytes.Length;
        success &= WriteUByte(13);
        success &= WriteUByte(10);
        return success;
    }

}
