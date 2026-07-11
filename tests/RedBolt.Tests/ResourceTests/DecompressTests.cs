using RedBolt;

namespace RedBolt.Tests;

public sealed class DecompressTests
{
    [Fact]
    public void Dxt1_image_decompresses_to_rgba()
    {
        var blocks = new byte[8];
        blocks[0] = 0x00;
        blocks[1] = 0xf8;
        blocks[2] = 0xe0;
        blocks[3] = 0x07;
        var rgba = new byte[4 * 4 * 4];

        Decompress.DecompressImageDXT(rgba, blocks, 4, 4, 1, CompressedFormat.Dxt1);

        Assert.Equal(255, rgba[0]);
        Assert.Equal(0, rgba[1]);
        Assert.Equal(0, rgba[2]);
        Assert.Equal(255, rgba[3]);
    }

    [Fact]
    public void Dxt_block_flips_preserve_supported_formats()
    {
        Assert.True(Decompress.IsFlipBlockImplemented(CompressedFormat.Rgba));
        Assert.True(Decompress.IsFlipBlockImplemented(CompressedFormat.Dxt1));
        Assert.True(Decompress.IsFlipBlockImplemented(CompressedFormat.Dxt3));
        Assert.True(Decompress.IsFlipBlockImplemented(CompressedFormat.Dxt5));
        Assert.False(Decompress.IsFlipBlockImplemented(CompressedFormat.Etc1));

        var source = new byte[8];
        source[4] = 0b_00_01_10_11;
        var destination = new byte[8];
        Decompress.FlipBlockHorizontal(destination, source, CompressedFormat.Dxt1);

        Assert.Equal(0b_11_10_01_00, destination[4]);
    }

    [Fact]
    public void Pvrtc4bpp_image_decompresses_to_rgba()
    {
        var blocks = new byte[32];
        for (var block = 0; block < 4; ++block)
        {
            var offset = block * 8;
            blocks[offset + 4] = 0x1f;
            blocks[offset + 5] = 0xfc;
            blocks[offset + 6] = 0x1f;
            blocks[offset + 7] = 0xfc;
        }
        var rgba = new byte[8 * 8 * 4];

        Decompress.DecompressImagePVRTC(rgba, blocks, 8, 8, CompressedFormat.PvrtcRgba4Bpp);

        Assert.Equal(255, rgba[0]);
        Assert.Equal(0, rgba[1]);
        Assert.Equal(255, rgba[2]);
        Assert.Equal(255, rgba[3]);
    }

    [Fact]
    public void Etc2_rgb_image_decompresses_to_rgba()
    {
        var rgba = new byte[4 * 4 * 4];

        Decompress.DecompressImageETC(rgba, new byte[8], 4, 4, hasAlpha: false);

        Assert.Equal(2, rgba[0]);
        Assert.Equal(2, rgba[1]);
        Assert.Equal(2, rgba[2]);
        Assert.Equal(255, rgba[3]);
    }

    [Fact]
    public void Etc2_rgba_image_decompresses_eac_alpha()
    {
        var rgba = new byte[4 * 4 * 4];

        Decompress.DecompressImageETC(rgba, new byte[16], 4, 4, hasAlpha: true);

        Assert.Equal(2, rgba[0]);
        Assert.Equal(2, rgba[1]);
        Assert.Equal(2, rgba[2]);
        Assert.Equal(0, rgba[3]);
    }

    [Fact]
    public void Etc2_special_modes_decompress_to_rgba()
    {
        var blocks = new byte[]
        {
            0xf9, 0x00, 0x00, 0x03, 0, 0, 0, 0,
            0x00, 0xf9, 0x00, 0x02, 0, 0, 0, 0,
            0x00, 0x00, 0xf9, 0x02, 0, 0, 0, 0
        };
        var rgba = new byte[12 * 4 * 4];

        Decompress.DecompressImageETC(rgba, blocks, 12, 4, hasAlpha: false);

        Assert.Equal(192, rgba.Length);
        Assert.Equal(255, rgba[3]);
        Assert.Equal(255, rgba[4 * 4 * 4 + 3]);
        Assert.Equal(255, rgba[8 * 4 * 4 + 3]);
    }
}
