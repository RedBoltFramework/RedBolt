using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

public sealed class MathDefsTests
{
    [Fact]
    public void Interpolation_helpers_work()
    {
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.InverseLerp(10.0f, 20.0f, 15.0f), 0.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.SmoothStep(0.0f, 1.0f, 0.5f), 0.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.Smooth(0.0f, 10.0f, 1.0f), 5.0f));
    }

    [Fact]
    public void Trigonometry_uses_degrees()
    {
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.Sin(90.0f), 1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.Cos(180.0f), -1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.Atan2(1.0f, 0.0f), 90.0f));
    }

    [Fact]
    public void Modulo_and_snapping_work()
    {
        Assert.Equal(9, MathDefs.AbsMod(-1, 10));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.SnapRound(2.25f, 0.5f), 2.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.RoundToNearestMultiple(-7.0f, 4.0f), -8.0f));
    }

    [Fact]
    public void Bit_helpers_work()
    {
        Assert.True(MathDefs.IsPowerOfTwo(8u));
        Assert.False(MathDefs.IsPowerOfTwo(0u));
        Assert.Equal(8u, MathDefs.NextPowerOfTwo(5u));
        Assert.Equal(4u, MathDefs.ClosestPowerOfTwo(6u));
        Assert.Equal(4u, MathDefs.LogBaseTwo(16u));
        Assert.Equal(3u, MathDefs.CountSetBits(0b1011u));
    }

    [Fact]
    public void Scalar_utility_helpers_work()
    {
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.ExponentialDecay(1.0f), 0.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.Wrap(12.0f, 0.0f, 10.0f), 2.0f));
        Assert.Equal(65u, MathDefs.SDBMHash(0u, (byte)'A'));
    }

    [Fact]
    public void Half_float_conversion_roundtrips()
    {
        Assert.Equal(0x3c00, MathDefs.FloatToHalf(1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.HalfToFloat(0x3c00), 1.0f));
        Assert.Equal(0, MathDefs.FloatToHalf(0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(MathDefs.HalfToFloat(0), 0.0f));
    }

    [Fact]
    public void Easing_endpoints_return_bounds()
    {
        Assert.True(MathDefs.ApproximatelyEquivalent(EaseMath.BounceInOut(0.0f), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(EaseMath.BounceInOut(1.0f), 1.0f));
    }
}
