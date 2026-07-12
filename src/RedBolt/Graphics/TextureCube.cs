namespace RedBolt;

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
public sealed class TextureCube : Texture
{
    private readonly Dictionary<(CubeMapFace Face, uint Level), byte[]> _faceData = new();
    private ImageCube? _loadImageCube;

    public TextureCube(string name)
        : base(name)
    {
        SetFilterMode(TextureFilterMode.Bilinear);
        SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
        SetAddressMode(TextureCoordinate.V, TextureAddressMode.Clamp);
        SetAddressMode(TextureCoordinate.W, TextureAddressMode.Clamp);
    }

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        // TODO: port: context->AddFactoryReflection<TextureCube>();
    }

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _loadImageCube = new ImageCube(Name)
        {
            OwnerCache = OwnerCache
        };
        _loadImageCube.SetAbsoluteFileName(AbsoluteFileName);
        if (!_loadImageCube.Load(source))
        {
            _loadImageCube = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finish resource loading. Always called from the main thread. Return true if successful.
    /// </summary>
    public override bool EndLoad()
    {
        if (_loadImageCube is null)
            return false;

        SetParameters(_loadImageCube.GetParametersXML());
        var images = _loadImageCube.GetImages();
        for (var i = 0; i < images.Count && i < (int)CubeMapFace.MaxCubeMapFaces; ++i)
            SetData((CubeMapFace)i, images[i]);

        _loadImageCube = null;
        return true;
    }

    /// <summary>
    /// Set size, format, usage and multisampling parameter for rendertargets. Note that cube textures always use autoresolve when multisampled due to lacking support (on all APIs) to multisample them in a shader. Return true if successful.
    /// </summary>
    public bool SetSize(int size, TextureFormat format, int multiSample = 1, bool noMultiSampledAutoResolve = false)
    {
        if (size <= 0)
            return false;

        SetDimensions(size, size, 1, format, multiSample, false);
        _faceData.Clear();
        RefreshTotalMemoryUse();
        return true;
    }

    /// <summary>
    /// Set data either partially or fully on a face's mip level. Return true if successful.
    /// </summary>
    public bool SetData(CubeMapFace face, uint level, int x, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        if (face >= CubeMapFace.MaxCubeMapFaces || level >= Levels || x < 0 || y < 0 || width <= 0 || height <= 0)
            return false;

        var levelSize = GetLevelWidth(level);
        if (x + width > levelSize || y + height > levelSize)
            return false;

        var bytesPerPixel = (int)GetComponents();
        if (bytesPerPixel == 0)
            return false;

        var expected = checked(width * height * bytesPerPixel);
        if (data.Length != expected)
            return false;

        var key = (face, level);
        if (!_faceData.TryGetValue(key, out var levelBytes))
        {
            levelBytes = new byte[checked(levelSize * levelSize * bytesPerPixel)];
            _faceData[key] = levelBytes;
        }

        for (var row = 0; row < height; ++row)
        {
            var sourceOffset = row * width * bytesPerPixel;
            var destinationOffset = ((y + row) * levelSize + x) * bytesPerPixel;
            data.Slice(sourceOffset, width * bytesPerPixel).CopyTo(levelBytes.AsSpan(destinationOffset, width * bytesPerPixel));
        }

        RefreshTotalMemoryUse();
        return true;
    }

    public bool SetData(CubeMapFace face, uint level, int x, int y, int width, int height, byte[] data) => SetData(face, level, x, y, width, height, data.AsSpan());

    /// <summary>
    /// Set data of one face from a stream. Return true if successful.
    /// </summary>
    public bool SetData(CubeMapFace face, Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var image = new Image($"{Name}#{face}")
        {
            OwnerCache = OwnerCache
        };
        return image.Load(source) && SetData(face, image);
    }

    /// <summary>
    /// Set data of one face from an image. Return true if successful. Optionally make a single channel image alpha-only.
    /// </summary>
    public bool SetData(CubeMapFace face, Image? image)
    {
        if (face >= CubeMapFace.MaxCubeMapFaces || image is null)
            return false;

        if (face == CubeMapFace.PositiveX)
        {
            var maxLevels = image.IsCompressed()
                ? Math.Max(image.NumCompressedLevels, 1u)
                : CalculateFullMipChainLength(Math.Max(image.Width, image.Height));
            _mostDetailedLevel = Math.Min((uint)GetMipsToSkip(MaterialQuality.High), maxLevels - 1u);
            var levels = _requestedLevels == 0
                ? maxLevels - _mostDetailedLevel
                : Math.Min(_requestedLevels, maxLevels - _mostDetailedLevel);
            var baseLevel = image.GetDecompressedImageLevel(_mostDetailedLevel);
            if (baseLevel is null)
                return false;

            if (!SetSize(baseLevel.Width, TextureFormat.RGBA8))
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

            if (!SetData(face, level, 0, 0, mipImage.Width, mipImage.Height, mipImage.GetData()))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get data from a face's mip level. The destination buffer must be big enough. Return true if successful.
    /// </summary>
    public byte[]? GetData(CubeMapFace face, uint level)
    {
        return _faceData.TryGetValue((face, level), out var bytes) ? bytes.ToArray() : null;
    }

    public bool GetData(CubeMapFace face, uint level, Span<byte> destination)
    {
        if (!_faceData.TryGetValue((face, level), out var bytes) || destination.Length < bytes.Length)
            return false;

        bytes.AsSpan().CopyTo(destination);
        return true;
    }

    public bool GetData(CubeMapFace face, uint level, byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return GetData(face, level, destination.AsSpan());
    }

    /// <summary>
    /// Get image data from a face's zero mip level. Only RGB and RGBA textures are supported.
    /// </summary>
    public Image? GetImage(CubeMapFace face)
    {
        if (face >= CubeMapFace.MaxCubeMapFaces || Format is not (TextureFormat.RGB8 or TextureFormat.RGBA8))
            return null;

        if (!_faceData.TryGetValue((face, 0u), out var faceBytes))
            return null;

        var image = new Image($"{Name}#{face}");
        var width = GetLevelWidth(0u);
        if (!image.SetSize(width, width, 4u))
            return null;

        if (Format == TextureFormat.RGBA8)
        {
            if (faceBytes.Length != width * width * 4)
                return null;
            image.SetData(faceBytes);
            return image;
        }

        var rgba = new byte[checked(width * width * 4)];
        for (var i = 0; i < width * width; ++i)
        {
            rgba[i * 4] = faceBytes[i * 3];
            rgba[i * 4 + 1] = faceBytes[i * 3 + 1];
            rgba[i * 4 + 2] = faceBytes[i * 3 + 2];
            rgba[i * 4 + 3] = 255;
        }
        image.SetData(rgba);
        return image;
    }

    /// <summary>
    /// Return render surface for one face.
    /// </summary>
    public object? GetRenderSurface(CubeMapFace face) => GetRenderSurface((uint)face);

    private void RefreshTotalMemoryUse()
    {
        var total = 0;
        foreach (var item in _faceData)
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
