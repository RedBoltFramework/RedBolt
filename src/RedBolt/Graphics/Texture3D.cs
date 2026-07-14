namespace RedBolt;

/// <summary>
/// 3D texture resource.
/// </summary>
public sealed class Texture3D : Texture
{
    private readonly Dictionary<uint, byte[]> _levelData = new();
    private Image? _loadImage;
    private XMLFile? _loadParameters;

    public Texture3D(string name)
        : base(name)
    {
    }

    protected override TextureType GetRawTextureType() => TextureType.Texture3D;

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<Texture3D>();
        reflection.SetObjectFactory(static () => new Texture3D(string.Empty));
    }

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var graphics = GetSubsystem<Graphics>();
        // In headless mode, do not actually load the texture, just return success.
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        _loadImage = null;
        _loadParameters = null;

        var parameters = new XMLFile(Name + "#params")
        {
            OwnerCache = OwnerCache
        };
        if (!parameters.Load(source))
            return false;

        _loadParameters = parameters;
        var root = parameters.Root;
        var volumeElement = root.GetChild("volume");
        if (volumeElement.NotNull)
        {
            _loadImage = ResourceLoading.LoadDependentResource<Image>(this, volumeElement.GetAttribute("name"));
            if (_loadImage is not null && AsyncLoadState == AsyncLoadState.Loading)
                _loadImage.PrecalculateLevels();
            return true;
        }

        var colorLutElement = root.GetChild("colorlut");
        if (!colorLutElement.NotNull)
            return false;

        using var colorLutStream = ResourceLoading.OpenDependentResource(this, colorLutElement.GetAttribute("name"));
        if (colorLutStream is null)
            return false;

        _loadImage = new Image(Name + "#colorlut")
        {
            OwnerCache = OwnerCache
        };
        if (!_loadImage.LoadColorLUT(colorLutStream))
        {
            _loadParameters = null;
            _loadImage = null;
            return false;
        }

        if (AsyncLoadState == AsyncLoadState.Loading)
            _loadImage.PrecalculateLevels();
        return true;
    }

    /// <summary>
    /// Finish resource loading. Always called from the main thread. Return true if successful.
    /// </summary>
    public override bool EndLoad()
    {
        // In headless mode, do not actually load the texture, just return success.
        if (GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null)
            return true;

        if (_loadParameters is null)
            return false;

        SetParameters(_loadParameters);
        var success = SetData(_loadImage);

        _loadImage = null;
        _loadParameters = null;

        return success;
    }

    /// <summary>
    /// Set size, format and usage. Zero size will follow application window size. Return true if successful.
    /// </summary>
    public bool SetSize(int width, int height, int depth, TextureFormat format, TextureFlag flags = TextureFlag.None)
    {
        if (width <= 0 || height <= 0 || depth <= 0)
            return false;

        SetDimensions(width, height, depth, format, 1, false, flags);
        _levelData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    /// <summary>
    /// Set data either partially or fully on a mip level. Return true if successful.
    /// </summary>
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

    /// <summary>
    /// Set data from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(Image? image)
    {
        if (image is null)
            return false;

        var maxLevels = image.IsCompressed()
            ? Math.Max(image.NumCompressedLevels, 1u)
            : CalculateFullMipChainLength(Math.Max(image.Width, Math.Max(image.Height, image.Depth)));
        var mostDetailedLevel = Math.Min((uint)GetMipsToSkip(MaterialQuality.High), maxLevels - 1u);
        var levels = _requestedLevels == 0
            ? maxLevels - mostDetailedLevel
            : Math.Min(_requestedLevels, maxLevels - mostDetailedLevel);

        _mostDetailedLevel = mostDetailedLevel;
        Levels = levels;

        var baseLevel = image.GetDecompressedImageLevel(mostDetailedLevel);
        if (baseLevel is null)
            return false;

        if (!SetSize(baseLevel.Width, baseLevel.Height, baseLevel.Depth, TextureFormat.RGBA8))
            return false;

        for (var level = 0u; level < levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(mostDetailedLevel + level);
            if (mipImage is null)
                return false;

            if (!SetData(level, 0, 0, 0, mipImage.Width, mipImage.Height, mipImage.Depth, mipImage.GetData()))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get data from a mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public byte[]? GetData(uint level)
    {
        return _levelData.TryGetValue(level, out var bytes) ? bytes.ToArray() : null;
    }

    public bool GetData(uint level, Span<byte> destination)
    {
        if (!_levelData.TryGetValue(level, out var bytes) || destination.Length < bytes.Length)
            return false;

        bytes.AsSpan().CopyTo(destination);
        return true;
    }

    public bool GetData(uint level, byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return GetData(level, destination.AsSpan());
    }

    private void RefreshTotalMemoryUse()
    {
        var total = 0;
        foreach (var item in _levelData)
            total += item.Value.Length;
        RefreshMemoryUse((uint)total);
    }

    private static uint CalculateFullMipChainLength(int largestDimension)
    {
        if (largestDimension <= 0)
            return 1u;

        var levels = 1u;
        while (largestDimension > 1)
        {
            largestDimension = Math.Max(largestDimension / 2, 1);
            ++levels;
        }

        return levels;
    }
}
