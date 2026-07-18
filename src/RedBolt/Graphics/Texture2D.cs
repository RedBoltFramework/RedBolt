namespace RedBolt;

/// <summary>
/// 2D texture resource.
/// </summary>
public class Texture2D : Texture
{
    // Image file acquired during BeginLoad.
    private Image? _loadImage;
    // Parameter file acquired during BeginLoad.
    private XMLFile? _loadParameters;

    /// <summary>
    /// Construct.
    /// </summary>
    public Texture2D(string name)
        : base(name)
    {
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        _loadParameters = null;
        _loadImage?.Dispose();
        _loadImage = null;
        base.Dispose();
    }

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<Texture2D>();
        reflection.SetObjectFactory(static () => new Texture2D(string.Empty));
    }

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Stream source)
    {
        var graphics = GetSubsystem<Graphics>();
        // In headless mode, do not actually load the texture, just return success.
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        _loadImage = new Image($"{Name}#source");
        if (!_loadImage.BeginLoad(source))
        {
            _loadImage.Dispose();
            _loadImage = null;
            return false;
        }

        if (AsyncLoadState == AsyncLoadState.Loading)
            _loadImage.PrecalculateLevels();

        var parametersName = Path.ChangeExtension(Name, ".xml");
        _loadParameters = ResourceLoading.LoadDependentResource<XMLFile>(this, parametersName);
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

        CheckTextureBudget(typeof(Texture2D).GetTypeId());
        SetParameters(_loadParameters);
        var success = SetData(_loadImage);

        _loadImage?.Dispose();
        _loadImage = null;
        _loadParameters = null;
        return success;
    }

    /// <summary>
    /// Set size, format, usage and multisampling parameters for rendertargets. Zero size will follow application window size. Return true if successful.
    /// </summary>
    /// <remarks>
    /// Autoresolve enabled means the multisampled texture will be automatically resolved to one sample after being rendered to and before being sampled as a texture.
    /// Autoresolve disabled means the multisampled texture will be read as individual samples in the shader.
    /// </remarks>
    public bool SetSize(int width, int height, TextureFormat format, TextureFlag flags = TextureFlag.None,
        int multiSample = 1)
    {
        if (width <= 0 || height <= 0)
            return false;

        SetDimensions(width, height, 1, format, multiSample,
            (flags & TextureFlag.NoMultiSampledAutoResolve) != 0, flags);
        return true;
    }

    /// <summary>
    /// Set data either partially or fully on a mip level. Return true if successful.
    /// </summary>
    public bool SetData(uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        GetRawTexture()?.Update(level, new Vector3i(x, y, 0), new Vector3i(width, height, 1), 0u, data);
        return true;
    }

    /// <summary>
    /// Set data from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(Image? image)
    {
        var parameters = new RawTextureParams(
            TextureType.Texture2D, TextureFormat.Unknown, TextureFlag.None, Vector3i.Zero,
            NumLevels: _requestedLevels);
        if (!CreateForImage(parameters, image))
            return false;

        return UpdateFromImage(0, image);
    }

    /// <summary>
    /// Get data from a mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public bool GetData(uint level, Span<byte> destination)
    {
        return GetRawTexture()?.Read(0, level, destination) == true;
    }

    /// <summary>
    /// Get image data from zero mip level. Only RGB and RGBA textures are supported.
    /// </summary>
    public bool GetImage(Image image)
    {
        return ReadToImage(0, 0, image);
    }

    /// <summary>
    /// Get image data from zero mip level. Only RGB and RGBA textures are supported.
    /// </summary>
    public Image? GetImage()
    {
        var image = new Image($"{Name}#image");
        if (GetImage(image))
            return image;

        image.Dispose();
        return null;
    }

}
