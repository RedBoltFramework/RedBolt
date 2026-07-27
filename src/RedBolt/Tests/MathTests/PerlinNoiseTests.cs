using SystemMath = global::System.Math;
using System.Reflection;

using RedBolt.Math;

namespace RedBolt.Tests.MathTests;

public class PerlinNoiseTests
{
    [Fact]
    public void Class_shape_and_copy_match_native_value_ownership()
    {
        Assert.True(typeof(PerlinNoise).IsClass);
        Assert.False(typeof(PerlinNoise).IsSealed);
        Assert.Equal(typeof(uint), typeof(PerlinNoise).GetField(nameof(PerlinNoise.NumPer))!.FieldType);
        Assert.Null(typeof(PerlinNoise).GetConstructor(Type.EmptyTypes));
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(PerlinNoise)));

        var source = new PerlinNoise(new RandomEngine(42));
        var clone = Assert.IsType<PerlinNoise>(((ICloneable)source).Clone());
        var field = typeof(PerlinNoise).GetField("_p", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var sourcePermutation = (int[])field.GetValue(source)!;
        var clonePermutation = (int[])field.GetValue(clone)!;

        Assert.NotSame(sourcePermutation, clonePermutation);
        Assert.Equal(sourcePermutation, clonePermutation);
        clonePermutation[0] = -1;
        Assert.NotEqual(sourcePermutation[0], clonePermutation[0]);
    }

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

    [Fact]
    public void Native_one_dimensional_range_is_preserved()
    {
        var max = double.MinValue;
        var min = double.MaxValue;

        var randomEngine = new RandomEngine(0);
        var noise = new PerlinNoise(randomEngine);

        for (uint i = 0; i < 1024; ++i)
        {
            var value = noise.GetDouble(i * 1.1);
            min = SystemMath.Min(min, value);
            max = SystemMath.Max(max, value);
        }

        Assert.True(min >= 0.0);
        Assert.True(max <= 1.0);
    }

    [Fact]
    public void Native_three_dimensional_range_is_preserved()
    {
        var max = double.MinValue;
        var min = double.MaxValue;

        var randomEngine = new RandomEngine(0);
        var noise = new PerlinNoise(randomEngine);

        for (uint x = 0; x < 100; ++x)
        {
            for (uint y = 0; y < 100; ++y)
            {
                for (uint z = 0; z < 100; ++z)
                {
                    var value = noise.GetDouble(x * 1.1, y * 1.1, z * 1.1);
                    min = SystemMath.Min(min, value);
                    max = SystemMath.Max(max, value);
                }
            }
        }

        Assert.True(min >= 0.0);
        Assert.True(max <= 1.0);
    }
}
