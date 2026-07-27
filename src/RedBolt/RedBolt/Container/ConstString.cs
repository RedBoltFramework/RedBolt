namespace RedBolt;

// C#: ConstString wraps string since System.String is sealed. Implicit operators bridge the gap.
/// <summary>
/// Immutable string with precomputed hash.
/// </summary>
/// <remarks>
/// Inherit it from string to allow implicit casts to ea::string without allocation.
/// It could be done with cast operator too, but this code would cause crash if
/// <c>operator const ea::string&amp;()</c> is used:
/// <c>const ea::string&amp; s2 = ConstString("text");</c>
/// </remarks>
public sealed class ConstString : ICloneable
{
    /// <summary>
    /// Construct an empty string.
    /// </summary>
    public ConstString()
        : this(string.Empty)
    {
    }

    /// <summary>
    /// Construct from a string.
    /// </summary>
    public ConstString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
        _hash = new StringHash(value);
    }

    /// <summary>
    /// Return hash.
    /// </summary>
    public StringHash GetHash() => _hash;

    public override string ToString() => _value;

    public object Clone() => new ConstString(_value);

    public static implicit operator StringHash(ConstString value) => value._hash;

    public static implicit operator string(ConstString value) => value._value;

    private readonly string _value;
    private readonly StringHash _hash;
}
