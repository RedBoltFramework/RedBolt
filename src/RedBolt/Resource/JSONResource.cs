using System.Text.Json;

namespace RedBolt;

public abstract class JSONResource<TValue> : JSONResourceBase
    where TValue : new()
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true
    };

    protected JSONResource(string name)
        : base(name)
    {
    }

    public TValue? Value { get; set; } = new();

    public static JsonSerializerOptions JsonSerializerOptions => SerializerOptions;

    public override bool DeserializeJson(string jsonString)
    {
        try
        {
            Value = string.IsNullOrWhiteSpace(jsonString)
                ? default
                : JsonSerializer.Deserialize<TValue>(jsonString, SerializerOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public override string SerializeJson()
    {
        return Value is null
            ? string.Empty
            : JsonSerializer.Serialize(Value, SerializerOptions);
    }
}
