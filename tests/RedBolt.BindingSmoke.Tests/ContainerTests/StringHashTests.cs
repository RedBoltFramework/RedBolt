using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

public sealed class StringHashTests
{
    [Fact]
    public void From_type_matches_calculated()
    {
        Assert.Equal(StringHash.Calculate(nameof(StaticModel)), new StringHash(typeof(StaticModel)).Value);
    }

    [Fact]
    public void From_uint_preserves_value()
    {
        Assert.Equal(123u, ((StringHash)123u).Value);
    }
}
