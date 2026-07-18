namespace Ea;

public partial class String
{
    public String(string? value)
        : this()
    {
        Assign(value);
    }

    public static implicit operator String(string? value)
        => new(value);

    public static implicit operator global::System.ReadOnlySpan<byte>(String value)
    {
        global::System.ArgumentNullException.ThrowIfNull(value);
        return value.AsUtf8Span();
    }
}
