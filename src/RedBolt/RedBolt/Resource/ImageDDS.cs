using System.Buffers.Binary;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.IO;
using RedBolt.RenderAPI;

namespace RedBolt.Resource;

/// <summary>DirectDraw color key definition.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct DDColorKey
{
    public uint ColorSpaceLowValue;
    public uint ColorSpaceHighValue;
}

/// <summary>DirectDraw pixel format definition.</summary>
[StructLayout(LayoutKind.Explicit, Size = 32)]
public struct DDPixelFormat
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MultiSampleCapabilities
    {
        public ushort FlipMSTypes;
        public ushort BltMSTypes;
    }

    [FieldOffset(0)] public uint Size;
    [FieldOffset(4)] public uint Flags;
    [FieldOffset(8)] public uint FourCc;

    [FieldOffset(12)] public uint RgbBitCount;
    [FieldOffset(12)] public uint YuvBitCount;
    [FieldOffset(12)] public uint ZBufferBitDepth;
    [FieldOffset(12)] public uint AlphaBitDepth;
    [FieldOffset(12)] public uint LuminanceBitCount;
    [FieldOffset(12)] public uint BumpBitCount;
    [FieldOffset(12)] public uint PrivateFormatBitCount;

    [FieldOffset(16)] public uint RBitMask;
    [FieldOffset(16)] public uint YBitMask;
    [FieldOffset(16)] public uint StencilBitDepth;
    [FieldOffset(16)] public uint LuminanceBitMask;
    [FieldOffset(16)] public uint BumpDuBitMask;
    [FieldOffset(16)] public uint Operations;

    [FieldOffset(20)] public uint GBitMask;
    [FieldOffset(20)] public uint UBitMask;
    [FieldOffset(20)] public uint ZBitMask;
    [FieldOffset(20)] public uint BumpDvBitMask;
    [FieldOffset(20)] public MultiSampleCapabilities MultiSampleCaps;

    [FieldOffset(24)] public uint BBitMask;
    [FieldOffset(24)] public uint VBitMask;
    [FieldOffset(24)] public uint StencilBitMask;
    [FieldOffset(24)] public uint BumpLuminanceBitMask;

    [FieldOffset(28)] public uint RgbAlphaBitMask;
    [FieldOffset(28)] public uint YuvAlphaBitMask;
    [FieldOffset(28)] public uint LuminanceAlphaBitMask;
    [FieldOffset(28)] public uint RgbZBitMask;
    [FieldOffset(28)] public uint YuvZBitMask;
}

/// <summary>DirectDraw surface capabilities.</summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct DDSCaps2
{
    [FieldOffset(0)] public uint Caps;
    [FieldOffset(4)] public uint Caps2;
    [FieldOffset(8)] public uint Caps3;
    [FieldOffset(12)] public uint Caps4;
    [FieldOffset(12)] public uint VolumeDepth;
}

[StructLayout(LayoutKind.Sequential)]
public struct DDSHeader10
{
    public uint DxgiFormat;
    public uint ResourceDimension;
    public uint MiscFlag;
    public uint ArraySize;
    public uint Reserved;
}

/// <summary>DirectDraw surface description.</summary>
[StructLayout(LayoutKind.Explicit, Size = 124)]
public struct DDSurfaceDesc2
{
    [FieldOffset(0)] public uint Size;
    [FieldOffset(4)] public uint Flags;
    [FieldOffset(8)] public uint Height;
    [FieldOffset(12)] public uint Width;
    [FieldOffset(16)] public uint Pitch;
    [FieldOffset(16)] public uint LinearSize;
    [FieldOffset(20)] public uint BackBufferCount;
    [FieldOffset(20)] public uint Depth;
    [FieldOffset(24)] public uint MipMapCount;
    [FieldOffset(24)] public uint RefreshRate;
    [FieldOffset(24)] public uint SrcVBHandle;
    [FieldOffset(28)] public uint AlphaBitDepth;
    [FieldOffset(32)] public uint Reserved;
    [FieldOffset(36)] public uint Surface;
    [FieldOffset(40)] public DDColorKey DestOverlayColorKey;
    [FieldOffset(40)] public uint EmptyFaceColor;
    [FieldOffset(48)] public DDColorKey DestBltColorKey;
    [FieldOffset(56)] public DDColorKey SrcOverlayColorKey;
    [FieldOffset(64)] public DDColorKey SrcBltColorKey;
    [FieldOffset(72)] public DDPixelFormat PixelFormat;
    [FieldOffset(72)] public uint Fvf;
    [FieldOffset(104)] public DDSCaps2 Caps;
    [FieldOffset(120)] public uint TextureStage;
}

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
                RedBolt.Resource.Decompress.DecompressImageDXT(destination, Data.Span, Width, Height, Depth, Format);
                return true;
            case TextureFormat.TexFormatEtc2Rgb8Unorm:
                RedBolt.Resource.Decompress.DecompressImageETC(destination, Data.Span, Width, Height, false);
                return true;
            case TextureFormat.TexFormatEtc2Rgba8Unorm:
                RedBolt.Resource.Decompress.DecompressImageETC(destination, Data.Span, Width, Height, true);
                return true;
        }

        if (Format == EmulatedTextureFormat.PvrtcRgb2Bpp
            || Format == EmulatedTextureFormat.PvrtcRgba2Bpp
            || Format == EmulatedTextureFormat.PvrtcRgb4Bpp
            || Format == EmulatedTextureFormat.PvrtcRgba4Bpp)
        {
            RedBolt.Resource.Decompress.DecompressImagePVRTC(destination, Data.Span, Width, Height, Format);
            return true;
        }

        return false;
    }
}

public static class ImageDds
{
    private const uint FileId = 0x20534444u;
    private const uint HeaderSize = 124u;
    private const uint PixelFormatSize = 32u;

    private const uint FourCcDx10 = 0x30315844u;
    private const uint FourCcDxt1 = 0x31545844u;
    private const uint FourCcDxt2 = 0x32545844u;
    private const uint FourCcDxt3 = 0x33545844u;
    private const uint FourCcDxt4 = 0x34545844u;
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

    private const uint DdsCapsComplex = 0x00000008u;
    private const uint DdsCapsTexture = 0x00001000u;
    private const uint DdsCapsMipmap = 0x00400000u;
    private const uint DdsCaps2Volume = 0x00200000u;
    private const uint DdsCaps2Cubemap = 0x00000200u;
    private const uint DdsCaps2CubemapPositiveX = 0x00000400u;
    private const uint DdsCaps2CubemapNegativeX = 0x00000800u;
    private const uint DdsCaps2CubemapPositiveY = 0x00001000u;
    private const uint DdsCaps2CubemapNegativeY = 0x00002000u;
    private const uint DdsCaps2CubemapPositiveZ = 0x00004000u;
    private const uint DdsCaps2CubemapNegativeZ = 0x00008000u;
    private const uint DdsCaps2CubemapAllFaces = 0x0000FC00u;

    private const uint DdsDimensionTexture1D = 2u;
    private const uint DdsDimensionTexture2D = 3u;
    private const uint DdsDimensionTexture3D = 4u;
    private const uint DdsResourceMiscTextureCube = 0x4u;

    /// <summary>Make four-CC constant from string.</summary>
    public static uint MakeFourCC(char ch0, char ch1, char ch2, char ch3)
    {
        return (uint)ch0 | ((uint)ch1 << 8) | ((uint)ch2 << 16) | ((uint)ch3 << 24);
    }

    private static DDPixelFormat MakePixelFormat(uint numBits, uint maskR, uint maskG, uint maskB, uint maskA)
    {
        return new DDPixelFormat
        {
            RgbBitCount = numBits,
            RBitMask = maskR,
            GBitMask = maskG,
            BBitMask = maskB,
            RgbAlphaBitMask = maskA
        };
    }

    private static bool IsSamePixelFormat(DDPixelFormat lhs, DDPixelFormat rhs)
    {
        return lhs.RgbBitCount == rhs.RgbBitCount
            && lhs.RBitMask == rhs.RBitMask
            && lhs.GBitMask == rhs.GBitMask
            && lhs.BBitMask == rhs.BBitMask
            && lhs.RgbAlphaBitMask == rhs.RgbAlphaBitMask;
    }

    /// <summary>Deduce texture format from DDS file format.</summary>
    public static TextureFormat PickTextureFormat(DDPixelFormat pixelFormat, uint dxgiFormat)
    {
        return dxgiFormat switch
        {
            DxgiFormatR32G32B32A32Float => TextureFormat.TexFormatRgba32Float,
            DxgiFormatR16G16B16A16Float => TextureFormat.TexFormatRgba16Float,
            DxgiFormatR16G16B16A16Unorm => TextureFormat.TexFormatRgba16Unorm,
            DxgiFormatR10G10B10A2Unorm => TextureFormat.TexFormatRgb10a2Unorm,
            DxgiFormatR8G8B8A8Unorm => TextureFormat.TexFormatRgba8Unorm,
            DxgiFormatR8G8B8A8UnormSrgb => TextureFormat.TexFormatRgba8UnormSrgb,
            DxgiFormatR9G9B9E5SharedExp => TextureFormat.TexFormatRgb9e5Sharedexp,
            DxgiFormatBc1Unorm => TextureFormat.TexFormatBc1Unorm,
            DxgiFormatBc1UnormSrgb => TextureFormat.TexFormatBc1UnormSrgb,
            DxgiFormatBc2Unorm => TextureFormat.TexFormatBc2Unorm,
            DxgiFormatBc2UnormSrgb => TextureFormat.TexFormatBc2UnormSrgb,
            DxgiFormatBc3Unorm => TextureFormat.TexFormatBc3Unorm,
            DxgiFormatBc3UnormSrgb => TextureFormat.TexFormatBc3UnormSrgb,
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

    /// <summary>Return whether the RGBA components in pixel format match texture format.</summary>
    public static bool AreTextureComponentsMatching(DDPixelFormat pixelFormat, TextureFormat textureFormat)
    {
        var expectedFormat = textureFormat switch
        {
            TextureFormat.TexFormatRgba8Unorm => MakePixelFormat(32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000),
            TextureFormat.TexFormatB5g6r5Unorm => MakePixelFormat(16, 0x0000f800, 0x000007e0, 0x0000001f, 0x00000000),
            TextureFormat.TexFormatB5g5r5a1Unorm => MakePixelFormat(16, 0x00007c00, 0x000003e0, 0x0000001f, 0x00008000),
            _ => default
        };
        return textureFormat is TextureFormat.TexFormatRgba8Unorm
                or TextureFormat.TexFormatB5g6r5Unorm
                or TextureFormat.TexFormatB5g5r5a1Unorm
            && IsSamePixelFormat(expectedFormat, pixelFormat);
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
        var depth = SystemMath.Max(ReadInt32(fileData, ref offset), 1);
        var mipMapCount = SystemMath.Max(ReadUInt32(fileData, ref offset), 1u);
        if (!Skip(fileData, ref offset, 44))
            return false;

        if (ReadUInt32(fileData, ref offset) != PixelFormatSize)
            return false;

        var pixelFormat = new DDPixelFormat
        {
            Flags = ReadUInt32(fileData, ref offset),
            FourCc = ReadUInt32(fileData, ref offset),
            RgbBitCount = ReadUInt32(fileData, ref offset),
            RBitMask = ReadUInt32(fileData, ref offset),
            GBitMask = ReadUInt32(fileData, ref offset),
            BBitMask = ReadUInt32(fileData, ref offset),
            RgbAlphaBitMask = ReadUInt32(fileData, ref offset)
        };

        _ = ReadUInt32(fileData, ref offset);
        var caps2 = ReadUInt32(fileData, ref offset);
        _ = ReadUInt32(fileData, ref offset);
        _ = ReadUInt32(fileData, ref offset);
        _ = ReadUInt32(fileData, ref offset);

        if (width <= 0 || height <= 0)
            return false;

        DDSHeader10? header10 = null;
        if (pixelFormat.FourCc == FourCcDx10)
        {
            header10 = new DDSHeader10
            {
                DxgiFormat = ReadUInt32(fileData, ref offset),
                ResourceDimension = ReadUInt32(fileData, ref offset),
                MiscFlag = ReadUInt32(fileData, ref offset),
                ArraySize = ReadUInt32(fileData, ref offset),
                Reserved = ReadUInt32(fileData, ref offset)
            };
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
            var target = imageIndex == 0 ? image : new Image();
            var storedData = formatInfo.Format == TextureFormat.TexFormatRgba8Unorm
                    && !AreTextureComponentsMatching(pixelFormat, formatInfo.Format)
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
        if (format == EmulatedTextureFormat.PvrtcRgb2Bpp
            || format == EmulatedTextureFormat.PvrtcRgba2Bpp
            || format == EmulatedTextureFormat.PvrtcRgb4Bpp
            || format == EmulatedTextureFormat.PvrtcRgba4Bpp)
        {
            widthInBlocks = SystemMath.Max(2, widthInBlocks);
            heightInBlocks = SystemMath.Max(2, heightInBlocks);
        }
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
        GetChannelShift(formatInfo.RMask, out var rShiftL, out var rShiftR);
        GetChannelShift(formatInfo.GMask, out var gShiftL, out var gShiftR);
        GetChannelShift(formatInfo.BMask, out var bShiftL, out var bShiftR);
        GetChannelShift(formatInfo.AMask, out var aShiftL, out var aShiftR);

        for (var sourceOffset = 0; sourceOffset < source.Length; sourceOffset += sourcePixelSize)
        {
            var destinationOffset = (sourceOffset / sourcePixelSize) * 4;
            uint pixel = sourcePixelSize switch
            {
                4 => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(sourceOffset, 4)),
                3 => (uint)(source[sourceOffset] | (source[sourceOffset + 1] << 8) | (source[sourceOffset + 2] << 16)),
                _ => BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(sourceOffset, 2))
            };

            destination[destinationOffset + 0] = (byte)(((pixel & formatInfo.RMask) << rShiftL) >> rShiftR);
            destination[destinationOffset + 1] = (byte)(((pixel & formatInfo.GMask) << gShiftL) >> gShiftR);
            destination[destinationOffset + 2] = (byte)(((pixel & formatInfo.BMask) << bShiftL) >> bShiftR);
            destination[destinationOffset + 3] = (byte)(((pixel & formatInfo.AMask) << aShiftL) >> aShiftR);
        }
    }

    private static DdsFormatInfo ResolveFormat(DDPixelFormat pixelFormat, DDSHeader10? header10)
    {
        var originalTextureFormat = PickTextureFormat(pixelFormat, header10?.DxgiFormat ?? 0);
        if (originalTextureFormat == TextureFormat.TexFormatUnknown)
            return default;

        var format = RenderAPIUtils.SetTextureFormatSRGB(originalTextureFormat, false);
        var formatAttribs = RenderAPIUtils.GetTextureFormatInfo(format);
        return new DdsFormatInfo(
            true,
            format,
            checked((uint)formatAttribs.NumComponents),
            pixelFormat.RgbBitCount >> 3,
            pixelFormat.RBitMask,
            pixelFormat.GBitMask,
            pixelFormat.BBitMask,
            pixelFormat.RgbAlphaBitMask,
            format != originalTextureFormat);
    }

    private static void GetChannelShift(uint mask, out int shiftLeft, out int shiftRight)
    {
        shiftLeft = 0;
        shiftRight = 0;
        if (mask >= 0x100)
        {
            while ((mask >> shiftRight) >= 0x100)
                ++shiftRight;
        }
        else if (mask != 0 && mask < 0x80)
        {
            while ((mask << shiftLeft) < 0x80)
                ++shiftLeft;
        }
    }

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
            value = SystemMath.Max(value / 2, 1);
        return value;
    }

    private readonly record struct DdsFormatInfo(
        bool IsSupported,
        TextureFormat Format,
        uint Components,
        uint SourceBytesPerPixel,
        uint RMask,
        uint GMask,
        uint BMask,
        uint AMask,
        bool SRgb);

}
