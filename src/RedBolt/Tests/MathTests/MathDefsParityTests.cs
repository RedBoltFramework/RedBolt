using RedBolt;

using RedBolt.Math;
namespace RedBolt.Tests;

public sealed class MathDefsParityTests
{
    [Fact]
    public void Next_power_of_two_preserves_unsigned_wraparound()
    {
        Assert.Equal(0u, MathDefs.NextPowerOfTwo(0u));
    }

    [Fact]
    public void Variable_length_integer_encoding_uses_bit_offset()
    {
        Span<byte> encoded = stackalloc byte[10];
        var length = MathDefs.EncodeVariableLength(123456UL, encoded);
        var decoded = 0UL;
        var offset = 0u;

        for (uint i = 0; i < length; ++i)
            MathDefs.DecodeVariableLength(ref decoded, ref offset, encoded[(int)i]);

        Assert.Equal(123456UL, decoded);
        Assert.Equal(length * 7, offset);
    }

    [Fact]
    public void Zigzag_integer_encoding_and_decoding_matches_cpp()
    {
        Assert.Equal(0u, MathDefs.ZigzagEncode(0));
        Assert.Equal(1u, MathDefs.ZigzagEncode(-1));
        Assert.Equal(2u, MathDefs.ZigzagEncode(1));
        Assert.Equal(3u, MathDefs.ZigzagEncode(-2));
        Assert.Equal(4u, MathDefs.ZigzagEncode(2));

        Assert.Equal(0, MathDefs.ZigzagDecode(0u));
        Assert.Equal(-1, MathDefs.ZigzagDecode(1u));
        Assert.Equal(1, MathDefs.ZigzagDecode(2u));
        Assert.Equal(-2, MathDefs.ZigzagDecode(3u));
        Assert.Equal(2, MathDefs.ZigzagDecode(4u));

        Assert.Equal((byte)3, MathDefs.ZigzagEncode((sbyte)-2));
        Assert.Equal((byte)4, MathDefs.ZigzagEncode((sbyte)2));
        Assert.Equal((byte)254, MathDefs.ZigzagEncode(sbyte.MinValue));
        Assert.Equal((sbyte)-2, MathDefs.ZigzagDecode((byte)3));
        Assert.Equal((sbyte)2, MathDefs.ZigzagDecode((byte)4));
        Assert.Equal(sbyte.MinValue, MathDefs.ZigzagDecode(byte.MaxValue));
        Assert.Equal(uint.MaxValue - 1, MathDefs.ZigzagEncode(int.MinValue));
        Assert.Equal(int.MinValue, MathDefs.ZigzagDecode(uint.MaxValue));
    }
}
