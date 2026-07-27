using RedBolt.Core;
using RedBolt.IO;
using RedBolt.RenderAPI;
using RedBolt.Resource;

namespace RedBolt.Graphics;

/// <summary>
/// 2D texture resource.
/// </summary>
public class Texture2D : Texture
{
    /// <summary>
    /// Construct.
    /// </summary>
    public Texture2D()
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
        reflection.SetObjectFactory(static () => new Texture2D());
    }

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Deserializer source)
    {
        var graphics = GetSubsystem<GraphicsSystem>();
        // In headless mode, do not actually load the texture, just return success
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        // Load the image data for EndLoad()
        _loadImage?.Dispose();
        _loadImage = new Image();
        if (!_loadImage.BeginLoad(source))
        {
            _loadImage.Dispose();
            _loadImage = null;
            return false;
        }

        // Precalculate mip levels if async loading
        if (AsyncLoadState == AsyncLoadState.Loading)
            _loadImage.PrecalculateLevels();

        // Load the optional parameters file
        var cache = GetSubsystem<ResourceCache>()!;
        var parametersName = Path.ChangeExtension(Name, ".xml");
        _loadParameters = cache.GetTempResource<XMLFile>(parametersName, false);
        return true;
    }

    /// <summary>
    /// Finish resource loading. Always called from the main thread. Return true if successful.
    /// </summary>
    public override bool EndLoad()
    {
        // In headless mode, do not actually load the texture, just return success
        if (GetSubsystem<GraphicsSystem>()?.GetNativeRenderDevice() is null)
            return true;

        // If over the texture budget, see if materials can be freed to allow textures to be freed
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
    /// Autoresolve true means the multisampled texture will be automatically resolved to 1-sample after being rendered to and before being sampled as a texture.
    /// Autoresolve false means the multisampled texture will be read as individual samples in the shader.
    /// </remarks>
    public bool SetSize(int width, int height, TextureFormat format, TextureFlag flags = TextureFlag.None,
        int multiSample = 1)
    {
        var parameters = new RawTextureParams(
            TextureType.Texture2D, format, flags, new Vector3i(width, height, 1),
            NumLevels: _requestedLevels, MultiSample: multiSample);
        if (_requestedSRGB)
            parameters = parameters with { Format = RenderAPIUtils.SetTextureFormatSRGB(parameters.Format) };
        return Create(parameters);
    }

    /// <summary>
    /// Set data either partially or fully on a mip level. Return true if successful.
    /// </summary>
    public bool SetData(int level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        GetRawTexture().Update(level, new Vector3i(x, y, 0), new Vector3i(width, height, 1), 0, data);
        return true;
    }

    /// <summary>
    /// Set data from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(Image? image)
    {
        var parameters = new RawTextureParams(
            TextureType.Texture2D, TextureFormat.TexFormatUnknown, TextureFlag.None, Vector3i.Zero,
            NumLevels: _requestedLevels);
        if (!CreateForImage(parameters, image))
            return false;

        return UpdateFromImage(0, image);
    }

    /// <summary>
    /// Get data from a mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public bool GetData(int level, Span<byte> destination)
    {
        return GetRawTexture().Read(0, level, destination);
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
        var image = new Image();
        if (GetImage(image))
            return image;

        image.Dispose();
        return null;
    }

    /// <summary>
    /// Image file acquired during BeginLoad.
    /// </summary>
    private Image? _loadImage;
    /// <summary>
    /// Parameter file acquired during BeginLoad.
    /// </summary>
    private XMLFile? _loadParameters;
}
