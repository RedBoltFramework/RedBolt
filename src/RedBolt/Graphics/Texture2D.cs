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

    // C++: bool SetSize(int width, int height, TextureFormat format, TextureFlags flags = TextureFlag::None, int multiSample = 1)
    // C++ uses Create(params) with RawTextureParams; also handles requestedSRGB_ format.
    public bool SetSize(int width, int height, TextureFormat format, int multiSample = 1, bool noMultiSampledAutoResolve = false)
        => SetSize(width, height, format, TextureFlag.None, multiSample, noMultiSampledAutoResolve);

    // C++: bool SetSize(int width, int height, TextureFormat format, TextureFlags flags = TextureFlag::None, int multiSample = 1)
    // C++ uses Create(params) with sRGB check; has no noMultiSampledAutoResolve param.
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

    // C++: bool SetData(unsigned level, int x, int y, int width, int height, const void* data)
    // C++ simply calls Update(level, {x,y,0}, {width,height,1}, 0, data). _levelData management is C#-added.
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

        GetRawTexture()?.Update(level, new Vector3i(x, y, 0), new Vector3i(width, height, 1), 0u, data);

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint level, int x, int y, int width, int height, byte[] data) => SetData(level, x, y, width, height, data.AsSpan());

    // C++: bool SetData(Image* image)
    // C++ uses CreateForImage(params, image) + UpdateFromImage(). C# manually computes mips.
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

    // C++: bool GetData(unsigned level, void* dest)
    // C++ calls Read(0, level, dest, M_MAX_UNSIGNED). C# reads from _levelData cache.
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

    // C++: bool GetImage(Image& image)
    // C++ calls ReadToImage(0, 0, &image). C# falls back to _levelData when ReadToImage returns empty.
    // C++: SharedPtr<Image> GetImage() -- returns pointer, no OwnerCache set.
    public bool GetImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (Format is not (TextureFormat.RGB8 or TextureFormat.RGBA8))
            return false;

        if (!_levelData.TryGetValue(0u, out var levelBytes))
            return ReadToImage(0u, 0u, image);

        if (!image.SetSize(Width, Height, 4u))
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
