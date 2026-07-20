using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public delegate void ArchiveValueSerializer<T>(Archive archive, string name, ref T value);

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

    public static void SerializeValue(Archive archive, string name, ref Vector2f value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Vector2d value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Vector3d value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Matrix3 value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Matrix4 value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Rect value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Quaternion value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Color value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Vector2i value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref Vector3i value) =>
        SerializeCompound(archive, name, ref value);

    public static void SerializeValue(Archive archive, string name, ref IntRect value) =>
        SerializeCompound(archive, name, ref value);

    private static string NumberArrayToString(ReadOnlySpan<float> values)
    {
        var result = new StringBuilder();
        for (var i = 0; i < values.Length; ++i)
        {
            if (i > 0)
                result.Append(' ');
            result.Append(values[i].ToString("g6", CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    private static string NumberArrayToString(ReadOnlySpan<double> values)
    {
        var result = new StringBuilder();
        for (var i = 0; i < values.Length; ++i)
        {
            if (i > 0)
                result.Append(' ');
            result.Append(values[i].ToString("g15", CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    private static string NumberArrayToString(ReadOnlySpan<int> values)
    {
        var result = new StringBuilder();
        for (var i = 0; i < values.Length; ++i)
        {
            if (i > 0)
                result.Append(' ');
            result.Append(values[i].ToString(CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    private static int StringToNumberArray(string text, Span<float> values)
    {
        var elements = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var size = Math.Min(elements.Length, values.Length);
        for (var i = 0; i < size; ++i)
        {
            if (!float.TryParse(elements[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                values[i] = 0.0f;
        }
        return elements.Length;
    }

    private static int StringToNumberArray(string text, Span<double> values)
    {
        var elements = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var size = Math.Min(elements.Length, values.Length);
        for (var i = 0; i < size; ++i)
        {
            if (!double.TryParse(elements[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                values[i] = 0.0;
        }
        return elements.Length;
    }

    private static int StringToNumberArray(string text, Span<int> values)
    {
        var elements = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var size = Math.Min(elements.Length, values.Length);
        for (var i = 0; i < size; ++i)
        {
            if (!int.TryParse(elements[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                values[i] = 0;
        }
        return elements.Length;
    }

    public static void SerializeEnum<T>(Archive archive, string name, ref T value, IReadOnlyList<string> names)
        where T : struct, Enum
    {
        if (archive.IsHumanReadable)
        {
            var underlyingType = Enum.GetUnderlyingType(typeof(T));
            var isSigned = underlyingType == typeof(sbyte) || underlyingType == typeof(short)
                || underlyingType == typeof(int) || underlyingType == typeof(long);
            var signedValue = isSigned ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0;
            var unsignedValue = isSigned ? 0 : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            var stringValue = string.Empty;
            if (!archive.IsInput)
            {
                if (isSigned)
                    stringValue = signedValue < 0 || signedValue >= names.Count
                        ? signedValue.ToString(CultureInfo.InvariantCulture)
                        : names[(int)signedValue];
                else
                    stringValue = unsignedValue >= (ulong)names.Count
                        ? unsignedValue.ToString(CultureInfo.InvariantCulture)
                        : names[(int)unsignedValue];
            }
            archive.Serialize(name, ref stringValue);
            if (archive.IsInput)
            {
                var index = 0;
                while (index < names.Count && !string.Equals(names[index], stringValue, StringComparison.Ordinal))
                    ++index;
                if (index < names.Count)
                    value = (T)Enum.ToObject(typeof(T), index);
                else
                {
                    var parsedValue = ulong.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var result) && result != ulong.MaxValue ? result : 0;
                    value = (T)Enum.ToObject(typeof(T), parsedValue);
                }
            }
        }
        else
        {
            object integerValue;
            var underlyingType = Enum.GetUnderlyingType(typeof(T));
            if (underlyingType == typeof(sbyte))
            {
                var convertedValue = Convert.ToSByte(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }
            else if (underlyingType == typeof(byte))
            {
                var convertedValue = Convert.ToByte(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }
            else if (underlyingType == typeof(short))
            {
                var convertedValue = Convert.ToInt16(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }
            else if (underlyingType == typeof(ushort))
            {
                var convertedValue = Convert.ToUInt16(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }
            else if (underlyingType == typeof(int))
            {
                var convertedValue = Convert.ToInt32(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }
            else if (underlyingType == typeof(uint))
            {
                var convertedValue = Convert.ToUInt32(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }
            else if (underlyingType == typeof(long))
            {
                var convertedValue = Convert.ToInt64(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }
            else
            {
                var convertedValue = Convert.ToUInt64(value);
                archive.Serialize(name, ref convertedValue);
                integerValue = convertedValue;
            }

            if (archive.IsInput)
                value = (T)Enum.ToObject(typeof(T), integerValue);
        }
    }

    /// <summary>Serialize optional element or block.</summary>
    public static void SerializeStrictlyOptionalValue<T>(Archive archive, string name, ref T value,
        T defaultValue, ArchiveValueSerializer<T> serializeValue)
    {
        var loading = archive.IsInput;

        if (!archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            using var block = archive.OpenUnorderedBlock(name);

            var initialized = !loading && !EqualityComparer<T>.Default.Equals(defaultValue, value);
            SerializeValue(archive, "initialized", ref initialized);

            if (initialized)
                serializeValue(archive, "value", ref value);
            else if (loading)
                value = defaultValue;
        }
        else
        {
            var initialized = loading
                ? archive.HasElementOrBlock(name)
                : !EqualityComparer<T>.Default.Equals(defaultValue, value);
            if (initialized)
                serializeValue(archive, name, ref value);
            else if (loading)
                value = defaultValue;
        }
    }

    /// <summary>Serialize element or block that's optional if archive type supports it.</summary>
    /// <remarks>There's no overhead on optionality if Archive doesn't support optional blocks.</remarks>
    public static void SerializeOptionalValue<T>(Archive archive, string name, ref T value,
        T defaultValue, ArchiveValueSerializer<T> serializeValue)
    {
        if (!archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            serializeValue(archive, name, ref value);
            return;
        }

        var loading = archive.IsInput;
        var initialized = loading
            ? archive.HasElementOrBlock(name)
            : !EqualityComparer<T>.Default.Equals(defaultValue, value);
        if (initialized)
            serializeValue(archive, name, ref value);
        else if (loading)
            value = defaultValue;
    }

    /// <summary>Serialize pair type.</summary>
    public static void SerializeValue<T, U>(Archive archive, string name, ref (T First, U Second) value,
        ArchiveValueSerializer<T> serializeFirst, ArchiveValueSerializer<U> serializeSecond)
    {
        using var block = archive.OpenUnorderedBlock(name);
        serializeFirst(archive, "first", ref value.First);
        serializeSecond(archive, "second", ref value.Second);
    }

    /// <summary>Serialize optional value.</summary>
    public static void SerializeValue<T>(Archive archive, string name, ref T? value,
        ArchiveValueSerializer<T> serializeValue)
        where T : struct
    {
        using var block = archive.OpenUnorderedBlock(name);

        var hasValue = value.HasValue;
        SerializeValue(archive, "hasValue", ref hasValue);

        if (archive.IsInput)
        {
            if (hasValue)
            {
                var serializedValue = value.GetValueOrDefault();
                serializeValue(archive, "value", ref serializedValue);
                value = serializedValue;
            }
            else
                value = null;
        }
        else if (hasValue)
        {
            var serializedValue = value.GetValueOrDefault();
            serializeValue(archive, "value", ref serializedValue);
        }
    }

    public static void SerializeValue(Archive archive, string name, ref VariantType value)
    {
        SerializeEnum(archive, name, ref value, Variant.GetTypeNameList());
    }

    public static void SerializeVariantInBlock(Archive archive, ref Variant value)
    {
        var type = value.Type;
        SerializeEnum(archive, "type", ref type, Variant.GetTypeNameList());
        SerializeVariantAsType(archive, "value", ref value, type);
    }

    public static void SerializeValue(Archive archive, string name, ref Variant value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        SerializeVariantInBlock(archive, ref value);
    }

    public static void SerializeValue(Archive archive, string name, ref ResourceRef value)
    {
        if (!archive.IsHumanReadable)
        {
            using var block = archive.OpenUnorderedBlock(name);
            SerializeValue(archive, "type", ref value.Type);
            archive.Serialize("name", ref value.Name);
            return;
        }

        var stringValue = archive.IsInput ? string.Empty : value.ToString(archive.Context!);
        archive.Serialize(name, ref stringValue);
        if (archive.IsInput)
        {
            var chunks = stringValue.Split(';', StringSplitOptions.None);
            if (chunks.Length != 2)
                throw new InvalidOperationException($"Unexpected format of ResourceRef '{archive.CurrentBlockPath}/{name}'");
            value = new ResourceRef(new StringHash(chunks[0]), chunks[1]);
        }
    }

    public static void SerializeValue(Archive archive, string name, ref ResourceRefList value)
    {
        if (!archive.IsHumanReadable)
        {
            using var block = archive.OpenUnorderedBlock(name);
            SerializeValue(archive, "type", ref value.Type);
            SerializeStringVector(archive, "names", value.Names);
            return;
        }

        var stringValue = archive.IsInput ? string.Empty : value.ToString(archive.Context!);
        archive.Serialize(name, ref stringValue);
        if (archive.IsInput)
        {
            if (stringValue.Length == 0)
                throw new InvalidOperationException($"Unexpected format of ResourceRefList '{archive.CurrentBlockPath}/{name}'");

            var chunks = stringValue.Split(';', StringSplitOptions.None);
            var names = new List<string>(chunks.Length - 1);
            for (var i = 1; i < chunks.Length; ++i)
                names.Add(chunks[i]);
            if (names.Count == 1 && names[0].Length == 0)
                names.Clear();
            value = new ResourceRefList(new StringHash(chunks[0]), names);
        }
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
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetVector2(), static item => new Variant(item));
            return;
        case VariantType.DoubleVector2:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetDoubleVector2(), static item => new Variant(item));
            return;
        case VariantType.Vector3:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetVector3(), static item => new Variant(item));
            return;
        case VariantType.DoubleVector3:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetDoubleVector3(), static item => new Variant(item));
            return;
        case VariantType.Vector4:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetVector4(), static item => new Variant(item));
            return;
        case VariantType.Quaternion:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetQuaternion(), static item => new Variant(item));
            return;
        case VariantType.Color:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetColor(), static item => new Variant(item));
            return;
        case VariantType.Rect:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetRect(), static item => new Variant(item));
            return;
        case VariantType.IntRect:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetIntRect(), static item => new Variant(item));
            return;
        case VariantType.Vector2i:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetIntVector2(), static item => new Variant(item));
            return;
        case VariantType.Vector3i:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetIntVector3(), static item => new Variant(item));
            return;
        case VariantType.Matrix3:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetMatrix3(), static item => new Variant(item));
            return;
        case VariantType.Matrix3x4:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetMatrix3x4(), static item => new Variant(item));
            return;
        case VariantType.Matrix4:
            SerializeVariantCompound(archive, name, ref value, static variant => variant.GetMatrix4(), static item => new Variant(item));
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
            if (value.Type != VariantType.Custom)
                throw new InvalidOperationException("Cannot serialize CustomVariant of unknown type");
            value.GetCustomVariantValue().Serialize(archive, name);
            return;
        case VariantType.VoidPtr:
        case VariantType.Ptr:
            throw new InvalidOperationException($"'{archive.CurrentBlockPath}/{name}' has unsupported variant type");
        default:
            throw new InvalidOperationException($"Archive serialization for Variant type '{type}' is not implemented.");
        }
    }

    private static void SerializeResourceRef(Archive archive, string name, ref Variant value)
    {
        var resourceRef = archive.IsInput ? ResourceRef.Empty : value.GetResourceRef();
        SerializeValue(archive, name, ref resourceRef);
        if (archive.IsInput)
        {
            var replacement = new Variant(resourceRef);
            value.Dispose();
            value = replacement;
        }
    }

    private static void SerializeVariantCurve(Archive archive, string name, ref Variant value)
    {
        if (archive.IsInput)
        {
            using var curve = new VariantCurve();
            using var block = archive.OpenUnorderedBlock(name);
            curve.SerializeInBlock(archive);
            var replacement = new Variant(curve);
            value.Dispose();
            value = replacement;
        }
        else
        {
            var curve = value.GetVariantCurve();
            using var block = archive.OpenUnorderedBlock(name);
            curve.SerializeInBlock(archive);
        }
    }

    private static void SerializeBuffer(Archive archive, string name, ref Variant value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        var replaceBuffer = archive.IsInput && value.Type != VariantType.Buffer;
        var buffer = replaceBuffer ? Array.Empty<byte>() : value.GetBuffer();
        var size = archive.IsInput ? 0u : checked((uint)buffer.Length);
        archive.SerializeVle("size", ref size);
        if (archive.IsInput && buffer.Length != checked((int)size))
        {
            buffer = new byte[checked((int)size)];
            replaceBuffer = true;
        }
        archive.SerializeBytes("data", buffer);
        if (replaceBuffer)
        {
            var replacement = new Variant(buffer);
            value.Dispose();
            value = replacement;
        }
    }

    private static void SerializeVariantVector(Archive archive, string name, ref Variant value)
    {
        if (archive.IsInput && value.Type != VariantType.VariantVector)
        {
            using var empty = new VariantVector();
            var replacement = new Variant(empty);
            value.Dispose();
            value = replacement;
        }

        SerializeValue(archive, name, value.GetVariantVector());
    }

    private static void SerializeVariantStringVector(Archive archive, string name, ref Variant value)
    {
        if (archive.IsInput && value.Type != VariantType.StringVector)
        {
            var replacement = new Variant(new StringVector());
            value.Dispose();
            value = replacement;
        }

        SerializeValue(archive, name, value.GetStringVector());
    }

    private static void SerializeVariantMap(Archive archive, string name, ref Variant value)
    {
        if (archive.IsInput && value.Type != VariantType.VariantMap)
        {
            using var empty = new VariantMap();
            var replacement = new Variant(empty);
            value.Dispose();
            value = replacement;
        }

        SerializeValue(archive, name, value.GetVariantMap());
    }

    private static void SerializeStringVariantMap(Archive archive, string name, ref Variant value)
    {
        if (archive.IsInput && value.Type != VariantType.StringVariantMap)
        {
            using var empty = new StringVariantMap();
            var replacement = new Variant(empty);
            value.Dispose();
            value = replacement;
        }

        SerializeValue(archive, name, value.GetStringVariantMap());
    }

    private static void SerializeCString(Archive archive, string name, ref Variant value)
    {
        var stringValue = archive.IsInput ? string.Empty : value.GetCString().ConvertToString();
        archive.Serialize(name, ref stringValue);
        if (archive.IsInput)
        {
            using var cstring = new CString(stringValue);
            var replacement = new Variant(cstring);
            value.Dispose();
            value = replacement;
        }
    }

    private static void SerializeResourceRefList(Archive archive, string name, ref Variant value)
    {
        var resourceRefList = archive.IsInput ? ResourceRefList.Empty : value.GetResourceRefList();
        SerializeValue(archive, name, ref resourceRefList);
        if (archive.IsInput)
        {
            var replacement = new Variant(resourceRefList);
            value.Dispose();
            value = replacement;
        }
    }

    public static void SerializeValue(Archive archive, string name, StringVector value)
    {
        SerializeStringVector(archive, name, value);
    }

    public static void SerializeValue(Archive archive, string name, VariantVector value)
    {
        var size = checked((uint)value.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            value.Dispose();
            for (var index = 0u; index < block.SizeHint; ++index)
            {
                var item = default(Variant);
                using var itemBlock = archive.OpenUnorderedBlock("element");
                SerializeVariantInBlock(archive, ref item);
                value.Add(item);
            }
        }
        else
        {
            foreach (var item in value)
            {
                var itemCopy = item;
                using var itemBlock = archive.OpenUnorderedBlock("element");
                SerializeVariantInBlock(archive, ref itemCopy);
            }
        }
    }

    public static void SerializeValue(Archive archive, string name, VariantMap value)
    {
        var size = checked((uint)value.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            value.Dispose();
            for (var index = 0u; index < block.SizeHint; ++index)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var key = StringHash.Empty;
                var item = default(Variant);
                SerializeValue(archive, "key", ref key);
                SerializeVariantInBlock(archive, ref item);
                if (value.TryGetValue(key, out var previous))
                    previous.Dispose();
                value[key] = item;
            }
        }
        else
        {
            foreach (var (key, item) in value)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var keyCopy = key;
                var itemCopy = item;
                SerializeValue(archive, "key", ref keyCopy);
                SerializeVariantInBlock(archive, ref itemCopy);
            }
        }
    }

    public static void SerializeValue(Archive archive, string name, StringVariantMap value)
    {
        var size = checked((uint)value.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            value.Dispose();
            for (var index = 0u; index < block.SizeHint; ++index)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var key = string.Empty;
                var item = default(Variant);
                archive.Serialize("key", ref key);
                SerializeVariantInBlock(archive, ref item);
                if (value.TryGetValue(key, out var previous))
                    previous.Dispose();
                value[key] = item;
            }
        }
        else
        {
            foreach (var (key, item) in value)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var keyCopy = key;
                var itemCopy = item;
                archive.Serialize("key", ref keyCopy);
                SerializeVariantInBlock(archive, ref itemCopy);
            }
        }
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
        var value = archive.IsInput
            ? typeof(T) == typeof(string) ? (T)(object)string.Empty : default!
            : getter(variant);
        SerializePrimitive(archive, name, ref value);
        if (archive.IsInput)
        {
            var replacement = constructor(value);
            variant.Dispose();
            variant = replacement;
        }
    }

    private static void SerializeVariantCompound<T>(Archive archive, string name, ref Variant variant,
        Func<Variant, T> getter, Func<T, Variant> constructor)
        where T : unmanaged
    {
        T value;
        if (!archive.IsInput)
            value = getter(variant);
        else if (typeof(T) == typeof(Quaternion))
            value = (T)(object)Quaternion.IDENTITY;
        else if (typeof(T) == typeof(Color))
            value = (T)(object)Color.White;
        else if (typeof(T) == typeof(Matrix3))
            value = (T)(object)Matrix3.IDENTITY;
        else if (typeof(T) == typeof(Matrix3x4))
            value = (T)(object)Matrix3x4.IDENTITY;
        else if (typeof(T) == typeof(Matrix4))
            value = (T)(object)Matrix4.IDENTITY;
        else
            value = default;

        SerializeCompound(archive, name, ref value);
        if (archive.IsInput)
        {
            var replacement = constructor(value);
            variant.Dispose();
            variant = replacement;
        }
    }

    private static void SerializeCompound<T>(Archive archive, string name, ref T value)
        where T : unmanaged
    {
        if (!archive.IsHumanReadable)
        {
            archive.SerializeBytes(name, MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)));
            return;
        }

        var values = MemoryMarshal.CreateSpan(ref value, 1);
        var stringValue = string.Empty;
        if (!archive.IsInput)
        {
            if (typeof(T) == typeof(Vector2d) || typeof(T) == typeof(Vector3d))
                stringValue = NumberArrayToString(MemoryMarshal.Cast<T, double>(values));
            else if (typeof(T) == typeof(Vector2i) || typeof(T) == typeof(Vector3i) || typeof(T) == typeof(IntRect))
                stringValue = NumberArrayToString(MemoryMarshal.Cast<T, int>(values));
            else
                stringValue = NumberArrayToString(MemoryMarshal.Cast<T, float>(values));
        }
        archive.Serialize(name, ref stringValue);
        if (archive.IsInput)
        {
            if (typeof(T) == typeof(Vector2d) || typeof(T) == typeof(Vector3d))
                StringToNumberArray(stringValue, MemoryMarshal.Cast<T, double>(values));
            else if (typeof(T) == typeof(Vector2i) || typeof(T) == typeof(Vector3i) || typeof(T) == typeof(IntRect))
                StringToNumberArray(stringValue, MemoryMarshal.Cast<T, int>(values));
            else
                StringToNumberArray(stringValue, MemoryMarshal.Cast<T, float>(values));
        }
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

    /// <summary>
    /// Wrapper that consumes ArchiveException and converts it to boolean status.
    /// </summary>
    public static bool ConsumeArchiveException(Action action, bool errorOnException = true)
    {
        try
        {
            action();
            return true;
        }
        catch (ArchiveException exception)
        {
            if (errorOnException)
                Log.GetLogger().LogError("Serialization error: {Message}", exception.Message);
            else
                Log.GetLogger().LogDebug("Archive cannot be serialization: {Message}", exception.Message);
            return false;
        }
    }

}
