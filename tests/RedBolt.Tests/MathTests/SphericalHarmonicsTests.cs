using RedBolt;

namespace RedBolt.Tests;

public sealed class SphericalHarmonicsTests
{
    [Fact]
    public void Basis_matches_directional_coefficients()
    {
        var basis = new SphericalHarmonics9(new Vector3f(1.0f, 0.0f, 0.0f));

        AssertClose(0.282095f, basis.Values[0]);
        AssertClose(0.488603f, basis.Values[3]);
        AssertClose(-0.315392f, basis.Values[6]);
        AssertClose(0.546274f, basis.Values[8]);
    }

    [Fact]
    public void Constant_color_evaluates_to_constant()
    {
        var color = new Vector3f(0.25f, 0.5f, 0.75f);
        var harmonics = new SphericalHarmonicsColor9(color);

        AssertClose(color, harmonics.Evaluate(new Vector3f(1.0f, 2.0f, 3.0f)));
        AssertClose(color, harmonics.EvaluateAverage());
    }

    [Fact]
    public void Dot_representation_matches_color_representation()
    {
        var harmonics = new SphericalHarmonicsColor9(
            new Vector3f(1.0f, 2.0f, 3.0f).Normalized,
            new Vector3f(0.2f, 0.4f, 0.8f));
        var packed = new SphericalHarmonicsDot9(harmonics);
        var direction = new Vector3f(-2.0f, 1.0f, 4.0f).Normalized;

        AssertClose(harmonics.Evaluate(direction), packed.Evaluate(direction));
        AssertClose(harmonics.EvaluateAverage(), packed.EvaluateAverage());
    }

    [Fact]
    public void Dot_accumulation_and_scaling_match_operators()
    {
        var harmonics = new SphericalHarmonicsDot9(new Vector3f(1.0f, 2.0f, 3.0f));
        harmonics += new Vector3f(2.0f, 3.0f, 4.0f);
        harmonics *= 0.5f;

        AssertClose(new Vector3f(1.5f, 2.5f, 3.5f), harmonics.EvaluateAverage());
    }

    private static void AssertClose(Vector3f expected, Vector3f actual)
    {
        AssertClose(expected.X, actual.X);
        AssertClose(expected.Y, actual.Y);
        AssertClose(expected.Z, actual.Z);
    }

    private static void AssertClose(float expected, float actual) =>
        Assert.InRange(actual, expected - 0.00001f, expected + 0.00001f);
}
