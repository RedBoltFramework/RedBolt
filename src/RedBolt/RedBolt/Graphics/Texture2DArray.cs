namespace RedBolt;

/// <summary>
/// 2D texture array resource.
/// </summary>
public class Texture2DArray : Texture
{
    /// <summary>
    /// Construct.
    /// </summary>
    public Texture2DArray()
    {
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        _loadParameters?.Dispose();
        _loadParameters = null;
        for (var i = _loadImages.Count - 1; i >= 0; --i)
            _loadImages[i]?.Dispose();
        _loadImages.Clear();
        base.Dispose();
    }

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<Texture2DArray>();
        reflection.SetObjectFactory(static () => new Texture2DArray());
    }

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Deserializer source)
    {
        var graphics = GetSubsystem<Graphics>();
        var cache = GetSubsystem<ResourceCache>()!;

        // In headless mode, do not actually load the texture, just return success
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        cache.ResetDependencies(this);

        var normalizedName = Name.Replace('\\', '/');
        var separatorIndex = normalizedName.LastIndexOf('/');
        var texturePath = separatorIndex >= 0 ? normalizedName[..(separatorIndex + 1)] : string.Empty;

        _loadParameters?.Dispose();
        _loadParameters = new XMLFile();
        if (!_loadParameters.Load(source))
        {
            _loadParameters.Dispose();
            _loadParameters = null;
            return false;
        }

        for (var i = _loadImages.Count - 1; i >= 0; --i)
            _loadImages[i]?.Dispose();
        _loadImages.Clear();

        for (var layerElement = _loadParameters.GetRoot().GetChild("layer"); layerElement.NotNull;
             layerElement = layerElement.GetNext("layer"))
        {
            var name = layerElement.GetAttribute("name");
            // If path is empty, add the XML file path
            if (!name.Contains('/') && !name.Contains('\\'))
                name = texturePath + name;

            _loadImages.Add(cache.GetTempResource<Image>(name));
            cache.StoreResourceDependency(this, name);
        }

        // Precalculate mip levels if async loading
        if (AsyncLoadState == AsyncLoadState.Loading)
        {
            for (var i = 0; i < _loadImages.Count; ++i)
            {
                if (_loadImages[i] is not null)
                    _loadImages[i]!.PrecalculateLevels();
            }
        }

        return true;
    }

    /// <summary>
    /// Finish resource loading. Always called from the main thread. Return true if successful.
    /// </summary>
    public override bool EndLoad()
    {
        // In headless mode, do not actually load the texture, just return success
        if (GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null)
            return true;

        // If over the texture budget, see if materials can be freed to allow textures to be freed
        CheckTextureBudget(typeof(Texture2DArray).GetTypeId());

        SetParameters(_loadParameters);
        SetLayers(_loadImages.Count);

        for (var i = 0; i < _loadImages.Count; ++i)
            SetData(i, _loadImages[i]);

        for (var i = _loadImages.Count - 1; i >= 0; --i)
            _loadImages[i]?.Dispose();
        _loadImages.Clear();
        _loadParameters?.Dispose();
        _loadParameters = null;

        return true;
    }

    /// <summary>
    /// Set the number of layers in the texture. To be used before SetData.
    /// </summary>
    public void SetLayers(int layers)
    {
        _layers = layers;
    }

    /// <summary>
    /// Set layers, size, format and usage. Set layers to zero to leave them unchanged. Return true if successful.
    /// </summary>
    public bool SetSize(int layers, int width, int height, TextureFormat format,
        TextureFlag flags = TextureFlag.None)
    {
        _layers = layers != 0 ? layers : _layers;
        var parameters = new RawTextureParams(
            TextureType.Texture2DArray, format, flags, new Vector3i(width, height, 1),
            ArraySize: _layers, NumLevels: _requestedLevels);
        if (_requestedSRGB)
            parameters = parameters with { Format = RenderAPIUtils.SetTextureFormatSRGB(parameters.Format) };
        return Create(parameters);
    }

    /// <summary>
    /// Set data either partially or fully on a layer's mip level. Return true if successful.
    /// </summary>
    public bool SetData(int layer, int level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        GetRawTexture().Update(level, new Vector3i(x, y, 0), new Vector3i(width, height, 1), layer, data);
        return true;
    }

    /// <summary>
    /// Set data of one layer from a stream. Return true if successful.
    /// </summary>
    public bool SetData(int layer, Deserializer source)
    {
        using var image = new Image();
        if (!image.Load(source))
            return false;

        return SetData(layer, image);
    }

    /// <summary>
    /// Set data of one layer from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(int layer, Image? image)
    {
        if (layer == 0)
        {
            var parameters = new RawTextureParams(
                TextureType.Texture2DArray, TextureFormat.TexFormatUnknown, TextureFlag.None, Vector3i.Zero,
                ArraySize: _layers, NumLevels: _requestedLevels);
            if (!CreateForImage(parameters, image))
                return false;
        }

        return UpdateFromImage(layer, image);
    }

    /// <summary>
    /// Return number of layers in the texture.
    /// </summary>
    public int Layers => _layers;

    /// <summary>
    /// Get data from a mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public bool GetData(int layer, int level, Span<byte> destination)
    {
        return GetRawTexture().Read(layer, level, destination);
    }

    /// <summary>
    /// Texture array layers number.
    /// </summary>
    private int _layers;
    /// <summary>
    /// Layer image files acquired during BeginLoad.
    /// </summary>
    private readonly List<Image?> _loadImages = [];
    /// <summary>
    /// Parameter file acquired during BeginLoad.
    /// </summary>
    private XMLFile? _loadParameters;
}
