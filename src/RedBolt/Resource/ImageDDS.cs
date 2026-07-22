using System.Numerics;
using System.Buffers.Binary;

namespace RedBolt;

/// <summary>Compressed image mip level.</summary>
public struct CompressedLevel
{
    /// <summary>Compressed image data.</summary>
    public Memory<byte> Data { get; set; }

    /// <summary>Compression format.</summary>
    public TextureFormat Format { get; set; }

    /// <summary>Width.</summary>
    public int Width { get; set; }

    /// <summary>Height.</summary>
    public int Height { get; set; }

    /// <summary>Depth.</summary>
    public int Depth { get; set; }

    /// <summary>Total data size in bytes.</summary>
    public int DataSize { get; set; }

    public bool Decompress(Span<byte> destination)
    {
        var expectedSize = checked(Width * Height * Depth * 4);
        if (Data.IsEmpty || destination.Length < expectedSize)
            return false;

        switch (Format)
        {
            case TextureFormat.TexFormatRgba8Unorm:
                Data.Span[..expectedSize].CopyTo(destination);
                return true;
            case TextureFormat.TexFormatBc1Unorm:
            case TextureFormat.TexFormatBc2Unorm:
            case TextureFormat.TexFormatBc3Unorm:
                RedBolt.Decompress.DecompressImageDXT(destination, Data.Span, Width, Height, Depth, Format);
                return true;
            case TextureFormat.TexFormatEtc2Rgb8Unorm:
                RedBolt.Decompress.DecompressImageETC(destination, Data.Span, Width, Height, false);
                return true;
            case TextureFormat.TexFormatEtc2Rgba8Unorm:
                RedBolt.Decompress.DecompressImageETC(destination, Data.Span, Width, Height, true);
                return true;
        }

        if (Format == EmulatedTextureFormat.PvrtcRgb2Bpp
            || Format == EmulatedTextureFormat.PvrtcRgba2Bpp
            || Format == EmulatedTextureFormat.PvrtcRgb4Bpp
            || Format == EmulatedTextureFormat.PvrtcRgba4Bpp)
        {
            RedBolt.Decompress.DecompressImagePVRTC(destination, Data.Span, Width, Height, Format);
            return true;
        }

        return false;
    }
}

internal static class ImageDds
{
    private const uint FileId = 0x20534444u;
    private const uint HeaderSize = 124u;
    private const uint PixelFormatSize = 32u;

    // C++ struct DDColorKey { unsigned dwColorSpaceLowValue_; unsigned dwColorSpaceHighValue_; };
    // C++ struct DDSCaps2 { unsigned dwCaps_; unsigned dwCaps2_; unsigned dwCaps3_; /* union with dwCaps4_/dwVolumeDepth_ */ };
    // C++ struct DDSurfaceDesc2 { /* full DirectDraw surface description with 18 fields */ };

    private const uint FourCcDx10 = 0x30315844u;
    private const uint FourCcDxt1 = 0x31545844u;
    private const uint FourCcDxt3 = 0x33545844u;
    private const uint FourCcDxt5 = 0x35545844u;

    private const uint DxgiFormatR32G32B32A32Float = 2u;
    private const uint DxgiFormatR16G16B16A16Float = 10u;
    private const uint DxgiFormatR16G16B16A16Unorm = 11u;
    private const uint DxgiFormatR10G10B10A2Unorm = 24u;
    private const uint DxgiFormatR9G9B9E5SharedExp = 67u;
    private const uint DxgiFormatR8G8B8A8Unorm = 28u;
    private const uint DxgiFormatR8G8B8A8UnormSrgb = 26u;
    private const uint DxgiFormatBc1Unorm = 71u;
    private const uint DxgiFormatBc1UnormSrgb = 72u;
    private const uint DxgiFormatBc2Unorm = 74u;
    private const uint DxgiFormatBc2UnormSrgb = 75u;
    private const uint DxgiFormatBc3Unorm = 77u;
    private const uint DxgiFormatBc3UnormSrgb = 78u;
    private const uint DxgiFormatB5G6R5Unorm = 85u;
    private const uint DxgiFormatB5G5R5A1Unorm = 86u;

    private const uint FourCcEtc1 = 0x31435445u;
    private const uint FourCcEtc2 = 0x32435445u;
    private const uint FourCcEtc2A = 0x41325445u;
    private const uint FourCcPtc2 = 0x32435450u;
    private const uint FourCcPtc4 = 0x34435450u;
    private const uint D3DFormatA16B16G16R16 = 36u;
    private const uint D3DFormatA16B16G16R16F = 113u;
    private const uint D3DFormatA32B32G32R32F = 116u;

    private const uint DdsCaps2CubemapAllFaces = 0x0000FC00u;
    private const uint DdsResourceMiscTextureCube = 0x4u;

    private static uint MakeFourCC(char ch0, char ch1, char ch2, char ch3)
    {
        return (uint)ch0 | ((uint)ch1 << 8) | ((uint)ch2 << 16) | ((uint)ch3 << 24);
    }

    private static TextureFormat PickTextureFormat(DdsPixelFormat pixelFormat, uint dxgiFormat)
    {
        return dxgiFormat switch
        {
            DxgiFormatR32G32B32A32Float or DxgiFormatR16G16B16A16Float or DxgiFormatR16G16B16A16Unorm
                or DxgiFormatR10G10B10A2Unorm or DxgiFormatR9G9B9E5SharedExp
                or DxgiFormatR8G8B8A8Unorm or DxgiFormatR8G8B8A8UnormSrgb => TextureFormat.TexFormatRgba8Unorm,
            DxgiFormatBc1Unorm or DxgiFormatBc1UnormSrgb => TextureFormat.TexFormatBc1Unorm,
            DxgiFormatBc2Unorm or DxgiFormatBc2UnormSrgb => TextureFormat.TexFormatBc2Unorm,
            DxgiFormatBc3Unorm or DxgiFormatBc3UnormSrgb => TextureFormat.TexFormatBc3Unorm,
            DxgiFormatB5G6R5Unorm => TextureFormat.TexFormatB5g6r5Unorm,
            DxgiFormatB5G5R5A1Unorm => TextureFormat.TexFormatB5g5r5a1Unorm,
            _ => pixelFormat.FourCc switch
            {
                FourCcDxt1 => TextureFormat.TexFormatBc1Unorm,
                FourCcDxt3 => TextureFormat.TexFormatBc2Unorm,
                FourCcDxt5 => TextureFormat.TexFormatBc3Unorm,
                FourCcEtc1 or FourCcEtc2 => TextureFormat.TexFormatEtc2Rgb8Unorm,
                FourCcEtc2A => TextureFormat.TexFormatEtc2Rgba8Unorm,
                FourCcPtc2 => EmulatedTextureFormat.PvrtcRgba2Bpp,
                FourCcPtc4 => EmulatedTextureFormat.PvrtcRgba4Bpp,
                D3DFormatA16B16G16R16 => TextureFormat.TexFormatRgba16Unorm,
                D3DFormatA16B16G16R16F => TextureFormat.TexFormatRgba16Float,
                D3DFormatA32B32G32R32F => TextureFormat.TexFormatRgba32Float,
                _ when AreTextureComponentsMatching(pixelFormat, TextureFormat.TexFormatRgba8Unorm)
                    => TextureFormat.TexFormatRgba8Unorm,
                _ when AreTextureComponentsMatching(pixelFormat, TextureFormat.TexFormatB5g6r5Unorm)
                    => TextureFormat.TexFormatB5g6r5Unorm,
                _ when AreTextureComponentsMatching(pixelFormat, TextureFormat.TexFormatB5g5r5a1Unorm)
                    => TextureFormat.TexFormatB5g5r5a1Unorm,
                _ when pixelFormat.RgbBitCount == 32 => TextureFormat.TexFormatRgba8Unorm,
                _ => TextureFormat.TexFormatUnknown
            }
        };
    }

    private static bool AreTextureComponentsMatching(DdsPixelFormat pixelFormat, TextureFormat textureFormat)
    {
        return textureFormat switch
        {
            TextureFormat.TexFormatRgba8Unorm => pixelFormat.RgbBitCount == 32
                && pixelFormat.RMask == 0x000000ff
                && pixelFormat.GMask == 0x0000ff00
                && pixelFormat.BMask == 0x00ff0000
                && pixelFormat.AMask == 0xff000000,
            TextureFormat.TexFormatB5g6r5Unorm => pixelFormat.RgbBitCount == 16
                && pixelFormat.RMask == 0x0000f800
                && pixelFormat.GMask == 0x000007e0
                && pixelFormat.BMask == 0x0000001f
                && pixelFormat.AMask == 0,
            TextureFormat.TexFormatB5g5r5a1Unorm => pixelFormat.RgbBitCount == 16
                && pixelFormat.RMask == 0x00007c00
                && pixelFormat.GMask == 0x000003e0
                && pixelFormat.BMask == 0x0000001f
                && pixelFormat.AMask == 0x00008000,
            _ => false
        };
    }

    internal static bool TryLoad(Image image, Stream source)
    {
        var fileData = ReadRemaining(source);
        var offset = 0;

        if (ReadUInt32(fileData, ref offset) != FileId)
            return false;
        if (ReadUInt32(fileData, ref offset) != HeaderSize)
            return false;

        _ = ReadUInt32(fileData, ref offset);
        var height = ReadInt32(fileData, ref offset);
        var width = ReadInt32(fileData, ref offset);
        _ = ReadUInt32(fileData, ref offset);
        var depth = Math.Max(ReadInt32(fileData, ref offset), 1);
        var mipMapCount = Math.Max(ReadUInt32(fileData, ref offset), 1u);
        if (!Skip(fileData, ref offset, 44))
            return false;

        if (ReadUInt32(fileData, ref offset) != PixelFormatSize)
            return false;

        var pixelFormat = new DdsPixelFormat(
            ReadUInt32(fileData, ref offset),
            ReadUInt32(fileData, ref offset),
            ReadUInt32(fileData, ref offset),
            ReadUInt32(fileData, ref offset),
            ReadUInt32(fileData, ref offset),
            ReadUInt32(fileData, ref offset),
            ReadUInt32(fileData, ref offset));

        _ = ReadUInt32(fileData, ref offset);
        var caps2 = ReadUInt32(fileData, ref offset);
        _ = ReadUInt32(fileData, ref offset);
        _ = ReadUInt32(fileData, ref offset);
        _ = ReadUInt32(fileData, ref offset);

        if (width <= 0 || height <= 0)
            return false;

        DdsHeader10? header10 = null;
        if (pixelFormat.FourCc == FourCcDx10)
        {
            header10 = new DdsHeader10(
                ReadUInt32(fileData, ref offset),
                ReadUInt32(fileData, ref offset),
                ReadUInt32(fileData, ref offset),
                ReadUInt32(fileData, ref offset),
                ReadUInt32(fileData, ref offset));
        }

        var formatInfo = ResolveFormat(pixelFormat, header10);
        if (!formatInfo.IsSupported)
            return false;

        var cubemap = (caps2 & DdsCaps2CubemapAllFaces) != 0
            || (header10 is { MiscFlag: var miscFlag } && (miscFlag & DdsResourceMiscTextureCube) != 0);
        var array = !cubemap && header10 is { ArraySize: > 1 };
        var imageChainCount = 1u;
        if (cubemap)
            imageChainCount = 6u;
        else if (header10 is { ArraySize: > 1 } dx10)
            imageChainCount = dx10.ArraySize;

        Image? previous = null;
        for (var imageIndex = 0u; imageIndex < imageChainCount; ++imageIndex)
        {
            var target = imageIndex == 0 ? image : new Image($"{image.Name}#{imageIndex}");
            var storedData = formatInfo.Format == TextureFormat.TexFormatRgba8Unorm
                ? ReadRgbaLevels(fileData, ref offset, width, height, depth, mipMapCount, formatInfo)
                : ReadCompressedLevels(fileData, ref offset, width, height, depth, mipMapCount, formatInfo.Format);

            if (storedData is null)
                return false;

            target.SetCompressedData(
                width,
                height,
                depth,
                checked((int)formatInfo.Components),
                formatInfo.Format,
                checked((int)mipMapCount),
                storedData,
                cubemap,
                array,
                formatInfo.SRgb);

            previous?.SetNextSibling(target);
            previous = target;
        }

        previous?.SetNextSibling(null);
        return true;
    }

    internal static int GetCompressedLevelSize(TextureFormat format, int width, int height, int depth)
    {
        var formatInfo = RenderAPIUtils.GetTextureFormatInfo(format);
        var widthInBlocks = (width + formatInfo.BlockWidth - 1) / formatInfo.BlockWidth;
        var heightInBlocks = (height + formatInfo.BlockHeight - 1) / formatInfo.BlockHeight;
        return checked(formatInfo.GetElementSize() * widthInBlocks * heightInBlocks * depth);
    }

    private static byte[]? ReadCompressedLevels(byte[] fileData, ref int offset, int width, int height, int depth, uint mipMapCount, TextureFormat format)
    {
        var totalSize = 0;
        for (var level = 0u; level < mipMapCount; ++level)
            totalSize = checked(totalSize + GetCompressedLevelSize(format, GetLevelDimension(width, level), GetLevelDimension(height, level), GetLevelDimension(depth, level)));

        var bytes = ReadBytes(fileData, ref offset, totalSize);
        return bytes.Length == totalSize ? bytes : null;
    }

    private static byte[]? ReadRgbaLevels(byte[] fileData, ref int offset, int width, int height, int depth, uint mipMapCount, DdsFormatInfo formatInfo)
    {
        var totalSize = 0;
        for (var level = 0u; level < mipMapCount; ++level)
        {
            totalSize = checked(totalSize + GetCompressedLevelSize(
                TextureFormat.TexFormatRgba8Unorm,
                GetLevelDimension(width, level),
                GetLevelDimension(height, level),
                GetLevelDimension(depth, level)));
        }

        var converted = new byte[totalSize];
        var destinationOffset = 0;
        for (var level = 0u; level < mipMapCount; ++level)
        {
            var levelWidth = GetLevelDimension(width, level);
            var levelHeight = GetLevelDimension(height, level);
            var levelDepth = GetLevelDimension(depth, level);
            var sourceLevelSize = checked(levelWidth * levelHeight * levelDepth * (int)formatInfo.SourceBytesPerPixel);
            var sourceLevel = ReadBytes(fileData, ref offset, sourceLevelSize);
            if (sourceLevel.Length != sourceLevelSize)
                return null;

            var convertedLevelSize = GetCompressedLevelSize(TextureFormat.TexFormatRgba8Unorm, levelWidth, levelHeight, levelDepth);
            ConvertToRgba(sourceLevel, converted.AsSpan(destinationOffset, convertedLevelSize), formatInfo);
            destinationOffset += convertedLevelSize;
        }

        return converted;
    }

    private static void ConvertToRgba(ReadOnlySpan<byte> source, Span<byte> destination, DdsFormatInfo formatInfo)
    {
        var sourcePixelSize = (int)formatInfo.SourceBytesPerPixel;
        for (var sourceOffset = 0; sourceOffset < source.Length; sourceOffset += sourcePixelSize)
        {
            var destinationOffset = (sourceOffset / sourcePixelSize) * 4;
            if (formatInfo.Encoding is DdsPixelEncoding.Float16 or DdsPixelEncoding.Unorm16)
            {
                var scale = formatInfo.Encoding == DdsPixelEncoding.Float16 ? 1.0f : 1.0f / ushort.MaxValue;
                for (var channel = 0; channel < 4; ++channel)
                {
                    var raw = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(sourceOffset + channel * 2, 2));
                    var value = formatInfo.Encoding == DdsPixelEncoding.Float16
                        ? (float)BitConverter.UInt16BitsToHalf(raw)
                        : raw * scale;
                    destination[destinationOffset + channel] = ToByte(value);
                }
                continue;
            }

            if (formatInfo.Encoding == DdsPixelEncoding.Float32)
            {
                for (var channel = 0; channel < 4; ++channel)
                {
                    var raw = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(sourceOffset + channel * 4, 4));
                    destination[destinationOffset + channel] = ToByte(BitConverter.Int32BitsToSingle((int)raw));
                }
                continue;
            }

            if (formatInfo.Encoding == DdsPixelEncoding.SharedExponent)
            {
                var packed = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(sourceOffset, 4));
                var exponent = (packed >> 27) & 0x1fu;
                var scale = MathF.Pow(2.0f, exponent - 24);
                destination[destinationOffset + 0] = ToByte((packed & 0x1ffu) * scale);
                destination[destinationOffset + 1] = ToByte(((packed >> 9) & 0x1ffu) * scale);
                destination[destinationOffset + 2] = ToByte(((packed >> 18) & 0x1ffu) * scale);
                destination[destinationOffset + 3] = 255;
                continue;
            }

            uint pixel = sourcePixelSize switch
            {
                2 => BitConverter.ToUInt16(source.Slice(sourceOffset, 2)),
                3 => (uint)(source[sourceOffset] | (source[sourceOffset + 1] << 8) | (source[sourceOffset + 2] << 16)),
                _ => BitConverter.ToUInt32(source.Slice(sourceOffset, 4))
            };

            destination[destinationOffset + 0] = ExtractChannel(pixel, formatInfo.RMask);
            destination[destinationOffset + 1] = ExtractChannel(pixel, formatInfo.GMask);
            destination[destinationOffset + 2] = ExtractChannel(pixel, formatInfo.BMask);
            destination[destinationOffset + 3] = formatInfo.AMask != 0 ? ExtractChannel(pixel, formatInfo.AMask) : (byte)255;
        }
    }

    private static DdsFormatInfo ResolveFormat(DdsPixelFormat pixelFormat, DdsHeader10? header10)
    {
        if (header10 is not null)
        {
            return header10.Value.DxgiFormat switch
            {
                DxgiFormatR32G32B32A32Float => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 16, 0, 0, 0, 0, false, DdsPixelEncoding.Float32),
                DxgiFormatR16G16B16A16Float => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 8, 0, 0, 0, 0, false, DdsPixelEncoding.Float16),
                DxgiFormatR16G16B16A16Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 8, 0, 0, 0, 0, false, DdsPixelEncoding.Unorm16),
                DxgiFormatR10G10B10A2Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 4, 0x000003ff, 0x000ffc00, 0x3ff00000, 0xc0000000, false),
                DxgiFormatR9G9B9E5SharedExp => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 3, 4, 0, 0, 0, 0, false, DdsPixelEncoding.SharedExponent),
                DxgiFormatR8G8B8A8Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 4, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, false),
                DxgiFormatR8G8B8A8UnormSrgb => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 4, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, true),
                DxgiFormatBc1Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatBc1Unorm, 3, 0, 0, 0, 0, 0, false),
                DxgiFormatBc1UnormSrgb => new DdsFormatInfo(true, TextureFormat.TexFormatBc1Unorm, 3, 0, 0, 0, 0, 0, true),
                DxgiFormatBc2Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatBc2Unorm, 4, 0, 0, 0, 0, 0, false),
                DxgiFormatBc2UnormSrgb => new DdsFormatInfo(true, TextureFormat.TexFormatBc2Unorm, 4, 0, 0, 0, 0, 0, true),
                DxgiFormatBc3Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatBc3Unorm, 4, 0, 0, 0, 0, 0, false),
                DxgiFormatBc3UnormSrgb => new DdsFormatInfo(true, TextureFormat.TexFormatBc3Unorm, 4, 0, 0, 0, 0, 0, true),
                DxgiFormatB5G6R5Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 3, 2, 0x0000f800, 0x000007e0, 0x0000001f, 0x00000000, false),
                DxgiFormatB5G5R5A1Unorm => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 2, 0x00007c00, 0x000003e0, 0x0000001f, 0x00008000, false),
                _ => default
            };
        }

        return pixelFormat.FourCc switch
        {
            FourCcDxt1 => new DdsFormatInfo(true, TextureFormat.TexFormatBc1Unorm, 3, 0, 0, 0, 0, 0, false),
            FourCcDxt3 => new DdsFormatInfo(true, TextureFormat.TexFormatBc2Unorm, 4, 0, 0, 0, 0, 0, false),
            FourCcDxt5 => new DdsFormatInfo(true, TextureFormat.TexFormatBc3Unorm, 4, 0, 0, 0, 0, 0, false),
            FourCcEtc1 or FourCcEtc2 => new DdsFormatInfo(true, TextureFormat.TexFormatEtc2Rgb8Unorm, 3, 0, 0, 0, 0, 0, false),
            FourCcEtc2A => new DdsFormatInfo(true, TextureFormat.TexFormatEtc2Rgba8Unorm, 4, 0, 0, 0, 0, 0, false),
            FourCcPtc2 => new DdsFormatInfo(true, EmulatedTextureFormat.PvrtcRgba2Bpp, 4, 0, 0, 0, 0, 0, false),
            FourCcPtc4 => new DdsFormatInfo(true, EmulatedTextureFormat.PvrtcRgba4Bpp, 4, 0, 0, 0, 0, 0, false),
            D3DFormatA16B16G16R16 => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 8, 0, 0, 0, 0, false, DdsPixelEncoding.Unorm16),
            D3DFormatA16B16G16R16F => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 8, 0, 0, 0, 0, false, DdsPixelEncoding.Float16),
            D3DFormatA32B32G32R32F => new DdsFormatInfo(true, TextureFormat.TexFormatRgba8Unorm, 4, 16, 0, 0, 0, 0, false, DdsPixelEncoding.Float32),
            _ when pixelFormat.RgbBitCount is 16u or 24u or 32u
                => new DdsFormatInfo(
                    true,
                    TextureFormat.TexFormatRgba8Unorm,
                    pixelFormat.AMask != 0 ? 4u : 3u,
                    Math.Max(pixelFormat.RgbBitCount / 8u, 1u),
                    pixelFormat.RMask,
                    pixelFormat.GMask,
                    pixelFormat.BMask,
                    pixelFormat.AMask,
                    false),
            _ => default
        };
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

    private static byte ToByte(float value)
        => (byte)Math.Clamp((int)(value * 255.0f + 0.5f), 0, 255);

    private static byte[] ReadRemaining(Stream source)
    {
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        return memory.ToArray();
    }

    private static uint ReadUInt32(byte[] fileData, ref int offset)
    {
        if (offset + 4 > fileData.Length)
            return 0;

        var value = BinaryPrimitives.ReadUInt32LittleEndian(fileData.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    private static int ReadInt32(byte[] fileData, ref int offset)
    {
        if (offset + 4 > fileData.Length)
            return 0;

        var value = BinaryPrimitives.ReadInt32LittleEndian(fileData.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    private static bool Skip(byte[] fileData, ref int offset, int count)
    {
        if (offset + count > fileData.Length)
            return false;
        offset += count;
        return true;
    }

    private static byte[] ReadBytes(byte[] fileData, ref int offset, int count)
    {
        if (offset + count > fileData.Length)
            return [];

        var bytes = new byte[count];
        Buffer.BlockCopy(fileData, offset, bytes, 0, count);
        offset += count;
        return bytes;
    }

    private static int GetLevelDimension(int size, uint level)
    {
        var value = size;
        for (var i = 0u; i < level && value > 1; ++i)
            value = Math.Max(value / 2, 1);
        return value;
    }

    private readonly record struct DdsPixelFormat(
        uint Flags,
        uint FourCc,
        uint RgbBitCount,
        uint RMask,
        uint GMask,
        uint BMask,
        uint AMask);

    private readonly record struct DdsHeader10(
        uint DxgiFormat,
        uint ResourceDimension,
        uint MiscFlag,
        uint ArraySize,
        uint Reserved);

    private readonly record struct DdsFormatInfo(
        bool IsSupported,
        TextureFormat Format,
        uint Components,
        uint SourceBytesPerPixel,
        uint RMask,
        uint GMask,
        uint BMask,
        uint AMask,
        bool SRgb,
        DdsPixelEncoding Encoding = DdsPixelEncoding.Packed);

    private enum DdsPixelEncoding
    {
        Packed,
        Unorm16,
        Float16,
        Float32,
        SharedExponent
    }

}
