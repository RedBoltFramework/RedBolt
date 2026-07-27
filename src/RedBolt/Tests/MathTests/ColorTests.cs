using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBolt;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
namespace RedBolt.Tests;

public sealed class ColorTests
{
    [Fact]
    public void Color_is_a_sixteen_byte_unmanaged_value()
    {
        Assert.True(typeof(Color).IsValueType);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Color>());
        Assert.Equal(16, Marshal.SizeOf<Color>());
    }

    [Fact]
    public void Channel_mask_is_a_sixteen_byte_unmanaged_value()
    {
        Assert.True(typeof(Color.ChannelMask).IsValueType);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Color.ChannelMask>());
        Assert.Equal(16, Marshal.SizeOf<Color.ChannelMask>());
    }

    [Fact]
    public void Default_construction_is_opaque_white_and_copy_is_independent()
    {
        var color = new Color();
        Assert.Equal(Color.White, color);
        Assert.Equal(new[] { 1.0f, 1.0f, 1.0f, 1.0f }, color.Data().ToArray());

        var copy = color;
        copy.R = 0.0f;
        Assert.Equal(1.0f, color.R);
        Assert.Equal(0.0f, copy.R);
    }

    [Fact]
    public void Native_default_constructed_color_members_are_opaque_white()
    {
        Assert.Equal(Color.White, new Billboard().Color);
        Assert.Equal(Color.White, new LightDataForAccumulator().Color);
    }

    [Fact]
    public void Arbitrary_channel_masks_follow_native_mask_arithmetic()
    {
        var mask = new Color.ChannelMask(0x0000000f, 0x000000f0, 0, 0);
        var packed = new Color(0.5f, 0.5f, 0.5f, 0.5f).ToUIntMask(mask);
        Assert.Equal(0x00000077u, packed);

        var unpacked = new Color(packed, mask);
        Assert.Equal(7.0f / 15.0f, unpacked.R);
        Assert.Equal(112.0f / 240.0f, unpacked.G);
        Assert.Equal(0.0f, unpacked.B);
        Assert.Equal(1.0f, unpacked.A);
    }

    [Fact]
    public void Hue_wraps_red_sector_into_unit_range()
    {
        var color = new Color(1.0f, 0.0f, 0.5f);
        Assert.InRange(MathF.Abs(11.0f / 12.0f - color.Hue()), 0.0f, 0.000001f);
    }
}
