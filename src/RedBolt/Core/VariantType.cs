using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedBolt;

public enum VariantType
{
    None = 0,
    Bool,
    Int,
    Float,
    Vector2,
    Vector3,
    Vector4,
    Quaternion,
    Color,
    IntRect,
    IntVector2,
    IntVector3,
    String
}

public readonly struct Variant : IEquatable<Variant>
{
    private readonly VariantType type;
    private readonly bool boolValue;
    private readonly int intValue;
    private readonly float floatValue;
    private readonly Vector2 vector2Value;
    private readonly Vector3 vector3Value;
    private readonly Vector4 vector4Value;
    private readonly Quaternion quaternionValue;
    private readonly Color colorValue;
    private readonly IntRect intRectValue;
    private readonly IntVector2 intVector2Value;
    private readonly IntVector3 intVector3Value;
    private readonly string? stringValue;

    public Variant(bool value)
    {
        type = VariantType.Bool;
        boolValue = value;
    }

    public Variant(int value)
    {
        type = VariantType.Int;
        intValue = value;
    }

    public Variant(float value)
    {
        type = VariantType.Float;
        floatValue = value;
    }

    public Variant(Vector2 value)
    {
        type = VariantType.Vector2;
        vector2Value = value;
    }

    public Variant(Vector3 value)
    {
        type = VariantType.Vector3;
        vector3Value = value;
    }

    public Variant(Vector4 value)
    {
        type = VariantType.Vector4;
        vector4Value = value;
    }

    public Variant(Quaternion value)
    {
        type = VariantType.Quaternion;
        quaternionValue = value;
    }

    public Variant(Color value)
    {
        type = VariantType.Color;
        colorValue = value;
    }

    public Variant(IntRect value)
    {
        type = VariantType.IntRect;
        intRectValue = value;
    }

    public Variant(IntVector2 value)
    {
        type = VariantType.IntVector2;
        intVector2Value = value;
    }

    public Variant(IntVector3 value)
    {
        type = VariantType.IntVector3;
        intVector3Value = value;
    }

    public Variant(string? value)
    {
        if (value is null)
            return;

        type = VariantType.String;
        stringValue = value;
    }

    public VariantType Type => type;

    public bool IsEmpty => type == VariantType.None;

    public bool GetBool() => type == VariantType.Bool ? boolValue : throw CreateInvalidCastException(VariantType.Bool);

    public int GetInt() => type == VariantType.Int ? intValue : throw CreateInvalidCastException(VariantType.Int);

    public float GetFloat() => type == VariantType.Float ? floatValue : throw CreateInvalidCastException(VariantType.Float);

    public Vector2 GetVector2() => type == VariantType.Vector2 ? vector2Value : throw CreateInvalidCastException(VariantType.Vector2);

    public Vector3 GetVector3() => type == VariantType.Vector3 ? vector3Value : throw CreateInvalidCastException(VariantType.Vector3);

    public Vector4 GetVector4() => type == VariantType.Vector4 ? vector4Value : throw CreateInvalidCastException(VariantType.Vector4);

    public Quaternion GetQuaternion() => type == VariantType.Quaternion ? quaternionValue : throw CreateInvalidCastException(VariantType.Quaternion);

    public Color GetColor() => type == VariantType.Color ? colorValue : throw CreateInvalidCastException(VariantType.Color);

    public IntRect GetIntRect() => type == VariantType.IntRect ? intRectValue : throw CreateInvalidCastException(VariantType.IntRect);

    public IntVector2 GetIntVector2() => type == VariantType.IntVector2 ? intVector2Value : throw CreateInvalidCastException(VariantType.IntVector2);

    public IntVector3 GetIntVector3() => type == VariantType.IntVector3 ? intVector3Value : throw CreateInvalidCastException(VariantType.IntVector3);

    public string GetString() => type == VariantType.String ? stringValue ?? string.Empty : throw CreateInvalidCastException(VariantType.String);

    public object? GetObject()
    {
        return type switch
        {
            VariantType.None => null,
            VariantType.Bool => boolValue,
            VariantType.Int => intValue,
            VariantType.Float => floatValue,
            VariantType.Vector2 => vector2Value,
            VariantType.Vector3 => vector3Value,
            VariantType.Vector4 => vector4Value,
            VariantType.Quaternion => quaternionValue,
            VariantType.Color => colorValue,
            VariantType.IntRect => intRectValue,
            VariantType.IntVector2 => intVector2Value,
            VariantType.IntVector3 => intVector3Value,
            VariantType.String => stringValue ?? string.Empty,
            _ => null
        };
    }

    public static Variant FromObject(object? value)
    {
        return value switch
        {
            null => default,
            Variant variant => variant,
            bool typedValue => typedValue,
            int typedValue => typedValue,
            float typedValue => typedValue,
            Vector2 typedValue => typedValue,
            Vector3 typedValue => typedValue,
            Vector4 typedValue => typedValue,
            Quaternion typedValue => typedValue,
            Color typedValue => typedValue,
            IntRect typedValue => typedValue,
            IntVector2 typedValue => typedValue,
            IntVector3 typedValue => typedValue,
            string typedValue => typedValue,
            _ => throw new ArgumentException($"Unsupported Variant value type '{value.GetType().Name}'.", nameof(value))
        };
    }

    public bool Equals(Variant other)
    {
        if (type != other.type)
            return false;

        return type switch
        {
            VariantType.None => true,
            VariantType.Bool => boolValue == other.boolValue,
            VariantType.Int => intValue == other.intValue,
            VariantType.Float => floatValue.Equals(other.floatValue),
            VariantType.Vector2 => vector2Value.Equals(other.vector2Value),
            VariantType.Vector3 => vector3Value.Equals(other.vector3Value),
            VariantType.Vector4 => vector4Value.Equals(other.vector4Value),
            VariantType.Quaternion => quaternionValue.Equals(other.quaternionValue),
            VariantType.Color => colorValue.Equals(other.colorValue),
            VariantType.IntRect => intRectValue.Equals(other.intRectValue),
            VariantType.IntVector2 => intVector2Value.Equals(other.intVector2Value),
            VariantType.IntVector3 => intVector3Value.Equals(other.intVector3Value),
            VariantType.String => string.Equals(stringValue, other.stringValue, StringComparison.Ordinal),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is Variant other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(type);
        switch (type)
        {
        case VariantType.Bool:
            hash.Add(boolValue);
            break;
        case VariantType.Int:
            hash.Add(intValue);
            break;
        case VariantType.Float:
            hash.Add(floatValue);
            break;
        case VariantType.Vector2:
            hash.Add(vector2Value);
            break;
        case VariantType.Vector3:
            hash.Add(vector3Value);
            break;
        case VariantType.Vector4:
            hash.Add(vector4Value);
            break;
        case VariantType.Quaternion:
            hash.Add(quaternionValue);
            break;
        case VariantType.Color:
            hash.Add(colorValue);
            break;
        case VariantType.IntRect:
            hash.Add(intRectValue);
            break;
        case VariantType.IntVector2:
            hash.Add(intVector2Value);
            break;
        case VariantType.IntVector3:
            hash.Add(intVector3Value);
            break;
        case VariantType.String:
            hash.Add(stringValue, StringComparer.Ordinal);
            break;
        }

        return hash.ToHashCode();
    }

    public override string ToString() => GetObject()?.ToString() ?? string.Empty;

    public static implicit operator Variant(bool value) => new(value);
    public static implicit operator Variant(int value) => new(value);
    public static implicit operator Variant(float value) => new(value);
    public static implicit operator Variant(Vector2 value) => new(value);
    public static implicit operator Variant(Vector3 value) => new(value);
    public static implicit operator Variant(Vector4 value) => new(value);
    public static implicit operator Variant(Quaternion value) => new(value);
    public static implicit operator Variant(Color value) => new(value);
    public static implicit operator Variant(IntRect value) => new(value);
    public static implicit operator Variant(IntVector2 value) => new(value);
    public static implicit operator Variant(IntVector3 value) => new(value);
    public static implicit operator Variant(string value) => new(value);

    private InvalidCastException CreateInvalidCastException(VariantType expectedType)
    {
        return new InvalidCastException($"Variant contains {type} instead of {expectedType}.");
    }
}

public sealed class VariantMap : Dictionary<StringHash, Variant>
{
    public VariantMap()
    {
    }

    public VariantMap(IDictionary<StringHash, Variant> dictionary)
        : base(dictionary)
    {
    }
}

public static class VariantTypeUtility
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = false
    };

    static VariantTypeUtility()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static VariantType GetVariantType(object? value)
    {
        return value switch
        {
            null => VariantType.None,
            Variant variant => variant.Type,
            bool => VariantType.Bool,
            int => VariantType.Int,
            float => VariantType.Float,
            Vector2 => VariantType.Vector2,
            Vector3 => VariantType.Vector3,
            Vector4 => VariantType.Vector4,
            Quaternion => VariantType.Quaternion,
            Color => VariantType.Color,
            IntRect => VariantType.IntRect,
            IntVector2 => VariantType.IntVector2,
            IntVector3 => VariantType.IntVector3,
            string => VariantType.String,
            _ => VariantType.None
        };
    }

    public static bool IsInterpolatable(VariantType type)
    {
        return type is VariantType.Float or VariantType.Vector2 or VariantType.Vector3 or VariantType.Vector4 or VariantType.Quaternion or VariantType.Color or VariantType.IntRect or VariantType.IntVector2 or VariantType.IntVector3;
    }

    public static string Serialize(object? value, VariantType type)
    {
        if (value is Variant variant)
            value = variant.GetObject();

        return JsonSerializer.Serialize(value, GetClrType(type), SerializerOptions);
    }

    public static string Serialize(Variant value)
    {
        return Serialize(value.GetObject(), value.Type);
    }

    public static object? Deserialize(string json, VariantType type)
    {
        return JsonSerializer.Deserialize(json, GetClrType(type), SerializerOptions);
    }

    public static Variant DeserializeVariant(string json, VariantType type)
    {
        return Variant.FromObject(Deserialize(json, type));
    }

    public static Type GetClrType(VariantType type)
    {
        return type switch
        {
            VariantType.Bool => typeof(bool),
            VariantType.Int => typeof(int),
            VariantType.Float => typeof(float),
            VariantType.Vector2 => typeof(Vector2),
            VariantType.Vector3 => typeof(Vector3),
            VariantType.Vector4 => typeof(Vector4),
            VariantType.Quaternion => typeof(Quaternion),
            VariantType.Color => typeof(Color),
            VariantType.IntRect => typeof(IntRect),
            VariantType.IntVector2 => typeof(IntVector2),
            VariantType.IntVector3 => typeof(IntVector3),
            VariantType.String => typeof(string),
            _ => typeof(object)
        };
    }
}
