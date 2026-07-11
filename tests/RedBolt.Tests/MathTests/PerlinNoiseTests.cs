namespace RedBolt.Tests.MathTests;

public class PerlinNoiseTests
{
    [Fact]
    public void FloatOverloadsMatchDoubleOverloads()
    {
        var noise = new PerlinNoise(new RandomEngine(42));

        Assert.Equal((float)noise.GetDouble(1.25), noise.Get(1.25f));
        Assert.Equal((float)noise.GetDouble(1.25, 2.5, -3.75), noise.Get(1.25f, 2.5f, -3.75f));
    }

    [Fact]
    public void RepeatMakesNoisePeriodic()
    {
        var noise = new PerlinNoise(new RandomEngine(42));

        Assert.Equal(noise.GetDouble(1.25, 8), noise.GetDouble(9.25, 8), 12);
        Assert.Equal(noise.GetDouble(1.25, 2.5, -3.75, 8), noise.GetDouble(9.25, 10.5, 4.25, 8), 12);
    }

    [Fact]
    public void IntegralCoordinatesHaveRbfxMidpointValue()
    {
        var noise = new PerlinNoise(new RandomEngine(42));

        Assert.Equal(0.5, noise.GetDouble(3.0), 12);
        Assert.Equal(0.5, noise.GetDouble(3.0, 4.0, 5.0), 12);
    }
}
