namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>
/// 3D texture resource.
/// </summary>
[Reflected]
public partial class Texture3D : Texture
{
    /// <summary>
    /// Construct.
    /// </summary>
    public Texture3D()
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

        var textureElement = _loadParameters.GetRoot();
        var volumeElement = textureElement.GetChild("volume");
        var colorLutElement = textureElement.GetChild("colorlut");

        if (volumeElement.NotNull)
        {
            var name = volumeElement.GetAttribute("name");
            // If path is empty, add the XML file path
            if (!name.Contains('/') && !name.Contains('\\'))
                name = texturePath + name;

            var image = cache.GetTempResource<Image>(name);
            _loadImage?.Dispose();
            _loadImage = image;
            // Precalculate mip levels if async loading
            if (_loadImage is not null && AsyncLoadState == AsyncLoadState.Loading)
                _loadImage.PrecalculateLevels();
            cache.StoreResourceDependency(this, name);
            return true;
        }
        else if (colorLutElement.NotNull)
        {
            var name = colorLutElement.GetAttribute("name");
            // If path is empty, add the XML file path
            if (!name.Contains('/') && !name.Contains('\\'))
                name = texturePath + name;

            using var file = cache.GetFile(name);
            _loadImage?.Dispose();
            _loadImage = new Image();
            if (!_loadImage.LoadColorLUT(file!))
            {
                _loadParameters.Dispose();
                _loadParameters = null;
                _loadImage.Dispose();
                _loadImage = null;
                return false;
            }
            // Precalculate mip levels if async loading
            if (AsyncLoadState == AsyncLoadState.Loading)
                _loadImage.PrecalculateLevels();
            cache.StoreResourceDependency(this, name);
            return true;
        }

        Log.GetLogger().LogError("Texture3D XML data for {Name} did not contain either volume or colorlut element", Name);
        return false;
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
        var parameters = new RawTextureParams(
            TextureType.Texture3D, format, TextureFlag.None, new Vector3i(width, height, depth),
            NumLevels: _requestedLevels);
        if (_requestedSRGB)
            parameters = parameters with { Format = RenderAPIUtils.SetTextureFormatSRGB(parameters.Format) };
        return Create(parameters);
    }

    /// <summary>
    /// Set data either partially or fully on a mip level. Return true if successful.
    /// </summary>
    public bool SetData(int level, int x, int y, int z, int width, int height, int depth,
        ReadOnlySpan<byte> data)
    {
        GetRawTexture().Update(level, new Vector3i(x, y, z), new Vector3i(width, height, depth), 0, data);
        return true;
    }

    /// <summary>
    /// Set data from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(Image? image)
    {
        var parameters = new RawTextureParams(
            TextureType.Texture3D, TextureFormat.TexFormatUnknown, TextureFlag.None, Vector3i.Zero,
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
    /// Image file acquired during BeginLoad.
    /// </summary>
    private Image? _loadImage;
    /// <summary>
    /// Parameter file acquired during BeginLoad.
    /// </summary>
    private XMLFile? _loadParameters;
}
