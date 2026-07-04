namespace RedBolt;

public readonly record struct StringHash(uint Value)
{
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

    public static implicit operator StringHash(string value) => new(Calculate(value));

    public override string ToString() => Value.ToString();
}
