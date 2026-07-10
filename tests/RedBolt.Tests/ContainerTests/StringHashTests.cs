using RedBolt;

namespace RedBolt.Tests;

public sealed class StringHashTests
{
    [Fact]
    public void From_string_matches_calculated()
    {
        Assert.Equal(StringHash.Calculate(nameof(StaticModel)), new StringHash(nameof(StaticModel)).Value);
    }

    [Fact]
    public void From_uint_preserves_value()
    {
        Assert.Equal(123u, ((StringHash)123u).Value);
    }

    [Fact]
    public void To_string_uses_hex_format()
    {
        Assert.Equal("0000007B", new StringHash(123u).ToString());
    }

    [Fact]
    public void Debug_reverse_uses_global_register()
    {
        var hash = new StringHash("StaticModel");

        Assert.Equal("StaticModel", hash.Reverse());
        Assert.Equal($"#{hash} 'StaticModel'", hash.ToDebugString());
    }

    [Fact]
    public void Register_can_register_and_lookup_strings()
    {
        var register = new StringHashRegister(threadSafe: false);
        var hash = register.RegisterString("Renderer.Debug");

        Assert.True(register.Contains(hash));
        Assert.Equal("Renderer.Debug", register.GetString(hash));
        Assert.Equal("Renderer.Debug", register.GetStringCopy(hash));
    }

    [Fact]
    public void No_reverse_constructor_does_not_populate_global_register()
    {
        var hash = new StringHash("NoReverse.Value", new StringHash.NoReverse());

        Assert.Equal(string.Empty, hash.Reverse());
    }

    [Fact]
    public void Mutable_value_and_bool_conversion_work()
    {
        var hash = new StringHash();
        Assert.False((bool)hash);

        hash.SetMutableValue(123u);

        Assert.True((bool)hash);
        Assert.Equal(123u, hash.MutableValue());
        Assert.Equal(123u, hash.Value);
    }

    [Fact]
    public void Calculate_with_length_matches_prefix()
    {
        var chars = "Renderer.Debug.Extra".ToCharArray();
        Assert.Equal(StringHash.Calculate("Renderer.Debug"), StringHash.Calculate(chars, 14u));
    }
}
