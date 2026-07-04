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
    private byte[] data = System.Array.Empty<byte>();
    private Image? nextLevel;
    private Image? nextSibling;

    public Image(string name)
        : base(name)
    {
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int Depth { get; private set; } = 1;

    public uint Components { get; private set; }

    public uint NumCompressedLevels => 0u;

    public bool Cubemap { get; internal set; }

    public bool Array { get; internal set; }

    public bool SRGB { get; internal set; }

    public CompressedFormat CompressedFormat => CompressedFormat.None;

    public override bool BeginLoad(Stream source) => false;

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(data);
        return true;
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
        data = new byte[checked(width * height * depth * (int)components)];
        MemoryUse = (uint)data.Length;
        nextLevel = null;
        return true;
    }

    public void SetData(byte[] pixelData)
    {
        ArgumentNullException.ThrowIfNull(pixelData);
        if (pixelData.Length != data.Length)
            throw new ArgumentException("Pixel data size does not match image size.", nameof(pixelData));

        pixelData.CopyTo(data, 0);
        nextLevel = null;
    }

    public void SetPixel(int x, int y, Color color) => SetPixel(x, y, 0, color);

    public void SetPixel(int x, int y, int z, Color color)
    {
        var offset = GetOffset(x, y, z);
        WriteColor(offset, color);
        nextLevel = null;
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

        nextLevel = null;
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

        nextLevel = null;
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
        data = resized.data;
        MemoryUse = resized.MemoryUse;
        nextLevel = null;
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

    public IntVector3 GetSize() => new(Width, Height, Depth);

    public byte[] GetData() => data;

    public bool IsCompressed() => false;

    public bool IsCubemap() => Cubemap;

    public bool IsArray() => Array;

    public bool IsSRGB() => SRGB;

    public Image GetNextLevel()
    {
        if (nextLevel is not null)
            return nextLevel;

        var nextWidth = Math.Max(Width / 2, 1);
        var nextHeight = Math.Max(Height / 2, 1);
        var nextDepth = Math.Max(Depth / 2, 1);
        nextLevel = new Image(Name + "#mip");
        nextLevel.SetSize(nextWidth, nextHeight, nextDepth, Components);
        nextLevel.Cubemap = Cubemap;
        nextLevel.Array = Array;
        nextLevel.SRGB = SRGB;
        nextLevel.SetNextSibling(nextSibling?.GetNextLevel());

        for (var z = 0; z < nextDepth; ++z)
        {
            for (var y = 0; y < nextHeight; ++y)
            {
                for (var x = 0; x < nextWidth; ++x)
                {
                    var u = nextWidth == 1 ? 0.5f : x / (float)(nextWidth - 1);
                    var v = nextHeight == 1 ? 0.5f : y / (float)(nextHeight - 1);
                    var w = nextDepth == 1 ? 0.5f : z / (float)(nextDepth - 1);
                    nextLevel.SetPixel(x, y, z, Depth == 1 ? GetPixelBilinear(u, v) : GetPixelTrilinear(u, v, w));
                }
            }
        }

        return nextLevel;
    }

    public Image? GetNextSibling() => nextSibling;

    internal void SetNextSibling(Image? sibling)
    {
        nextSibling = sibling;
    }

    public Image ConvertToRGBA()
    {
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

    public bool HasAlphaChannel() => Components is 2 or 4;

    private Image Clone()
    {
        var clone = new Image(Name + "#clone");
        clone.SetSize(Width, Height, Depth, Components);
        clone.SetData((byte[])data.Clone());
        clone.Cubemap = Cubemap;
        clone.Array = Array;
        clone.SRGB = SRGB;
        return clone;
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
            1 => new Color(data[offset] / 255.0f, data[offset] / 255.0f, data[offset] / 255.0f, 1.0f),
            2 => new Color(data[offset] / 255.0f, data[offset] / 255.0f, data[offset] / 255.0f, data[offset + 1] / 255.0f),
            3 => new Color(data[offset] / 255.0f, data[offset + 1] / 255.0f, data[offset + 2] / 255.0f, 1.0f),
            4 => new Color(data[offset] / 255.0f, data[offset + 1] / 255.0f, data[offset + 2] / 255.0f, data[offset + 3] / 255.0f),
            _ => Color.Transparent
        };
    }

    private void SwapPixels(int leftOffset, int rightOffset)
    {
        for (var i = 0; i < Components; ++i)
            (data[leftOffset + i], data[rightOffset + i]) = (data[rightOffset + i], data[leftOffset + i]);
    }

    private void SwapRows(int topOffset, int bottomOffset, int rowStride)
    {
        for (var i = 0; i < rowStride; ++i)
            (data[topOffset + i], data[bottomOffset + i]) = (data[bottomOffset + i], data[topOffset + i]);
    }

    private void WriteColor(int offset, Color color)
    {
        color.Clip(clipAlpha: true);
        var packed = color.ToUInt();
        data[offset] = (byte)(packed & 0xff);
        if (Components > 1)
            data[offset + 1] = (byte)((packed >> 8) & 0xff);
        if (Components > 2)
            data[offset + 2] = (byte)((packed >> 16) & 0xff);
        if (Components > 3)
            data[offset + 3] = (byte)((packed >> 24) & 0xff);
    }
}
