namespace RedBolt;

public enum CubeMapFace
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ
}

public sealed class TextureCube : Texture
{
    private readonly Dictionary<(CubeMapFace Face, uint Level), byte[]> _faceData = new();

    public TextureCube(string name)
        : base(name)
    {
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var imageCube = new ImageCube(Name)
        {
            OwnerCache = OwnerCache
        };
        imageCube.SetAbsoluteFileName(AbsoluteFileName);
        if (!imageCube.Load(source))
            return false;

        foreach (CubeMapFace face in Enum.GetValues<CubeMapFace>())
        {
            var image = imageCube.GetImage(face);
            if (image is null || !SetData(face, image))
                return false;
        }

        return true;
    }

    public bool SetSize(int size, TextureFormat format, int multiSample = 1, bool noMultiSampledAutoResolve = false)
    {
        if (size <= 0)
            return false;

        SetDimensions(size, size, 1, format, multiSample, noMultiSampledAutoResolve);
        _faceData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(CubeMapFace face, uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (level >= Levels || x < 0 || y < 0 || width <= 0 || height <= 0)
            return false;

        var levelSize = GetLevelWidth(level);
        if (x + width > levelSize || y + height > levelSize)
            return false;

        var bytesPerPixel = (int)GetComponents();
        if (bytesPerPixel == 0)
            return false;

        var expected = checked(width * height * bytesPerPixel);
        if (data.Length != expected)
            return false;

        var key = (face, level);
        if (!_faceData.TryGetValue(key, out var levelBytes))
        {
            levelBytes = new byte[checked(levelSize * levelSize * bytesPerPixel)];
            _faceData[key] = levelBytes;
        }

        for (var row = 0; row < height; ++row)
        {
            var sourceOffset = row * width * bytesPerPixel;
            var destinationOffset = ((y + row) * levelSize + x) * bytesPerPixel;
            data.Slice(sourceOffset, width * bytesPerPixel).CopyTo(levelBytes.AsSpan(destinationOffset, width * bytesPerPixel));
        }

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(CubeMapFace face, uint level, int x, int y, int width, int height, byte[] data) => SetData(face, level, x, y, width, height, data.AsSpan());

    public bool SetData(CubeMapFace face, Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var image = new Image($"{Name}#{face}")
        {
            OwnerCache = OwnerCache
        };
        return image.Load(source) && SetData(face, image);
    }

    public bool SetData(CubeMapFace face, Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var levels = image.IsCompressed() ? Math.Max(image.NumCompressedLevels, 1u) : 1u;
        var baseLevel = image.GetDecompressedImageLevel(0u);
        if (baseLevel is null)
            return false;

        if (Width == 0)
        {
            Levels = levels;
            if (!SetSize(baseLevel.Width, TextureFormat.RGBA8))
                return false;
        }

        for (var level = 0u; level < levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(level);
            if (mipImage is null)
                return false;

            if (!SetData(face, level, 0, 0, mipImage.Width, mipImage.Height, mipImage.GetData()))
                return false;
        }

        return true;
    }

    public byte[]? GetData(CubeMapFace face, uint level)
    {
        return _faceData.TryGetValue((face, level), out var bytes) ? bytes.ToArray() : null;
    }

    private void RefreshTotalMemoryUse()
    {
        RefreshMemoryUse((uint)_faceData.Sum(static item => item.Value.Length));
    }
}
