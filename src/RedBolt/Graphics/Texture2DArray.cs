namespace RedBolt;

public sealed class Texture2DArray : Texture
{
    private readonly Dictionary<(uint Slice, uint Level), byte[]> _sliceData = new();

    public Texture2DArray(string name)
        : base(name)
    {
    }

    public uint Layers => (uint)Depth;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var parameters = new XMLFile(Name + "#params");
        if (!parameters.Load(source))
            return false;

        var images = new List<Image>();
        for (var layerElement = parameters.Root.GetChild("layer"); layerElement.NotNull; layerElement = layerElement.GetNext("layer"))
        {
            var imageName = layerElement.GetAttribute("name");
            var image = ResourceLoading.LoadDependentResource<Image>(this, imageName);
            if (image is null)
                return false;

            images.Add(image);
        }

        if (images.Count == 0)
            return false;

        var baseLevel = images[0].GetDecompressedImageLevel(0u);
        if (baseLevel is null)
            return false;

        Levels = images[0].IsCompressed() ? Math.Max(images[0].NumCompressedLevels, 1u) : 1u;
        if (!SetLayers((uint)images.Count, baseLevel.Width, baseLevel.Height, TextureFormat.RGBA8))
            return false;

        for (var i = 0; i < images.Count; ++i)
        {
            if (!SetData((uint)i, images[i]))
                return false;
        }

        return true;
    }

    public bool SetLayers(uint layers, int width, int height, TextureFormat format, int multiSample = 1, bool noMultiSampledAutoResolve = false)
    {
        if (layers == 0 || width <= 0 || height <= 0)
            return false;

        SetDimensions(width, height, (int)layers, format, multiSample, noMultiSampledAutoResolve);
        _sliceData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint slice, uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (slice >= Layers || level >= Levels || x < 0 || y < 0 || width <= 0 || height <= 0)
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

        var key = (slice, level);
        if (!_sliceData.TryGetValue(key, out var levelBytes))
        {
            levelBytes = new byte[checked(levelWidth * levelHeight * bytesPerPixel)];
            _sliceData[key] = levelBytes;
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

    public bool SetData(uint slice, uint level, int x, int y, int width, int height, byte[] data) => SetData(slice, level, x, y, width, height, data.AsSpan());

    public bool SetData(uint slice, Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var image = new Image($"{Name}#slice{slice}")
        {
            OwnerCache = OwnerCache
        };
        return image.Load(source) && SetData(slice, image);
    }

    public bool SetData(uint slice, Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var levels = image.IsCompressed() ? Math.Max(image.NumCompressedLevels, 1u) : 1u;
        if (levels != Levels)
            return false;

        for (var level = 0u; level < levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(level);
            if (mipImage is null)
                return false;

            if (!SetData(slice, level, 0, 0, mipImage.Width, mipImage.Height, mipImage.GetData()))
                return false;
        }

        return true;
    }

    public byte[]? GetData(uint slice, uint level)
    {
        return _sliceData.TryGetValue((slice, level), out var bytes) ? bytes.ToArray() : null;
    }

    private void RefreshTotalMemoryUse()
    {
        var total = 0;
        foreach (var item in _sliceData)
            total += item.Value.Length;
        RefreshMemoryUse((uint)total);
    }
}
