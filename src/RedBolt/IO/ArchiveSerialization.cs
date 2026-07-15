using System.Runtime.InteropServices;

namespace RedBolt;

public static partial class ArchiveSerialization
{
    public static void SerializeValue(Archive archive, string name, ref bool value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref sbyte value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref byte value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref short value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref ushort value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref int value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref uint value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref long value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref ulong value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref float value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref double value) => archive.Serialize(name, ref value);
    public static void SerializeValue(Archive archive, string name, ref string value) => archive.Serialize(name, ref value);

    public static void SerializeValue(Archive archive, string name, ref CString value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var managed = archive.IsInput ? string.Empty : value.ConvertToString();
        archive.Serialize(name, ref managed);
        if (archive.IsInput)
        {
            value.Dispose();
            value = new CString(managed);
        }
    }

    public static void SerializeValue(Archive archive, string name, ref StringHash value)
    {
        var rawValue = value.Value;
        archive.Serialize(name, ref rawValue);
        if (archive.IsInput)
            value = new StringHash(rawValue);
    }

    public static void SerializeEnum<T>(Archive archive, string name, ref T value, IReadOnlyList<string> names)
        where T : struct, Enum
    {
        if (archive.IsHumanReadable)
        {
            var stringValue = archive.IsInput ? string.Empty : names[Convert.ToInt32(value)];
            archive.Serialize(name, ref stringValue);
            if (archive.IsInput)
            {
                var index = 0;
                while (index < names.Count && !string.Equals(names[index], stringValue, StringComparison.Ordinal))
                    ++index;
                value = (T)Enum.ToObject(typeof(T), index >= names.Count ? 0 : index);
            }
        }
        else
        {
            var integerValue = Convert.ToInt32(value);
            archive.Serialize(name, ref integerValue);
            if (archive.IsInput)
                value = (T)Enum.ToObject(typeof(T), integerValue);
        }
    }

    public static void SerializeVariantInBlock(Archive archive, ref Variant value)
    {
        var type = value.Type;
        SerializeEnum(archive, "type", ref type, Variant.GetTypeNameList());
        SerializeVariantAsType(archive, "value", ref value, type);
    }

    public static void SerializeVariantAsType(Archive archive, string name, ref Variant value, VariantType type)
    {
        switch (type)
        {
        case VariantType.None:
            return;
        case VariantType.Int:
            SerializeVariantPrimitive(archive, name, ref value, static variant => variant.GetInt(), static item => new Variant(item));
            return;
        case VariantType.Bool:
            SerializeVariantPrimitive(archive, name, ref value, static variant => variant.GetBool(), static item => new Variant(item));
            return;
        case VariantType.Float:
            SerializeVariantPrimitive(archive, name, ref value, static variant => variant.GetFloat(), static item => new Variant(item));
            return;
        case VariantType.Double:
            SerializeVariantPrimitive(archive, name, ref value, static variant => variant.GetDouble(), static item => new Variant(item));
            return;
        case VariantType.Int64:
            SerializeVariantPrimitive(archive, name, ref value, static variant => variant.GetInt64(), static item => new Variant(item));
            return;
        case VariantType.String:
            SerializeVariantPrimitive(archive, name, ref value, static variant => variant.GetString(), static item => new Variant(item));
            return;
        case VariantType.CString:
            SerializeCString(archive, name, ref value);
            return;
        case VariantType.ResourceRef:
            SerializeResourceRef(archive, name, ref value);
            return;
        case VariantType.ResourceRefList:
            SerializeResourceRefList(archive, name, ref value);
            return;
        case VariantType.Vector2:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetVector2(), static item => new Variant(item), static text => ParseVariant(VariantType.Vector2, text).GetVector2());
            return;
        case VariantType.DoubleVector2:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetDoubleVector2(), static item => new Variant(item), static text => ParseVariant(VariantType.DoubleVector2, text).GetDoubleVector2());
            return;
        case VariantType.Vector3:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetVector3(), static item => new Variant(item), static text => ParseVariant(VariantType.Vector3, text).GetVector3());
            return;
        case VariantType.DoubleVector3:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetDoubleVector3(), static item => new Variant(item), static text => ParseVariant(VariantType.DoubleVector3, text).GetDoubleVector3());
            return;
        case VariantType.Vector4:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetVector4(), static item => new Variant(item), static text => ParseVariant(VariantType.Vector4, text).GetVector4());
            return;
        case VariantType.Quaternion:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetQuaternion(), static item => new Variant(item), static text => ParseVariant(VariantType.Quaternion, text).GetQuaternion());
            return;
        case VariantType.Color:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetColor(), static item => new Variant(item), static text => ParseVariant(VariantType.Color, text).GetColor());
            return;
        case VariantType.Rect:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetRect(), static item => new Variant(item), static text => ParseVariant(VariantType.Rect, text).GetRect());
            return;
        case VariantType.IntRect:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetIntRect(), static item => new Variant(item), static text => ParseVariant(VariantType.IntRect, text).GetIntRect());
            return;
        case VariantType.Vector2i:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetIntVector2(), static item => new Variant(item), static text => ParseVariant(VariantType.Vector2i, text).GetIntVector2());
            return;
        case VariantType.Vector3i:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetIntVector3(), static item => new Variant(item), static text => ParseVariant(VariantType.Vector3i, text).GetIntVector3());
            return;
        case VariantType.Matrix3:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetMatrix3(), static item => new Variant(item), static text => ParseVariant(VariantType.Matrix3, text).GetMatrix3());
            return;
        case VariantType.Matrix3x4:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetMatrix3x4(), static item => new Variant(item), static text => ParseVariant(VariantType.Matrix3x4, text).GetMatrix3x4());
            return;
        case VariantType.Matrix4:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetMatrix4(), static item => new Variant(item), static text => ParseVariant(VariantType.Matrix4, text).GetMatrix4());
            return;
        case VariantType.VariantCurve:
            SerializeVariantCurve(archive, name, ref value);
            return;
        case VariantType.Buffer:
            SerializeBuffer(archive, name, ref value);
            return;
        case VariantType.VariantVector:
            SerializeVariantVector(archive, name, ref value);
            return;
        case VariantType.StringVector:
            SerializeVariantStringVector(archive, name, ref value);
            return;
        case VariantType.VariantMap:
            SerializeVariantMap(archive, name, ref value);
            return;
        case VariantType.StringVariantMap:
            SerializeStringVariantMap(archive, name, ref value);
            return;
        case VariantType.Custom:
            value.GetCustomVariantValue().Serialize(archive, name);
            return;
        default:
            throw new NotSupportedException($"Archive serialization for Variant type '{type}' is not implemented.");
        }
    }

    public static void SerializeResourceRef(Archive archive, string name, ref Variant value)
    {
        var resourceRef = archive.IsInput ? ResourceRef.Empty : value.GetResourceRef();
        using var block = archive.OpenUnorderedBlock(name);
        var type = resourceRef.Type;
        var resourceName = resourceRef.Name;
        SerializeValue(archive, "type", ref type);
        archive.Serialize("name", ref resourceName);
        if (archive.IsInput)
            value = new Variant(new ResourceRef(type, resourceName));
    }

    public static void SerializeVariantCurve(Archive archive, string name, ref Variant value)
    {
        var curve = archive.IsInput ? new VariantCurve() : value.GetVariantCurve();
        using var block = archive.OpenUnorderedBlock(name);
        curve.SerializeInBlock(archive);
        if (archive.IsInput)
            value = new Variant(curve);
    }

    public static void SerializeBuffer(Archive archive, string name, ref Variant value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        var buffer = archive.IsInput ? Array.Empty<byte>() : value.GetBuffer();
        var size = archive.IsInput ? 0u : checked((uint)buffer.Length);
        archive.SerializeVle("size", ref size);
        if (archive.IsInput)
            buffer = new byte[checked((int)size)];
        archive.SerializeBytes("data", buffer);
        if (archive.IsInput)
            value = new Variant(buffer);
    }

    public static void SerializeVariantVector(Archive archive, string name, ref Variant value)
    {
        var vector = archive.IsInput ? new VariantVector() : value.GetVariantVector();
        var size = checked((uint)vector.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            vector.Clear();
            for (var index = 0u; index < block.SizeHint; ++index)
            {
                var item = default(Variant);
                using var itemBlock = archive.OpenUnorderedBlock("element");
                SerializeVariantInBlock(archive, ref item);
                vector.Add(item);
            }
            value = new Variant(vector);
        }
        else
        {
            foreach (var item in vector)
            {
                var itemCopy = item;
                using var itemBlock = archive.OpenUnorderedBlock("element");
                SerializeVariantInBlock(archive, ref itemCopy);
            }
        }
    }

    public static void SerializeVariantStringVector(Archive archive, string name, ref Variant value)
    {
        var vector = archive.IsInput ? new StringVector() : value.GetStringVector();
        SerializeStringVector(archive, name, vector);
        if (archive.IsInput)
            value = new Variant(vector);
    }

    public static void SerializeVariantMap(Archive archive, string name, ref Variant value)
    {
        var map = archive.IsInput ? new VariantMap() : value.GetVariantMap();
        var size = checked((uint)map.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            map.Clear();
            for (var index = 0u; index < block.SizeHint; ++index)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var key = StringHash.Empty;
                var item = default(Variant);
                SerializeValue(archive, "key", ref key);
                SerializeVariantInBlock(archive, ref item);
                map[key] = item;
            }
            value = new Variant(map);
        }
        else
        {
            foreach (var (key, item) in map)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var keyCopy = key;
                var itemCopy = item;
                SerializeValue(archive, "key", ref keyCopy);
                SerializeVariantInBlock(archive, ref itemCopy);
            }
        }
    }

    public static void SerializeStringVariantMap(Archive archive, string name, ref Variant value)
    {
        var map = archive.IsInput ? new StringVariantMap() : value.GetStringVariantMap();
        var size = checked((uint)map.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            map.Clear();
            for (var index = 0u; index < block.SizeHint; ++index)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var key = string.Empty;
                var item = default(Variant);
                archive.Serialize("key", ref key);
                SerializeVariantInBlock(archive, ref item);
                map[key] = item;
            }
            value = new Variant(map);
        }
        else
        {
            foreach (var (key, item) in map)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var keyCopy = key;
                var itemCopy = item;
                archive.Serialize("key", ref keyCopy);
                SerializeVariantInBlock(archive, ref itemCopy);
            }
        }
    }

    public static void SerializeCString(Archive archive, string name, ref Variant value)
    {
        var stringValue = archive.IsInput ? string.Empty : value.GetCString().ConvertToString();
        archive.Serialize(name, ref stringValue);
        if (archive.IsInput)
            value = new Variant(new CString(stringValue));
    }

    public static void SerializeResourceRefList(Archive archive, string name, ref Variant value)
    {
        var resourceRefList = archive.IsInput ? ResourceRefList.Empty : value.GetResourceRefList();
        using var block = archive.OpenUnorderedBlock(name);
        var type = resourceRefList.Type;
        SerializeValue(archive, "type", ref type);
        var names = archive.IsInput ? new List<string>() : resourceRefList.Names.ToList();
        SerializeStringVector(archive, "names", names);
        if (archive.IsInput)
            value = new Variant(new ResourceRefList(type, names));
    }

    public static void SerializeStringVector(Archive archive, string name, List<string> values)
    {
        var size = checked((uint)values.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            values.Clear();
            for (var index = 0u; index < block.SizeHint; ++index)
            {
                var value = string.Empty;
                archive.Serialize("element", ref value);
                values.Add(value);
            }
        }
        else
        {
            foreach (var value in values)
            {
                var item = value;
                archive.Serialize("element", ref item);
            }
        }
    }

    private static void SerializeVariantPrimitive<T>(Archive archive, string name, ref Variant variant,
        Func<Variant, T> getter, Func<T, Variant> constructor)
    {
        var value = archive.IsInput ? default! : getter(variant);
        SerializePrimitive(archive, name, ref value);
        if (archive.IsInput)
            variant = constructor(value);
    }

    private static void SerializeVariantCompound<T>(Archive archive, string name, ref Variant variant,
        Func<Variant, T> getter, Func<T, Variant> constructor, Func<string, T> parser)
        where T : unmanaged
    {
        var value = archive.IsInput ? default : getter(variant);
        SerializeCompound(archive, name, ref value, parser);
        if (archive.IsInput)
            variant = constructor(value);
    }

    private static void SerializeCompound<T>(Archive archive, string name, ref T value, Func<string, T> parser)
        where T : unmanaged
    {
        if (!archive.IsHumanReadable)
        {
            archive.SerializeBytes(name, MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)));
            return;
        }

        var stringValue = archive.IsInput ? string.Empty : value.ToString()!;
        archive.Serialize(name, ref stringValue);
        if (archive.IsInput)
            value = parser(stringValue);
    }

    private static void SerializePrimitive<T>(Archive archive, string name, ref T value)
    {
        if (typeof(T) == typeof(bool))
        {
            var boolValue = (bool)(object)value!;
            archive.Serialize(name, ref boolValue);
            value = (T)(object)boolValue;
        }
        else if (typeof(T) == typeof(int))
        {
            var intValue = (int)(object)value!;
            archive.Serialize(name, ref intValue);
            value = (T)(object)intValue;
        }
        else if (typeof(T) == typeof(long))
        {
            var longValue = (long)(object)value!;
            archive.Serialize(name, ref longValue);
            value = (T)(object)longValue;
        }
        else if (typeof(T) == typeof(float))
        {
            var floatValue = (float)(object)value!;
            archive.Serialize(name, ref floatValue);
            value = (T)(object)floatValue;
        }
        else if (typeof(T) == typeof(double))
        {
            var doubleValue = (double)(object)value!;
            archive.Serialize(name, ref doubleValue);
            value = (T)(object)doubleValue;
        }
        else if (typeof(T) == typeof(string))
        {
            var stringValue = (string)(object)value! ?? string.Empty;
            archive.Serialize(name, ref stringValue);
            value = (T)(object)stringValue;
        }
        else
        {
            throw new NotSupportedException($"Archive serialization for '{typeof(T).Name}' is not implemented.");
        }
    }

    private static Variant ParseVariant(VariantType type, string text)
    {
        var result = default(Variant);
        result.FromString(type, text);
        return result;
    }
}
