namespace RedBolt;

public sealed class ResourceRefList : IEquatable<ResourceRefList>
{
    private readonly List<string> names = [];

    public ResourceRefList(uint type)
        : this(type, [])
    {
    }

    public ResourceRefList(uint type, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        Type = type;
        this.names.AddRange(names);
    }

    public static ResourceRefList Empty { get; } = new(0u);

    public IReadOnlyList<string> Names => names;

    public uint Type { get; }

    public static bool IsNullOrEmpty(ResourceRefList? resourceRefList)
    {
        return resourceRefList is null || resourceRefList.Type == 0u || resourceRefList.Names.Count == 0;
    }

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

    public uint ToHash()
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = (hash ^ Type) * 16777619u;
            foreach (var name in names)
                hash = (hash ^ StringHash.Calculate(name)) * 16777619u;
            return hash;
        }
    }

    public bool Equals(ResourceRefList? other)
    {
        return other is not null && Type == other.Type && names.SequenceEqual(other.names);
    }

    public override bool Equals(object? obj)
    {
        return obj is ResourceRefList other && Equals(other);
    }

    public override int GetHashCode() => unchecked((int)ToHash());

    public override string ToString()
    {
        return Type == 0u ? string.Join(',', names) : $"{Type};{string.Join(',', names)}";
    }
}
