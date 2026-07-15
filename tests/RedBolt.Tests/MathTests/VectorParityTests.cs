namespace RedBolt.Tests.MathTests;

public class VectorParityTests
{
    [Fact]
    public void NormalizedOrDefaultUsesConfiguredFallbackAndEpsilon()
    {
        var fallback2 = new Vector2f(2.0f, 3.0f);
        var fallback3 = new Vector3f(2.0f, 3.0f, 4.0f);

        Assert.Equal(fallback2, new Vector2f(0.01f, 0.0f).NormalizedOrDefault(fallback2, 0.1f));
        Assert.Equal(fallback3, new Vector3f(0.01f, 0.0f, 0.0f).NormalizedOrDefault(fallback3, 0.1f));
        Assert.True(new Vector2f(3.0f, 4.0f).NormalizedOrDefault().ApproximatelyEquivalent(new Vector2f(0.6f, 0.8f)));
    }

    [Fact]
    public void VectorFreeFunctionsMatchRbfxBehavior()
    {
        Assert.Equal(new Vector2f(2.0f, 3.0f), Vector2f.Sqrt(new Vector2f(4.0f, 9.0f)));
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), Vector3f.Sqrt(new Vector3f(4.0f, 9.0f, 16.0f)));

        var random2 = Vector2f.StableRandom(new Vector2f(1.0f, 2.0f));
        var random3 = Vector3f.StableRandom(new Vector3f(1.0f, 2.0f, 3.0f));
        Assert.InRange(random2, 0.0f, 1.0f);
        Assert.InRange(random3, 0.0f, 1.0f);
        Assert.Equal(random2, Vector2f.StableRandom(new Vector2f(1.0f, 2.0f)));
    }

    [Fact]
    public void IntegerVector3HasArrayHashOrderingAndConversionParity()
    {
        var value = new Vector3i([1, 2, 3]);

        Assert.True(value < new Vector3i(1, 2, 4));
        Assert.Equal(new Vector3i(1, 2, 3).ToHash(), value.ToHash());
        Assert.Equal(new Vector3i(1, 2, 3), Vector3i.Abs(new Vector3i(-1, -2, -3)));
        Assert.Equal(new Vector4f(1, 2, 3, 4), value.ToVector4(4));
    }

    [Fact]
    public void VectorArrayConstructorsAndQuaternionZeroMatchRbfx()
    {
        Assert.Equal(new Vector3f(1, 2, 3), new Vector3f([1, 2, 3]));
        Assert.Equal(new Quaternion(0, 0, 0, 0), Quaternion.Zero);
    }
}
