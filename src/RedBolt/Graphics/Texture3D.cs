namespace RedBolt;

public sealed class Texture3D : Texture
{
    private readonly Dictionary<uint, byte[]> _levelData = new();

    public Texture3D(string name)
        : base(name)
    {
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var parameters = new XMLFile(Name + "#params");
        if (!parameters.Load(source))
            return false;

        var root = parameters.Root;
        var volumeElement = root.GetChild("volume");
        if (volumeElement.NotNull)
        {
            var image = ResourceLoading.LoadDependentResource<Image>(this, volumeElement.GetAttribute("name"));
            return image is not null && SetData(image);
        }

        var colorLutElement = root.GetChild("colorlut");
        if (!colorLutElement.NotNull)
            return false;

        using var colorLutStream = ResourceLoading.OpenDependentResource(this, colorLutElement.GetAttribute("name"));
        if (colorLutStream is null)
            return false;

        var colorLutImage = new Image(Name + "#colorlut")
        {
            OwnerCache = OwnerCache
        };
        return colorLutImage.LoadColorLUT(colorLutStream) && SetData(colorLutImage);
    }

    public bool SetSize(int width, int height, int depth, TextureFormat format)
    {
        if (width <= 0 || height <= 0 || depth <= 0)
            return false;

        SetDimensions(width, height, depth, format, 1, false);
        _levelData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint level, int x, int y, int z, int width, int height, int depth, ReadOnlySpan<byte> data)
    {
        if (level >= Levels || x < 0 || y < 0 || z < 0 || width <= 0 || height <= 0 || depth <= 0)
            return false;

        var levelWidth = GetLevelWidth(level);
        var levelHeight = GetLevelHeight(level);
        var levelDepth = GetLevelDepth(level);
        if (x + width > levelWidth || y + height > levelHeight || z + depth > levelDepth)
            return false;

        var bytesPerPixel = (int)GetComponents();
        if (bytesPerPixel == 0)
            return false;

        var expected = checked(width * height * depth * bytesPerPixel);
        if (data.Length != expected)
            return false;

        if (!_levelData.TryGetValue(level, out var levelBytes))
        {
            levelBytes = new byte[checked(levelWidth * levelHeight * levelDepth * bytesPerPixel)];
            _levelData[level] = levelBytes;
        }

        for (var slice = 0; slice < depth; ++slice)
        {
            for (var row = 0; row < height; ++row)
            {
                var sourceOffset = ((slice * height) + row) * width * bytesPerPixel;
                var destinationOffset = ((((z + slice) * levelHeight) + (y + row)) * levelWidth + x) * bytesPerPixel;
                data.Slice(sourceOffset, width * bytesPerPixel).CopyTo(levelBytes.AsSpan(destinationOffset, width * bytesPerPixel));
            }
        }

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint level, int x, int y, int z, int width, int height, int depth, byte[] data) => SetData(level, x, y, z, width, height, depth, data.AsSpan());

    public bool SetData(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var levels = image.IsCompressed() ? Math.Max(image.NumCompressedLevels, 1u) : 1u;
        var baseLevel = image.GetDecompressedImageLevel(0u);
        if (baseLevel is null)
            return false;

        Levels = levels;
        if (!SetSize(baseLevel.Width, baseLevel.Height, baseLevel.Depth, TextureFormat.RGBA8))
            return false;

        for (var level = 0u; level < levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(level);
            if (mipImage is null)
                return false;

            if (!SetData(level, 0, 0, 0, mipImage.Width, mipImage.Height, mipImage.Depth, mipImage.GetData()))
                return false;
        }

        return true;
    }

    public byte[]? GetData(uint level)
    {
        return _levelData.TryGetValue(level, out var bytes) ? bytes.ToArray() : null;
    }

    private void RefreshTotalMemoryUse()
    {
        RefreshMemoryUse((uint)_levelData.Sum(static item => item.Value.Length));
    }
}
