using RedBolt;
using System.Reflection;

namespace RedBolt.Tests;

public sealed class GraphicsLifetimeTests
{
    [Fact]
    public void Window_settings_clone_has_independent_orientation_storage()
    {
        var orientations = new List<string> { "Portrait" };
        var source = new WindowSettings
        {
            Size = new Vector2i(640, 480),
            Orientations = orientations
        };
        var cloneMethod = typeof(Graphics).GetMethod("CloneWindowSettings", BindingFlags.NonPublic | BindingFlags.Static);

        var clone = Assert.IsType<WindowSettings>(cloneMethod!.Invoke(null, [source]));
        orientations[0] = "LandscapeLeft";
        source.Size = new Vector2i(1, 1);

        Assert.Equal(new Vector2i(640, 480), clone.Size);
        Assert.Equal("Portrait", Assert.Single(clone.Orientations));
    }
}
