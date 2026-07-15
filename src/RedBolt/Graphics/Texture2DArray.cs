namespace RedBolt;

/// <summary>
/// 2D texture array resource.
/// </summary>
public sealed class Texture2DArray : Texture
{
    private readonly Dictionary<(uint Slice, uint Level), byte[]> _sliceData = new();
    private uint _layers;
    private readonly List<Image?> _loadImages = new();
    private XMLFile? _loadParameters;

    public Texture2DArray(string name)
        : base(name)
    {
    }

    protected override TextureType GetRawTextureType() => TextureType.Texture2DArray;

    protected override uint GetRawTextureArraySize() => _layers;

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<Texture2DArray>();
        reflection.SetObjectFactory(static () => new Texture2DArray(string.Empty));
    }

    /// <summary>
    /// Return number of layers in the texture.
    /// </summary>
    public uint Layers => _layers;

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

        _loadImages.Clear();
        _loadParameters = new XMLFile(Name + "#params")
        {
            OwnerCache = OwnerCache
        };
        if (!_loadParameters.Load(source))
        {
            _loadParameters = null;
            return false;
        }

        for (var layerElement = _loadParameters.Root.GetChild("layer"); layerElement.NotNull; layerElement = layerElement.GetNext("layer"))
        {
            var imageName = layerElement.GetAttribute("name");
            var image = ResourceLoading.LoadDependentResource<Image>(this, imageName);
            _loadImages.Add(image);
            if (image is not null && AsyncLoadState == AsyncLoadState.Loading)
                image.PrecalculateLevels();
        }

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
        SetLayers((uint)_loadImages.Count);
        for (var i = 0; i < _loadImages.Count; ++i)
            SetData((uint)i, _loadImages[i]);

        _loadImages.Clear();
        _loadParameters = null;
        return true;
    }

    /// <summary>
    /// Set the number of layers in the texture. To be used before SetData.
    /// </summary>
    public void SetLayers(uint layers)
    {
        _layers = layers;
    }

    /// <summary>
    /// Set layers, size, format and usage. Set layers to zero to leave them unchanged. Return true if successful.
    /// </summary>
    // C++ has no multiSample/noMultiSampledAutoResolve params; uses Create(params).
    public bool SetSize(uint layers, int width, int height, TextureFormat format, TextureFlag flags = TextureFlag.None,
        int multiSample = 1, bool noMultiSampledAutoResolve = false)
    {
        _layers = layers == 0 ? _layers : layers;
        if (_layers == 0 || width <= 0 || height <= 0)
            return false;

        SetDimensions(width, height, (int)_layers, format, multiSample, noMultiSampledAutoResolve, flags);
        Levels = _requestedLevels == 0
            ? CalculateFullMipChainLength(Math.Max(width, height))
            : Math.Max(_requestedLevels, 1u);
        _sliceData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetLayers(uint layers, int width, int height, TextureFormat format, TextureFlag flags = TextureFlag.None,
        int multiSample = 1, bool noMultiSampledAutoResolve = false)
    {
        return SetSize(layers, width, height, format, flags, multiSample, noMultiSampledAutoResolve);
    }

    /// <summary>
    /// Set data either partially or fully on a layer's mip level. Return true if successful.
    /// </summary>
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

        GetRawTexture()?.Update(level, new Vector3i(x, y, 0), new Vector3i(width, height, 1), slice, data);

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(uint slice, uint level, int x, int y, int width, int height, byte[] data) => SetData(slice, level, x, y, width, height, data.AsSpan());

    /// <summary>
    /// Set data of one layer from a stream. Return true if successful.
    /// </summary>
    public bool SetData(uint slice, Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var image = new Image($"{Name}#slice{slice}")
        {
            OwnerCache = OwnerCache
        };
        return image.Load(source) && SetData(slice, image);
    }

    /// <summary>
    /// Set data of one layer from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(uint slice, Image? image)
    {
        if (slice >= Layers || image is null)
            return false;

        if (slice == 0)
        {
            var maxLevels = image.IsCompressed()
                ? Math.Max(image.NumCompressedLevels, 1u)
                : CalculateFullMipChainLength(Math.Max(image.Width, image.Height));
            _mostDetailedLevel = Math.Min((uint)GetMipsToSkip(MaterialQuality.High), maxLevels - 1u);
            var levels = _requestedLevels == 0
                ? maxLevels - _mostDetailedLevel
                : Math.Min(_requestedLevels, maxLevels - _mostDetailedLevel);
            var baseLevel = image.GetDecompressedImageLevel(_mostDetailedLevel);
            if (baseLevel is null || !SetSize(0, baseLevel.Width, baseLevel.Height, TextureFormat.RGBA8))
                return false;
            Levels = levels;
        }
        else if (Width == 0)
            return false;

        for (var level = 0u; level < Levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(_mostDetailedLevel + level);
            if (mipImage is null)
                return false;

            if (!SetData(slice, level, 0, 0, mipImage.Width, mipImage.Height, mipImage.GetData()))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get data from a mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public byte[]? GetData(uint slice, uint level)
    {
        return _sliceData.TryGetValue((slice, level), out var bytes) ? bytes.ToArray() : null;
    }

    public bool GetData(uint slice, uint level, Span<byte> destination)
    {
        if (!_sliceData.TryGetValue((slice, level), out var bytes) || destination.Length < bytes.Length)
            return false;

        bytes.AsSpan().CopyTo(destination);
        return true;
    }

    public bool GetData(uint slice, uint level, byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return GetData(slice, level, destination.AsSpan());
    }

    private void RefreshTotalMemoryUse()
    {
        var total = 0;
        foreach (var item in _sliceData)
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
