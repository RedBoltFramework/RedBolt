namespace RedBolt;

public static class Decompress
{
    /// <summary>Decompress a DXT compressed image to RGBA.</summary>
    public static void DecompressImageDXT(
        Span<byte> rgba,
        ReadOnlySpan<byte> blocks,
        int width,
        int height,
        int depth,
        CompressedFormat format)
    {
        var bytesPerBlock = format == CompressedFormat.Dxt1 ? 8 : 16;
        var sourceOffset = 0;
        Span<byte> targetRgba = stackalloc byte[4 * 16];

        for (var z = 0; z < depth; ++z)
        {
            var sliceOffset = width * height * 4 * z;
            for (var y = 0; y < height; y += 4)
            {
                for (var x = 0; x < width; x += 4)
                {
                    DecodeDxtBlock(targetRgba, blocks.Slice(sourceOffset, bytesPerBlock), format);
                    sourceOffset += bytesPerBlock;

                    var sourcePixelOffset = 0;
                    for (var py = 0; py < 4; ++py)
                    {
                        for (var px = 0; px < 4; ++px)
                        {
                            var sx = x + px;
                            var sy = y + py;
                            if (sx < width && sy < height)
                            {
                                var targetPixelOffset = sliceOffset + 4 * (width * sy + sx);
                                targetRgba.Slice(sourcePixelOffset, 4).CopyTo(rgba.Slice(targetPixelOffset, 4));
                            }
                            sourcePixelOffset += 4;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Decompress an ETC1/ETC2 compressed image to RGBA.</summary>
    public static void DecompressImageETC(
        Span<byte> destination,
        ReadOnlySpan<byte> blocks,
        int width,
        int height,
        bool hasAlpha)
    {
        // TODO: port C++ ETCPACK implementation:
        // void DecompressImageETC(unsigned char* dstImage, const void* blocks, int width, int height, bool hasAlpha)
    }

    /// <summary>Decompress a PVRTC compressed image to RGBA.</summary>
    public static void DecompressImagePVRTC(
        Span<byte> rgba,
        ReadOnlySpan<byte> blocks,
        int width,
        int height,
        CompressedFormat format)
    {
        // TODO: port C++ PVRTC implementation:
        // void DecompressImagePVRTC(unsigned char* rgba, const void* blocks, int width, int height, TextureFormat format)
    }

    /// <summary>Returns whether flipping is implemented for texture format.</summary>
    public static bool IsFlipBlockImplemented(CompressedFormat format)
    {
        return format is CompressedFormat.Rgba
            or CompressedFormat.Dxt1
            or CompressedFormat.Dxt3
            or CompressedFormat.Dxt5;
    }

    /// <summary>Flip a compressed block vertically.</summary>
    public static void FlipBlockVertical(Span<byte> destination, ReadOnlySpan<byte> source, CompressedFormat format)
    {
        switch (format)
        {
            case CompressedFormat.Rgba:
                for (var i = 0; i < 4; ++i)
                    destination[i] = source[i];
                break;

            case CompressedFormat.Dxt1:
                for (var i = 0; i < 4; ++i)
                {
                    destination[i] = source[i];
                    destination[i + 4] = source[7 - i];
                }
                break;

            case CompressedFormat.Dxt3:
                for (var i = 0; i < 8; i += 2)
                {
                    destination[i] = source[6 - i];
                    destination[i + 1] = source[6 - i + 1];
                }
                for (var i = 0; i < 4; ++i)
                {
                    destination[i + 8] = source[i + 8];
                    destination[i + 12] = source[15 - i];
                }
                break;

            case CompressedFormat.Dxt5:
                destination[0] = source[0];
                destination[1] = source[1];
                var a1 = (uint)(source[2] | (source[3] << 8) | (source[4] << 16));
                var a2 = (uint)(source[5] | (source[6] << 8) | (source[7] << 16));
                var b1 = ((a1 & 0x000fff) << 12) | ((a1 & 0xfff000) >> 12);
                var b2 = ((a2 & 0x000fff) << 12) | ((a2 & 0xfff000) >> 12);
                destination[2] = (byte)(b2 & 0xff);
                destination[3] = (byte)((b2 >> 8) & 0xff);
                destination[4] = (byte)((b2 >> 16) & 0xff);
                destination[5] = (byte)(b1 & 0xff);
                destination[6] = (byte)((b1 >> 8) & 0xff);
                destination[7] = (byte)((b1 >> 16) & 0xff);
                for (var i = 0; i < 4; ++i)
                {
                    destination[i + 8] = source[i + 8];
                    destination[i + 12] = source[15 - i];
                }
                break;
        }
    }

    /// <summary>Flip a compressed block horizontally.</summary>
    public static void FlipBlockHorizontal(Span<byte> destination, ReadOnlySpan<byte> source, CompressedFormat format)
    {
        switch (format)
        {
            case CompressedFormat.Rgba:
                for (var i = 0; i < 4; ++i)
                    destination[i] = source[i];
                break;

            case CompressedFormat.Dxt1:
                for (var i = 0; i < 4; ++i)
                {
                    destination[i] = source[i];
                    destination[i + 4] = FlipDxt1Horizontal(source[i + 4]);
                }
                break;

            case CompressedFormat.Dxt3:
                for (var i = 0; i < 8; i += 2)
                {
                    destination[i] = (byte)(((source[i + 1] & 0xf0) >> 4) | ((source[i + 1] & 0xf) << 4));
                    destination[i + 1] = (byte)(((source[i] & 0xf0) >> 4) | ((source[i] & 0xf) << 4));
                }
                for (var i = 0; i < 4; ++i)
                {
                    destination[i + 8] = source[i + 8];
                    destination[i + 12] = FlipDxt1Horizontal(source[i + 12]);
                }
                break;

            case CompressedFormat.Dxt5:
                destination[0] = source[0];
                destination[1] = source[1];
                var a1 = (uint)(source[2] | (source[3] << 8) | (source[4] << 16));
                var a2 = (uint)(source[5] | (source[6] << 8) | (source[7] << 16));
                var b1 = FlipDxt5AlphaHorizontal(a1);
                var b2 = FlipDxt5AlphaHorizontal(a2);
                destination[2] = (byte)(b1 & 0xff);
                destination[3] = (byte)((b1 >> 8) & 0xff);
                destination[4] = (byte)((b1 >> 16) & 0xff);
                destination[5] = (byte)(b2 & 0xff);
                destination[6] = (byte)((b2 >> 8) & 0xff);
                destination[7] = (byte)((b2 >> 16) & 0xff);
                for (var i = 0; i < 4; ++i)
                {
                    destination[i + 8] = source[i + 8];
                    destination[i + 12] = FlipDxt1Horizontal(source[i + 12]);
                }
                break;
        }
    }

    private static void DecodeDxtBlock(Span<byte> output, ReadOnlySpan<byte> block, CompressedFormat format)
    {
        Span<uint> colors = stackalloc uint[4];
        switch (format)
        {
            case CompressedFormat.Dxt1:
                BuildBc1Palette(block[..4], colors, opaqueOnly: false);
                WriteColorIndices(block[4..8], colors, output);
                break;
            case CompressedFormat.Dxt3:
                BuildBc1Palette(block[8..12], colors, opaqueOnly: true);
                WriteColorIndices(block[12..16], colors, output);
                for (var i = 0; i < 16; ++i)
                {
                    var alphaByte = block[i / 2];
                    var alpha4 = (alphaByte >> ((i & 1) * 4)) & 0x0f;
                    output[i * 4 + 3] = (byte)((alpha4 << 4) | alpha4);
                }
                break;
            case CompressedFormat.Dxt5:
                Span<byte> alphaPalette = stackalloc byte[8];
                BuildBc3AlphaPalette(block[0], block[1], alphaPalette);
                ulong alphaIndices = 0;
                for (var i = 0; i < 6; ++i)
                    alphaIndices |= (ulong)block[2 + i] << (8 * i);
                BuildBc1Palette(block[8..12], colors, opaqueOnly: true);
                WriteColorIndices(block[12..16], colors, output);
                for (var i = 0; i < 16; ++i)
                    output[i * 4 + 3] = alphaPalette[(int)((alphaIndices >> (3 * i)) & 0x7)];
                break;
        }
    }

    private static void BuildBc1Palette(ReadOnlySpan<byte> colorBlock, Span<uint> palette, bool opaqueOnly)
    {
        var c0 = BitConverter.ToUInt16(colorBlock[..2]);
        var c1 = BitConverter.ToUInt16(colorBlock[2..4]);
        palette[0] = ExpandRgb565(c0, 255);
        palette[1] = ExpandRgb565(c1, 255);
        var c0Channels = ToChannels(palette[0]);
        var c1Channels = ToChannels(palette[1]);

        if (opaqueOnly || c0 > c1)
        {
            palette[2] = ToPackedColor(
                (2 * c0Channels.R + c1Channels.R) / 3,
                (2 * c0Channels.G + c1Channels.G) / 3,
                (2 * c0Channels.B + c1Channels.B) / 3,
                255);
            palette[3] = ToPackedColor(
                (c0Channels.R + 2 * c1Channels.R) / 3,
                (c0Channels.G + 2 * c1Channels.G) / 3,
                (c0Channels.B + 2 * c1Channels.B) / 3,
                255);
        }
        else
        {
            palette[2] = ToPackedColor(
                (c0Channels.R + c1Channels.R) / 2,
                (c0Channels.G + c1Channels.G) / 2,
                (c0Channels.B + c1Channels.B) / 2,
                255);
            palette[3] = ToPackedColor(0, 0, 0, 0);
        }
    }

    private static void BuildBc3AlphaPalette(byte a0, byte a1, Span<byte> palette)
    {
        palette[0] = a0;
        palette[1] = a1;
        if (a0 > a1)
        {
            palette[2] = (byte)((6 * a0 + a1) / 7);
            palette[3] = (byte)((5 * a0 + 2 * a1) / 7);
            palette[4] = (byte)((4 * a0 + 3 * a1) / 7);
            palette[5] = (byte)((3 * a0 + 4 * a1) / 7);
            palette[6] = (byte)((2 * a0 + 5 * a1) / 7);
            palette[7] = (byte)((a0 + 6 * a1) / 7);
        }
        else
        {
            palette[2] = (byte)((4 * a0 + a1) / 5);
            palette[3] = (byte)((3 * a0 + 2 * a1) / 5);
            palette[4] = (byte)((2 * a0 + 3 * a1) / 5);
            palette[5] = (byte)((a0 + 4 * a1) / 5);
            palette[6] = 0;
            palette[7] = 255;
        }
    }

    private static void WriteColorIndices(ReadOnlySpan<byte> indexData, ReadOnlySpan<uint> palette, Span<byte> output)
    {
        var indices = BitConverter.ToUInt32(indexData);
        for (var i = 0; i < 16; ++i)
        {
            var color = palette[(int)((indices >> (2 * i)) & 0x3)];
            output[i * 4] = (byte)(color & 0xff);
            output[i * 4 + 1] = (byte)((color >> 8) & 0xff);
            output[i * 4 + 2] = (byte)((color >> 16) & 0xff);
            output[i * 4 + 3] = (byte)((color >> 24) & 0xff);
        }
    }

    private static uint ExpandRgb565(ushort value, byte alpha)
    {
        var r = (byte)(((value >> 11) & 0x1f) * 255 / 31);
        var g = (byte)(((value >> 5) & 0x3f) * 255 / 63);
        var b = (byte)((value & 0x1f) * 255 / 31);
        return ToPackedColor(r, g, b, alpha);
    }

    private static (int R, int G, int B) ToChannels(uint color)
    {
        return ((int)(color & 0xff), (int)((color >> 8) & 0xff), (int)((color >> 16) & 0xff));
    }

    private static uint ToPackedColor(int r, int g, int b, int a)
    {
        return (uint)(r & 0xff)
            | ((uint)(g & 0xff) << 8)
            | ((uint)(b & 0xff) << 16)
            | ((uint)(a & 0xff) << 24);
    }

    private static byte FlipDxt1Horizontal(byte source)
    {
        return (byte)(((source & 0x3) << 6)
            | ((source & 0xc) << 2)
            | ((source & 0x30) >> 2)
            | ((source & 0xc0) >> 6));
    }

    private static uint FlipDxt5AlphaHorizontal(uint source)
    {
        return ((source & 0x7) << 9)
            | ((source & 0x38) << 3)
            | ((source & 0x1c0) >> 3)
            | ((source & 0xe00) >> 9)
            | ((source & 0x7000) << 9)
            | ((source & 0x38000) << 3)
            | ((source & 0x1c0000) >> 3)
            | ((source & 0xe00000) >> 9);
    }
}
