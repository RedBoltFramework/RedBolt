namespace RedBolt;

public record struct ResourceRef
{
    public StringHash Type;
    public string Name;

    public ResourceRef()
    {
        Type = default;
        Name = string.Empty;
    }

    public ResourceRef(StringHash type)
        : this(type, string.Empty)
    {
    }

    public ResourceRef(StringHash type, string name)
    {
        Type = type;
        Name = name;
    }

    public ResourceRef(string type, string name)
        : this(new StringHash(type), name)
    {
    }

    public static ResourceRef Empty { get; } = new();

    public static bool IsNullOrEmpty(ResourceRef resourceRef)
    {
        return resourceRef.Type.IsEmpty() || string.IsNullOrEmpty(resourceRef.Name);
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
        var type = uint.TryParse(typePart, out var parsedType) ? new StringHash(parsedType) : new StringHash(typePart);
        return new ResourceRef(type, namePart);
    }

    public uint ToHash()
    {
        unchecked
        {
            var result = 0u;
            CombineHash(ref result, Type.ToHash());
            CombineHash(ref result, StringHash.Calculate(Name));
            return result;
        }
    }

    public override string ToString() => $"{Type.Reverse()};{Name}";

    public string ToString(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{context.GetTypeName(Type)};{Name}";
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
