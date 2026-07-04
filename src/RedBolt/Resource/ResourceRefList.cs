namespace RedBolt;

public sealed class ResourceRefList
{
    private readonly List<string> names = [];

    public ResourceRefList(uint type, IEnumerable<string> names)
    {
        Type = type;
        this.names.AddRange(names);
    }

    public IReadOnlyList<string> Names => names;

    public uint Type { get; }

    public static ResourceRefList Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new ResourceRefList(0u, []);

        var parts = value.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
            return new ResourceRefList(0u, parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var type = uint.TryParse(parts[0], out var parsedType) ? parsedType : StringHash.Calculate(parts[0]);
        return new ResourceRefList(type, parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public override string ToString()
    {
        return Type == 0u ? string.Join(',', names) : $"{Type};{string.Join(',', names)}";
    }
}
