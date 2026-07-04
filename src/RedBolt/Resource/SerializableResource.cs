using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedBolt;

public class SerializableResource : Resource
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = true
    };

    private static readonly Lazy<Dictionary<string, Type>> SerializableTypes = new(BuildSerializableTypeMap);

    static SerializableResource()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public SerializableResource(string name)
        : base(name)
    {
    }

    public Serializable? Value { get; private set; }

    public Serializable? GetValue() => Value;

    public void SetValue(Serializable? serializable)
    {
        Value = serializable;
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, leaveOpen: true);
        var text = reader.ReadToEnd();
        var payload = JsonSerializer.Deserialize<SerializedSerializable>(text, SerializerOptions);
        if (payload is null || string.IsNullOrEmpty(payload.TypeName))
            return false;

        if (!SerializableTypes.Value.TryGetValue(payload.TypeName, out var type))
            return false;

        if (Activator.CreateInstance(type) is not Serializable serializable)
            return false;

        serializable.SetTemporary(payload.Temporary);
        foreach (var attribute in payload.Attributes)
            serializable.SetAttribute(attribute.Name, DeserializeValue(attribute.Value));
        serializable.ApplyAttributes();
        Value = serializable;
        return true;
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var payload = Serialize(Value);
        JsonSerializer.Serialize(destination, payload, SerializerOptions);
        return true;
    }

    private static Dictionary<string, Type> BuildSerializableTypeMap()
    {
        var result = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in typeof(Serializable).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(Serializable).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
                continue;

            result[type.Name] = type;
            if (!string.IsNullOrEmpty(type.FullName))
                result[type.FullName] = type;
            if (!string.IsNullOrEmpty(type.AssemblyQualifiedName))
                result[type.AssemblyQualifiedName] = type;
        }

        return result;
    }

    private static SerializedSerializable Serialize(Serializable? serializable)
    {
        if (serializable is null)
            return new SerializedSerializable();

        var payload = new SerializedSerializable
        {
            TypeName = serializable.GetType().FullName ?? serializable.GetType().Name,
            Temporary = serializable.IsTemporary
        };

        foreach (var attribute in serializable.Attributes)
        {
            if (!attribute.ShouldSave)
                continue;

            payload.Attributes.Add(new SerializedAttribute
            {
                Name = attribute.Name,
                Value = SerializeValue(serializable.GetAttribute(attribute.Name), attribute.ValueType)
            });
        }

        return payload;
    }

    private static SerializedValue? SerializeValue(object? value, Type? declaredType = null)
    {
        if (value is null)
            return null;

        var type = value.GetType();
        if (declaredType is not null && declaredType != typeof(object) && !declaredType.IsInterface && !declaredType.IsAssignableFrom(type))
            type = declaredType;

        return new SerializedValue
        {
            TypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            Json = JsonSerializer.Serialize(value, type, SerializerOptions)
        };
    }

    private static object? DeserializeValue(SerializedValue? value)
    {
        if (value is null || string.IsNullOrEmpty(value.TypeName))
            return null;

        var type = Type.GetType(value.TypeName, throwOnError: false);
        return type is null ? null : JsonSerializer.Deserialize(value.Json ?? "null", type, SerializerOptions);
    }

    private sealed class SerializedSerializable
    {
        public string TypeName { get; set; } = string.Empty;
        public bool Temporary { get; set; }
        public List<SerializedAttribute> Attributes { get; set; } = [];
    }

    private sealed class SerializedAttribute
    {
        public string Name { get; set; } = string.Empty;
        public SerializedValue? Value { get; set; }
    }

    private sealed class SerializedValue
    {
        public string TypeName { get; set; } = string.Empty;
        public string Json { get; set; } = "null";
    }
}
