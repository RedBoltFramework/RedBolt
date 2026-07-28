namespace RedBolt.Tests;

using RedBolt;
using RedBolt.RenderAPI;

public sealed class GraphicsLifetimeTests
{
    [Fact]
    public void Window_settings_clone_has_independent_orientation_storage()
    {
        var source = new WindowSettings
        {
            Size = new Vector2i(640, 480),
            Orientations = ["Portrait"]
        };
        var clone = source;
        source.Orientations = ["LandscapeLeft"];
        source.Size = new Vector2i(1, 1);

        Assert.Equal(new Vector2i(640, 480), clone.Size);
        Assert.Equal("Portrait", Assert.Single(clone.Orientations));
    }
}
