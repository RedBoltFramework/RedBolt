using RedBolt;

using RedBolt.Container;
using RedBolt.Graphics;
namespace RedBolt.Tests;

public sealed class ConstStringTests
{
    [Fact]
    public void Default_constructor_creates_empty_string()
    {
        var value = new ConstString();

        Assert.Equal(string.Empty, value.ToString());
        Assert.Equal(StringHash.Empty, value.GetHash());
    }

    [Fact]
    public void String_constructor_precomputes_hash()
    {
        var value = new ConstString(nameof(StaticModel));

        Assert.Equal(nameof(StaticModel), (string)value);
        Assert.Equal(new StringHash(nameof(StaticModel)), value.GetHash());
        Assert.Equal(value.GetHash(), (StringHash)value);
    }

    [Fact]
    public void Clone_returns_independent_wrapper_with_same_value()
    {
        var value = new ConstString(nameof(StaticModel));
        var clone = (ConstString)value.Clone();

        Assert.NotSame(value, clone);
        Assert.Equal((string)value, (string)clone);
        Assert.Equal(value.GetHash(), clone.GetHash());
    }
}
