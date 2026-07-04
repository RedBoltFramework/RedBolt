namespace RedBolt;

public readonly record struct StringHash(uint Value)
{
    public StringHash(string value)
        : this(Calculate(value))
    {
    }

    public StringHash(Type type)
        : this(Calculate(type.Name))
    {
    }

    public static uint Calculate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= char.ToLowerInvariant(character);
                hash *= 16777619u;
            }
            return hash;
        }
    }

    public static implicit operator uint(StringHash hash) => hash.Value;

    public static implicit operator StringHash(uint value) => new(value);

    public static implicit operator StringHash(string value) => new(value);

    public static implicit operator StringHash(Type type) => new(type);

    public override string ToString() => Value.ToString();
}
