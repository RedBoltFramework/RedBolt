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

public sealed class Image : Resource
{
    private byte[] _data = System.Array.Empty<byte>();
    private CompressedFormat _compressedFormat;
    private Image? _nextLevel;
    private Image? _nextSibling;
    private uint _numCompressedLevels;

    public Image(string name)
        : base(name)
    {
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int Depth { get; private set; } = 1;

    public uint Components { get; private set; }

    public uint NumCompressedLevels => _numCompressedLevels;

    public bool Cubemap { get; internal set; }

    public bool Array { get; internal set; }

    public bool SRGB { get; internal set; }

    public CompressedFormat CompressedFormat => _compressedFormat;

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

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var rgba = Components == 4 ? this : ConvertToRGBA();
        ImageFileWriter.Save(destination, ".png", rgba.Width, rgba.Height, rgba.GetData());
        return true;
    }

    public bool SavePNG(string fileName) => SaveEncodedFile(fileName, ".png");

    public bool SaveJPG(string fileName, int quality) => ImageSdl.SaveJpg(this, fileName, quality);

    public bool SaveBMP(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return SaveEncodedFile(fileName, ".bmp");
    }

    public bool SaveTGA(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var rgba = Components == 4 ? this : ConvertToRGBA();
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

    private bool SaveEncodedFile(string fileName, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var rgba = Components == 4 ? this : ConvertToRGBA();
        using var stream = new MemoryStream();
        ImageFileWriter.Save(stream, extension, rgba.Width, rgba.Height, rgba.GetData());
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
        var file = virtualFileSystem.OpenFile(identifier, File.FileMode.Write);
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
            _ => base.SaveFile(fileName)
        };
    }

    public bool SetSize(int width, int height, uint components) => SetSize(width, height, 1, components);

    public bool SetSize(int width, int height, int depth, uint components)
    {
        if (width <= 0 || height <= 0 || depth <= 0 || components is 0 or > 4)
            return false;

        Width = width;
        Height = height;
        Depth = depth;
        Components = components;
        _data = new byte[checked(width * height * depth * (int)components)];
        MemoryUse = (uint)_data.Length;
        _compressedFormat = CompressedFormat.None;
        _numCompressedLevels = 0;
        _nextLevel = null;
        _nextSibling = null;
        return true;
    }

    public void SetData(byte[] pixelData)
    {
        ArgumentNullException.ThrowIfNull(pixelData);
        if (IsCompressed())
            throw new InvalidOperationException("Cannot overwrite pixel data for a compressed image.");
        if (pixelData.Length != _data.Length)
            throw new ArgumentException("Pixel data size does not match image size.", nameof(pixelData));

        pixelData.CopyTo(_data, 0);
        _nextLevel = null;
    }

    public void SetPixel(int x, int y, Color color) => SetPixel(x, y, 0, color);

    public void SetPixel(int x, int y, int z, Color color)
    {
        var offset = GetOffset(x, y, z);
        WriteColor(offset, color);
        _nextLevel = null;
    }

    public void SetPixelInt(int x, int y, uint uintColor) => SetPixelInt(x, y, 0, uintColor);

    public void SetPixelInt(int x, int y, int z, uint uintColor)
    {
        var color = new Color();
        color.FromUInt(uintColor);
        SetPixel(x, y, z, color);
    }

    public bool FlipHorizontal()
    {
        if (Width <= 1)
            return true;

        var rowStride = Width * (int)Components;
        var pixelStride = (int)Components;
        for (var z = 0; z < Depth; ++z)
        {
            for (var y = 0; y < Height; ++y)
            {
                var rowStart = ((z * Height) + y) * rowStride;
                for (var x = 0; x < Width / 2; ++x)
                {
                    var left = rowStart + x * pixelStride;
                    var right = rowStart + (Width - 1 - x) * pixelStride;
                    SwapPixels(left, right);
                }
            }
        }

        _nextLevel = null;
        return true;
    }

    public bool FlipVertical()
    {
        if (Height <= 1)
            return true;

        var rowStride = Width * (int)Components;
        for (var z = 0; z < Depth; ++z)
        {
            for (var y = 0; y < Height / 2; ++y)
            {
                var top = ((z * Height) + y) * rowStride;
                var bottom = ((z * Height) + (Height - 1 - y)) * rowStride;
                SwapRows(top, bottom, rowStride);
            }
        }

        _nextLevel = null;
        return true;
    }

    public bool Resize(int width, int height)
    {
        if (Depth != 1 || width <= 0 || height <= 0 || Components == 0)
            return false;

        var resized = new Image(Name);
        if (!resized.SetSize(width, height, Components))
            return false;

        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
            {
                var u = width == 1 ? 0.0f : x / (float)(width - 1);
                var v = height == 1 ? 0.0f : y / (float)(height - 1);
                resized.SetPixel(x, y, GetPixelBilinear(u, v));
            }
        }

        Width = resized.Width;
        Height = resized.Height;
        _data = resized._data;
        MemoryUse = resized.MemoryUse;
        _nextLevel = null;
        return true;
    }

    public void Clear(Color color)
    {
        for (var z = 0; z < Depth; ++z)
        {
            for (var y = 0; y < Height; ++y)
            {
                for (var x = 0; x < Width; ++x)
                    SetPixel(x, y, z, color);
            }
        }
    }

    public void ClearInt(uint uintColor)
    {
        var color = new Color();
        color.FromUInt(uintColor);
        Clear(color);
    }

    public Color GetPixel(int x, int y) => GetPixel(x, y, 0);

    public Color GetPixel(int x, int y, int z)
    {
        var offset = GetOffset(x, y, z);
        return ReadColor(offset);
    }

    public uint GetPixelInt(int x, int y) => GetPixelInt(x, y, 0);

    public uint GetPixelInt(int x, int y, int z) => GetPixel(x, y, z).ToUInt();

    public Color GetPixelBilinear(float x, float y)
    {
        if (Width == 0 || Height == 0)
            return Color.Transparent;

        var fx = MathDefs.Clamp(x, 0.0f, 1.0f) * Math.Max(Width - 1, 0);
        var fy = MathDefs.Clamp(y, 0.0f, 1.0f) * Math.Max(Height - 1, 0);
        var x0 = (int)MathF.Floor(fx);
        var y0 = (int)MathF.Floor(fy);
        var x1 = Math.Min(x0 + 1, Width - 1);
        var y1 = Math.Min(y0 + 1, Height - 1);
        var tx = fx - x0;
        var ty = fy - y0;

        var top = GetPixel(x0, y0).Lerp(GetPixel(x1, y0), tx);
        var bottom = GetPixel(x0, y1).Lerp(GetPixel(x1, y1), tx);
        return top.Lerp(bottom, ty);
    }

    public Color GetPixelTrilinear(float x, float y, float z)
    {
        if (Width == 0 || Height == 0 || Depth == 0)
            return Color.Transparent;

        var fx = MathDefs.Clamp(x, 0.0f, 1.0f) * Math.Max(Width - 1, 0);
        var fy = MathDefs.Clamp(y, 0.0f, 1.0f) * Math.Max(Height - 1, 0);
        var fz = MathDefs.Clamp(z, 0.0f, 1.0f) * Math.Max(Depth - 1, 0);
        var x0 = (int)MathF.Floor(fx);
        var y0 = (int)MathF.Floor(fy);
        var z0 = (int)MathF.Floor(fz);
        var x1 = Math.Min(x0 + 1, Width - 1);
        var y1 = Math.Min(y0 + 1, Height - 1);
        var z1 = Math.Min(z0 + 1, Depth - 1);
        var tx = fx - x0;
        var ty = fy - y0;
        var tz = fz - z0;

        var c00 = GetPixel(x0, y0, z0).Lerp(GetPixel(x1, y0, z0), tx);
        var c01 = GetPixel(x0, y1, z0).Lerp(GetPixel(x1, y1, z0), tx);
        var c10 = GetPixel(x0, y0, z1).Lerp(GetPixel(x1, y0, z1), tx);
        var c11 = GetPixel(x0, y1, z1).Lerp(GetPixel(x1, y1, z1), tx);
        var low = c00.Lerp(c01, ty);
        var high = c10.Lerp(c11, ty);
        return low.Lerp(high, tz);
    }

    public TextureFormat GetGPUFormat()
    {
        if (IsCompressed())
            return TextureFormat.Unknown;

        return Components switch
        {
            1 => TextureFormat.R8,
            3 => TextureFormat.RGB8,
            4 => TextureFormat.RGBA8,
            _ => TextureFormat.Unknown
        };
    }

    public int GetWidth() => Width;

    public int GetHeight() => Height;

    public int GetDepth() => Depth;

    public uint GetComponents() => Components;

    public Vector3i GetSize() => new(Width, Height, Depth);

    public byte[] GetData() => _data;

    public bool IsCompressed() => _compressedFormat != CompressedFormat.None;

    public bool IsCubemap() => Cubemap;

    public bool IsArray() => Array;

    public bool IsSRGB() => SRGB;

    public Image GetNextLevel()
    {
        if (IsCompressed())
            return GetDecompressedImageLevel(1) ?? GetDecompressedImage() ?? this;

        if (_nextLevel is not null)
            return _nextLevel;

        var nextWidth = Math.Max(Width / 2, 1);
        var nextHeight = Math.Max(Height / 2, 1);
        var nextDepth = Math.Max(Depth / 2, 1);
        _nextLevel = new Image(Name + "#mip");
        _nextLevel.SetSize(nextWidth, nextHeight, nextDepth, Components);
        _nextLevel.Cubemap = Cubemap;
        _nextLevel.Array = Array;
        _nextLevel.SRGB = SRGB;
        _nextLevel.SetNextSibling(_nextSibling?.GetNextLevel());

        for (var z = 0; z < nextDepth; ++z)
        {
            for (var y = 0; y < nextHeight; ++y)
            {
                for (var x = 0; x < nextWidth; ++x)
                {
                    var u = nextWidth == 1 ? 0.5f : x / (float)(nextWidth - 1);
                    var v = nextHeight == 1 ? 0.5f : y / (float)(nextHeight - 1);
                    var w = nextDepth == 1 ? 0.5f : z / (float)(nextDepth - 1);
                    _nextLevel.SetPixel(x, y, z, Depth == 1 ? GetPixelBilinear(u, v) : GetPixelTrilinear(u, v, w));
                }
            }
        }

        return _nextLevel;
    }

    public Image? GetNextSibling() => _nextSibling;

    internal void SetNextSibling(Image? sibling)
    {
        _nextSibling = sibling;
    }

    public Image ConvertToRGBA()
    {
        if (IsCompressed())
            return GetDecompressedImage() ?? this;

        if (Components == 4)
            return Clone();

        var rgba = new Image(Name + "#rgba");
        rgba.SetSize(Width, Height, Depth, 4);
        for (var z = 0; z < Depth; ++z)
        {
            for (var y = 0; y < Height; ++y)
            {
                for (var x = 0; x < Width; ++x)
                    rgba.SetPixel(x, y, z, GetPixel(x, y, z));
            }
        }

        return rgba;
    }

    public Image? GetSubimage(IntRect rect)
    {
        if (Depth != 1 || rect.Width <= 0 || rect.Height <= 0 || rect.Left < 0 || rect.Top < 0 || rect.Right > Width || rect.Bottom > Height)
            return null;

        var subimage = new Image(Name + "#sub");
        subimage.SetSize(rect.Width, rect.Height, Components);
        for (var y = 0; y < rect.Height; ++y)
        {
            for (var x = 0; x < rect.Width; ++x)
                subimage.SetPixel(x, y, GetPixel(rect.Left + x, rect.Top + y));
        }

        return subimage;
    }

    public bool SetSubimage(Image image, IntRect rect)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (IsCompressed() || image.IsCompressed() || rect.Width <= 0 || rect.Height <= 0
            || rect.Left < 0 || rect.Top < 0 || rect.Right > Width || rect.Bottom > Height)
            return false;

        for (var y = rect.Top; y < rect.Bottom; ++y)
        {
            var v = rect.Height == 1 ? 0.0f : (float)(y - rect.Top) / (rect.Height - 1);
            for (var x = rect.Left; x < rect.Right; ++x)
            {
                var u = rect.Width == 1 ? 0.0f : (float)(x - rect.Left) / (rect.Width - 1);
                SetPixel(x, y, image.GetPixelBilinear(u, v));
            }
        }
        return true;
    }

    public void PrecalculateLevels()
    {
        if (IsCompressed())
            return;
        for (var level = this; level.Width > 1 || level.Height > 1 || level.Depth > 1; level = level.GetNextLevel())
        {
        }
    }

    public void CleanupLevels() => _nextLevel = null;

    public void GetLevels(ICollection<Image> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        for (Image? level = this; level is not null; level = level._nextLevel)
            levels.Add(level);
    }

    public bool HasAlphaChannel() => Components is 2 or 4;

    public CompressedLevel GetCompressedLevel(uint index)
    {
        if (!IsCompressed() || index >= NumCompressedLevels)
            return default;

        var offset = 0;
        for (var level = 0u; level < index; ++level)
        {
            offset = checked(offset + ImageDds.GetCompressedLevelSize(
                _compressedFormat,
                GetLevelDimension(Width, level),
                GetLevelDimension(Height, level),
                GetLevelDimension(Depth, level)));
        }

        var levelWidth = GetLevelDimension(Width, index);
        var levelHeight = GetLevelDimension(Height, index);
        var levelDepth = GetLevelDimension(Depth, index);
        var levelSize = ImageDds.GetCompressedLevelSize(_compressedFormat, levelWidth, levelHeight, levelDepth);
        if (offset + levelSize > _data.Length)
            return default;

        return new CompressedLevel(new ReadOnlyMemory<byte>(_data, offset, levelSize), _compressedFormat, levelWidth, levelHeight, levelDepth);
    }

    public Image? GetDecompressedImageLevel(uint index)
    {
        if (!IsCompressed())
        {
            if (index == 0)
                return ConvertToRGBA();

            var mip = this;
            for (var level = 0u; level < index; ++level)
                mip = mip.GetNextLevel();
            return mip.ConvertToRGBA();
        }

        if (NumCompressedLevels == 0)
            return null;

        var clampedIndex = Math.Min(index, NumCompressedLevels - 1);
        var compressedLevel = GetCompressedLevel(clampedIndex);
        if (compressedLevel.DataSize == 0)
            return null;

        var decompressed = new Image($"{Name}#rgba{clampedIndex}");
        if (!decompressed.SetSize(compressedLevel.Width, compressedLevel.Height, compressedLevel.Depth, 4))
            return null;

        if (!compressedLevel.Decompress(decompressed._data))
            return null;

        decompressed.Cubemap = Cubemap;
        decompressed.Array = Array;
        decompressed.SRGB = SRGB;
        return decompressed;
    }

    public Image? GetDecompressedImage() => GetDecompressedImageLevel(0u);

    public bool LoadColorLUT(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ImageSdl.TryLoadColorLut(this, source);
    }

    private Image Clone()
    {
        var clone = new Image(Name + "#clone");
        clone.SetSize(Width, Height, Depth, Components);
        clone.SetData((byte[])_data.Clone());
        clone.Cubemap = Cubemap;
        clone.Array = Array;
        clone.SRGB = SRGB;
        return clone;
    }

    internal void SetCompressedData(
        int width,
        int height,
        int depth,
        uint components,
        CompressedFormat format,
        uint levels,
        byte[] pixelData,
        bool cubemap,
        bool array,
        bool sRgb)
    {
        Width = width;
        Height = height;
        Depth = depth;
        Components = components;
        _compressedFormat = format;
        _numCompressedLevels = Math.Max(levels, 1u);
        _data = pixelData;
        Cubemap = cubemap;
        Array = array;
        SRGB = sRgb;
        MemoryUse = (uint)pixelData.Length;
        _nextLevel = null;
    }

    private int GetOffset(int x, int y, int z)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
            throw new ArgumentOutOfRangeException();

        return ((((z * Height) + y) * Width) + x) * (int)Components;
    }

    private Color ReadColor(int offset)
    {
        return Components switch
        {
            1 => new Color(_data[offset] / 255.0f, _data[offset] / 255.0f, _data[offset] / 255.0f, 1.0f),
            2 => new Color(_data[offset] / 255.0f, _data[offset] / 255.0f, _data[offset] / 255.0f, _data[offset + 1] / 255.0f),
            3 => new Color(_data[offset] / 255.0f, _data[offset + 1] / 255.0f, _data[offset + 2] / 255.0f, 1.0f),
            4 => new Color(_data[offset] / 255.0f, _data[offset + 1] / 255.0f, _data[offset + 2] / 255.0f, _data[offset + 3] / 255.0f),
            _ => Color.Transparent
        };
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

    private static int GetLevelDimension(int size, uint level)
    {
        var value = size;
        for (var i = 0u; i < level && value > 1; ++i)
            value = Math.Max(value / 2, 1);
        return value;
    }
}
