namespace RedBolt;

/// <summary>Cube map faces.</summary>
public enum CubeMapFace
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ,
    MaxCubeMapFaces
}

/// <summary>
/// Cube texture resource.
/// </summary>
public class TextureCube : Texture
{
    // Face image files acquired during BeginLoad.
    private ImageCube? _loadImageCube;

    /// <summary>
    /// Construct.
    /// </summary>
    public TextureCube(string name)
        : base(name)
    {
        SetSamplerStateDesc(SamplerStateDesc.Bilinear(TextureAddressMode.Clamp));
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        _loadImageCube?.Dispose();
        _loadImageCube = null;
        base.Dispose();
    }

    protected override TextureType GetRawTextureType() => TextureType.TextureCube;

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<TextureCube>();
        reflection.SetObjectFactory(static () => new TextureCube(string.Empty));
    }

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Stream source)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var graphics = GetSubsystem<Graphics>();

        // In headless mode, do not actually load the texture, just return success.
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        cache.ResetDependencies(this);

        // Load resource.
        _loadImageCube?.Dispose();
        _loadImageCube = cache.GetTempResource<ImageCube>(Name);
        if (_loadImageCube is null)
            return false;

        // Update dependencies.
        foreach (var image in _loadImageCube.GetImages())
        {
            if (image is not null && !string.IsNullOrEmpty(image.Name))
                cache.StoreResourceDependency(this, image.Name);
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

        // If over the texture budget, see if materials can be freed to allow textures to be freed.
        CheckTextureBudget(typeof(TextureCube).GetTypeId());

        SetParameters(_loadImageCube!.GetParametersXML());

        var images = _loadImageCube.GetImages();
        for (var i = 0; i < images.Count && i < (int)CubeMapFace.MaxCubeMapFaces; ++i)
            SetData((CubeMapFace)i, images[i]);

        _loadImageCube.Dispose();
        _loadImageCube = null;

        return true;
    }

    /// <summary>
    /// Set size, format, usage and multisampling parameter for rendertargets. Note that cube textures always use autoresolve when multisampled due to lacking support (on all APIs) to multisample them in a shader. Return true if successful.
    /// </summary>
    public bool SetSize(int size, TextureFormat format, TextureFlag flags = TextureFlag.None, int multiSample = 1)
    {
        if (size <= 0)
            return false;

        SetDimensions(size, size, 1, format, multiSample,
            (flags & TextureFlag.NoMultiSampledAutoResolve) != 0, flags);
        return true;
    }

    /// <summary>
    /// Set data either partially or fully on a face's mip level. Return true if successful.
    /// </summary>
    public bool SetData(CubeMapFace face, uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        GetRawTexture()?.Update(checked((int)level), new Vector3i(x, y, 0), new Vector3i(width, height, 1), (int)face, data);
        return true;
    }

    /// <summary>
    /// Set data of one face from a stream. Return true if successful.
    /// </summary>
    public bool SetData(CubeMapFace face, Stream source)
    {
        using var image = new Image($"{Name}#{face}");
        if (!image.Load(source))
            return false;

        return SetData(face, image);
    }

    /// <summary>
    /// Set data of one face from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(CubeMapFace face, Image? image)
    {
        if (face == CubeMapFace.PositiveX)
        {
            var parameters = new RawTextureParams(
                TextureType.TextureCube, TextureFormat.Unknown, TextureFlag.None, Vector3i.Zero,
                NumLevels: checked((int)_requestedLevels));
            if (!CreateForImage(parameters, image))
                return false;
        }

        return UpdateFromImage((int)face, image);
    }

    /// <summary>
    /// Get data from a face's mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public bool GetData(CubeMapFace face, uint level, Span<byte> destination)
    {
        return GetRawTexture()?.Read((int)face, checked((int)level), destination) == true;
    }

    /// <summary>
    /// Get image data from a face's zero mip level. Only RGB and RGBA textures are supported.
    /// </summary>
    public Image? GetImage(CubeMapFace face)
    {
        var image = new Image($"{Name}#{face}");
        if (ReadToImage((uint)face, 0, image))
            return image;

        image.Dispose();
        return null;
    }

    /// <summary>
    /// Return render surface for one face.
    /// </summary>
    public RenderSurface? GetRenderSurface(CubeMapFace face) => GetRenderSurface((uint)face);
}
