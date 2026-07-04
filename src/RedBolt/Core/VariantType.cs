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

public sealed class VariantMap : Dictionary<StringHash, object?>
{
    public VariantMap()
    {
    }

    public VariantMap(IDictionary<StringHash, object?> dictionary)
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
        return JsonSerializer.Serialize(value, GetClrType(type), SerializerOptions);
    }

    public static object? Deserialize(string json, VariantType type)
    {
        return JsonSerializer.Deserialize(json, GetClrType(type), SerializerOptions);
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
