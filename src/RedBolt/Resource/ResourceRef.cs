namespace RedBolt;

public struct ResourceRef : IEquatable<ResourceRef>, ICloneable
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

    public static ResourceRef Parse(string? value)
    {
        if (value is null)
            return Empty;

        var values = value.Split(';');
        if (values.Length >= 2)
            return new ResourceRef(values[0], values[1]);
        if (values.Length != 0 && values[0].Length != 0)
            return new ResourceRef(new StringHash(values[0]));
        return Empty;
    }

    public readonly ResourceRef Clone() => new(Type, Name);

    readonly object ICloneable.Clone() => Clone();

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

    public readonly bool Equals(ResourceRef other) => Type == other.Type && Name == other.Name;

    public override readonly bool Equals(object? obj) => obj is ResourceRef other && Equals(other);

    public override readonly int GetHashCode() => unchecked((int)ToHash());

    public static bool operator ==(ResourceRef left, ResourceRef right) => left.Equals(right);

    public static bool operator !=(ResourceRef left, ResourceRef right) => !left.Equals(right);

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
