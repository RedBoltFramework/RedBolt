// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderAPI;
using TextureAddressMode = global::RedBolt.RenderAPI.TextureAddressMode;
using RedBolt.Resource;

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
[Reflected]
public partial class TextureCube : Texture
{
    /// <summary>
    /// Construct.
    /// </summary>
    public TextureCube()
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


    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Deserializer source)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var graphics = GetSubsystem<Graphics>();

        // In headless mode, do not actually load the texture, just return success
        if (graphics?.GetNativeRenderDevice() is null)
            return true;

        cache.ResetDependencies(this);

        // Load resource
        _loadImageCube?.Dispose();
        _loadImageCube = cache.GetTempResource<ImageCube>(Name);
        if (_loadImageCube is null)
            return false;

        _sphericalHarmonics = _loadImageCube.GetSphericalHarmonics();

        // Update dependencies
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
        // In headless mode, do not actually load the texture, just return success
        if (GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null)
            return true;

        // If over the texture budget, see if materials can be freed to allow textures to be freed
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
        var parameters = new RawTextureParams(
            TextureType.TextureCube, format, flags, new Vector3i(size, size, 1),
            NumLevels: _requestedLevels, MultiSample: multiSample);
        if (_requestedSRGB)
            parameters = parameters with { Format = RenderAPIUtils.SetTextureFormatSRGB(parameters.Format) };
        return Create(parameters);
    }

    /// <summary>
    /// Set data either partially or fully on a face's mip level. Return true if successful.
    /// </summary>
    public bool SetData(CubeMapFace face, int level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        GetRawTexture().Update(level, new Vector3i(x, y, 0), new Vector3i(width, height, 1), (int)face, data);
        return true;
    }

    /// <summary>
    /// Set data of one face from a stream. Return true if successful.
    /// </summary>
    public bool SetData(CubeMapFace face, Deserializer source)
    {
        using var image = new Image();
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
                TextureType.TextureCube, TextureFormat.TexFormatUnknown, TextureFlag.None, Vector3i.Zero,
                NumLevels: _requestedLevels);
            if (!CreateForImage(parameters, image))
                return false;
        }

        return UpdateFromImage((int)face, image);
    }

    /// <summary>
    /// Get data from a face's mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public bool GetData(CubeMapFace face, int level, Span<byte> destination)
    {
        return GetRawTexture().Read((int)face, level, destination);
    }

    /// <summary>
    /// Return spherical harmonics of the texture, if the source image provided them on load.
    /// </summary>
    public SphericalHarmonicsDot9? GetSphericalHarmonics() => _sphericalHarmonics;

    /// <summary>
    /// Get image data from a face's zero mip level. Only RGB and RGBA textures are supported.
    /// </summary>
    public Image? GetImage(CubeMapFace face)
    {
        var image = new Image();
        if (ReadToImage((int)face, 0, image))
            return image;

        image.Dispose();
        return null;
    }

    /// <summary>
    /// Return render surface for one face.
    /// </summary>
    public RenderSurface? GetRenderSurface(CubeMapFace face) => GetRenderSurface((int)face);

    /// <summary>
    /// Face image files acquired during BeginLoad.
    /// </summary>
    private ImageCube? _loadImageCube;

    /// <summary>
    /// Spherical harmonics, copied from the source image on load.
    /// </summary>
    private SphericalHarmonicsDot9? _sphericalHarmonics;
}
