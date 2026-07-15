namespace RedBolt;

public struct ResourceRefList : IEquatable<ResourceRefList>
{
    public StringHash Type;
    public List<string> Names;

    public ResourceRefList()
    {
        Type = default;
        Names = [];
    }

    public ResourceRefList(StringHash type)
        : this(type, [])
    {
    }

    public ResourceRefList(string type)
        : this(new StringHash(type), [])
    {
    }

    public ResourceRefList(StringHash type, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        Type = type;
        Names = [.. names];
    }

    public ResourceRefList(string type, IEnumerable<string> names)
        : this(new StringHash(type), names)
    {
    }

    public static ResourceRefList Empty { get; } = new();

    public static bool IsNullOrEmpty(ResourceRefList resourceRefList)
    {
        return resourceRefList.Type.IsEmpty() || resourceRefList.Names.Count == 0;
    }

    public static ResourceRefList Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        var parts = value.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
            return Empty;

        var type = uint.TryParse(parts[0], out var parsedType) ? new StringHash(parsedType) : new StringHash(parts[0]);
        if (parts.Length == 1)
            return new ResourceRefList(type);

        return new ResourceRefList(type, parts[1..].Where(static name => !string.IsNullOrEmpty(name)));
    }

    public uint ToHash()
    {
        unchecked
        {
            var result = 0u;
            CombineHash(ref result, Type.ToHash());
            foreach (var name in Names)
                CombineHash(ref result, StringHash.Calculate(name));
            return result;
        }
    }

    public bool Equals(ResourceRefList other)
    {
        return Type == other.Type && ManagedSequence.Equal(Names, other.Names);
    }

    public override bool Equals(object? obj) => obj is ResourceRefList other && Equals(other);

    public override int GetHashCode() => unchecked((int)ToHash());

    public override string ToString() => $"{Type.Reverse()};{string.Join(';', Names)}";

    public string ToString(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{context.GetTypeName(Type)};{string.Join(';', Names)}";
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
