using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace RedBolt;

public class Image : Resource
{
    public Image()
    {
    }

    public override void Dispose()
    {
        _nextSibling = null;
        _nextLevel = null;
        _data = System.Array.Empty<byte>();
        base.Dispose();
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int Depth { get; private set; }

    public int Components { get; private set; }

    public int NumCompressedLevels { get; private set; }

    public bool Cubemap { get; internal set; }

    public bool Array { get; internal set; }

    public bool SRGB { get; internal set; }

    public TextureFormat CompressedFormat { get; private set; }

    private byte[] _data = System.Array.Empty<byte>();

    private Image? _nextLevel;

    private Image? _nextSibling;

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Check for DDS, KTX or PVR compressed format
        var fileID = source.ReadFileID();

        if (fileID == "DDS ")
        {
            source.Seek(0);
            var data = new byte[checked((int)source.Size)];
            if (source.Read(data) != source.Size)
                return false;

            using var stream = new MemoryStream(data, writable: false);
            return ImageDds.TryLoad(this, stream);
        }
        else if (fileID == "\u00abKTX")
        {
            source.Seek(12);

            var endianness = source.ReadUInt();
            var type = source.ReadUInt();
            /* uint typeSize = */ source.ReadUInt();
            var format = source.ReadUInt();
            var internalFormat = source.ReadUInt();
            /* uint baseInternalFormat = */ source.ReadUInt();
            var width = source.ReadUInt();
            var height = source.ReadUInt();
            var depth = source.ReadUInt();
            /* uint arrayElements = */ source.ReadUInt();
            var faces = source.ReadUInt();
            var mipmaps = source.ReadUInt();
            var keyValueBytes = source.ReadUInt();

            if (endianness != 0x04030201)
            {
                Log.GetLogger().LogError("Big-endian KTX files not supported");
                return false;
            }

            if (type != 0 || format != 0)
            {
                Log.GetLogger().LogError("Uncompressed KTX files not supported");
                return false;
            }

            if (faces > 1 || depth > 1)
            {
                Log.GetLogger().LogError("3D or cube KTX files not supported");
                return false;
            }

            if (mipmaps == 0)
            {
                Log.GetLogger().LogError("KTX files without explicitly specified mipmap count not supported");
                return false;
            }

            switch (internalFormat)
            {
                case 0x83f1:
                    CompressedFormat = TextureFormat.TexFormatBc1Unorm;
                    Components = 4;
                    break;

                case 0x83f2:
                    CompressedFormat = TextureFormat.TexFormatBc2Unorm;
                    Components = 4;
                    break;

                case 0x83f3:
                    CompressedFormat = TextureFormat.TexFormatBc3Unorm;
                    Components = 4;
                    break;

                case 0x8d64:
                case 0x9274:
                    CompressedFormat = TextureFormat.TexFormatEtc2Rgb8Unorm;
                    Components = 3;
                    break;

                case 0x9278:
                    CompressedFormat = TextureFormat.TexFormatEtc2Rgba8Unorm;
                    Components = 4;
                    break;

                case 0x8c00:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgb4Bpp;
                    Components = 3;
                    break;

                case 0x8c01:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgb2Bpp;
                    Components = 3;
                    break;

                case 0x8c02:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgba4Bpp;
                    Components = 4;
                    break;

                case 0x8c03:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgba2Bpp;
                    Components = 4;
                    break;

                default:
                    CompressedFormat = TextureFormat.TexFormatUnknown;
                    break;
            }

            if (CompressedFormat == TextureFormat.TexFormatUnknown)
            {
                Log.GetLogger().LogError("Unsupported texture format in KTX file");
                return false;
            }

            source.Seek(source.Position + keyValueBytes);
            var dataSize = source.Size - source.Position - mipmaps * sizeof(uint);
            _data = new byte[checked((int)dataSize)];
            Width = checked((int)width);
            Height = checked((int)height);
            NumCompressedLevels = checked((int)mipmaps);
            var dataOffset = 0;
            for (var i = 0; i < mipmaps; ++i)
            {
                var levelSize = source.ReadUInt();
                if (levelSize + dataOffset > dataSize)
                {
                    Log.GetLogger().LogError("KTX mipmap level data size exceeds file size");
                    return false;
                }

                _ = source.Read(_data.AsSpan(dataOffset, checked((int)levelSize)));
                dataOffset += checked((int)levelSize);
                if ((source.Position & 3) != 0)
                    source.Seek((source.Position + 3) & 0xfffffffc);
            }

            MemoryUse = checked((int)dataSize);
        }
        else if (fileID == "PVR\u0003")
        {
            /* uint flags = */ source.ReadUInt();
            var pixelFormatLo = source.ReadUInt();
            /* uint pixelFormatHi = */ source.ReadUInt();
            /* uint colourSpace = */ source.ReadUInt();
            /* uint channelType = */ source.ReadUInt();
            var height = source.ReadUInt();
            var width = source.ReadUInt();
            var depth = source.ReadUInt();
            /* uint numSurfaces = */ source.ReadUInt();
            var numFaces = source.ReadUInt();
            var mipmapCount = source.ReadUInt();
            var metaDataSize = source.ReadUInt();

            if (depth > 1 || numFaces > 1)
            {
                Log.GetLogger().LogError("3D or cube PVR files not supported");
                return false;
            }

            if (mipmapCount == 0)
            {
                Log.GetLogger().LogError("PVR files without explicitly specified mipmap count not supported");
                return false;
            }

            switch (pixelFormatLo)
            {
                case 0:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgb2Bpp;
                    Components = 3;
                    break;

                case 1:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgba2Bpp;
                    Components = 4;
                    break;

                case 2:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgb4Bpp;
                    Components = 3;
                    break;

                case 3:
                    CompressedFormat = EmulatedTextureFormat.PvrtcRgba4Bpp;
                    Components = 4;
                    break;

                case 6:
                    CompressedFormat = TextureFormat.TexFormatEtc2Rgb8Unorm;
                    Components = 3;
                    break;

                case 7:
                    CompressedFormat = TextureFormat.TexFormatBc1Unorm;
                    Components = 4;
                    break;

                case 9:
                    CompressedFormat = TextureFormat.TexFormatBc2Unorm;
                    Components = 4;
                    break;

                case 11:
                    CompressedFormat = TextureFormat.TexFormatBc3Unorm;
                    Components = 4;
                    break;

                // .pvr files also support ETC2 texture format.
                case 22:
                    CompressedFormat = TextureFormat.TexFormatEtc2Rgb8Unorm;
                    Components = 3;
                    break;

                case 23:
                    CompressedFormat = TextureFormat.TexFormatEtc2Rgba8Unorm;
                    Components = 4;
                    break;

                default:
                    CompressedFormat = TextureFormat.TexFormatUnknown;
                    break;
            }

            if (CompressedFormat == TextureFormat.TexFormatUnknown)
            {
                Log.GetLogger().LogError("Unsupported texture format in PVR file");
                return false;
            }

            source.Seek(source.Position + metaDataSize);
            var dataSize = source.Size - source.Position;
            _data = new byte[checked((int)dataSize)];
            Width = checked((int)width);
            Height = checked((int)height);
            NumCompressedLevels = checked((int)mipmapCount);
            _ = source.Read(_data);
            MemoryUse = checked((int)dataSize);
        }
        else if (fileID == "RIFF")
        {
            // WebP: https://developers.google.com/speed/webp/docs/api

            // RIFF layout is:
            //   Offset  tag
            //   0...3   "RIFF" 4-byte tag
            //   4...7   size of image data (including metadata) starting at offset 8
            //   8...11  "WEBP"   our form-type signature
            const int tagSize = 4;

            source.Seek(8);
            Span<byte> fourCC = stackalloc byte[tagSize];
            var bytesRead = source.Read(fourCC);
            if (bytesRead != tagSize)
            {
                // Truncated.
                Log.GetLogger().LogError("Truncated RIFF data");
                return false;
            }
            ReadOnlySpan<byte> webp = [(byte)'W', (byte)'E', (byte)'B', (byte)'P'];
            if (!fourCC.SequenceEqual(webp))
            {
                // VP8_STATUS_BITSTREAM_ERROR
                Log.GetLogger().LogError("Invalid header");
                return false;
            }

            // Read the file to buffer.
            var data = new byte[checked((int)source.Size)];
            source.Seek(0);
            _ = source.Read(data);

            using var features = new RedBolt.Bindings.WebP.WebPBitstreamFeatures();
            const int webpDecoderAbiVersion = 0x0210;
            if (RedBolt.Bindings.WebP.WebPNativeBindings.WebPGetFeaturesInternal(
                    in data[0], (nuint)data.Length, features, webpDecoderAbiVersion)
                != RedBolt.Bindings.WebP.VP8StatusCode.Vp8StatusOk)
            {
                Log.GetLogger().LogError("Error reading WebP image: {Name}", source.Name);
                return false;
            }

            var width = features.Width;
            var height = features.Height;
            var components = features.HasAlpha != 0 ? 4 : 3;
            var imageData = new byte[checked(width * height * components)];
            var decoded = components == 4
                ? RedBolt.Bindings.WebP.WebPNativeBindings.WebPDecodeRGBAInto(
                    in data[0], (nuint)data.Length, out imageData[0], (nuint)imageData.Length, 4 * width)
                : RedBolt.Bindings.WebP.WebPNativeBindings.WebPDecodeRGBInto(
                    in data[0], (nuint)data.Length, out imageData[0], (nuint)imageData.Length, 3 * width);
            if (decoded == IntPtr.Zero)
            {
                Log.GetLogger().LogError("Error decoding WebP image:{Name}", source.Name);
                return false;
            }

            SetSize(width, height, components);
            SetData(imageData);
        }
        else
        {
            // Not DDS, KTX or PVR, use STBImage to load other image formats as uncompressed
            source.Seek(0);
            var pixelData = GetImageData(source, out var width, out var height, out var components);
            if (pixelData == IntPtr.Zero)
            {
                Log.GetLogger().LogError("Could not load image {Name}: {Reason}", source.Name, GetStbiFailureReason());
                return false;
            }

            SetSize(width, height, components);
            var imageData = new byte[checked(width * height * components)];
            Marshal.Copy(pixelData, imageData, 0, imageData.Length);
            SetData(imageData);
            FreeImageData(pixelData);
        }

        return true;
    }

    public override unsafe bool Save(Serializer destination)
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
                return destination.Write(new ReadOnlySpan<byte>((void*)png, length)) == length;
            }
            finally
            {
                NativeMemory.Free((void*)png);
            }
        }
    }

    public override bool SaveFile(FileIdentifier fileName)
    {
        // TODO(vfs): This function can save only to the host filesystem.
        var absoluteFileName = fileName.FileName;
        if (fileName.Scheme != "file")
        {
            Log.GetLogger().LogError("Can not save image {FileName}", fileName.ToUri());
            return false;
        }

        var directoryName = Path.GetDirectoryName(absoluteFileName);
        if (!string.IsNullOrEmpty(directoryName))
            Directory.CreateDirectory(directoryName);

        return Path.GetExtension(absoluteFileName).ToLowerInvariant() switch
        {
            ".dds" => SaveDDS(absoluteFileName),
            ".bmp" => SaveBMP(absoluteFileName),
            ".jpg" or ".jpeg" => SaveJPG(absoluteFileName, 100),
            ".tga" => SaveTGA(absoluteFileName),
            ".webp" => SaveWEBP(absoluteFileName, 100.0f),
            _ => SavePNG(absoluteFileName)
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
        {
            Log.GetLogger().LogError("More than 4 color components are not supported");
            return false;
        }

        _data = new byte[checked(width * height * depth * components)];
        Width = width;
        Height = height;
        Depth = depth;
        Components = components;
        CompressedFormat = TextureFormat.TexFormatUnknown;
        NumCompressedLevels = 0;
        _nextLevel = null;

        MemoryUse = _data.Length;
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

    public bool LoadColorLUT(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var fileId = source.ReadFileID();
        if (fileId is "DDS " or "\u00abKTX" or "PVR\u0003")
        {
            Log.GetLogger().LogError("Invalid image format, can not load image");
            return false;
        }

        source.Seek(0);
        var pixelData = GetImageData(source, out var width, out var height, out var components);
        if (pixelData == IntPtr.Zero)
        {
            Log.GetLogger().LogError("Could not load image {Name}: {Reason}", source.Name, GetStbiFailureReason());
            return false;
        }
        if (components != 3)
        {
            Log.GetLogger().LogError("Invalid image format, can not load image");
            return false;
        }

        const int colorLutSize = 16;
        SetSize(colorLutSize, colorLutSize, colorLutSize, components);
        var pixelDataIn = new byte[checked(width * height * components)];
        Marshal.Copy(pixelData, pixelDataIn, 0, pixelDataIn.Length);
        var pixelDataOut = GetData();
        for (var z = 0; z < Depth; ++z)
        {
            for (var y = 0; y < Height; ++y)
            {
                var inputOffset = z * Width * 3 + y * width * 3;
                var outputOffset = z * Width * Height * 3 + y * Width * 3;
                for (var x = 0; x < Width * 3; x += 3)
                {
                    pixelDataOut[outputOffset + x] = pixelDataIn[inputOffset + x];
                    pixelDataOut[outputOffset + x + 1] = pixelDataIn[inputOffset + x + 1];
                    pixelDataOut[outputOffset + x + 2] = pixelDataIn[inputOffset + x + 2];
                }
            }
        }

        FreeImageData(pixelData);
        return true;
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

    public unsafe bool SaveBMP(string fileName)
    {
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

    public bool SavePNG(string fileName)
    {
        var directoryName = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);

        using var outFile = new File(fileName, FileMode.Write);
        return outFile.IsOpen && Save(outFile);
    }

    public unsafe bool SaveTGA(string fileName)
    {
        if (IsCompressed())
        {
            Log.GetLogger().LogError("Can not save compressed image to TGA");
            return false;
        }

        if (_data.Length == 0)
            return false;

        var nativeFileName = System.Text.Encoding.UTF8.GetBytes(fileName + '\0');
        fixed (byte* data = _data)
        {
            return RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiWriteTga(
                nativeFileName, Width, Height, Components, (nint)data) != 0;
        }
    }

    public unsafe bool SaveJPG(string fileName, int quality)
    {
        if (IsCompressed())
        {
            Log.GetLogger().LogError("Can not save compressed image to JPG");
            return false;
        }

        if (_data.Length == 0)
            return false;

        var nativeFileName = System.Text.Encoding.UTF8.GetBytes(fileName + '\0');
        fixed (byte* data = _data)
        {
            return RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiWriteJpg(
                nativeFileName, Width, Height, Components, (nint)data, quality) != 0;
        }
    }

    public bool SaveDDS(string fileName)
    {
        using var outFile = new File(fileName, FileMode.Write);
        if (!outFile.IsOpen)
        {
            Log.GetLogger().LogError("Access denied to {FileName}", fileName);
            return false;
        }

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

        _ = outFile.Write(stream.ToArray());
        return true;
    }

    public bool SaveWEBP(string fileName, float compression = 0.0f)
    {
        using var outFile = new File(fileName, FileMode.Write);

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

        if (Components != 4 && Components != 3)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, $"Can not save image with {Components} components to WebP, which requires 3 or 4; Try ConvertToRGBA first?", null, static (state, _) => state);
            return false;
        }

        if (_data.Length == 0)
        {
            Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default, "No image data to save", null, static (state, _) => state);
            return false;
        }

        using var picture = new RedBolt.Bindings.WebP.WebPPicture();
        using var config = new RedBolt.Bindings.WebP.WebPConfig();
        using var writer = new RedBolt.Bindings.WebP.WebPMemoryWriter();
        const int webpEncoderAbiVersion = 0x0210;
        if (RedBolt.Bindings.WebP.WebPNativeBindings.WebPConfigInitInternal(
                config, RedBolt.Bindings.WebP.WebPPreset.WebpPresetDefault, compression, webpEncoderAbiVersion) == 0
            || RedBolt.Bindings.WebP.WebPNativeBindings.WebPPictureInitInternal(picture, webpEncoderAbiVersion) == 0)
        {
            Log.GetLogger().LogError("WebP initialization failed; check installation");
            return false;
        }

        config.Lossless = 1;
        config.Exact = 1;

        picture.UseArgb = 1;
        picture.Width = Width;
        picture.Height = Height;
        picture.Writer = RedBolt.Bindings.WebP.WebPNativeBindings.WebPMemoryWriteFunctionPointer;
        picture.CustomPtr = writer.Handle;
        RedBolt.Bindings.WebP.WebPNativeBindings.WebPMemoryWriterInit(writer);

        var pictureInitialized = true;
        try
        {
            var importResult = Components == 4
                ? RedBolt.Bindings.WebP.WebPNativeBindings.WebPPictureImportRGBA(
                    picture, in _data[0], checked(Components * Width))
                : RedBolt.Bindings.WebP.WebPNativeBindings.WebPPictureImportRGB(
                    picture, in _data[0], checked(Components * Width));
            if (importResult == 0)
            {
                Log.GetLogger().LogError("WebP import of image data failed (truncated RGBA/RGB data or memory error?)");
                return false;
            }

            if (RedBolt.Bindings.WebP.WebPNativeBindings.WebPEncode(config, picture) == 0)
            {
                Log.GetLogger().LogError("WebP encoding failed (memory error?). WebPEncodingError = {Error}", picture.ErrorCode);
                return false;
            }

            RedBolt.Bindings.WebP.WebPNativeBindings.WebPPictureFree(picture);
            pictureInitialized = false;
            var encoded = new byte[checked((int)writer.Size)];
            Marshal.Copy(writer.Mem, encoded, 0, encoded.Length);
            _ = outFile.Write(encoded);
            return true;
        }
        finally
        {
            if (pictureInitialized)
                RedBolt.Bindings.WebP.WebPNativeBindings.WebPPictureFree(picture);
            RedBolt.Bindings.WebP.WebPNativeBindings.WebPMemoryWriterClear(writer);
        }
    }

    public bool IsCubemap() => Cubemap;

    public bool IsArray() => Array;

    public bool IsSRGB() => SRGB;

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

    public Vector3i GetSize() => new(Width, Height, Depth);

    public int GetComponents() => Components;

    public byte[] GetData() => _data;

    public bool IsCompressed() => CompressedFormat != TextureFormat.TexFormatUnknown;

    public TextureFormat GetCompressedFormat() => CompressedFormat;

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
        var mipImage = new Image();
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

    public Image? ConvertToRGBA()
    {
        if (IsCompressed())
            return null;

        if (Components is < 1 or > 4 || _data.Length == 0)
            return null;

        var rgba = new Image();
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

    public Image? GetDecompressedImage() => GetDecompressedImageLevel(0);

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

        var decompressed = new Image();
        if (!decompressed.SetSize(compressedLevel.Width, compressedLevel.Height, 4))
            return null;

        if (!compressedLevel.Decompress(decompressed._data))
            return null;

        return decompressed;
    }

    public Image? GetSubimage(IntRect rect)
    {
        if (_data.Length == 0 || Depth > 1 || rect.Width <= 0 || rect.Height <= 0 || rect.Left < 0 || rect.Top < 0 || rect.Right > Width || rect.Bottom > Height)
            return null;

        if (!IsCompressed())
        {
            var subimage = new Image();
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
            TextureFormat.TexFormatRgba8Unorm => (1, 1, 4),
            TextureFormat.TexFormatBc1Unorm or TextureFormat.TexFormatEtc2Rgb8Unorm => (4, 4, 8),
            TextureFormat.TexFormatBc2Unorm or TextureFormat.TexFormatBc3Unorm
                or TextureFormat.TexFormatEtc2Rgba8Unorm => (4, 4, 16),
            _ => (0, 0, 0)
        };
        if (CompressedFormat == EmulatedTextureFormat.PvrtcRgb2Bpp
            || CompressedFormat == EmulatedTextureFormat.PvrtcRgba2Bpp)
            (blockWidth, blockHeight, blockSize) = (8, 4, 8);
        else if (CompressedFormat == EmulatedTextureFormat.PvrtcRgb4Bpp
            || CompressedFormat == EmulatedTextureFormat.PvrtcRgba4Bpp)
            (blockWidth, blockHeight, blockSize) = (4, 4, 8);
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

        var compressedSubimage = new Image()
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

    /// <summary>Return an SDL surface from the image, or null if failed. Only RGB images are supported. Specify rect to only return partial image. You must free the surface yourself.</summary>
    public RedBolt.Bindings.Sdl.SdlSurface? GetSDLSurface(IntRect rect = default)
    {
        if (_data.Length == 0)
            return null;

        if (Depth > 1)
        {
            Log.GetLogger().LogError("Can not get SDL surface from 3D image");
            return null;
        }

        if (IsCompressed())
        {
            Log.GetLogger().LogError("Can not get SDL surface from compressed image {Name}", Name);
            return null;
        }

        if (Components < 3)
        {
            Log.GetLogger().LogError("Can not get SDL surface from image {Name} with less than 3 components", Name);
            return null;
        }

        var imageRect = rect;
        if (imageRect.Left < 0 || imageRect.Top < 0 || imageRect.Right > Width || imageRect.Bottom > Height
            || imageRect.Left >= imageRect.Right || imageRect.Top >= imageRect.Bottom)
        {
            imageRect = new IntRect(0, 0, Width, Height);
        }

        var width = imageRect.Width;
        var height = imageRect.Height;
        var surface = RedBolt.Bindings.Sdl.SdlNativeBindings.SdlCreateRGBSurface(
            0, width, height, Components * 8, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);
        if (surface.IsNull)
        {
            Log.GetLogger().LogError("Failed to create SDL surface from image {Name}", Name);
            return null;
        }

        RedBolt.Bindings.Sdl.SdlNativeBindings.SdlLockSurface(surface);
        var sourceOffset = Components * (Width * imageRect.Top + imageRect.Left);
        for (var i = 0; i < height; ++i)
        {
            Marshal.Copy(_data, sourceOffset, IntPtr.Add(surface.Pixels, i * surface.Pitch), Components * width);
            sourceOffset += Components * Width;
        }
        RedBolt.Bindings.Sdl.SdlNativeBindings.SdlUnlockSurface(surface);
        return surface;
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

    public bool HasAlphaChannel() => Components > 3;

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

    public void CleanupLevels() => _nextLevel = null;

    public void GetLevels(ICollection<Image> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        levels.Clear();
        for (Image? level = this; level is not null; level = level._nextLevel)
            levels.Add(level);
    }

    private static IntPtr GetImageData(Deserializer source, out int width, out int height, out int components)
    {
        var data = new byte[checked((int)source.Size)];
        source.Read(data);
        if (data.Length == 0)
        {
            width = 0;
            height = 0;
            components = 0;
            return IntPtr.Zero;
        }

        return RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiLoadFromMemory(
            in data[0], data.Length, out width, out height, out components, 0);
    }

    private static void FreeImageData(IntPtr pixelData)
    {
        if (pixelData == IntPtr.Zero)
            return;

        RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiImageFree(pixelData);
    }

    internal void SetNextSibling(Image? sibling) => _nextSibling = sibling;

    internal void SetCompressedData(
        int width,
        int height,
        int depth,
        int components,
        TextureFormat format,
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

    private static int GetLevelDimension(int size, int level)
    {
        var value = size;
        for (var i = 0; i < level && value > 1; ++i)
            value = Math.Max(value / 2, 1);
        return value;
    }

    private static string GetStbiFailureReason()
    {
        var reason = RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiFailureReason();
        var length = reason.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(length >= 0 ? reason[..length] : reason);
    }
}
