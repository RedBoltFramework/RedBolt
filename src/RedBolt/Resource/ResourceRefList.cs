namespace RedBolt;

public sealed class ResourceRefList : IEquatable<ResourceRefList>
{
    private readonly List<string> _names = [];

    public ResourceRefList(StringHash type)
        : this(type.Value, [])
    {
    }

    public ResourceRefList(string type)
        : this(new StringHash(type).Value, [])
    {
    }

    public ResourceRefList(uint type)
        : this(type, [])
    {
    }

    public ResourceRefList(StringHash type, IEnumerable<string> names)
        : this(type.Value, names)
    {
    }

    public ResourceRefList(string type, IEnumerable<string> names)
        : this(new StringHash(type).Value, names)
    {
    }

    public ResourceRefList(uint type, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        Type = type;
        _names.AddRange(names);
    }

    public static ResourceRefList Empty { get; } = new(0u);

    public IReadOnlyList<string> Names => _names;

    public uint Type { get; }

    public static bool IsNullOrEmpty(ResourceRefList resourceRefList)
    {
        return resourceRefList is null || resourceRefList.Type == 0u || resourceRefList.Names.Count == 0;
    }

    public static ResourceRefList Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new ResourceRefList(0u, []);

        var parts = value.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
            return Empty;

        var type = uint.TryParse(parts[0], out var parsedType) ? parsedType : new StringHash(parts[0]).Value;
        if (parts.Length == 1)
            return new ResourceRefList(type);

        return new ResourceRefList(type, parts.Skip(1).Where(static name => !string.IsNullOrEmpty(name)));
    }

    public uint ToHash()
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = (hash ^ Type) * 16777619u;
            foreach (var name in _names)
                hash = (hash ^ StringHash.Calculate(name)) * 16777619u;
            return hash;
        }
    }

    public bool Equals(ResourceRefList other)
    {
        return other is not null && Type == other.Type && _names.SequenceEqual(other._names);
    }

    public override bool Equals(object obj)
    {
        return obj is ResourceRefList other && Equals(other);
    }

    public override int GetHashCode() => unchecked((int)ToHash());

    public override string ToString()
    {
        var typeName = new StringHash(Type).Reverse();
        if (string.IsNullOrEmpty(typeName))
            typeName = Type != 0u ? Type.ToString() : string.Empty;

        if (_names.Count == 0)
            return typeName;

        return string.IsNullOrEmpty(typeName)
            ? string.Join(';', _names)
            : $"{typeName};{string.Join(';', _names)}";
    }
}
