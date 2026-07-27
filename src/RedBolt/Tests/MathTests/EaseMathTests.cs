using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class EaseMathTests
{
    [Theory]
    [InlineData(0.0f, -0.0f)]
    [InlineData(0.25f, -0.064136565f)]
    [InlineData(0.5f, -0.087697506f)]
    [InlineData(0.75f, 0.1825903f)]
    [InlineData(1.0f, 1.0f)]
    public void BackIn(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.BackIn(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.8174097f)]
    [InlineData(0.5f, 1.0876975f)]
    [InlineData(0.75f, 1.0641365f)]
    [InlineData(1.0f, 1.0f)]
    public void BackOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.BackOut(time)));

    [Theory]
    [InlineData(0.0f, -0.0f)]
    [InlineData(0.25f, -0.09968184f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.75f, 1.0996819f)]
    [InlineData(1.0f, 1.0f)]
    public void BackInOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.BackInOut(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.47265625f)]
    [InlineData(0.5f, 0.765625f)]
    [InlineData(0.75f, 0.97265625f)]
    [InlineData(1.0f, 1.0f)]
    public void BounceOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.BounceOut(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.02734375f)]
    [InlineData(0.5f, 0.234375f)]
    [InlineData(0.75f, 0.52734375f)]
    [InlineData(1.0f, 1.0f)]
    public void BounceIn(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.BounceIn(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.1171875f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.75f, 0.8828125f)]
    [InlineData(1.0f, 1.0f)]
    public void BounceInOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.BounceInOut(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.38268346f)]
    [InlineData(0.5f, 0.70710677f)]
    [InlineData(0.75f, 0.9238795f)]
    [InlineData(1.0f, 1.0f)]
    public void SineOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.SineOut(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.076120496f)]
    [InlineData(0.5f, 0.29289323f)]
    [InlineData(0.75f, 0.6173166f)]
    [InlineData(1.0f, 1.0f)]
    public void SineIn(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.SineIn(time)));

    [Theory]
    [InlineData(0.0f, -0.0f)]
    [InlineData(0.25f, 0.14644662f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.75f, 0.8535534f)]
    [InlineData(1.0f, 1.0f)]
    public void SineInOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.SineInOut(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.8232233f)]
    [InlineData(0.5f, 0.96875f)]
    [InlineData(0.75f, 0.9944757f)]
    [InlineData(1.0f, 1.0f)]
    public void ExponentialOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.ExponentialOut(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.0045242715f)]
    [InlineData(0.5f, 0.03025f)]
    [InlineData(0.75f, 0.17577669f)]
    [InlineData(1.0f, 0.999f)]
    public void ExponentialIn(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.ExponentialIn(time)));

    [Theory]
    [InlineData(0.0f, 0.00048828125f)]
    [InlineData(0.25f, 0.015625f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.75f, 0.984375f)]
    [InlineData(1.0f, 0.9995117f)]
    public void ExponentialInOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.ExponentialInOut(time)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, -0.0034443287f)]
    [InlineData(0.5f, 0.0023353111f)]
    [InlineData(0.75f, 0.12958647f)]
    [InlineData(1.0f, 1.0f)]
    public void ElasticIn(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.ElasticIn(time, 2.1f)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.87041354f)]
    [InlineData(0.5f, 0.9976647f)]
    [InlineData(0.75f, 1.0034443f)]
    [InlineData(1.0f, 1.0f)]
    public void ElasticOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.ElasticOut(time, 2.1f)));

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.25f, 0.0011676556f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.75f, 0.99883235f)]
    [InlineData(1.0f, 1.0f)]
    public void ElasticInOut(float time, float expected) => Assert.True(MathDefs.Equals(expected, EaseMath.ElasticInOut(time, 2.1f)));
}
