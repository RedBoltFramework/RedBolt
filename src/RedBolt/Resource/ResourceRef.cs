namespace RedBolt;

public readonly record struct ResourceRef
{
    public ResourceRef(StringHash type)
        : this(type.Value, string.Empty)
    {
    }

    public ResourceRef(StringHash type, string name)
        : this(type.Value, name)
    {
    }

    public ResourceRef(string type, string name)
        : this(new StringHash(type).Value, name)
    {
    }

    public ResourceRef(uint type, string name)
    {
        Type = type;
        Name = name ?? string.Empty;
    }

    public static ResourceRef Empty { get; } = new(0u, string.Empty);

    public uint Type { get; init; }

    public string Name { get; init; }

    public static bool IsNullOrEmpty(ResourceRef resourceRef)
    {
        return resourceRef.Type == 0u || string.IsNullOrEmpty(resourceRef.Name);
    }

    public static ResourceRef Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Empty;

        var separator = value.IndexOf(';', StringComparison.Ordinal);
        if (separator < 0)
            return new ResourceRef(value.Trim(), string.Empty);

        var typePart = value[..separator].Trim();
        var namePart = value[(separator + 1)..].Trim();
        var type = uint.TryParse(typePart, out var parsedType) ? parsedType : new StringHash(typePart).Value;
        return new ResourceRef(type, namePart);
    }

    public uint ToHash()
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = (hash ^ Type) * 16777619u;
            hash = (hash ^ StringHash.Calculate(Name ?? string.Empty)) * 16777619u;
            return hash;
        }
    }

    public override string ToString()
    {
        var typeName = new StringHash(Type).Reverse();
        return $"{typeName};{Name}";
    }

    public string ToString(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{context.GetTypeName(new StringHash(Type))};{Name}";
    }
}
