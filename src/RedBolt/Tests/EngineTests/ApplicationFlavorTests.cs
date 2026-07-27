using RedBolt;

namespace RedBolt.Tests;

public sealed class ApplicationFlavorTests
{
    [Fact]
    public void Copies_have_independent_component_containers()
    {
        var components = new ApplicationFlavorMap
        {
            ["platform"] = new ApplicationFlavorComponent { "desktop", "linux" }
        };
        var flavor = new ApplicationFlavor(components);
        var flavorCopy = flavor.Clone();
        var pattern = new ApplicationFlavorPattern(components);
        var patternCopy = pattern.Clone();

        components["platform"].Add("source");
        flavorCopy.Components["platform"].Add("copy");
        patternCopy.Components["platform"].Add("copy");

        Assert.DoesNotContain("source", flavor.Components["platform"]);
        Assert.DoesNotContain("copy", flavor.Components["platform"]);
        Assert.DoesNotContain("source", pattern.Components["platform"]);
        Assert.DoesNotContain("copy", pattern.Components["platform"]);

        var universal = ApplicationFlavor.Universal;
        universal.Components["*"].Add("changed");
        Assert.DoesNotContain("changed", ApplicationFlavor.Universal.Components["*"]);
    }

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
