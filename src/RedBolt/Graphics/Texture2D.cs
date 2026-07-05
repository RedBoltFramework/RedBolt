using System.Numerics;

namespace RedBolt;

public sealed class Texture2D : Texture
{
    private const uint DdsHeaderSize = 124u;
    private const uint DdsPixelFormatSize = 32u;
    private const uint FourCcDxt1 = 0x31545844u;
    private const uint FourCcDxt3 = 0x33545844u;
    private const uint FourCcDxt5 = 0x35545844u;

    private readonly Dictionary<uint, byte[]> levelData = new();

    public Texture2D(string name)
        : base(name)
    {
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var reader = new BinaryReader(source, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != 0x20534444u)
            return false;

        var headerSize = reader.ReadUInt32();
        if (headerSize != DdsHeaderSize)
            return false;

        _ = reader.ReadUInt32();
        var height = reader.ReadInt32();
        var width = reader.ReadInt32();
        var pitchOrLinearSize = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var mipMapCount = reader.ReadUInt32();
        _ = reader.ReadBytes(44);

        var pixelFormatSize = reader.ReadUInt32();
        if (pixelFormatSize != DdsPixelFormatSize)
            return false;

        var pixelFormatFlags = reader.ReadUInt32();
        var fourCc = reader.ReadUInt32();
        var rgbBitCount = reader.ReadUInt32();
        var rMask = reader.ReadUInt32();
        var gMask = reader.ReadUInt32();
        var bMask = reader.ReadUInt32();
        var aMask = reader.ReadUInt32();

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();

        if (width <= 0 || height <= 0)
            return false;

        byte[] rgbaData;
        if ((pixelFormatFlags & 0x4u) != 0)
        {
            rgbaData = fourCc switch
            {
                FourCcDxt1 => DecodeBc1(reader.ReadBytes(GetCompressedSize(width, height, 8))),
                FourCcDxt3 => DecodeBc2(reader.ReadBytes(GetCompressedSize(width, height, 16))),
                FourCcDxt5 => DecodeBc3(reader.ReadBytes(GetCompressedSize(width, height, 16))),
                _ => []
            };
        }
        else if ((pixelFormatFlags & 0x40u) != 0 && rgbBitCount == 32u)
        {
            var size = checked(width * height * 4);
            rgbaData = DecodeRgba(reader.ReadBytes(size), rMask, gMask, bMask, aMask);
        }
        else
        {
            return false;
        }

        if (rgbaData.Length != checked(width * height * 4))
            return false;

        levelData.Clear();
        Levels = Math.Max(mipMapCount, 1u);
        SetSize(width, height, TextureFormat.RGBA8);
        Levels = 1u;
        levelData[0] = rgbaData;
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetSize(int width, int height, TextureFormat format)
    {
        if (width <= 0 || height <= 0)
            return false;

        SetDimensions(width, height, 1, format);
        levelData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (level >= Levels || x < 0 || y < 0 || width <= 0 || height <= 0)
            return false;

        var levelWidth = GetLevelWidth(level);
        var levelHeight = GetLevelHeight(level);
        if (x + width > levelWidth || y + height > levelHeight)
            return false;

        var bytesPerPixel = (int)GetComponents();
        if (bytesPerPixel == 0)
            return false;

        var expected = checked(width * height * bytesPerPixel);
        if (data.Length != expected)
            return false;

        if (!levelData.TryGetValue(level, out var levelBytes))
        {
            levelBytes = new byte[checked(levelWidth * levelHeight * bytesPerPixel)];
            levelData[level] = levelBytes;
        }

        for (var row = 0; row < height; ++row)
        {
            var sourceOffset = row * width * bytesPerPixel;
            var destinationOffset = ((y + row) * levelWidth + x) * bytesPerPixel;
            data.Slice(sourceOffset, width * bytesPerPixel).CopyTo(levelBytes.AsSpan(destinationOffset, width * bytesPerPixel));
        }

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint level, int x, int y, int width, int height, byte[] data) => SetData(level, x, y, width, height, data.AsSpan());

    public byte[]? GetData(uint level)
    {
        return levelData.TryGetValue(level, out var bytes) ? bytes.ToArray() : null;
    }

    private void RefreshTotalMemoryUse()
    {
        RefreshMemoryUse((uint)levelData.Sum(static item => item.Value.Length));
    }

    private static int GetCompressedSize(int width, int height, int bytesPerBlock)
    {
        var blockWidth = Math.Max((width + 3) / 4, 1);
        var blockHeight = Math.Max((height + 3) / 4, 1);
        return checked(blockWidth * blockHeight * bytesPerBlock);
    }

    private static byte[] DecodeBc1(byte[] data)
    {
        var output = new byte[(data.Length / 8) * 64];
        for (int inputOffset = 0, outputOffset = 0; inputOffset < data.Length; inputOffset += 8, outputOffset += 64)
            DecodeBc1Block(data.AsSpan(inputOffset, 8), output.AsSpan(outputOffset, 64));
        return output;
    }

    private static byte[] DecodeBc2(byte[] data)
    {
        var output = new byte[(data.Length / 16) * 64];
        for (int inputOffset = 0, outputOffset = 0; inputOffset < data.Length; inputOffset += 16, outputOffset += 64)
            DecodeBc2Block(data.AsSpan(inputOffset, 16), output.AsSpan(outputOffset, 64));
        return output;
    }

    private static byte[] DecodeBc3(byte[] data)
    {
        var output = new byte[(data.Length / 16) * 64];
        for (int inputOffset = 0, outputOffset = 0; inputOffset < data.Length; inputOffset += 16, outputOffset += 64)
            DecodeBc3Block(data.AsSpan(inputOffset, 16), output.AsSpan(outputOffset, 64));
        return output;
    }

    private static void DecodeBc1Block(ReadOnlySpan<byte> block, Span<byte> output)
    {
        Span<uint> colors = stackalloc uint[4];
        BuildBc1Palette(block[..4], colors, opaqueOnly: false);
        WriteColorIndices(block[4..8], colors, output);
    }

    private static void DecodeBc2Block(ReadOnlySpan<byte> block, Span<byte> output)
    {
        Span<uint> colors = stackalloc uint[4];
        BuildBc1Palette(block[8..12], colors, opaqueOnly: true);
        WriteColorIndices(block[12..16], colors, output);

        for (var i = 0; i < 16; ++i)
        {
            var alphaByte = block[i / 2];
            var alpha4 = (alphaByte >> ((i & 1) * 4)) & 0x0f;
            output[i * 4 + 3] = (byte)((alpha4 << 4) | alpha4);
        }
    }

    private static void DecodeBc3Block(ReadOnlySpan<byte> block, Span<byte> output)
    {
        Span<byte> alphaPalette = stackalloc byte[8];
        BuildBc3AlphaPalette(block[0], block[1], alphaPalette);

        ulong alphaIndices = 0;
        for (var i = 0; i < 6; ++i)
            alphaIndices |= (ulong)block[2 + i] << (8 * i);

        Span<uint> colors = stackalloc uint[4];
        BuildBc1Palette(block[8..12], colors, opaqueOnly: true);
        WriteColorIndices(block[12..16], colors, output);

        for (var i = 0; i < 16; ++i)
        {
            var paletteIndex = (int)((alphaIndices >> (3 * i)) & 0x7);
            output[i * 4 + 3] = alphaPalette[paletteIndex];
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
            output[i * 4 + 0] = (byte)(color & 0xff);
            output[i * 4 + 1] = (byte)((color >> 8) & 0xff);
            output[i * 4 + 2] = (byte)((color >> 16) & 0xff);
            output[i * 4 + 3] = (byte)((color >> 24) & 0xff);
        }
    }

    private static byte[] DecodeRgba(byte[] data, uint rMask, uint gMask, uint bMask, uint aMask)
    {
        var output = new byte[data.Length];
        for (var i = 0; i < data.Length; i += 4)
        {
            var pixel = BitConverter.ToUInt32(data, i);
            output[i + 0] = ExtractChannel(pixel, rMask);
            output[i + 1] = ExtractChannel(pixel, gMask);
            output[i + 2] = ExtractChannel(pixel, bMask);
            output[i + 3] = aMask != 0 ? ExtractChannel(pixel, aMask) : (byte)255;
        }

        return output;
    }

    private static byte ExtractChannel(uint pixel, uint mask)
    {
        if (mask == 0)
            return 0;

        var shift = BitOperations.TrailingZeroCount(mask);
        var bits = BitOperations.PopCount(mask);
        var value = (pixel & mask) >> shift;
        return bits >= 8
            ? (byte)(value >> (bits - 8))
            : (byte)((value * 255u) / ((1u << bits) - 1u));
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
}
