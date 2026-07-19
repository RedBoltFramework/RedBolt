namespace RedBolt;

/// <summary>
/// 3D texture resource.
/// </summary>
public class Texture3D : Texture
{
    // Image file acquired during BeginLoad.
    private Image? _loadImage;
    // Parameter file acquired during BeginLoad.
    private XMLFile? _loadParameters;

    /// <summary>
    /// Construct.
    /// </summary>
    public Texture3D(string name)
        : base(name)
    {
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        _loadParameters?.Dispose();
        _loadParameters = null;
        _loadImage?.Dispose();
        _loadImage = null;
        base.Dispose();
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
        var graphics = GetSubsystem<Graphics>();
        var cache = GetSubsystem<ResourceCache>()!;

        // In headless mode, do not actually load the texture, just return success.
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        cache.ResetDependencies(this);

        _loadParameters?.Dispose();
        _loadParameters = new XMLFile($"{Name}#params")
        {
            OwnerCache = OwnerCache
        };
        if (!_loadParameters.Load(source))
        {
            _loadParameters.Dispose();
            _loadParameters = null;
            return false;
        }

        var textureElement = _loadParameters.Root;
        var volumeElement = textureElement.GetChild("volume");
        var colorLutElement = textureElement.GetChild("colorlut");

        if (volumeElement.NotNull)
        {
            var name = ResourceLoading.ResolveRelativeResourceName(this, volumeElement.GetAttribute("name"));
            var image = cache.GetTempResource<Image>(name);
            _loadImage?.Dispose();
            _loadImage = image;
            // Precalculate mip levels if async loading.
            if (_loadImage is not null && AsyncLoadState == AsyncLoadState.Loading)
                _loadImage.PrecalculateLevels();
            cache.StoreResourceDependency(this, name);
            return true;
        }
        else if (colorLutElement.NotNull)
        {
            var name = ResourceLoading.ResolveRelativeResourceName(this, colorLutElement.GetAttribute("name"));
            using var file = ResourceLoading.OpenDependentResource(this, name);
            _loadImage?.Dispose();
            _loadImage = new Image($"{Name}#colorlut")
            {
                OwnerCache = OwnerCache
            };
            if (!_loadImage.LoadColorLUT(file!))
            {
                _loadParameters.Dispose();
                _loadParameters = null;
                _loadImage.Dispose();
                _loadImage = null;
                return false;
            }
            // Precalculate mip levels if async loading.
            if (AsyncLoadState == AsyncLoadState.Loading)
                _loadImage.PrecalculateLevels();
            cache.StoreResourceDependency(this, name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finish resource loading. Always called from the main thread. Return true if successful.
    /// </summary>
    public override bool EndLoad()
    {
        // In headless mode, do not actually load the texture, just return success.
        if (GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null)
            return true;

        // If over the texture budget, see if materials can be freed to allow textures to be freed.
        CheckTextureBudget(typeof(Texture3D).GetTypeId());

        SetParameters(_loadParameters);
        var success = SetData(_loadImage);

        _loadImage?.Dispose();
        _loadImage = null;
        _loadParameters?.Dispose();
        _loadParameters = null;

        return success;
    }

    /// <summary>
    /// Set size, format and usage. Zero size will follow application window size. Return true if successful.
    /// </summary>
    public bool SetSize(int width, int height, int depth, TextureFormat format,
        TextureFlag flags = TextureFlag.None)
    {
        if (width <= 0 || height <= 0 || depth <= 0)
            return false;

        SetDimensions(width, height, depth, format);
        return true;
    }

    /// <summary>
    /// Set data either partially or fully on a mip level. Return true if successful.
    /// </summary>
    public bool SetData(uint level, int x, int y, int z, int width, int height, int depth,
        ReadOnlySpan<byte> data)
    {
        GetRawTexture()?.Update(checked((int)level), new Vector3i(x, y, z), new Vector3i(width, height, depth), 0, data);
        return true;
    }

    /// <summary>
    /// Set data from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(Image? image)
    {
        var parameters = new RawTextureParams(
            TextureType.Texture3D, TextureFormat.Unknown, TextureFlag.None, Vector3i.Zero,
            NumLevels: checked((int)_requestedLevels));
        if (!CreateForImage(parameters, image))
            return false;

        return UpdateFromImage(0, image);
    }

    /// <summary>
    /// Get data from a mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public bool GetData(uint level, Span<byte> destination)
    {
        return GetRawTexture()?.Read(0, checked((int)level), destination) == true;
    }
}
