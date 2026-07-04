namespace RedBolt;

public readonly record struct ResourceRef(uint Type, string Name)
{
    public static ResourceRef Empty { get; } = new(0u, string.Empty);

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
            return new ResourceRef(0u, value.Trim());

        var typePart = value[..separator].Trim();
        var namePart = value[(separator + 1)..].Trim();
        var type = uint.TryParse(typePart, out var parsedType) ? parsedType : StringHash.Calculate(typePart);
        return new ResourceRef(type, namePart);
    }

    public override string ToString()
    {
        return Type == 0u ? Name : $"{Type};{Name}";
    }
}
