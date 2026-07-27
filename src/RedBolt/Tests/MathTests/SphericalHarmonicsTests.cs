using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBolt;

using RedBolt.Math;
namespace RedBolt.Tests;

public sealed class SphericalHarmonicsTests
{
    [Fact]
    public void Shapes_and_defaults_match_native()
    {
        Assert.True(typeof(SphericalHarmonics9).IsValueType);
        Assert.True(typeof(SphericalHarmonicsColor9).IsValueType);
        Assert.True(typeof(SphericalHarmonicsDot9).IsValueType);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<SphericalHarmonics9>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<SphericalHarmonicsColor9>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<SphericalHarmonicsDot9>());
        Assert.Equal(36, Marshal.SizeOf<SphericalHarmonics9>());
        Assert.Equal(108, Marshal.SizeOf<SphericalHarmonicsColor9>());
        Assert.Equal(112, Marshal.SizeOf<SphericalHarmonicsDot9>());
        Assert.Equal(0, Marshal.OffsetOf<SphericalHarmonics9>(nameof(SphericalHarmonics9.Values)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<SphericalHarmonicsColor9>(nameof(SphericalHarmonicsColor9.Values)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<SphericalHarmonicsDot9>(nameof(SphericalHarmonicsDot9.Ar)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<SphericalHarmonicsDot9>(nameof(SphericalHarmonicsDot9.Ag)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<SphericalHarmonicsDot9>(nameof(SphericalHarmonicsDot9.Ab)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<SphericalHarmonicsDot9>(nameof(SphericalHarmonicsDot9.Br)).ToInt32());
        Assert.Equal(64, Marshal.OffsetOf<SphericalHarmonicsDot9>(nameof(SphericalHarmonicsDot9.Bg)).ToInt32());
        Assert.Equal(80, Marshal.OffsetOf<SphericalHarmonicsDot9>(nameof(SphericalHarmonicsDot9.Bb)).ToInt32());
        Assert.Equal(96, Marshal.OffsetOf<SphericalHarmonicsDot9>(nameof(SphericalHarmonicsDot9.C)).ToInt32());

        var scalar = new SphericalHarmonics9();
        var color = new SphericalHarmonicsColor9();
        var dot = new SphericalHarmonicsDot9();
        for (var i = 0; i < 9; ++i)
        {
            Assert.Equal(0.0f, scalar.Values[i]);
            Assert.Equal(Vector3f.Zero, color.Values[i]);
        }
        Assert.Equal(default, dot.Ar);
        Assert.Equal(default, dot.Ag);
        Assert.Equal(default, dot.Ab);
        Assert.Equal(default, dot.Br);
        Assert.Equal(default, dot.Bg);
        Assert.Equal(default, dot.Bb);
        Assert.Equal(default, dot.C);
    }

    [Fact]
    public void Fixed_storage_copies_are_independent()
    {
        var scalar = new SphericalHarmonics9(Vector3f.Right);
        var scalarCopy = scalar;
        scalar.Values[0] = 10.0f;
        AssertClose(0.282095f, scalarCopy.Values[0]);

        var color = new SphericalHarmonicsColor9(Vector3f.Right, new Vector3f(1.0f, 2.0f, 3.0f));
        var colorCopy = color;
        color.Values[0] = new Vector3f(10.0f, 20.0f, 30.0f);
        AssertClose(new Vector3f(0.282095f, 0.56419f, 0.846285f), colorCopy.Values[0]);

        var dot = new SphericalHarmonicsDot9(new Vector3f(1.0f, 2.0f, 3.0f));
        var dotCopy = dot;
        dot.Ar.W = 10.0f;
        Assert.Equal(1.0f, dotCopy.Ar.W);
    }

    [Fact]
    public void Dot_conversion_mutates_only_its_by_value_copy()
    {
        var harmonics = new SphericalHarmonicsColor9(
            new Vector3f(1.0f, 2.0f, 3.0f).Normalized,
            new Vector3f(0.2f, 0.4f, 0.8f));
        var original = harmonics;

        _ = new SphericalHarmonicsDot9(harmonics);

        for (var i = 0; i < 9; ++i)
            Assert.Equal(original.Values[i], harmonics.Values[i]);
    }

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
