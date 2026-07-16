namespace RedBolt;

public sealed class Texture2D : Texture
{
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
        return true;
    }

    public bool SetData(uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        GetRawTexture()?.Update(level, new Vector3i(x, y, 0), new Vector3i(width, height, 1), 0u, data);
        return true;
    }

    public bool SetData(uint level, int x, int y, int width, int height, byte[] data) => SetData(level, x, y, width, height, data.AsSpan());

    public bool SetData(Image? image)
    {
        var parameters = new RawTextureParams(
            TextureType.Texture2D, TextureFormat.Unknown, TextureFlag.None, Vector3i.Zero,
            NumLevels: _requestedLevels);
        if (!CreateForImage(parameters, image))
            return false;

        return UpdateFromImage(0, image);
    }

    public bool GetData(uint level, Span<byte> destination)
    {
        return GetRawTexture()?.Read(0, level, destination) == true;
    }

    public bool GetData(uint level, byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return GetData(level, destination.AsSpan());
    }

    public bool GetImage(Image image)
    {
        return ReadToImage(0, 0, image);
    }

    public Image? GetImage()
    {
        var image = new Image($"{Name}#image");
        return GetImage(image) ? image : null;
    }

}
