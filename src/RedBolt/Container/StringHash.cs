namespace RedBolt;

public struct StringHash : IComparable<StringHash>, IEquatable<StringHash>
{
    public readonly struct NoReverse
    {
    }

    private uint _value;

    public static readonly uint EmptyValue = Calculate(string.Empty);
    public static readonly StringHash Empty = new(EmptyValue);

    public StringHash()
        : this(EmptyValue)
    {
    }

    public StringHash(uint value)
    {
        _value = value;
    }

    public StringHash(string value, NoReverse noReverse)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = Calculate(value);
    }

    public StringHash(string value)
        : this(value, new NoReverse())
    {
        GetGlobalStringHashRegister().RegisterString(this, value);
    }

    public uint Value => _value;

    public bool IsEmpty() => _value == EmptyValue;

    public static explicit operator bool(StringHash hash) => !hash.IsEmpty();

    public string ToDebugString()
    {
        return $"#{ToString()} '{Reverse()}'";
    }

    public string Reverse()
    {
        return GetGlobalStringHashRegister().GetStringCopy(this);
    }

    public uint ToHash() => _value;

    public uint MutableValue()
    {
        return _value;
    }

    public void SetMutableValue(uint value)
    {
        _value = value;
    }

    public int CompareTo(StringHash other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString("X8");

    public override int GetHashCode() => (int)_value;

    public override bool Equals(object obj) => obj is StringHash other && Equals(other);

    public bool Equals(StringHash other) => _value == other._value;

    public static bool operator ==(StringHash lhs, StringHash rhs) => lhs._value == rhs._value;

    public static bool operator !=(StringHash lhs, StringHash rhs) => lhs._value != rhs._value;

    public static bool operator <(StringHash lhs, StringHash rhs) => lhs._value < rhs._value;

    public static bool operator >(StringHash lhs, StringHash rhs) => lhs._value > rhs._value;

    public static implicit operator uint(StringHash hash) => hash._value;

    public static implicit operator StringHash(uint value) => new(value);

    public static implicit operator StringHash(string value) => new(value);

    public static uint Calculate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Calculate(value.AsSpan());
    }

    public static uint Calculate(ReadOnlySpan<char> value)
    {
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

    public static uint Calculate(char[] data, uint length)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Calculate(data.AsSpan(0, checked((int)length)));
    }

    public static StringHashRegister GetGlobalStringHashRegister() => StringHashRegister.Instance;
}
