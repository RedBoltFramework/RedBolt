using RedBolt;

namespace RedBolt.Tests;

public sealed class ApplicationFlavorTests
{
    [Fact]
    public void Flavor_parser_rejects_invalid_components_and_matches_universal_distance()
    {
        var pattern = new ApplicationFlavorPattern("platform=desktop,linux;quality=high");
        var flavor = new ApplicationFlavor("platform=desktop,linux;quality=high,fast");

        Assert.Equal(1u, flavor.Matches(pattern));
        Assert.Equal(uint.MaxValue, ApplicationFlavor.Universal.Matches(pattern));

        var invalid = new ApplicationFlavorPattern("platform;quality=high");
        Assert.Single(invalid.Components);
        Assert.Equal("platform=desktop,linux;quality=fast,high", flavor.ToString());
    }
}
