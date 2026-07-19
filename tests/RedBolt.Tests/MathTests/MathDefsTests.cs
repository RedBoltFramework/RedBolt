using RedBolt;

namespace RedBolt.Tests;

public sealed class MathDefsTests
{
    [Fact]
    public void Class_shape_and_constants_match_cpp()
    {
        Assert.True(typeof(MathDefs).IsAbstract);
        Assert.True(typeof(MathDefs).IsSealed);
        Assert.Empty(typeof(MathDefs).GetConstructors());
        Assert.Empty(typeof(MathDefs).GetFields(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic));

        Assert.Equal(0x8000, MathDefs.MinShort);
        Assert.Equal(typeof(int), typeof(MathDefs).GetField(nameof(MathDefs.MinShort))!.FieldType);
        Assert.Equal(0x7fff, MathDefs.MaxShort);
        Assert.Equal(1.0f / MathDefs.DegToRad, MathDefs.RadToDeg);

        Assert.Null(typeof(MathDefs).GetField("PiOver2"));
        Assert.Null(typeof(MathDefs).GetMethod("ApproximatelyEquivalent"));
        Assert.Null(typeof(MathDefs).GetMethod("DegreesToRadians"));
    }

    [Fact]
    public void Equality_and_angle_conversions_match_cpp()
    {
        Assert.True(MathDefs.Equals(float.PositiveInfinity, float.PositiveInfinity));
        Assert.True(MathDefs.Equals(float.NegativeInfinity, float.NegativeInfinity));
        Assert.False(MathDefs.Equals(float.NaN, float.NaN));
        Assert.True(MathDefs.Equals(MathDefs.ToRadians(180.0f), MathDefs.Pi));
        Assert.True(MathDefs.Equals(MathDefs.ToDegrees(MathDefs.Pi), 180.0f));
    }

    [Fact]
    public void Interpolation_helpers_work()
    {
        Assert.True(MathDefs.Equals(MathDefs.InverseLerp(10.0f, 20.0f, 15.0f), 0.5f));
        Assert.True(MathDefs.Equals(MathDefs.SmoothStep(0.0f, 1.0f, 0.5f), 0.5f));
        Assert.True(MathDefs.Equals(MathDefs.Smooth(0.0f, 10.0f, 1.0f), 5.0f));
    }

    [Fact]
    public void Trigonometry_uses_degrees()
    {
        Assert.True(MathDefs.Equals(MathDefs.Sin(90.0f), 1.0f));
        Assert.True(MathDefs.Equals(MathDefs.Cos(180.0f), -1.0f));
        Assert.True(MathDefs.Equals(MathDefs.Atan2(1.0f, 0.0f), 90.0f));
    }

    [Fact]
    public void Modulo_and_snapping_work()
    {
        Assert.Equal(9, MathDefs.AbsMod(-1, 10));
        Assert.True(MathDefs.Equals(MathDefs.SnapRound(2.25f, 0.5f), 2.5f));
        Assert.True(MathDefs.Equals(MathDefs.RoundToNearestMultiple(-7.0f, 4.0f), -8.0f));
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
    public void Variable_length_and_zigzag_helpers_match_cpp()
    {
        Assert.Equal(5, MathDefs.MaxVariableLengthBytes<uint>());
        Assert.Equal(10, MathDefs.MaxVariableLengthBytes<ulong>());

        Span<byte> encoded = stackalloc byte[10];
        var length = MathDefs.EncodeVariableLength(123456u, encoded);
        uint decoded = 0;
        uint offset = 0;
        for (uint i = 0; i < length; ++i)
            MathDefs.DecodeVariableLength(ref decoded, ref offset, encoded[(int)i]);

        Assert.Equal(123456u, decoded);
        Assert.Equal(21u, offset);

        Assert.Equal((byte)0, MathDefs.ZigzagEncode((sbyte)0));
        Assert.Equal((byte)1, MathDefs.ZigzagEncode((sbyte)-1));
        Assert.Equal((byte)2, MathDefs.ZigzagEncode((sbyte)1));
        Assert.Equal((sbyte)-2, MathDefs.ZigzagDecode(MathDefs.ZigzagEncode((sbyte)-2)));
        Assert.Equal(-123456, MathDefs.ZigzagDecode(MathDefs.ZigzagEncode(-123456)));
    }

    [Fact]
    public void Vector_min_and_max_preserve_cpp_operand_order()
    {
        var lhsNaN = new Vector4f(float.NaN, 1.0f, 1.0f, 1.0f);
        var rhsNaN = new Vector4f(2.0f, float.NaN, 2.0f, 2.0f);

        Assert.Equal(1.0f, MathDefs.VectorMin(lhsNaN, Vector4f.One).X);
        Assert.True(float.IsNaN(MathDefs.VectorMin(Vector4f.One, rhsNaN).Y));
        Assert.Equal(1.0f, MathDefs.VectorMax(lhsNaN, Vector4f.One).X);
        Assert.True(float.IsNaN(MathDefs.VectorMax(Vector4f.One, rhsNaN).Y));
    }

    [Fact]
    public void Scalar_utility_helpers_work()
    {
        Assert.True(MathDefs.Equals(MathDefs.ExponentialDecay(1.0f), 0.5f));
        Assert.True(MathDefs.Equals(MathDefs.Wrap(12.0f, 0.0f, 10.0f), 2.0f));
        Assert.Equal(65u, MathDefs.SDBMHash(0u, (byte)'A'));
    }

    [Fact]
    public void Clamp_preserves_cpp_branch_order_for_inverted_bounds()
    {
        Assert.Equal(0, MathDefs.Clamp(-1, 0, -1));
        Assert.Equal(-1, MathDefs.Clamp(0, 0, -1));
        Assert.Equal(0.0f, MathDefs.Clamp(-1.0f, 0.0f, -1.0f));
        Assert.Equal(-1.0f, MathDefs.Clamp(0.0f, 0.0f, -1.0f));
    }

    [Fact]
    public void Half_float_conversion_roundtrips()
    {
        Assert.Equal(0x3c00, MathDefs.FloatToHalf(1.0f));
        Assert.True(MathDefs.Equals(MathDefs.HalfToFloat(0x3c00), 1.0f));
        Assert.Equal(0, MathDefs.FloatToHalf(0.0f));
        Assert.True(MathDefs.Equals(MathDefs.HalfToFloat(0), 0.0f));
    }

    [Fact]
    public void Easing_endpoints_return_bounds()
    {
        Assert.True(MathDefs.Equals(EaseMath.BounceInOut(0.0f), 0.0f));
        Assert.True(MathDefs.Equals(EaseMath.BounceInOut(1.0f), 1.0f));
    }
}
