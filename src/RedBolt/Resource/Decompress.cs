namespace RedBolt;

using System.Buffers.Binary;

public static class Decompress
{
    private static readonly int[] _table59T = { 3, 6, 11, 16, 23, 32, 41, 64 };
    private static readonly int[] _table58H = { 3, 6, 11, 16, 23, 32, 41, 64 };
    private static readonly int[,] _compressParams =
    {
        { -8, -2, 2, 8 }, { -8, -2, 2, 8 },
        { -17, -5, 5, 17 }, { -17, -5, 5, 17 },
        { -29, -9, 9, 29 }, { -29, -9, 9, 29 },
        { -42, -13, 13, 42 }, { -42, -13, 13, 42 },
        { -60, -18, 18, 60 }, { -60, -18, 18, 60 },
        { -80, -24, 24, 80 }, { -80, -24, 24, 80 },
        { -106, -33, 33, 106 }, { -106, -33, 33, 106 },
        { -183, -47, 47, 183 }, { -183, -47, 47, 183 }
    };
    private static readonly int[] _unscramble = { 2, 3, 1, 0 };
    private static readonly int[] _alphaTable = new int[256 * 8];
    private static readonly int[,] _alphaBase =
    {
        { -15, -9, -6, -3 }, { -13, -10, -7, -3 },
        { -13, -8, -5, -2 }, { -13, -6, -4, -2 },
        { -12, -8, -6, -3 }, { -11, -9, -7, -3 },
        { -11, -8, -7, -4 }, { -11, -8, -5, -3 },
        { -10, -8, -6, -2 }, { -10, -8, -5, -2 },
        { -10, -8, -4, -2 }, { -10, -7, -5, -2 },
        { -10, -7, -4, -3 }, { -10, -3, -2, -1 },
        { -9, -8, -6, -4 }, { -9, -7, -5, -3 }
    };
    private static bool _alphaTableInitialized;

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
        SetupAlphaTable();

        var channelCount = hasAlpha ? 4 : 3;
        var sourceOffset = 0;

        // ETCPACK write 4x4 blocks, so it needs padding.
        var widthInBlocks = (width + 3) / 4;
        var heightInBlocks = (height + 3) / 4;
        Span<byte> buffer4x4 = stackalloc byte[4 * 4 * 4];

        for (var y = 0; y < heightInBlocks; ++y)
        {
            for (var x = 0; x < widthInBlocks; ++x)
            {
                buffer4x4.Fill(0xff);
                if (hasAlpha)
                {
                    DecompressBlockAlpha(blocks.Slice(sourceOffset, 8), buffer4x4.Slice(3), 4, 4, 0, 0, channelCount);
                    sourceOffset += 8;
                }

                var blockPart1 = ReadBigEndian4ByteWord(blocks, sourceOffset);
                sourceOffset += 4;
                var blockPart2 = ReadBigEndian4ByteWord(blocks, sourceOffset);
                sourceOffset += 4;
                DecompressBlockEtc2(blockPart1, blockPart2, buffer4x4, 4, 4, 0, 0, 4);

                var widthInBuffer = Math.Min(width - x * 4, 4);
                var heightInBuffer = Math.Min(height - y * 4, 4);
                for (var dy = 0; dy < heightInBuffer; ++dy)
                {
                    for (var dx = 0; dx < widthInBuffer; ++dx)
                    {
                        var destinationOffset = ((y * 4 + dy) * width + x * 4 + dx) * 4;
                        var bufferOffset = (dy * 4 + dx) * 4;
                        destination[destinationOffset] = buffer4x4[bufferOffset];
                        destination[destinationOffset + 1] = buffer4x4[bufferOffset + 1];
                        destination[destinationOffset + 2] = buffer4x4[bufferOffset + 2];
                        destination[destinationOffset + 3] = buffer4x4[bufferOffset + 3];
                    }
                }
            }
        }
    }

    /// <summary>Decompress a PVRTC compressed image to RGBA.</summary>
    public static void DecompressImagePVRTC(
        Span<byte> rgba,
        ReadOnlySpan<byte> blocks,
        int width,
        int height,
        CompressedFormat format)
    {
        var do2bitMode = format is CompressedFormat.PvrtcRgb2Bpp or CompressedFormat.PvrtcRgba2Bpp;
        var xBlockSize = do2bitMode ? 8 : 4;
        var blockXDim = Math.Max(2, width / xBlockSize);
        var blockYDim = Math.Max(2, height / 4);

        Span<int> modulationValues = stackalloc int[8 * 16];
        Span<int> modulationModes = stackalloc int[8 * 16];
        Span<uint> previousBlocks = stackalloc uint[4];
        Span<int> colours = stackalloc int[2 * 2 * 2 * 4];
        Span<int> aSignal = stackalloc int[4];
        Span<int> bSignal = stackalloc int[4];
        Span<int> result = stackalloc int[4];

        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
            {
                // Map this pixel to the top left neighbourhood of blocks
                var blockX = LimitCoordinate(x - xBlockSize / 2, width) / xBlockSize;
                var blockY = LimitCoordinate(y - 4 / 2, height) / 4;

                // Compute the positions of the other 3 blocks
                var blockXPlusOne = LimitCoordinate(blockX + 1, blockXDim);
                var blockYPlusOne = LimitCoordinate(blockY + 1, blockYDim);

                // Map to block memory locations
                var block00 = (int)(TwiddleUv((uint)blockYDim, (uint)blockXDim, (uint)blockY, (uint)blockX) * 8);
                var block01 = (int)(TwiddleUv((uint)blockYDim, (uint)blockXDim, (uint)blockY, (uint)blockXPlusOne) * 8);
                var block10 = (int)(TwiddleUv((uint)blockYDim, (uint)blockXDim, (uint)blockYPlusOne, (uint)blockX) * 8);
                var block11 = (int)(TwiddleUv((uint)blockYDim, (uint)blockXDim, (uint)blockYPlusOne, (uint)blockXPlusOne) * 8);

                // Extract the colours and the modulation information IF the previous values
                // have changed.
                if (previousBlocks[0] != (uint)block00 || previousBlocks[1] != (uint)block01
                    || previousBlocks[2] != (uint)block10 || previousBlocks[3] != (uint)block11)
                {
                    var startY = 0;
                    for (var i = 0; i < 2; ++i)
                    {
                        var startX = 0;
                        for (var j = 0; j < 2; ++j)
                        {
                            var blockOffset = (i == 0 ? (j == 0 ? block00 : block01) : (j == 0 ? block10 : block11));
                            var colourOffset = (i * 2 + j) * 8;
                            Unpack5554Colour(blocks, blockOffset, colours.Slice(colourOffset, 8));
                            UnpackModulations(blocks, blockOffset, do2bitMode, modulationValues, modulationModes, startX, startY);
                            startX += xBlockSize;
                        }
                        startY += 4;
                    }

                    previousBlocks[0] = (uint)block00;
                    previousBlocks[1] = (uint)block01;
                    previousBlocks[2] = (uint)block10;
                    previousBlocks[3] = (uint)block11;
                }

                // Decompress the pixel. First compute the interpolated A and B signals
                InterpolateColours(
                    colours.Slice(0, 4),
                    colours.Slice(8, 4),
                    colours.Slice(16, 4),
                    colours.Slice(24, 4),
                    do2bitMode,
                    x,
                    y,
                    aSignal);
                InterpolateColours(
                    colours.Slice(4, 4),
                    colours.Slice(12, 4),
                    colours.Slice(20, 4),
                    colours.Slice(28, 4),
                    do2bitMode,
                    x,
                    y,
                    bSignal);

                GetModulationValue(x, y, do2bitMode, modulationValues, modulationModes, out var modulation, out var doPunchThrough);

                // Compute the modulated colour
                for (var i = 0; i < 4; ++i)
                {
                    result[i] = aSignal[i] * 8 + modulation * (bSignal[i] - aSignal[i]);
                    result[i] >>= 3;
                }
                if (doPunchThrough)
                    result[3] = 0;

                // Store the result in the output image
                var outputPosition = (x + y * width) << 2;
                rgba[outputPosition] = (byte)result[0];
                rgba[outputPosition + 1] = (byte)result[1];
                rgba[outputPosition + 2] = (byte)result[2];
                rgba[outputPosition + 3] = (byte)result[3];
            }
        }
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

    private static void SetupAlphaTable()
    {
        if (_alphaTableInitialized)
            return;
        _alphaTableInitialized = true;

        // read table used for alpha compression
        for (var i = 16; i < 32; ++i)
        {
            for (var j = 0; j < 8; ++j)
            {
                var value = _alphaBase[i - 16, 3 - j % 4];
                _alphaTable[i * 8 + j] = j < 4 ? value : -value - 1;
            }
        }

        // beyond the first 16 values, the rest of the table is implicit.. so calculate that!
        for (var i = 0; i < 256; ++i)
        {
            // fill remaining slots in table with multiples of the first ones.
            var multiplier = i / 16;
            var old = 16 + i % 16;
            for (var j = 0; j < 8; ++j)
                _alphaTable[i * 8 + j] = _alphaTable[old * 8 + j] * multiplier;
        }
    }

    private static uint GetBits(uint source, int size, int startPosition)
    {
        return (source >> (startPosition - size + 1)) & ((1u << size) - 1);
    }

    private static uint GetBitsHigh(uint source, int size, int startPosition)
    {
        return (source >> (startPosition - 32 - size + 1)) & ((1u << size) - 1);
    }

    private static void PutBits(ref uint destination, uint data, int size, int startPosition)
    {
        var shift = startPosition - size + 1;
        var mask = ((1u << size) - 1) << shift;
        destination = (destination & ~mask) | ((data << shift) & mask);
    }

    private static void PutBitsHigh(ref uint destination, uint data, int size, int startPosition)
    {
        var shift = startPosition - 32 - size + 1;
        var mask = ((1u << size) - 1) << shift;
        destination = (destination & ~mask) | ((data << shift) & mask);
    }

    private static void Unstuff57Bits(uint planarWord1, uint planarWord2, out uint planar57Word1, out uint planar57Word2)
    {
        uint redO = GetBitsHigh(planarWord1, 6, 62);
        uint greenO1 = GetBitsHigh(planarWord1, 1, 56);
        uint greenO2 = GetBitsHigh(planarWord1, 6, 54);
        uint blueO1 = GetBitsHigh(planarWord1, 1, 48);
        uint blueO2 = GetBitsHigh(planarWord1, 2, 44);
        uint blueO3 = GetBitsHigh(planarWord1, 3, 41);
        uint redH1 = GetBitsHigh(planarWord1, 5, 38);
        uint redH2 = GetBitsHigh(planarWord1, 1, 32);
        uint greenH = GetBits(planarWord2, 7, 31);
        uint blueH = GetBits(planarWord2, 6, 24);
        uint redV = GetBits(planarWord2, 6, 18);
        uint greenV = GetBits(planarWord2, 7, 12);
        uint blueV = GetBits(planarWord2, 6, 5);

        planar57Word1 = 0;
        planar57Word2 = 0;
        PutBitsHigh(ref planar57Word1, redO, 6, 63);
        PutBitsHigh(ref planar57Word1, greenO1, 1, 57);
        PutBitsHigh(ref planar57Word1, greenO2, 6, 56);
        PutBitsHigh(ref planar57Word1, blueO1, 1, 50);
        PutBitsHigh(ref planar57Word1, blueO2, 2, 49);
        PutBitsHigh(ref planar57Word1, blueO3, 3, 47);
        PutBitsHigh(ref planar57Word1, redH1, 5, 44);
        PutBitsHigh(ref planar57Word1, redH2, 1, 39);
        PutBitsHigh(ref planar57Word1, greenH, 7, 38);
        PutBits(ref planar57Word2, blueH, 6, 31);
        PutBits(ref planar57Word2, redV, 6, 25);
        PutBits(ref planar57Word2, greenV, 7, 19);
        PutBits(ref planar57Word2, blueV, 6, 12);
    }

    private static void Unstuff58Bits(uint thumbHWord1, uint thumbHWord2, out uint thumbH58Word1, out uint thumbH58Word2)
    {
        var part0 = GetBitsHigh(thumbHWord1, 7, 62);
        var part1 = GetBitsHigh(thumbHWord1, 2, 52);
        var part2 = GetBitsHigh(thumbHWord1, 16, 49);
        var part3 = GetBitsHigh(thumbHWord1, 1, 32);
        thumbH58Word1 = 0;
        PutBitsHigh(ref thumbH58Word1, part0, 7, 57);
        PutBitsHigh(ref thumbH58Word1, part1, 2, 50);
        PutBitsHigh(ref thumbH58Word1, part2, 16, 48);
        PutBitsHigh(ref thumbH58Word1, part3, 1, 32);
        thumbH58Word2 = thumbHWord2;
    }

    private static void Unstuff59Bits(uint thumbTWord1, uint thumbTWord2, out uint thumbT59Word1, out uint thumbT59Word2)
    {
        thumbT59Word1 = thumbTWord1 >> 1;
        PutBitsHigh(ref thumbT59Word1, thumbTWord1, 1, 32);
        var red0a = GetBitsHigh(thumbTWord1, 2, 60);
        PutBitsHigh(ref thumbT59Word1, red0a, 2, 58);
        PutBitsHigh(ref thumbT59Word1, 0, 5, 63);
        thumbT59Word2 = thumbTWord2;
    }

    private static void DecompressColor(int redBits, int greenBits, int blueBits, ReadOnlySpan<byte> colorsRgb444, Span<byte> colors)
    {
        colors[0] = (byte)((colorsRgb444[0] << (8 - redBits)) | (colorsRgb444[0] >> (redBits - (8 - redBits))));
        colors[1] = (byte)((colorsRgb444[1] << (8 - greenBits)) | (colorsRgb444[1] >> (greenBits - (8 - greenBits))));
        colors[2] = (byte)((colorsRgb444[2] << (8 - blueBits)) | (colorsRgb444[2] >> (blueBits - (8 - blueBits))));
        colors[3] = (byte)((colorsRgb444[3] << (8 - redBits)) | (colorsRgb444[3] >> (redBits - (8 - redBits))));
        colors[4] = (byte)((colorsRgb444[4] << (8 - greenBits)) | (colorsRgb444[4] >> (greenBits - (8 - greenBits))));
        colors[5] = (byte)((colorsRgb444[5] << (8 - blueBits)) | (colorsRgb444[5] >> (blueBits - (8 - blueBits))));
    }

    private static void CalculatePaintColors59T(byte distance, ReadOnlySpan<byte> colors, Span<byte> possibleColors)
    {
        var value = _table59T[distance];
        possibleColors[9] = (byte)Clamp(colors[3] - value);
        possibleColors[10] = (byte)Clamp(colors[4] - value);
        possibleColors[11] = (byte)Clamp(colors[5] - value);
        possibleColors[0] = colors[0];
        possibleColors[1] = colors[1];
        possibleColors[2] = colors[2];
        possibleColors[3] = (byte)Clamp(colors[3] + value);
        possibleColors[4] = (byte)Clamp(colors[4] + value);
        possibleColors[5] = (byte)Clamp(colors[5] + value);
        possibleColors[6] = colors[3];
        possibleColors[7] = colors[4];
        possibleColors[8] = colors[5];
    }

    private static void CalculatePaintColors58H(byte distance, ReadOnlySpan<byte> colors, Span<byte> possibleColors)
    {
        var value = _table58H[distance];
        possibleColors[9] = (byte)Clamp(colors[3] - value);
        possibleColors[10] = (byte)Clamp(colors[4] - value);
        possibleColors[11] = (byte)Clamp(colors[5] - value);
        possibleColors[0] = (byte)Clamp(colors[0] + value);
        possibleColors[1] = (byte)Clamp(colors[1] + value);
        possibleColors[2] = (byte)Clamp(colors[2] + value);
        possibleColors[3] = (byte)Clamp(colors[0] - value);
        possibleColors[4] = (byte)Clamp(colors[1] - value);
        possibleColors[5] = (byte)Clamp(colors[2] - value);
        possibleColors[6] = (byte)Clamp(colors[3] + value);
        possibleColors[7] = (byte)Clamp(colors[4] + value);
        possibleColors[8] = (byte)Clamp(colors[5] + value);
    }

    private static void DecompressBlockThumb59(uint blockPart1, uint blockPart2, Span<byte> image, int width, int startX, int startY, int channels)
    {
        Span<byte> colorsRgb444 = stackalloc byte[6];
        Span<byte> colors = stackalloc byte[6];
        Span<byte> paintColors = stackalloc byte[12];
        colorsRgb444[0] = (byte)GetBitsHigh(blockPart1, 4, 58);
        colorsRgb444[1] = (byte)GetBitsHigh(blockPart1, 4, 54);
        colorsRgb444[2] = (byte)GetBitsHigh(blockPart1, 4, 50);
        colorsRgb444[3] = (byte)GetBitsHigh(blockPart1, 4, 46);
        colorsRgb444[4] = (byte)GetBitsHigh(blockPart1, 4, 42);
        colorsRgb444[5] = (byte)GetBitsHigh(blockPart1, 4, 38);
        var distance = (byte)GetBitsHigh(blockPart1, 3, 34);
        DecompressColor(4, 4, 4, colorsRgb444, colors);
        CalculatePaintColors59T(distance, colors, paintColors);

        for (var x = 0; x < 4; ++x)
        {
            for (var y = 0; y < 4; ++y)
            {
                var mask = (int)(GetBits(blockPart2, 1, y + x * 4 + 16) << 1);
                mask |= (int)GetBits(blockPart2, 1, y + x * 4);
                WriteEtcRgb(image, width, startX + x, startY + y, channels, paintColors, mask * 3);
            }
        }
    }

    private static void DecompressBlockThumb58(uint blockPart1, uint blockPart2, Span<byte> image, int width, int startX, int startY, int channels)
    {
        Span<byte> colorsRgb444 = stackalloc byte[6];
        Span<byte> colors = stackalloc byte[6];
        Span<byte> paintColors = stackalloc byte[12];
        colorsRgb444[0] = (byte)GetBitsHigh(blockPart1, 4, 57);
        colorsRgb444[1] = (byte)GetBitsHigh(blockPart1, 4, 53);
        colorsRgb444[2] = (byte)GetBitsHigh(blockPart1, 4, 49);
        colorsRgb444[3] = (byte)GetBitsHigh(blockPart1, 4, 45);
        colorsRgb444[4] = (byte)GetBitsHigh(blockPart1, 4, 41);
        colorsRgb444[5] = (byte)GetBitsHigh(blockPart1, 4, 37);
        var distance = (byte)(GetBitsHigh(blockPart1, 2, 33) << 1);
        var color0 = GetBitsHigh(blockPart1, 12, 57);
        var color1 = GetBitsHigh(blockPart1, 12, 45);
        if (color0 >= color1)
            distance |= 1;
        DecompressColor(4, 4, 4, colorsRgb444, colors);
        CalculatePaintColors58H(distance, colors, paintColors);

        for (var x = 0; x < 4; ++x)
        {
            for (var y = 0; y < 4; ++y)
            {
                var mask = (int)(GetBits(blockPart2, 1, y + x * 4 + 16) << 1);
                mask |= (int)GetBits(blockPart2, 1, y + x * 4);
                WriteEtcRgb(image, width, startX + x, startY + y, channels, paintColors, mask * 3);
            }
        }
    }

    private static void DecompressBlockPlanar(uint compressed57Part1, uint compressed57Part2, Span<byte> image, int width, int startX, int startY, int channels)
    {
        Span<int> colorO = stackalloc int[3];
        Span<int> colorH = stackalloc int[3];
        Span<int> colorV = stackalloc int[3];
        colorO[0] = (int)GetBitsHigh(compressed57Part1, 6, 63);
        colorO[1] = (int)GetBitsHigh(compressed57Part1, 7, 57);
        colorO[2] = (int)GetBitsHigh(compressed57Part1, 6, 50);
        colorH[0] = (int)GetBitsHigh(compressed57Part1, 6, 44);
        colorH[1] = (int)GetBitsHigh(compressed57Part1, 7, 38);
        colorH[2] = (int)GetBits(compressed57Part2, 6, 31);
        colorV[0] = (int)GetBits(compressed57Part2, 6, 25);
        colorV[1] = (int)GetBits(compressed57Part2, 7, 19);
        colorV[2] = (int)GetBits(compressed57Part2, 6, 12);

        colorO[0] = (colorO[0] << 2) | (colorO[0] >> 4);
        colorO[1] = (colorO[1] << 1) | (colorO[1] >> 6);
        colorO[2] = (colorO[2] << 2) | (colorO[2] >> 4);
        colorH[0] = (colorH[0] << 2) | (colorH[0] >> 4);
        colorH[1] = (colorH[1] << 1) | (colorH[1] >> 6);
        colorH[2] = (colorH[2] << 2) | (colorH[2] >> 4);
        colorV[0] = (colorV[0] << 2) | (colorV[0] >> 4);
        colorV[1] = (colorV[1] << 1) | (colorV[1] >> 6);
        colorV[2] = (colorV[2] << 2) | (colorV[2] >> 4);

        for (var x = 0; x < 4; ++x)
        {
            for (var y = 0; y < 4; ++y)
            {
                WriteEtcRgb(
                    image,
                    width,
                    startX + x,
                    startY + y,
                    channels,
                    colorO,
                    colorH,
                    colorV,
                    x,
                    y);
            }
        }
    }

    private static void WriteEtcRgb(Span<byte> image, int width, int x, int y, int channels, ReadOnlySpan<byte> paintColors, int colorOffset)
    {
        var offset = channels * (y * width + x);
        image[offset] = paintColors[colorOffset];
        image[offset + 1] = paintColors[colorOffset + 1];
        image[offset + 2] = paintColors[colorOffset + 2];
    }

    private static void WriteEtcRgb(Span<byte> image, int width, int x, int y, int channels, ReadOnlySpan<int> colorO, ReadOnlySpan<int> colorH, ReadOnlySpan<int> colorV, int blockX, int blockY)
    {
        var offset = channels * (y * width + x);
        image[offset] = (byte)Clamp((blockX * (colorH[0] - colorO[0]) + blockY * (colorV[0] - colorO[0]) + 4 * colorO[0] + 2) >> 2);
        image[offset + 1] = (byte)Clamp((blockX * (colorH[1] - colorO[1]) + blockY * (colorV[1] - colorO[1]) + 4 * colorO[1] + 2) >> 2);
        image[offset + 2] = (byte)Clamp((blockX * (colorH[2] - colorO[2]) + blockY * (colorV[2] - colorO[2]) + 4 * colorO[2] + 2) >> 2);
    }

    private static void DecodeEtcSubblock(
        Span<byte> image,
        int width,
        int channels,
        ReadOnlySpan<int> averageColor,
        int table,
        uint pixelIndicesMsb,
        uint pixelIndicesLsb,
        int startX,
        int startY,
        int blockWidth,
        int blockHeight,
        int shiftStart,
        bool flip)
    {
        var shift = shiftStart;
        for (var x = startX; x < startX + blockWidth; ++x)
        {
            for (var y = startY; y < startY + blockHeight; ++y)
            {
                var index = (int)(((pixelIndicesMsb >> shift) & 1) << 1);
                index |= (int)((pixelIndicesLsb >> shift) & 1);
                shift++;
                index = _unscramble[index];
                var value = _compressParams[table, index];
                var offset = channels * (y * width + x);
                image[offset] = (byte)Clamp(averageColor[0] + value);
                image[offset + 1] = (byte)Clamp(averageColor[1] + value);
                image[offset + 2] = (byte)Clamp(averageColor[2] + value);
            }
            if (flip)
                shift += 2;
        }
    }

    private static void DecompressBlockDiffFlip(uint blockPart1, uint blockPart2, Span<byte> image, int width, int startX, int startY, int channels)
    {
        Span<int> averageColor = stackalloc int[3];
        Span<int> encodedColor1 = stackalloc int[3];
        Span<int> difference = stackalloc int[3];
        var differenceBit = (int)GetBitsHigh(blockPart1, 1, 33);
        var flipBit = GetBitsHigh(blockPart1, 1, 32) != 0;

        if (differenceBit == 0)
        {
            // We have diffbit = 0.
            averageColor[0] = (int)GetBitsHigh(blockPart1, 4, 63);
            averageColor[1] = (int)GetBitsHigh(blockPart1, 4, 55);
            averageColor[2] = (int)GetBitsHigh(blockPart1, 4, 47);
            averageColor[0] |= averageColor[0] << 4;
            averageColor[1] |= averageColor[1] << 4;
            averageColor[2] |= averageColor[2] << 4;
            var table = (int)GetBitsHigh(blockPart1, 3, 39) << 1;
            var pixelIndicesMsb = GetBits(blockPart2, 16, 31);
            var pixelIndicesLsb = GetBits(blockPart2, 16, 15);
            DecodeEtcSubblock(image, width, channels, averageColor, table, pixelIndicesMsb, pixelIndicesLsb,
                startX, startY, flipBit ? 4 : 2, flipBit ? 2 : 4, 0, flipBit);

            averageColor[0] = (int)GetBitsHigh(blockPart1, 4, 59);
            averageColor[1] = (int)GetBitsHigh(blockPart1, 4, 51);
            averageColor[2] = (int)GetBitsHigh(blockPart1, 4, 43);
            averageColor[0] |= averageColor[0] << 4;
            averageColor[1] |= averageColor[1] << 4;
            averageColor[2] |= averageColor[2] << 4;
            table = (int)GetBitsHigh(blockPart1, 3, 36) << 1;
            pixelIndicesMsb = GetBits(blockPart2, 16, 31);
            pixelIndicesLsb = GetBits(blockPart2, 16, 15);
            DecodeEtcSubblock(image, width, channels, averageColor, table, pixelIndicesMsb, pixelIndicesLsb,
                flipBit ? startX : startX + 2, flipBit ? startY + 2 : startY,
                flipBit ? 4 : 2, flipBit ? 2 : 4, flipBit ? 2 : 8, flipBit);
        }
        else
        {
            // We have diffbit = 1.
            encodedColor1[0] = (int)GetBitsHigh(blockPart1, 5, 63);
            encodedColor1[1] = (int)GetBitsHigh(blockPart1, 5, 55);
            encodedColor1[2] = (int)GetBitsHigh(blockPart1, 5, 47);
            averageColor[0] = (encodedColor1[0] << 3) | (encodedColor1[0] >> 2);
            averageColor[1] = (encodedColor1[1] << 3) | (encodedColor1[1] >> 2);
            averageColor[2] = (encodedColor1[2] << 3) | (encodedColor1[2] >> 2);
            var table = (int)GetBitsHigh(blockPart1, 3, 39) << 1;
            var pixelIndicesMsb = GetBits(blockPart2, 16, 31);
            var pixelIndicesLsb = GetBits(blockPart2, 16, 15);
            DecodeEtcSubblock(image, width, channels, averageColor, table, pixelIndicesMsb, pixelIndicesLsb,
                startX, startY, flipBit ? 4 : 2, flipBit ? 2 : 4, 0, flipBit);

            difference[0] = (int)GetBitsHigh(blockPart1, 3, 58);
            difference[1] = (int)GetBitsHigh(blockPart1, 3, 50);
            difference[2] = (int)GetBitsHigh(blockPart1, 3, 42);
            for (var i = 0; i < 3; ++i)
                if ((difference[i] & 4) != 0)
                    difference[i] -= 8;

            Span<int> encodedColor2 = stackalloc int[3];
            encodedColor2[0] = encodedColor1[0] + difference[0];
            encodedColor2[1] = encodedColor1[1] + difference[1];
            encodedColor2[2] = encodedColor1[2] + difference[2];
            averageColor[0] = (encodedColor2[0] << 3) | (encodedColor2[0] >> 2);
            averageColor[1] = (encodedColor2[1] << 3) | (encodedColor2[1] >> 2);
            averageColor[2] = (encodedColor2[2] << 3) | (encodedColor2[2] >> 2);
            table = (int)GetBitsHigh(blockPart1, 3, 36) << 1;
            pixelIndicesMsb = GetBits(blockPart2, 16, 31);
            pixelIndicesLsb = GetBits(blockPart2, 16, 15);
            DecodeEtcSubblock(image, width, channels, averageColor, table, pixelIndicesMsb, pixelIndicesLsb,
                flipBit ? startX : startX + 2, flipBit ? startY + 2 : startY,
                flipBit ? 4 : 2, flipBit ? 2 : 4, flipBit ? 2 : 8, flipBit);
        }
    }

    private static void DecompressBlockEtc2(uint blockPart1, uint blockPart2, Span<byte> image, int width, int height, int startX, int startY, int channels)
    {
        var differenceBit = GetBitsHigh(blockPart1, 1, 33) != 0;
        if (!differenceBit)
        {
            // We have diffbit = 0;
            DecompressBlockDiffFlip(blockPart1, blockPart2, image, width, startX, startY, channels);
            return;
        }

        Span<int> color1 = stackalloc int[3];
        Span<int> difference = stackalloc int[3];
        color1[0] = (int)GetBitsHigh(blockPart1, 5, 63);
        color1[1] = (int)GetBitsHigh(blockPart1, 5, 55);
        color1[2] = (int)GetBitsHigh(blockPart1, 5, 47);
        difference[0] = (int)GetBitsHigh(blockPart1, 3, 58);
        difference[1] = (int)GetBitsHigh(blockPart1, 3, 50);
        difference[2] = (int)GetBitsHigh(blockPart1, 3, 42);
        for (var i = 0; i < 3; ++i)
            if ((difference[i] & 4) != 0)
                difference[i] -= 8;

        var red = color1[0] + difference[0];
        var green = color1[1] + difference[1];
        var blue = color1[2] + difference[2];
        if (red < 0 || red > 31)
        {
            Unstuff59Bits(blockPart1, blockPart2, out var block59Part1, out var block59Part2);
            DecompressBlockThumb59(block59Part1, block59Part2, image, width, startX, startY, channels);
        }
        else if (green < 0 || green > 31)
        {
            Unstuff58Bits(blockPart1, blockPart2, out var block58Part1, out var block58Part2);
            DecompressBlockThumb58(block58Part1, block58Part2, image, width, startX, startY, channels);
        }
        else if (blue < 0 || blue > 31)
        {
            Unstuff57Bits(blockPart1, blockPart2, out var block57Part1, out var block57Part2);
            DecompressBlockPlanar(block57Part1, block57Part2, image, width, startX, startY, channels);
        }
        else
        {
            DecompressBlockDiffFlip(blockPart1, blockPart2, image, width, startX, startY, channels);
        }
    }

    private static int GetAlphaBit(byte input, int fromPosition, int toPosition)
    {
        if (fromPosition > toPosition)
            return ((1 << fromPosition) & input) >> (fromPosition - toPosition);
        return ((1 << fromPosition) & input) << (toPosition - fromPosition);
    }

    private static int Clamp(int value)
    {
        if (value < 0)
            value = 0;
        if (value > 255)
            value = 255;
        return value;
    }

    private static void DecompressBlockAlpha(
        ReadOnlySpan<byte> data,
        Span<byte> image,
        int width,
        int height,
        int startX,
        int startY,
        int channels)
    {
        var alpha = data[0];
        var table = data[1];
        var bit = 0;
        var dataByte = 2;

        // extract an alpha value for each pixel.
        for (var x = 0; x < 4; ++x)
        {
            for (var y = 0; y < 4; ++y)
            {
                // Extract table index
                var index = 0;
                for (var bitPosition = 0; bitPosition < 3; ++bitPosition)
                {
                    index |= GetAlphaBit(data[dataByte], 7 - bit, 2 - bitPosition);
                    bit++;
                    if (bit > 7)
                    {
                        bit = 0;
                        dataByte++;
                    }
                }
                image[(startX + x + (startY + y) * width) * channels] = (byte)Clamp(alpha + _alphaTable[table * 8 + index]);
            }
        }
    }

    private static uint ReadBigEndian4ByteWord(ReadOnlySpan<byte> source, int offset)
    {
        return ((uint)source[offset] << 24)
            | ((uint)source[offset + 1] << 16)
            | ((uint)source[offset + 2] << 8)
            | source[offset + 3];
    }

    private static int LimitCoordinate(int value, int size)
    {
        return value & (size - 1);
    }

    private static void Unpack5554Colour(ReadOnlySpan<byte> blocks, int blockOffset, Span<int> abColours)
    {
        Span<uint> rawBits = stackalloc uint[2];
        var packedData1 = BinaryPrimitives.ReadUInt32LittleEndian(blocks.Slice(blockOffset + 4, 4));

        // Extract A and B
        rawBits[0] = packedData1 & 0xfffe;
        rawBits[1] = packedData1 >> 16;

        // Step through both colours
        for (var i = 0; i < 2; ++i)
        {
            var colourOffset = i * 4;
            // If completely opaque
            if ((rawBits[i] & (1 << 15)) != 0)
            {
                // Extract R and G (both 5 bit)
                abColours[colourOffset] = (int)((rawBits[i] >> 10) & 0x1f);
                abColours[colourOffset + 1] = (int)((rawBits[i] >> 5) & 0x1f);

                // The precision of Blue depends on A or B. If A then we need to
                // replicate the top bit to get 5 bits in total
                abColours[colourOffset + 2] = (int)(rawBits[i] & 0x1f);
                if (i == 0)
                    abColours[2] |= abColours[2] >> 4;

                // Set 4bit alpha fully on...
                abColours[colourOffset + 3] = 0xf;
            }
            // Else if colour has variable translucency
            else
            {
                // Extract R and G (both 4 bit).
                // (Leave a space on the end for the replication of bits
                abColours[colourOffset] = (int)((rawBits[i] >> (8 - 1)) & 0x1e);
                abColours[colourOffset + 1] = (int)((rawBits[i] >> (4 - 1)) & 0x1e);

                // Replicate bits to truly expand to 5 bits
                abColours[colourOffset] |= abColours[colourOffset] >> 4;
                abColours[colourOffset + 1] |= abColours[colourOffset + 1] >> 4;

                // Grab the 3(+padding) or 4 bits of blue and add an extra padding bit
                abColours[colourOffset + 2] = (int)((rawBits[i] & 0xf) << 1);

                // Expand from 3 to 5 bits if this is from colour A, or 4 to 5 bits if from
                // colour B
                if (i == 0)
                    abColours[2] |= abColours[2] >> 3;
                else
                    abColours[2] |= abColours[2] >> 4;

                // Set the alpha bits to be 3 + a zero on the end
                abColours[colourOffset + 3] = (int)((rawBits[i] >> 11) & 0xe);
            }
        }
    }

    private static void UnpackModulations(
        ReadOnlySpan<byte> blocks,
        int blockOffset,
        bool do2bitMode,
        Span<int> modulationValues,
        Span<int> modulationModes,
        int startX,
        int startY)
    {
        var blockModulationMode = (int)(BinaryPrimitives.ReadUInt32LittleEndian(blocks.Slice(blockOffset + 4, 4)) & 1);
        var modulationBits = BinaryPrimitives.ReadUInt32LittleEndian(blocks.Slice(blockOffset, 4));

        // If it's in an interpolated mode
        if (do2bitMode && blockModulationMode != 0)
        {
            // Run through all the pixels in the block. Note we can now treat all the
            // "stored" values as if they have 2bits (even when they didn't!)
            for (var y = 0; y < 4; ++y)
            {
                for (var x = 0; x < 8; ++x)
                {
                    modulationModes[(y + startY) * 16 + x + startX] = blockModulationMode;

                    // If this is a stored value...
                    if (((x ^ y) & 1) == 0)
                    {
                        modulationValues[(y + startY) * 16 + x + startX] = (int)(modulationBits & 3);
                        modulationBits >>= 2;
                    }
                }
            }
        }
        // Else if direct encoded 2bit mode - i.e. 1 mode bit per pixel
        else if (do2bitMode)
        {
            for (var y = 0; y < 4; ++y)
            {
                for (var x = 0; x < 8; ++x)
                {
                    modulationModes[(y + startY) * 16 + x + startX] = blockModulationMode;

                    // Double the bits so 0=> 00, and 1=>11
                    modulationValues[(y + startY) * 16 + x + startX] = (modulationBits & 1) != 0 ? 0x3 : 0x0;
                    modulationBits >>= 1;
                }
            }
        }
        // Else its the 4bpp mode so each value has 2 bits
        else
        {
            for (var y = 0; y < 4; ++y)
            {
                for (var x = 0; x < 4; ++x)
                {
                    modulationModes[(y + startY) * 16 + x + startX] = blockModulationMode;
                    modulationValues[(y + startY) * 16 + x + startX] = (int)(modulationBits & 3);
                    modulationBits >>= 2;
                }
            }
        }
    }

    private static void InterpolateColours(
        ReadOnlySpan<int> colourP,
        ReadOnlySpan<int> colourQ,
        ReadOnlySpan<int> colourR,
        ReadOnlySpan<int> colourS,
        bool do2bitMode,
        int x,
        int y,
        Span<int> result)
    {
        Span<int> p = stackalloc int[4];
        Span<int> q = stackalloc int[4];
        Span<int> r = stackalloc int[4];
        Span<int> s = stackalloc int[4];

        // Copy the colours
        for (var k = 0; k < 4; ++k)
        {
            p[k] = colourP[k];
            q[k] = colourQ[k];
            r[k] = colourR[k];
            s[k] = colourS[k];
        }

        // Put the x and y values into the right range
        var v = (y & 0x3) | ((~y & 0x2) << 1);
        var u = do2bitMode ? (x & 0x7) | ((~x & 0x4) << 1) : (x & 0x3) | ((~x & 0x2) << 1);

        // Get the u and v scale amounts
        v -= 4 / 2;
        var uScale = do2bitMode ? 8 : 4;
        u -= do2bitMode ? 8 / 2 : 4 / 2;

        for (var k = 0; k < 4; ++k)
        {
            var tmp1 = p[k] * uScale + u * (q[k] - p[k]);
            var tmp2 = r[k] * uScale + u * (s[k] - r[k]);
            tmp1 = tmp1 * 4 + v * (tmp2 - tmp1);
            result[k] = tmp1;
        }

        // Lop off the appropriate number of bits to get us to 8 bit precision
        if (do2bitMode)
        {
            // Do RGB
            for (var k = 0; k < 3; ++k)
                result[k] >>= 2;
            result[3] >>= 1;
        }
        else
        {
            // Do RGB  (A is ok)
            for (var k = 0; k < 3; ++k)
                result[k] >>= 1;
        }

        // Convert from 5554 to 8888
        // do RGB 5.3 => 8
        for (var k = 0; k < 3; ++k)
            result[k] += result[k] >> 5;
        result[3] += result[3] >> 4;
    }

    private static void GetModulationValue(
        int x,
        int y,
        bool do2bitMode,
        ReadOnlySpan<int> modulationValues,
        ReadOnlySpan<int> modulationModes,
        out int modulation,
        out bool doPunchThrough)
    {
        ReadOnlySpan<int> replicatedValues = stackalloc[] { 0, 3, 5, 8 };
        ReadOnlySpan<int> replicatedValuesPunchThrough = stackalloc[] { 0, 4, 4, 8 };

        // Map X and Y into the local 2x2 block
        y = (y & 0x3) | ((~y & 0x2) << 1);
        x = do2bitMode ? (x & 0x7) | ((~x & 0x4) << 1) : (x & 0x3) | ((~x & 0x2) << 1);

        // Assume no PT for now
        doPunchThrough = false;
        var modulationMode = modulationModes[y * 16 + x];

        // Extract the modulation value. If a simple encoding
        if (modulationMode == 0)
        {
            modulation = replicatedValues[modulationValues[y * 16 + x]];
        }
        else if (do2bitMode)
        {
            // If this is a stored value
            if (((x ^ y) & 1) == 0)
            {
                modulation = replicatedValues[modulationValues[y * 16 + x]];
            }
            // Else average from the neighbours
            // If H&V interpolation...
            else if (modulationMode == 1)
            {
                modulation = (replicatedValues[modulationValues[(y - 1) * 16 + x]]
                    + replicatedValues[modulationValues[(y + 1) * 16 + x]]
                    + replicatedValues[modulationValues[y * 16 + x - 1]]
                    + replicatedValues[modulationValues[y * 16 + x + 1]] + 2) / 4;
            }
            // Else if H-Only
            else if (modulationMode == 2)
            {
                modulation = (replicatedValues[modulationValues[y * 16 + x - 1]]
                    + replicatedValues[modulationValues[y * 16 + x + 1]] + 1) / 2;
            }
            // Else it's V-Only
            else
            {
                modulation = (replicatedValues[modulationValues[(y - 1) * 16 + x]]
                    + replicatedValues[modulationValues[(y + 1) * 16 + x]] + 1) / 2;
            }
        }
        // Else it's 4BPP and PT encoding
        else
        {
            var value = modulationValues[y * 16 + x];
            modulation = replicatedValuesPunchThrough[value];
            doPunchThrough = value == 2;
        }
    }

    private static uint TwiddleUv(uint ySize, uint xSize, uint yPosition, uint xPosition)
    {
        uint minDimension;
        uint maxValue;
        if (ySize < xSize)
        {
            minDimension = ySize;
            maxValue = xPosition;
        }
        else
        {
            minDimension = xSize;
            maxValue = yPosition;
        }

        // Step through all the bits in the "minimum" dimension
        uint sourceBitPosition = 1;
        uint destinationBitPosition = 1;
        uint twiddled = 0;
        var shiftCount = 0;
        while (sourceBitPosition < minDimension)
        {
            if ((yPosition & sourceBitPosition) != 0)
                twiddled |= destinationBitPosition;
            if ((xPosition & sourceBitPosition) != 0)
                twiddled |= destinationBitPosition << 1;
            sourceBitPosition <<= 1;
            destinationBitPosition <<= 2;
            shiftCount += 1;
        }

        // Prepend any unused bits
        maxValue >>= shiftCount;
        twiddled |= maxValue << (2 * shiftCount);
        return twiddled;
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
