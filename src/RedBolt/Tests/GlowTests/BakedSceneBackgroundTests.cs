using RedBolt.Glow;
using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class BakedSceneBackgroundTests
{
    [Fact]
    public void Background_defaults_to_white_with_zero_intensity()
    {
        var background = new BakedSceneBackground();

        Assert.Equal(Color.White, background.Color);
        Assert.Equal(0.0f, background.Intensity);
        Assert.Null(background.Image);
        Assert.Equal(Vector3f.Zero, background.SampleLinear(Vector3f.Forward));
    }

    [Fact]
    public void Background_samples_fallback_color_in_linear_space()
    {
        var background = new BakedSceneBackground
        {
            Intensity = 2.0f,
            Color = new Color(0.25f, 0.5f, 0.75f)
        };

        Assert.Equal(2.0f * background.Color.GammaToLinear().ToVector3(),
            background.SampleLinear(Vector3f.Forward));
    }
}
