namespace RedBolt;

public sealed class Texture2D : Texture
{
    private readonly Dictionary<uint, byte[]> _levelData = new();
    private Image? _loadImage;
    private XMLFile? _loadParameters;

    public Texture2D(string name)
        : base(name)
    {
    }

    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<Texture2D>();
        reflection.SetObjectFactory(static () => new Texture2D(string.Empty));
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var graphics = GetSubsystem<Graphics>();
        // In headless mode, do not actually load the texture, just return success.
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        _loadImage = new Image($"{Name}#source");
        if (!_loadImage.BeginLoad(source))
        {
            _loadImage = null;
            return false;
        }

        if (AsyncLoadState == AsyncLoadState.Loading)
            _loadImage.PrecalculateLevels();

        var parametersName = Path.ChangeExtension(Name, ".xml");
        _loadParameters = ResourceLoading.LoadDependentResource<XMLFile>(this, parametersName);
        return true;
    }

    public override bool EndLoad()
    {
        // In headless mode, do not actually load the texture, just return success.
        if (GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null)
            return true;

        if (_loadImage is null)
            return false;

        CheckTextureBudget(typeof(Texture2D).GetTypeId());
        SetParameters(_loadParameters);
        var success = SetData(_loadImage);

        _loadImage = null;
        _loadParameters = null;
        return success;
    }

    public bool SetSize(int width, int height, TextureFormat format, int multiSample = 1, bool noMultiSampledAutoResolve = false)
        => SetSize(width, height, format, TextureFlag.None, multiSample, noMultiSampledAutoResolve);

    public bool SetSize(int width, int height, TextureFormat format, TextureFlag flags, int multiSample = 1,
        bool noMultiSampledAutoResolve = false)
    {
        if (width <= 0 || height <= 0)
            return false;

        SetDimensions(width, height, 1, format, multiSample, noMultiSampledAutoResolve, flags);
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

    public bool SetData(Image? image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var maxLevels = image.IsCompressed()
            ? Math.Max(image.NumCompressedLevels, 1u)
            : CalculateFullMipChainLength(Math.Max(image.Width, Math.Max(image.Height, image.Depth)));
        var mostDetailedLevel = Math.Min((uint)GetMipsToSkip(MaterialQuality.High), maxLevels - 1u);
        var numLevels = _requestedLevels == 0
            ? maxLevels - mostDetailedLevel
            : Math.Min(_requestedLevels, maxLevels - mostDetailedLevel);

        _mostDetailedLevel = mostDetailedLevel;
        SetNumLevels(numLevels);
        Levels = numLevels;

        var baseLevel = image.GetDecompressedImageLevel(mostDetailedLevel);
        if (baseLevel is null || !SetSize(baseLevel.Width, baseLevel.Height, TextureFormat.RGBA8))
            return false;

        for (var level = 0u; level < Levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(mostDetailedLevel + level);
            if (mipImage is null)
                return false;

            if (!SetData(level, 0, 0, mipImage.Width, mipImage.Height, mipImage.GetData()))
                return false;
        }

        return true;
    }

    public byte[]? GetData(uint level)
    {
        return GetDataNoCopy(level)?.ToArray();
    }

    public bool GetData(uint level, Span<byte> destination)
    {
        if (!_levelData.TryGetValue(level, out var levelBytes) || destination.Length < levelBytes.Length)
            return false;

        levelBytes.AsSpan().CopyTo(destination);
        return true;
    }

    public bool GetData(uint level, byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return GetData(level, destination.AsSpan());
    }

    public bool GetImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (Format is not (TextureFormat.RGB8 or TextureFormat.RGBA8))
            return false;

        if (!_levelData.TryGetValue(0u, out var levelBytes) || !image.SetSize(Width, Height, 4u))
            return false;

        image.SetData(levelBytes);
        return true;
    }

    public Image? GetImage()
    {
        var image = new Image($"{Name}#image");
        return GetImage(image) ? image : null;
    }

    internal byte[]? GetDataNoCopy(uint level) => _levelData.TryGetValue(level, out var bytes) ? bytes : null;

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
