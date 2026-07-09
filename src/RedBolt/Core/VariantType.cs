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
    private readonly VariantType _type;
    private readonly bool _boolValue;
    private readonly int _intValue;
    private readonly float _floatValue;
    private readonly Vector2 _vector2Value;
    private readonly Vector3 _vector3Value;
    private readonly Vector4 _vector4Value;
    private readonly Quaternion _quaternionValue;
    private readonly Color _colorValue;
    private readonly IntRect _intRectValue;
    private readonly IntVector2 _intVector2Value;
    private readonly IntVector3 _intVector3Value;
    private readonly string? _stringValue;

    public Variant(bool value)
    {
        _type = VariantType.Bool;
        _boolValue = value;
    }

    public Variant(int value)
    {
        _type = VariantType.Int;
        _intValue = value;
    }

    public Variant(float value)
    {
        _type = VariantType.Float;
        _floatValue = value;
    }

    public Variant(Vector2 value)
    {
        _type = VariantType.Vector2;
        _vector2Value = value;
    }

    public Variant(Vector3 value)
    {
        _type = VariantType.Vector3;
        _vector3Value = value;
    }

    public Variant(Vector4 value)
    {
        _type = VariantType.Vector4;
        _vector4Value = value;
    }

    public Variant(Quaternion value)
    {
        _type = VariantType.Quaternion;
        _quaternionValue = value;
    }

    public Variant(Color value)
    {
        _type = VariantType.Color;
        _colorValue = value;
    }

    public Variant(IntRect value)
    {
        _type = VariantType.IntRect;
        _intRectValue = value;
    }

    public Variant(IntVector2 value)
    {
        _type = VariantType.IntVector2;
        _intVector2Value = value;
    }

    public Variant(IntVector3 value)
    {
        _type = VariantType.IntVector3;
        _intVector3Value = value;
    }

    public Variant(string? value)
    {
        if (value is null)
            return;

        _type = VariantType.String;
        _stringValue = value;
    }

    public VariantType Type => _type;

    public bool IsEmpty => _type == VariantType.None;

    public bool GetBool() => _type == VariantType.Bool ? _boolValue : throw CreateInvalidCastException(VariantType.Bool);

    public int GetInt() => _type == VariantType.Int ? _intValue : throw CreateInvalidCastException(VariantType.Int);

    public float GetFloat() => _type == VariantType.Float ? _floatValue : throw CreateInvalidCastException(VariantType.Float);

    public Vector2 GetVector2() => _type == VariantType.Vector2 ? _vector2Value : throw CreateInvalidCastException(VariantType.Vector2);

    public Vector3 GetVector3() => _type == VariantType.Vector3 ? _vector3Value : throw CreateInvalidCastException(VariantType.Vector3);

    public Vector4 GetVector4() => _type == VariantType.Vector4 ? _vector4Value : throw CreateInvalidCastException(VariantType.Vector4);

    public Quaternion GetQuaternion() => _type == VariantType.Quaternion ? _quaternionValue : throw CreateInvalidCastException(VariantType.Quaternion);

    public Color GetColor() => _type == VariantType.Color ? _colorValue : throw CreateInvalidCastException(VariantType.Color);

    public IntRect GetIntRect() => _type == VariantType.IntRect ? _intRectValue : throw CreateInvalidCastException(VariantType.IntRect);

    public IntVector2 GetIntVector2() => _type == VariantType.IntVector2 ? _intVector2Value : throw CreateInvalidCastException(VariantType.IntVector2);

    public IntVector3 GetIntVector3() => _type == VariantType.IntVector3 ? _intVector3Value : throw CreateInvalidCastException(VariantType.IntVector3);

    public string GetString() => _type == VariantType.String ? _stringValue ?? string.Empty : throw CreateInvalidCastException(VariantType.String);

    public object? GetObject()
    {
        return _type switch
        {
            VariantType.None => null,
            VariantType.Bool => _boolValue,
            VariantType.Int => _intValue,
            VariantType.Float => _floatValue,
            VariantType.Vector2 => _vector2Value,
            VariantType.Vector3 => _vector3Value,
            VariantType.Vector4 => _vector4Value,
            VariantType.Quaternion => _quaternionValue,
            VariantType.Color => _colorValue,
            VariantType.IntRect => _intRectValue,
            VariantType.IntVector2 => _intVector2Value,
            VariantType.IntVector3 => _intVector3Value,
            VariantType.String => _stringValue ?? string.Empty,
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
        if (_type != other._type)
            return false;

        return _type switch
        {
            VariantType.None => true,
            VariantType.Bool => _boolValue == other._boolValue,
            VariantType.Int => _intValue == other._intValue,
            VariantType.Float => _floatValue.Equals(other._floatValue),
            VariantType.Vector2 => _vector2Value.Equals(other._vector2Value),
            VariantType.Vector3 => _vector3Value.Equals(other._vector3Value),
            VariantType.Vector4 => _vector4Value.Equals(other._vector4Value),
            VariantType.Quaternion => _quaternionValue.Equals(other._quaternionValue),
            VariantType.Color => _colorValue.Equals(other._colorValue),
            VariantType.IntRect => _intRectValue.Equals(other._intRectValue),
            VariantType.IntVector2 => _intVector2Value.Equals(other._intVector2Value),
            VariantType.IntVector3 => _intVector3Value.Equals(other._intVector3Value),
            VariantType.String => string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal),
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is Variant other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_type);
        switch (_type)
        {
        case VariantType.Bool:
            hash.Add(_boolValue);
            break;
        case VariantType.Int:
            hash.Add(_intValue);
            break;
        case VariantType.Float:
            hash.Add(_floatValue);
            break;
        case VariantType.Vector2:
            hash.Add(_vector2Value);
            break;
        case VariantType.Vector3:
            hash.Add(_vector3Value);
            break;
        case VariantType.Vector4:
            hash.Add(_vector4Value);
            break;
        case VariantType.Quaternion:
            hash.Add(_quaternionValue);
            break;
        case VariantType.Color:
            hash.Add(_colorValue);
            break;
        case VariantType.IntRect:
            hash.Add(_intRectValue);
            break;
        case VariantType.IntVector2:
            hash.Add(_intVector2Value);
            break;
        case VariantType.IntVector3:
            hash.Add(_intVector3Value);
            break;
        case VariantType.String:
            hash.Add(_stringValue, StringComparer.Ordinal);
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
        return new InvalidCastException($"Variant contains {_type} instead of {expectedType}.");
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
