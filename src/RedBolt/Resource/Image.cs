using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace RedBolt;

public enum CompressedFormat
{
    None = 0,
    Rgba,
    Dxt1,
    Dxt3,
    Dxt5,
    Etc1,
    Etc2Rgb,
    Etc2Rgba,
    PvrtcRgb2Bpp,
    PvrtcRgba2Bpp,
    PvrtcRgb4Bpp,
    PvrtcRgba4Bpp
}

public class Image : Resource
{
    public Image(string name)
        : base(name)
    {
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int Depth { get; private set; } = 1;

    public int Components { get; private set; }

    public int NumCompressedLevels { get; private set; }

    public bool Cubemap { get; internal set; }

    public bool Array { get; internal set; }

    public bool SRGB { get; internal set; }

    public CompressedFormat CompressedFormat { get; private set; }

    private byte[] _data = System.Array.Empty<byte>();

    private Image? _nextLevel;

    private Image? _nextSibling;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Stream seekableSource = source;
        MemoryStream? bufferedSource = null;
        if (!source.CanSeek)
        {
            bufferedSource = new MemoryStream();
            source.CopyTo(bufferedSource);
            bufferedSource.Position = 0;
            seekableSource = bufferedSource;
        }

        try
        {
            seekableSource.Seek(0, SeekOrigin.Begin);
            if (ImageDds.TryLoad(this, seekableSource))
                return true;

            seekableSource.Seek(0, SeekOrigin.Begin);
            return ImageSdl.TryLoad(this, seekableSource);
        }
        finally
        {
            bufferedSource?.Dispose();
        }
    }

    public override unsafe bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (IsCompressed())
        {
            Log.GetLogger().LogError("Can not save compressed image {Name}", Name);
            return false;
        }

        if (_data.Length == 0)
        {
            Log.GetLogger().LogError("Can not save zero-sized image {Name}", Name);
            return false;
        }

        fixed (byte* data = _data)
        {
            var png = RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiWritePngToMem(
                (nint)data, 0, Width, Height, (int)Components, out var length);
            try
            {
                destination.Write(new ReadOnlySpan<byte>((void*)png, length));
                return true;
            }
            finally
            {
                NativeMemory.Free((void*)png);
            }
        }
    }

    public bool SavePNG(string fileName) => SaveEncodedFile(fileName);

    public bool SaveJPG(string fileName, int quality) => ImageSdl.SaveJpg(this, fileName, quality);

    public bool SaveWEBP(string fileName, float compression = 0.0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (IsCompressed())
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "Can not save compressed image to WebP", null, static (state, _) => state);
            return false;
        }

        const int maxDimension = 16383;
        if (Height > maxDimension || Width > maxDimension)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, $"Maximum dimension supported by WebP is {maxDimension}", null, static (state, _) => state);
            return false;
        }

        if (Components is not (3 or 4))
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, $"Can not save image with {Components} components to WebP, which requires 3 or 4", null, static (state, _) => state);
            return false;
        }

        if (_data.Length == 0)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "No image data to save", null, static (state, _) => state);
            return false;
        }

        _ = compression;
        var pinnedData = GCHandle.Alloc(_data, GCHandleType.Pinned);
        try
        {
            var input = pinnedData.AddrOfPinnedObject();
            var stride = checked(Width * (int)Components);
            var output = IntPtr.Zero;
            var outputSize = Components == 4
                ? RedBolt.Bindings.WebP.WebPNativeBindings.WebPEncodeLosslessRGBA(input, Width, Height, stride, out output)
                : RedBolt.Bindings.WebP.WebPNativeBindings.WebPEncodeLosslessRGB(input, Width, Height, stride, out output);
            if (outputSize == 0 || output == IntPtr.Zero)
                return false;

            try
            {
                var encoded = new byte[checked((int)outputSize)];
                Marshal.Copy(output, encoded, 0, encoded.Length);
                return WriteFile(fileName, encoded);
            }
            finally
            {
                RedBolt.Bindings.WebP.WebPNativeBindings.WebPFree(output);
            }
        }
        finally
        {
            pinnedData.Free();
        }
    }

    /// <summary>Return an SDL surface from the image, or null if failed. Only RGB images are supported. Specify rect to only return partial image. You must free the surface yourself.</summary>
    public RedBolt.Bindings.Sdl.SdlSurface? GetSDLSurface(IntRect rect = default) => ImageSdl.GetSdlSurface(this, rect);

    public bool SaveDDS(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (IsCompressed())
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "Can not save compressed image to DDS", null, static (state, _) => state);
            return false;
        }

        if (Components != 4)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, $"Can not save image with {Components} components to DDS", null, static (state, _) => state);
            return false;
        }

        var levels = new List<Image>();
        GetLevels(levels);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("DDS "));
            writer.Write(124u);
            writer.Write(0x00000001u | 0x00000002u | 0x00000004u | 0x00020000u | 0x00001000u);
            writer.Write((uint)Height);
            writer.Write((uint)Width);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write((uint)levels.Count);
            writer.Write(new byte[44]);
            writer.Write(32u);
            writer.Write(0x00000040u | 0x00000001u);
            writer.Write(32u);
            writer.Write(0x000000ffu);
            writer.Write(0x0000ff00u);
            writer.Write(0x00ff0000u);
            writer.Write(0xff000000u);
            writer.Write(new byte[16]);
            writer.Flush();
        }

        foreach (var level in levels)
            stream.Write(level.GetData(), 0, checked(level.Width * level.Height * 4));

        return WriteFile(fileName, stream.ToArray());
    }

    public unsafe bool SaveBMP(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (IsCompressed())
        {
            Log.GetLogger().LogError("Can not save compressed image to BMP");
            return false;
        }

        if (_data.Length == 0)
            return false;

        var nativeFileName = System.Text.Encoding.UTF8.GetBytes(fileName + '\0');
        fixed (byte* data = _data)
        {
            return RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiWriteBmp(
                nativeFileName, Width, Height, (int)Components, (nint)data) != 0;
        }
    }

    public bool SaveTGA(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var rgba = Components == 4 ? this : ConvertToRGBA();
        if (rgba is null)
            return false;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)2);
        writer.Write(new byte[5]); writer.Write((short)0); writer.Write((short)0);
        writer.Write((ushort)rgba.Width); writer.Write((ushort)rgba.Height); writer.Write((byte)32); writer.Write((byte)0x28);
        var data = rgba.GetData();
        for (var i = 0; i < data.Length; i += 4)
        {
            writer.Write(data[i + 2]); writer.Write(data[i + 1]); writer.Write(data[i]); writer.Write(data[i + 3]);
        }
        writer.Flush();
        return WriteFile(fileName, stream.ToArray());
    }

    private bool SaveEncodedFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        using var stream = new MemoryStream();
        if (!Save(stream))
            return false;
        return WriteFile(fileName, stream.ToArray());
    }

    private bool WriteFile(string fileName, ReadOnlySpan<byte> data)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        using var temporaryFileSystem = virtualFileSystem is null ? new VirtualFileSystem() : null;
        if (temporaryFileSystem is not null)
        {
            temporaryFileSystem.MountRoot();
            virtualFileSystem = temporaryFileSystem;
        }

        var identifier = new FileIdentifier(fileName);
        using var rootMount = temporaryFileSystem is null && identifier.Scheme == "file"
            ? new MountPointGuard(virtualFileSystem.MountRoot())
            : null;
        var file = virtualFileSystem.OpenFile(identifier, FileMode.Write);
        if (file is null)
            return false;

        try
        {
            return file.Write(data) == data.Length;
        }
        finally
        {
            file.Close();
        }
    }

    public override bool SaveFile(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".bmp" => SaveBMP(fileName),
            ".tga" => SaveTGA(fileName),
            ".jpg" or ".jpeg" => SaveJPG(fileName, 90),
            ".png" => SavePNG(fileName),
            ".webp" => SaveWEBP(fileName),
            ".dds" => SaveDDS(fileName),
            _ => base.SaveFile(fileName)
        };
    }

    public bool SetSize(int width, int height, int components) => SetSize(width, height, 1, components);

    public bool SetSize(int width, int height, int depth, int components)
    {
        if (width == Width && height == Height && depth == Depth && components == Components)
            return true;

        if (width <= 0 || height <= 0 || depth <= 0)
            return false;

        if (components is < 0 or > 4)
            return false;

        Width = width;
        Height = height;
        Depth = depth;
        Components = components;
        _data = new byte[checked(width * height * depth * components)];
        MemoryUse = _data.Length;
        CompressedFormat = CompressedFormat.None;
        NumCompressedLevels = 0;
        _nextLevel = null;
        return true;
    }

    public void SetData(byte[]? pixelData)
    {
        if (_data.Length == 0)
            return;

        if (IsCompressed())
            return;

        if (pixelData is not null)
            pixelData.AsSpan(0, _data.Length).CopyTo(_data);
        else
            _data.AsSpan().Clear();
        _nextLevel = null;
    }

    public void SetPixel(int x, int y, Color color) => SetPixel(x, y, 0, color);

    public void SetPixel(int x, int y, int z, Color color)
    {
        SetPixelInt(x, y, z, color.ToUInt());
    }

    public void SetPixelInt(int x, int y, uint uintColor) => SetPixelInt(x, y, 0, uintColor);

    public void SetPixelInt(int x, int y, int z, uint uintColor)
    {
        if (_data.Length == 0 || x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth || IsCompressed())
            return;

        var offset = (z * Width * Height + y * Width + x) * Components;
        _data[offset] = (byte)uintColor;
        if (Components > 1)
            _data[offset + 1] = (byte)(uintColor >> 8);
        if (Components > 2)
            _data[offset + 2] = (byte)(uintColor >> 16);
        if (Components > 3)
            _data[offset + 3] = (byte)(uintColor >> 24);
    }

    public bool FlipHorizontal()
    {
        if (_data.Length == 0)
            return false;

        if (Depth > 1)
            return false;

        var formatInfo = RenderAPIUtils.GetTextureFormatInfo(GetCompressedFormat());
        var isWidthPowerOf2 = MathDefs.IsPowerOfTwo(checked((uint)Width));
        var isSimpleFlip = !IsCompressed() || (formatInfo.BlockWidth == 1 && formatInfo.BlockHeight == 1);
        var isBlockFlip = !isSimpleFlip && Decompress.IsFlipBlockImplemented(CompressedFormat) && isWidthPowerOf2;
        if (!isSimpleFlip && !isBlockFlip)
            return false;

        if (!IsCompressed())
        {
            for (var y = 0; y < Height; ++y)
            {
                for (var x = 0; x < Width / 2; ++x)
                {
                    var left = (y * Width + x) * Components;
                    var right = (y * Width + Width - x - 1) * Components;
                    SwapPixels(left, right);
                }
            }
        }
        else
        {
            var blockSize = formatInfo.GetElementSize();
            var temporaryBlock1 = new byte[blockSize];
            var temporaryBlock2 = new byte[blockSize];

            for (var i = 0; i < NumCompressedLevels; ++i)
            {
                var level = GetCompressedLevel(i);
                if (level.Data.IsEmpty)
                    return false;

                // Bug: Small blocks cannot be flipped in current implementation.
                if (level.Width < formatInfo.BlockWidth)
                    continue;

                var numRows = (level.Height + formatInfo.BlockHeight - 1) / formatInfo.BlockHeight;
                var numBlocksInRow = (level.Width + formatInfo.BlockWidth - 1) / formatInfo.BlockWidth;
                var rowSize = numBlocksInRow * blockSize;
                for (var y = 0; y < numRows; ++y)
                {
                    for (var x = 0; x < numBlocksInRow / 2; ++x)
                    {
                        var firstOffset = y * rowSize + (numBlocksInRow - x - 1) * blockSize;
                        var secondOffset = y * rowSize + x * blockSize;
                        var first = level.Data.Span.Slice(firstOffset, blockSize);
                        var second = level.Data.Span.Slice(secondOffset, blockSize);
                        if (isSimpleFlip)
                        {
                            first.CopyTo(temporaryBlock1);
                            second.CopyTo(first);
                            temporaryBlock1.CopyTo(second);
                        }
                        else
                        {
                            Decompress.FlipBlockHorizontal(temporaryBlock1, first, CompressedFormat);
                            Decompress.FlipBlockHorizontal(temporaryBlock2, second, CompressedFormat);
                            temporaryBlock1.CopyTo(second);
                            temporaryBlock2.CopyTo(first);
                        }
                    }
                }
            }
        }

        return true;
    }

    public bool FlipVertical()
    {
        if (_data.Length == 0)
            return false;

        if (Depth > 1)
            return false;

        var formatInfo = RenderAPIUtils.GetTextureFormatInfo(GetCompressedFormat());
        var isHeightPowerOf2 = MathDefs.IsPowerOfTwo(checked((uint)Height));
        var isSimpleFlip = !IsCompressed() || (formatInfo.BlockWidth == 1 && formatInfo.BlockHeight == 1);
        var isBlockFlip = !isSimpleFlip && Decompress.IsFlipBlockImplemented(CompressedFormat) && isHeightPowerOf2;
        if (!isSimpleFlip && !isBlockFlip)
            return false;

        if (!IsCompressed())
        {
            var rowSize = Width * Components;
            for (var y = 0; y < Height / 2; ++y)
            {
                var top = y * rowSize;
                var bottom = (Height - y - 1) * rowSize;
                SwapRows(top, bottom, rowSize);
            }
        }
        else
        {
            var blockSize = formatInfo.GetElementSize();
            var temporaryBlock = new byte[blockSize];

            for (var i = 0; i < NumCompressedLevels; ++i)
            {
                var level = GetCompressedLevel(i);
                if (level.Data.IsEmpty)
                    return false;

                // Bug: Small blocks cannot be flipped in current implementation.
                if (level.Height < formatInfo.BlockHeight)
                    continue;

                var numRows = (level.Height + formatInfo.BlockHeight - 1) / formatInfo.BlockHeight;
                var numBlocksInRow = (level.Width + formatInfo.BlockWidth - 1) / formatInfo.BlockWidth;
                var rowSize = numBlocksInRow * blockSize;
                var rowBuffer = new byte[rowSize];
                for (var y = 0; y < numRows / 2; ++y)
                {
                    var first = level.Data.Span.Slice(y * rowSize, rowSize);
                    var second = level.Data.Span.Slice((numRows - y - 1) * rowSize, rowSize);
                    first.CopyTo(rowBuffer);
                    second.CopyTo(first);
                    rowBuffer.CopyTo(second);

                    if (isSimpleFlip)
                        continue;

                    for (var x = 0; x < numBlocksInRow; ++x)
                    {
                        var firstBlock = first.Slice(x * blockSize, blockSize);
                        Decompress.FlipBlockVertical(temporaryBlock, firstBlock, CompressedFormat);
                        temporaryBlock.CopyTo(firstBlock);

                        var secondBlock = second.Slice(x * blockSize, blockSize);
                        Decompress.FlipBlockVertical(temporaryBlock, secondBlock, CompressedFormat);
                        temporaryBlock.CopyTo(secondBlock);
                    }
                }
            }
        }

        return true;
    }

    public bool Resize(int width, int height)
    {
        if (IsCompressed() || Depth > 1 || _data.Length == 0 || width <= 0 || height <= 0)
            return false;

        var newData = new byte[checked(width * height * Components)];
        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
            {
                var xF = Width > 1 ? (float)x / (width - 1) : 0.0f;
                var yF = Height > 1 ? (float)y / (height - 1) : 0.0f;
                var uintColor = GetPixelBilinear(xF, yF).ToUInt();
                var destination = (y * width + x) * Components;
                for (var component = 0; component < Components; ++component)
                    newData[destination + component] = (byte)(uintColor >> (component * 8));
            }
        }

        Width = width;
        Height = height;
        _data = newData;
        MemoryUse = checked(width * height * Depth * Components);
        return true;
    }

    public void Clear(Color color) => ClearInt(color.ToUInt());

    public void ClearInt(uint uintColor)
    {
        if (_data.Length == 0 || IsCompressed())
            return;

        if (Components == 4)
        {
            for (var i = 0; i < _data.Length; i += 4)
            {
                _data[i] = (byte)uintColor;
                _data[i + 1] = (byte)(uintColor >> 8);
                _data[i + 2] = (byte)(uintColor >> 16);
                _data[i + 3] = (byte)(uintColor >> 24);
            }
        }
        else
        {
            for (var i = 0; i < _data.Length; ++i)
                _data[i] = (byte)(uintColor >> ((i % Components) * 8));
        }
    }

    public Color GetPixel(int x, int y) => GetPixel(x, y, 0);

    public Color GetPixel(int x, int y, int z)
    {
        if (_data.Length == 0 || z < 0 || z >= Depth || IsCompressed())
            return Color.Black;
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);

        var offset = (z * Width * Height + y * Width + x) * (int)Components;
        var ret = new Color();
        switch (Components)
        {
            case 4:
                ret.A = _data[offset + 3] / 255.0f;
                ret.B = _data[offset + 2] / 255.0f;
                ret.G = _data[offset + 1] / 255.0f;
                ret.R = _data[offset] / 255.0f;
                break;
            case 3:
                ret.B = _data[offset + 2] / 255.0f;
                ret.G = _data[offset + 1] / 255.0f;
                ret.R = _data[offset] / 255.0f;
                break;
            case 2:
                ret.G = _data[offset + 1] / 255.0f;
                ret.R = _data[offset] / 255.0f;
                break;
            default:
                ret.R = ret.G = ret.B = _data[offset] / 255.0f;
                break;
        }

        return ret;
    }

    public uint GetPixelInt(int x, int y) => GetPixelInt(x, y, 0);

    public uint GetPixelInt(int x, int y, int z)
    {
        if (_data.Length == 0 || z < 0 || z >= Depth || IsCompressed())
            return 0xff000000;
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);

        var offset = (z * Width * Height + y * Width + x) * (int)Components;
        uint ret = Components < 4 ? 0xff000000 : 0;
        switch (Components)
        {
            case 4:
                ret |= (uint)_data[offset + 3] << 24;
                ret |= (uint)_data[offset + 2] << 16;
                ret |= (uint)_data[offset + 1] << 8;
                ret |= _data[offset];
                break;
            case 3:
                ret |= (uint)_data[offset + 2] << 16;
                ret |= (uint)_data[offset + 1] << 8;
                ret |= _data[offset];
                break;
            case 2:
                ret |= (uint)_data[offset + 1] << 8;
                ret |= _data[offset];
                break;
            default:
                ret |= (uint)_data[offset] << 16;
                ret |= (uint)_data[offset] << 8;
                ret |= _data[offset];
                break;
        }

        return ret;
    }

    public Color GetPixelBilinear(float x, float y)
    {
        x = MathDefs.Clamp(x * Width - 0.5f, 0.0f, Width - 1.0f);
        y = MathDefs.Clamp(y * Height - 0.5f, 0.0f, Height - 1.0f);

        var xI = (int)x;
        var yI = (int)y;
        var xF = MathDefs.Fract(x);
        var yF = MathDefs.Fract(y);

        var topColor = GetPixel(xI, yI).Lerp(GetPixel(xI + 1, yI), xF);
        var bottomColor = GetPixel(xI, yI + 1).Lerp(GetPixel(xI + 1, yI + 1), xF);
        return topColor.Lerp(bottomColor, yF);
    }

    public Color GetPixelTrilinear(float x, float y, float z)
    {
        if (Depth < 2)
            return GetPixelBilinear(x, y);

        x = MathDefs.Clamp(x * Width - 0.5f, 0.0f, Width - 1.0f);
        y = MathDefs.Clamp(y * Height - 0.5f, 0.0f, Height - 1.0f);
        z = MathDefs.Clamp(z * Depth - 0.5f, 0.0f, Depth - 1.0f);

        var xI = (int)x;
        var yI = (int)y;
        var zI = (int)z;
        if (zI == Depth - 1)
            return GetPixelBilinear(x, y);
        var xF = MathDefs.Fract(x);
        var yF = MathDefs.Fract(y);
        var zF = MathDefs.Fract(z);

        var topColorNear = GetPixel(xI, yI, zI).Lerp(GetPixel(xI + 1, yI, zI), xF);
        var bottomColorNear = GetPixel(xI, yI + 1, zI).Lerp(GetPixel(xI + 1, yI + 1, zI), xF);
        var colorNear = topColorNear.Lerp(bottomColorNear, yF);
        var topColorFar = GetPixel(xI, yI, zI + 1).Lerp(GetPixel(xI + 1, yI, zI + 1), xF);
        var bottomColorFar = GetPixel(xI, yI + 1, zI + 1).Lerp(GetPixel(xI + 1, yI + 1, zI + 1), xF);
        var colorFar = topColorFar.Lerp(bottomColorFar, yF);
        return colorNear.Lerp(colorFar, zF);
    }

    public TextureFormat GetGPUFormat()
    {
        if (!IsCompressed())
        {
            return Components switch
            {
                1 => TextureFormat.TexFormatR8Unorm,
                2 => TextureFormat.TexFormatRg8Unorm,
                3 => TextureFormat.TexFormatUnknown,
                4 => TextureFormat.TexFormatRgba8Unorm,
                _ => TextureFormat.TexFormatUnknown
            };
        }

        var compressedFormat = GetCompressedFormat();
        return compressedFormat == EmulatedTextureFormat.PvrtcRgb2Bpp
            || compressedFormat == EmulatedTextureFormat.PvrtcRgba2Bpp
            || compressedFormat == EmulatedTextureFormat.PvrtcRgb4Bpp
            || compressedFormat == EmulatedTextureFormat.PvrtcRgba4Bpp
                ? TextureFormat.TexFormatUnknown
                : compressedFormat;
    }

    public int GetWidth() => Width;

    public int GetHeight() => Height;

    public int GetDepth() => Depth;

    public int GetComponents() => Components;

    public Vector3i GetSize() => new(Width, Height, Depth);

    public byte[] GetData() => _data;

    public bool IsCompressed() => CompressedFormat != CompressedFormat.None;

    public TextureFormat GetCompressedFormat() => CompressedFormat switch
    {
        CompressedFormat.None => TextureFormat.TexFormatUnknown,
        CompressedFormat.Rgba => TextureFormat.TexFormatRgba8Unorm,
        CompressedFormat.Dxt1 => TextureFormat.TexFormatBc1Unorm,
        CompressedFormat.Dxt3 => TextureFormat.TexFormatBc2Unorm,
        CompressedFormat.Dxt5 => TextureFormat.TexFormatBc3Unorm,
        CompressedFormat.Etc1 => TextureFormat.TexFormatEtc2Rgb8Unorm,
        CompressedFormat.Etc2Rgb => TextureFormat.TexFormatEtc2Rgb8Unorm,
        CompressedFormat.Etc2Rgba => TextureFormat.TexFormatEtc2Rgba8Unorm,
        CompressedFormat.PvrtcRgb2Bpp => EmulatedTextureFormat.PvrtcRgb2Bpp,
        CompressedFormat.PvrtcRgba2Bpp => EmulatedTextureFormat.PvrtcRgba2Bpp,
        CompressedFormat.PvrtcRgb4Bpp => EmulatedTextureFormat.PvrtcRgb4Bpp,
        CompressedFormat.PvrtcRgba4Bpp => EmulatedTextureFormat.PvrtcRgba4Bpp,
        _ => TextureFormat.TexFormatUnknown
    };

    public bool IsCubemap() => Cubemap;

    public bool IsArray() => Array;

    public bool IsSRGB() => SRGB;

    public Image? GetNextLevel()
    {
        if (IsCompressed())
            return null;

        if (Components is < 1 or > 4)
            return null;

        // No next level for 1x1 (or 1x1x1) images.
        if (Width <= 1 && Height <= 1 && Depth <= 1)
            return null;

        if (_nextLevel is not null)
            return _nextLevel;

        var nextWidth = Math.Max(Width / 2, 1);
        var nextHeight = Math.Max(Height / 2, 1);
        var nextDepth = Math.Max(Depth / 2, 1);
        var mipImage = new Image(string.Empty);
        if (Depth > 1)
            mipImage.SetSize(nextWidth, nextHeight, nextDepth, Components);
        else
            mipImage.SetSize(nextWidth, nextHeight, Components);

        // 1D case.
        if (Depth == 1 && (Height == 1 || Width == 1))
        {
            var outputLength = Math.Max(nextWidth, nextHeight);
            for (var x = 0; x < outputLength; ++x)
            {
                for (var component = 0; component < Components; ++component)
                {
                    var sourceOffset = x * 2 * Components + component;
                    mipImage._data[x * Components + component] =
                        (byte)((_data[sourceOffset] + _data[sourceOffset + Components]) >> 1);
                }
            }
        }
        // 2D case.
        else if (Depth == 1)
        {
            for (var y = 0; y < nextHeight; ++y)
            {
                for (var x = 0; x < nextWidth; ++x)
                {
                    var upperLeft = ((y * 2) * Width + x * 2) * Components;
                    var lowerLeft = upperLeft + Width * Components;
                    var output = (y * nextWidth + x) * Components;
                    for (var component = 0; component < Components; ++component)
                    {
                        mipImage._data[output + component] = (byte)((
                            _data[upperLeft + component] + _data[upperLeft + Components + component]
                            + _data[lowerLeft + component] + _data[lowerLeft + Components + component]) >> 2);
                    }
                }
            }
        }
        // 3D case.
        else
        {
            var sourceSliceStride = Width * Height * Components;
            for (var z = 0; z < nextDepth; ++z)
            {
                for (var y = 0; y < nextHeight; ++y)
                {
                    for (var x = 0; x < nextWidth; ++x)
                    {
                        var outerUpperLeft = ((z * 2 * Height + y * 2) * Width + x * 2) * Components;
                        var innerUpperLeft = outerUpperLeft + sourceSliceStride;
                        var output = ((z * nextHeight + y) * nextWidth + x) * Components;
                        for (var component = 0; component < Components; ++component)
                        {
                            mipImage._data[output + component] = (byte)((
                                _data[outerUpperLeft + component]
                                + _data[outerUpperLeft + Components + component]
                                + _data[outerUpperLeft + Width * Components + component]
                                + _data[outerUpperLeft + Width * Components + Components + component]
                                + _data[innerUpperLeft + component]
                                + _data[innerUpperLeft + Components + component]
                                + _data[innerUpperLeft + Width * Components + component]
                                + _data[innerUpperLeft + Width * Components + Components + component]) >> 3);
                        }
                    }
                }
            }
        }

        return mipImage;
    }

    public Image? GetNextSibling() => _nextSibling;

    internal void SetNextSibling(Image? sibling) => _nextSibling = sibling;

    public Image? ConvertToRGBA()
    {
        if (IsCompressed())
            return null;

        if (Components is < 1 or > 4 || _data.Length == 0)
            return null;

        var rgba = new Image(string.Empty);
        rgba.SetSize(Width, Height, Depth, 4);
        var source = 0;
        var destination = 0;
        for (var i = 0; i < Width * Height * Depth; ++i)
        {
            switch (Components)
            {
                case 1:
                    rgba._data[destination++] = _data[source];
                    rgba._data[destination++] = _data[source];
                    rgba._data[destination++] = _data[source++];
                    rgba._data[destination++] = 255;
                    break;
                case 2:
                    rgba._data[destination++] = _data[source];
                    rgba._data[destination++] = _data[source];
                    rgba._data[destination++] = _data[source++];
                    rgba._data[destination++] = _data[source++];
                    break;
                case 3:
                    rgba._data[destination++] = _data[source++];
                    rgba._data[destination++] = _data[source++];
                    rgba._data[destination++] = _data[source++];
                    rgba._data[destination++] = 255;
                    break;
                case 4:
                    _data.AsSpan(source, 4).CopyTo(rgba._data.AsSpan(destination, 4));
                    source += 4;
                    destination += 4;
                    break;
            }
        }

        return rgba;
    }

    public Image? GetSubimage(IntRect rect)
    {
        if (_data.Length == 0 || Depth > 1 || rect.Width <= 0 || rect.Height <= 0 || rect.Left < 0 || rect.Top < 0 || rect.Right > Width || rect.Bottom > Height)
            return null;

        if (!IsCompressed())
        {
            var subimage = new Image(string.Empty);
            subimage.SetSize(rect.Width, rect.Height, Components);
            var rowSize = rect.Width * Components;
            var source = (rect.Top * Width + rect.Left) * Components;
            for (var y = 0; y < rect.Height; ++y)
            {
                _data.AsSpan(source, rowSize).CopyTo(subimage._data.AsSpan(y * rowSize, rowSize));
                source += Width * Components;
            }
            return subimage;
        }

        var (blockWidth, blockHeight, blockSize) = CompressedFormat switch
        {
            CompressedFormat.Rgba => (1, 1, 4),
            CompressedFormat.Dxt1 or CompressedFormat.Etc1 or CompressedFormat.Etc2Rgb
                or CompressedFormat.PvrtcRgb4Bpp or CompressedFormat.PvrtcRgba4Bpp => (4, 4, 8),
            CompressedFormat.Dxt3 or CompressedFormat.Dxt5 or CompressedFormat.Etc2Rgba => (4, 4, 16),
            CompressedFormat.PvrtcRgb2Bpp or CompressedFormat.PvrtcRgba2Bpp => (8, 4, 8),
            _ => (0, 0, 0)
        };
        if (blockSize == 0)
            return null;

        // Pad the region to be a multiple of block size.
        var paddedRect = new IntRect(
            rect.Left / blockWidth * blockWidth,
            rect.Top / blockHeight * blockHeight,
            rect.Right / blockWidth * blockWidth,
            rect.Bottom / blockHeight * blockHeight);
        var currentRect = paddedRect;
        var subimageData = new List<byte>();
        var subimageLevels = 0;

        // Save as many mips as possible until the next mip would cross a block boundary.
        for (var i = 0; i < NumCompressedLevels; ++i)
        {
            var level = GetCompressedLevel(i);
            if (level.Data.IsEmpty)
                break;

            // Mips are stored continuously.
            var sourceBlocksInRow = (level.Width + blockWidth - 1) / blockWidth;
            var sourceRowSize = sourceBlocksInRow * blockSize;
            var destinationRowSize = currentRect.Width / blockWidth * blockSize;
            var destinationSize = currentRect.Height / blockHeight * destinationRowSize;
            if (destinationSize == 0)
                break;

            var destinationStartOffset = subimageData.Count;
            subimageData.AddRange(new byte[destinationSize]);
            var beginX = currentRect.Left / blockWidth;
            var beginY = currentRect.Top / blockHeight;
            var endY = currentRect.Bottom / blockHeight;
            var destinationOffset = destinationStartOffset;
            for (var y = beginY; y < endY; ++y)
            {
                level.Data.Span.Slice(sourceRowSize * y + beginX * blockSize, destinationRowSize)
                    .CopyTo(CollectionsMarshal.AsSpan(subimageData).Slice(destinationOffset, destinationRowSize));
                destinationOffset += destinationRowSize;
            }

            ++subimageLevels;
            if ((currentRect.Left & blockWidth) != 0 || (currentRect.Right & blockWidth) != 0
                || (currentRect.Top & blockHeight) != 0 || (currentRect.Bottom & blockHeight) != 0)
                break;

            currentRect.Left /= 2;
            currentRect.Right /= 2;
            currentRect.Top /= 2;
            currentRect.Bottom /= 2;
        }

        if (subimageLevels == 0)
            return null;

        var compressedSubimage = new Image(string.Empty)
        {
            Width = paddedRect.Width,
            Height = paddedRect.Height,
            Depth = 1,
            CompressedFormat = CompressedFormat,
            NumCompressedLevels = subimageLevels,
            Components = Components,
            _data = subimageData.ToArray()
        };
        compressedSubimage.MemoryUse = compressedSubimage._data.Length;
        return compressedSubimage;
    }

    public bool SetSubimage(Image image, IntRect rect)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_data.Length == 0 || Depth > 1 || IsCompressed())
            return false;

        if (Components != image.Components)
            return false;

        if (rect.Width <= 0 || rect.Height <= 0
            || rect.Left < 0 || rect.Top < 0 || rect.Right > Width || rect.Bottom > Height)
            return false;

        if (rect.Width == image.Width && rect.Height == image.Height)
        {
            var source = 0;
            var destination = (rect.Top * Width + rect.Left) * Components;
            var rowSize = rect.Width * Components;
            for (var y = 0; y < rect.Height; ++y)
            {
                image._data.AsSpan(source, rowSize).CopyTo(_data.AsSpan(destination, rowSize));
                source += rect.Width * image.Components;
                destination += Width * Components;
            }
        }
        else
        {
            var destination = (rect.Top * Width + rect.Left) * Components;
            for (var y = 0; y < rect.Height; ++y)
            {
                for (var x = 0; x < rect.Width; ++x)
                {
                    var xF = image.Width > 1 ? (float)x / (rect.Width - 1) : 0.0f;
                    var yF = image.Height > 1 ? (float)y / (rect.Height - 1) : 0.0f;
                    var uintColor = image.GetPixelBilinear(xF, yF).ToUInt();
                    for (var component = 0; component < Components; ++component)
                        _data[destination++] = (byte)(uintColor >> (component * 8));
                }
                destination += (Width - rect.Width) * Components;
            }
        }
        return true;
    }

    public void PrecalculateLevels()
    {
        if (_data.Length == 0 || IsCompressed())
            return;

        _nextLevel = null;

        if (Width > 1 || Height > 1)
        {
            var current = GetNextLevel();
            _nextLevel = current;
            while (current is not null && (current.Width > 1 || current.Height > 1))
            {
                current._nextLevel = current.GetNextLevel();
                current = current._nextLevel;
            }
        }
    }

    public void CleanupLevels() => _nextLevel = null;

    public void GetLevels(ICollection<Image> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        levels.Clear();
        for (Image? level = this; level is not null; level = level._nextLevel)
            levels.Add(level);
    }

    public bool HasAlphaChannel() => Components > 3;

    public CompressedLevel GetCompressedLevel(int index)
    {
        if (!IsCompressed() || index < 0 || index >= NumCompressedLevels)
            return default;

        var offset = 0;
        for (var level = 0; level < index; ++level)
        {
            offset = checked(offset + ImageDds.GetCompressedLevelSize(
                CompressedFormat,
                GetLevelDimension(Width, level),
                GetLevelDimension(Height, level),
                GetLevelDimension(Depth, level)));
        }

        var levelWidth = GetLevelDimension(Width, index);
        var levelHeight = GetLevelDimension(Height, index);
        var levelDepth = GetLevelDimension(Depth, index);
        var levelSize = ImageDds.GetCompressedLevelSize(CompressedFormat, levelWidth, levelHeight, levelDepth);
        if (offset + levelSize > _data.Length)
            return default;

        return new CompressedLevel
        {
            Data = new Memory<byte>(_data, offset, levelSize),
            Format = CompressedFormat,
            Width = levelWidth,
            Height = levelHeight,
            Depth = levelDepth,
            DataSize = levelSize
        };
    }

    public Image? GetDecompressedImageLevel(int index)
    {
        if (!IsCompressed())
        {
            if (index == 0)
                return ConvertToRGBA();

            Image? mip = this;
            for (var level = 0; level < index; ++level)
                mip = mip?.GetNextLevel();
            return mip?.ConvertToRGBA();
        }

        if (NumCompressedLevels == 0)
            return null;

        var clampedIndex = Math.Min(index, NumCompressedLevels);
        var compressedLevel = GetCompressedLevel(clampedIndex);
        if (compressedLevel.DataSize == 0)
            return null;

        var decompressed = new Image(string.Empty);
        if (!decompressed.SetSize(compressedLevel.Width, compressedLevel.Height, 4))
            return null;

        if (!compressedLevel.Decompress(decompressed._data))
            return null;

        return decompressed;
    }

    public Image? GetDecompressedImage() => GetDecompressedImageLevel(0);

    public bool LoadColorLUT(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ImageSdl.TryLoadColorLut(this, source);
    }

    internal void SetCompressedData(
        int width,
        int height,
        int depth,
        int components,
        CompressedFormat format,
        int levels,
        byte[] pixelData,
        bool cubemap,
        bool array,
        bool sRgb)
    {
        Width = width;
        Height = height;
        Depth = depth;
        Components = components;
        CompressedFormat = format;
        NumCompressedLevels = Math.Max(levels, 1);
        _data = pixelData;
        Cubemap = cubemap;
        Array = array;
        SRGB = sRgb;
        MemoryUse = pixelData.Length;
        _nextLevel = null;
    }

    private int GetOffset(int x, int y, int z)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
            throw new ArgumentOutOfRangeException();

        return ((((z * Height) + y) * Width) + x) * (int)Components;
    }

    private void SwapPixels(int leftOffset, int rightOffset)
    {
        for (var i = 0; i < Components; ++i)
            (_data[leftOffset + i], _data[rightOffset + i]) = (_data[rightOffset + i], _data[leftOffset + i]);
    }

    private void SwapRows(int topOffset, int bottomOffset, int rowStride)
    {
        for (var i = 0; i < rowStride; ++i)
            (_data[topOffset + i], _data[bottomOffset + i]) = (_data[bottomOffset + i], _data[topOffset + i]);
    }

    private void WriteColor(int offset, Color color)
    {
        color.Clip(clipAlpha: true);
        var packed = color.ToUInt();
        _data[offset] = (byte)(packed & 0xff);
        if (Components > 1)
            _data[offset + 1] = (byte)((packed >> 8) & 0xff);
        if (Components > 2)
            _data[offset + 2] = (byte)((packed >> 16) & 0xff);
        if (Components > 3)
            _data[offset + 3] = (byte)((packed >> 24) & 0xff);
    }

    private static int GetLevelDimension(int size, int level)
    {
        var value = size;
        for (var i = 0; i < level && value > 1; ++i)
            value = Math.Max(value / 2, 1);
        return value;
    }
}
