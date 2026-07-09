namespace RedBolt;

public sealed class Texture2D : Texture
{
    private readonly Dictionary<uint, byte[]> _levelData = new();

    public Texture2D(string name)
        : base(name)
    {
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var image = new Image($"{Name}#source");
        if (!image.BeginLoad(source))
            return false;

        var levels = image.IsCompressed() ? Math.Max(image.NumCompressedLevels, 1u) : 1u;
        var baseLevel = image.GetDecompressedImageLevel(0u);
        if (baseLevel is null)
            return false;

        Levels = levels;
        if (!SetSize(baseLevel.Width, baseLevel.Height, TextureFormat.RGBA8))
            return false;

        for (var level = 0u; level < levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(level);
            if (mipImage is null)
                return false;

            if (!SetData(level, 0, 0, mipImage.Width, mipImage.Height, mipImage.GetData()))
                return false;
        }

        return true;
    }

    public bool SetSize(int width, int height, TextureFormat format, int multiSample = 1, bool noMultiSampledAutoResolve = false)
    {
        if (width <= 0 || height <= 0)
            return false;

        SetDimensions(width, height, 1, format, multiSample, noMultiSampledAutoResolve);
        _levelData.Clear();
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

        if (!_levelData.TryGetValue(level, out var levelBytes))
        {
            levelBytes = new byte[checked(levelWidth * levelHeight * bytesPerPixel)];
            _levelData[level] = levelBytes;
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
        return GetDataNoCopy(level)?.ToArray();
    }

    internal byte[]? GetDataNoCopy(uint level) => _levelData.TryGetValue(level, out var bytes) ? bytes : null;

    private void RefreshTotalMemoryUse()
    {
        RefreshMemoryUse((uint)_levelData.Sum(static item => item.Value.Length));
    }
}
