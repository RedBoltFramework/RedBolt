using RedBolt.Container;
using RedBolt.Core;

namespace RedBolt.Resource;

public struct ResourceRefList : IEquatable<ResourceRefList>, ICloneable
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

    public static ResourceRefList Parse(string? value)
    {
        if (value is null)
            return Empty;

        var values = value.Split(';', StringSplitOptions.None);
        if (values.Length == 0)
            return Empty;

        var result = new ResourceRefList(new StringHash(values[0]));
        for (var i = 1; i < values.Length; ++i)
            result.Names.Add(values[i]);
        return result;
    }

    public readonly ResourceRefList Clone() => new(Type, Names);

    readonly object ICloneable.Clone() => Clone();

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

    public static bool operator ==(ResourceRefList left, ResourceRefList right) => left.Equals(right);

    public static bool operator !=(ResourceRefList left, ResourceRefList right) => !left.Equals(right);

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
