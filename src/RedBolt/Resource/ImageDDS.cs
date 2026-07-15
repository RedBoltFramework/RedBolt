// C++: Defined in ImageDDS.h/.cpp.
// TODO: port - C++ structs DDColorKey, DDSCaps2, and DDSurfaceDesc2 retain fields not needed by the managed loader.
// TODO: port - C++ DDPixelFormat union members are represented by the fields used by the managed loader.
using System.Numerics;
using System.Buffers.Binary;

namespace RedBolt;

public readonly record struct CompressedLevel(
    ReadOnlyMemory<byte> Data,
    CompressedFormat Format,
    int Width,
    int Height,
    int Depth)
{
    public int DataSize => Data.Length;

    public bool Decompress(Span<byte> destination)
    {
        var expectedSize = checked(Width * Height * Depth * 4);
        if (destination.Length < expectedSize)
            return false;

        return Format switch
        {
            CompressedFormat.Rgba => DecompressRgba(destination[..expectedSize]),
            CompressedFormat.Dxt1 => DecompressBlocks(destination[..expectedSize], bytesPerBlock: 8),
            CompressedFormat.Dxt3 => DecompressBlocks(destination[..expectedSize], bytesPerBlock: 16),
            CompressedFormat.Dxt5 => DecompressBlocks(destination[..expectedSize], bytesPerBlock: 16),
            _ => false
        };
    }

    private bool DecompressRgba(Span<byte> destination)
    {
        if (Data.Length != destination.Length)
            return false;

        Data.Span.CopyTo(destination);
        return true;
    }

    private bool DecompressBlocks(Span<byte> destination, int bytesPerBlock)
    {
        var expectedDataSize = ImageDds.GetCompressedLevelSize(Format, Width, Height, Depth);
        if (Data.Length != expectedDataSize)
            return false;

        var source = Data.Span;
        Span<byte> blockPixels = stackalloc byte[4 * 4 * 4];
        var offset = 0;
        var blocksX = Math.Max((Width + 3) / 4, 1);
        var blocksY = Math.Max((Height + 3) / 4, 1);
        var sliceStride = checked(Width * Height * 4);

        for (var z = 0; z < Depth; ++z)
        {
            var sliceOffset = z * sliceStride;
            for (var blockY = 0; blockY < blocksY; ++blockY)
            {
                for (var blockX = 0; blockX < blocksX; ++blockX)
                {
                    var block = source.Slice(offset, bytesPerBlock);
                    switch (Format)
                    {
                        case CompressedFormat.Dxt1:
                            DecodeBc1Block(block, blockPixels);
                            break;
                        case CompressedFormat.Dxt3:
                            DecodeBc2Block(block, blockPixels);
                            break;
                        case CompressedFormat.Dxt5:
                            DecodeBc3Block(block, blockPixels);
                            break;
                        default:
                            return false;
                    }
                    offset += bytesPerBlock;

                    for (var localY = 0; localY < 4; ++localY)
                    {
                        var y = blockY * 4 + localY;
                        if (y >= Height)
                            break;

                        for (var localX = 0; localX < 4; ++localX)
                        {
                            var x = blockX * 4 + localX;
                            if (x >= Width)
                                break;

                            var sourceOffset = (localY * 4 + localX) * 4;
                            var destinationOffset = sliceOffset + (y * Width + x) * 4;
                            blockPixels.Slice(sourceOffset, 4).CopyTo(destination.Slice(destinationOffset, 4));
                        }
                    }
                }
            }
        }

        return true;
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

internal static class ImageDds
{
    private const uint FileId = 0x20534444u;
    private const uint HeaderSize = 124u;
    private const uint PixelFormatSize = 32u;

    // TODO: port - C++ code:
    // constexpr unsigned MakeFourCC(char ch0, char ch1, char ch2, char ch3)
    // {
    //     return static_cast<unsigned>(ch0) | (static_cast<unsigned>(ch1) << 8)
    //         | (static_cast<unsigned>(ch2) << 16) | (static_cast<unsigned>(ch3) << 24);
    // }

    // TODO: port - C++ code:
    // /// Deduce texture format from DDS file format.
    // URHO3D_API TextureFormat PickTextureFormat(const DDPixelFormat& pixelFormat, unsigned dxgiFormat);
    //
    // /// Return whether the RGBA components in pixel format match texture format.
    // URHO3D_API bool AreTextureComponentsMatching(const DDPixelFormat& pixelFormat, TextureFormat textureFormat);

    // C++ struct DDColorKey { unsigned dwColorSpaceLowValue_; unsigned dwColorSpaceHighValue_; };
    // C++ struct DDSCaps2 { unsigned dwCaps_; unsigned dwCaps2_; unsigned dwCaps3_; /* union with dwCaps4_/dwVolumeDepth_ */ };
    // C++ struct DDSurfaceDesc2 { /* full DirectDraw surface description with 18 fields */ };

    private const uint FourCcDx10 = 0x30315844u;
    private const uint FourCcDxt1 = 0x31545844u;
    private const uint FourCcDxt3 = 0x33545844u;
    private const uint FourCcDxt5 = 0x35545844u;

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

    private static CompressedFormat PickTextureFormat(DdsPixelFormat pixelFormat, uint dxgiFormat)
    {
        return dxgiFormat switch
        {
            DxgiFormatR8G8B8A8Unorm or DxgiFormatR8G8B8A8UnormSrgb => CompressedFormat.Rgba,
            DxgiFormatBc1Unorm or DxgiFormatBc1UnormSrgb => CompressedFormat.Dxt1,
            DxgiFormatBc2Unorm or DxgiFormatBc2UnormSrgb => CompressedFormat.Dxt3,
            DxgiFormatBc3Unorm or DxgiFormatBc3UnormSrgb => CompressedFormat.Dxt5,
            DxgiFormatB5G6R5Unorm or DxgiFormatB5G5R5A1Unorm when AreTextureComponentsMatching(pixelFormat, CompressedFormat.Rgba)
                => CompressedFormat.Rgba,
            _ => pixelFormat.FourCc switch
            {
                FourCcDxt1 => CompressedFormat.Dxt1,
                FourCcDxt3 => CompressedFormat.Dxt3,
                FourCcDxt5 => CompressedFormat.Dxt5,
                FourCcEtc1 => CompressedFormat.Etc1,
                FourCcEtc2 => CompressedFormat.Etc2Rgb,
                FourCcEtc2A => CompressedFormat.Etc2Rgba,
                FourCcPtc2 => CompressedFormat.PvrtcRgba2Bpp,
                FourCcPtc4 => CompressedFormat.PvrtcRgba4Bpp,
                D3DFormatA16B16G16R16 or D3DFormatA16B16G16R16F or D3DFormatA32B32G32R32F => CompressedFormat.Rgba,
                _ when AreTextureComponentsMatching(pixelFormat, CompressedFormat.Rgba) => CompressedFormat.Rgba,
                _ when pixelFormat.RgbBitCount == 32 => CompressedFormat.Rgba,
                _ => CompressedFormat.None
            }
        };
    }

    private static bool AreTextureComponentsMatching(DdsPixelFormat pixelFormat, CompressedFormat textureFormat)
    {
        return textureFormat switch
        {
            CompressedFormat.Rgba => pixelFormat.RgbBitCount == 32
                && pixelFormat.RMask == 0x000000ff
                && pixelFormat.GMask == 0x0000ff00
                && pixelFormat.BMask == 0x00ff0000
                && pixelFormat.AMask == 0xff000000,
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
            var storedData = formatInfo.Format == CompressedFormat.Rgba
                ? ReadRgbaLevels(fileData, ref offset, width, height, depth, mipMapCount, formatInfo)
                : ReadCompressedLevels(fileData, ref offset, width, height, depth, mipMapCount, formatInfo.Format);

            if (storedData is null)
                return false;

            target.SetCompressedData(
                width,
                height,
                depth,
                formatInfo.Components,
                formatInfo.Format,
                mipMapCount,
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

    internal static int GetCompressedLevelSize(CompressedFormat format, int width, int height, int depth)
    {
        return format switch
        {
            CompressedFormat.Rgba => checked(width * height * depth * 4),
            CompressedFormat.Dxt1 => GetBlockCompressedSize(width, height, depth, 8),
            CompressedFormat.Dxt3 or CompressedFormat.Dxt5 => GetBlockCompressedSize(width, height, depth, 16),
            _ => 0
        };
    }

    private static byte[]? ReadCompressedLevels(byte[] fileData, ref int offset, int width, int height, int depth, uint mipMapCount, CompressedFormat format)
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
                CompressedFormat.Rgba,
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

            var convertedLevelSize = GetCompressedLevelSize(CompressedFormat.Rgba, levelWidth, levelHeight, levelDepth);
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
            uint pixel = sourcePixelSize switch
            {
                2 => BitConverter.ToUInt16(source.Slice(sourceOffset, 2)),
                3 => (uint)(source[sourceOffset] | (source[sourceOffset + 1] << 8) | (source[sourceOffset + 2] << 16)),
                _ => BitConverter.ToUInt32(source.Slice(sourceOffset, 4))
            };

            var destinationOffset = (sourceOffset / sourcePixelSize) * 4;
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
                DxgiFormatR8G8B8A8Unorm => new DdsFormatInfo(true, CompressedFormat.Rgba, 4, 4, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, false),
                DxgiFormatR8G8B8A8UnormSrgb => new DdsFormatInfo(true, CompressedFormat.Rgba, 4, 4, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000, true),
                DxgiFormatBc1Unorm => new DdsFormatInfo(true, CompressedFormat.Dxt1, 3, 0, 0, 0, 0, 0, false),
                DxgiFormatBc1UnormSrgb => new DdsFormatInfo(true, CompressedFormat.Dxt1, 3, 0, 0, 0, 0, 0, true),
                DxgiFormatBc2Unorm => new DdsFormatInfo(true, CompressedFormat.Dxt3, 4, 0, 0, 0, 0, 0, false),
                DxgiFormatBc2UnormSrgb => new DdsFormatInfo(true, CompressedFormat.Dxt3, 4, 0, 0, 0, 0, 0, true),
                DxgiFormatBc3Unorm => new DdsFormatInfo(true, CompressedFormat.Dxt5, 4, 0, 0, 0, 0, 0, false),
                DxgiFormatBc3UnormSrgb => new DdsFormatInfo(true, CompressedFormat.Dxt5, 4, 0, 0, 0, 0, 0, true),
                DxgiFormatB5G6R5Unorm => new DdsFormatInfo(true, CompressedFormat.Rgba, 3, 2, 0x0000f800, 0x000007e0, 0x0000001f, 0x00000000, false),
                DxgiFormatB5G5R5A1Unorm => new DdsFormatInfo(true, CompressedFormat.Rgba, 4, 2, 0x00007c00, 0x000003e0, 0x0000001f, 0x00008000, false),
                _ => default
            };
        }

        return pixelFormat.FourCc switch
        {
            FourCcDxt1 => new DdsFormatInfo(true, CompressedFormat.Dxt1, 3, 0, 0, 0, 0, 0, false),
            FourCcDxt3 => new DdsFormatInfo(true, CompressedFormat.Dxt3, 4, 0, 0, 0, 0, 0, false),
            FourCcDxt5 => new DdsFormatInfo(true, CompressedFormat.Dxt5, 4, 0, 0, 0, 0, 0, false),
            _ when pixelFormat.RgbBitCount is 16u or 24u or 32u
                => new DdsFormatInfo(
                    true,
                    CompressedFormat.Rgba,
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

    private static int GetBlockCompressedSize(int width, int height, int depth, int bytesPerBlock)
    {
        var blockWidth = Math.Max((width + 3) / 4, 1);
        var blockHeight = Math.Max((height + 3) / 4, 1);
        return checked(blockWidth * blockHeight * Math.Max(depth, 1) * bytesPerBlock);
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
        CompressedFormat Format,
        uint Components,
        uint SourceBytesPerPixel,
        uint RMask,
        uint GMask,
        uint BMask,
        uint AMask,
        bool SRgb);
}
