namespace RedBolt;

public sealed class ImageCube : Resource
{
    private readonly Image?[] _faceImages = new Image[6];
    private XMLFile? _parametersXml;
    private SphericalHarmonicsDot9? _cachedSphericalHarmonics;
    private int _width;

    public ImageCube(string name)
        : base(name)
    {
    }

    // C++: SplitPath(GetName()) for path resolution; ResetDependencies(this); loads cachedSphericalHarmonics_
    // C++: from XML <sh> element; async loading PrecalculateLevels; error logging via URHO3D_LOGERROR
    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _parametersXml = new XMLFile(Name + "#params");
        if (!_parametersXml.Load(source))
        {
            _parametersXml = null;
            return false;
        }

        Array.Clear(_faceImages);

        var root = _parametersXml.Root;
        var imageElement = root.GetChild("image");
        if (imageElement.NotNull)
        {
            var imageName = imageElement.GetAttribute("name");
            var image = ResourceLoading.LoadDependentResource<Image>(this, imageName);
            if (image is null)
                return false;

            if (image.IsCubemap())
            {
                var current = image;
                for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; face = (CubeMapFace)((int)face + 1))
                {
                    if (current is null)
                        return false;

                    SetImage(face, current);
                    current = current.GetNextSibling();
                }
            }
            else
            {
                if (!LoadSingleImageLayout(image, imageElement.GetAttribute("layout")))
                    return false;
            }
        }
        else
        {
            var faceElement = root.GetChild("face");
            var faceIndex = 0;
            while (faceElement.NotNull && faceIndex < _faceImages.Length)
            {
                var imageName = faceElement.GetAttribute("name");
                var image = ResourceLoading.LoadDependentResource<Image>(this, imageName);
                if (image is null)
                    return false;

                SetImage((CubeMapFace)faceIndex, image);
                ++faceIndex;
                faceElement = faceElement.GetNext("face");
            }

            if (faceIndex != _faceImages.Length)
                return false;
        }

        var expectedSize = 0;
        var memoryUse = 0u;
        foreach (var image in _faceImages)
        {
            if (image is null)
                return false;

            memoryUse += image.MemoryUse;
            if (expectedSize == 0)
                expectedSize = image.Width;

            if (image.Width != expectedSize || image.Height != expectedSize)
                return false;
        }

        MemoryUse = memoryUse;
        _width = expectedSize;
        LinkSiblings();
        return true;
    }

    public IReadOnlyList<Image?> GetImages() => _faceImages;

    public XMLFile? GetParametersXML() => _parametersXml;

    public Image? GetImage(CubeMapFace face) => _faceImages[(int)face];

    public void SetImage(CubeMapFace face, Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _faceImages[(int)face] = image;
        if (_width == 0)
            _width = image.Width;
        LinkSiblings();
    }

    public uint GetSphericalHarmonicsMipLevel()
    {
        var width = GetImage(CubeMapFace.PositiveX)?.Width ?? 1;
        var maxLevel = (uint)Math.Max(0, (int)MathF.Log2(width));
        return maxLevel - Math.Min(maxLevel, 3u);
    }

    public ImageCube GetDecompressedImageLevel(uint index)
    {
        var cube = new ImageCube(Name + "#level");
        cube._parametersXml = _parametersXml;
        cube._width = Math.Max(1, _width >> checked((int)index));
        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; face = (CubeMapFace)((int)face + 1))
        {
            var image = GetImage(face);
            if (image is null)
                continue;

            var mip = image.GetDecompressedImageLevel(index);
            if (mip is null)
                continue;

            cube._faceImages[(int)face] = mip;
        }

        cube.LinkSiblings();

        return cube;
    }

    public ImageCube GetDecompressedImage() => GetDecompressedImageLevel(0u);

    public SphericalHarmonicsColor9 CalculateSphericalHarmonics()
    {
        var result = new SphericalHarmonicsColor9(); var weightSum = 0f;
        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; face = (CubeMapFace)((int)face + 1))
        {
            var source = GetImage(face); if (source is null) continue;
            var level = Math.Min(source.IsCompressed() ? source.NumCompressedLevels - 1 : uint.MaxValue, GetSphericalHarmonicsMipLevel());
            var image = source.GetDecompressedImageLevel(level); if (image is null) continue;
            for (var y = 0; y < image.Height; ++y) for (var x = 0; x < image.Width; ++x)
            {
                var offset = ProjectTexelOnCubeLevel(face, x, y, level); var distance = offset.Length;
                if (distance <= 0) continue;
                var weight = 1f / (distance * distance * distance);
                result.AddScaled(new SphericalHarmonicsColor9(offset / distance, image.GetPixel(x, y).GammaToLinear()), weight);
                weightSum += weight;
            }
        }
        if (weightSum > 0) result.Scale(4f * MathF.PI / weightSum);
        return result;
    }

    public SphericalHarmonicsDot9 GetOrCreateSphericalHarmonics()
        => _cachedSphericalHarmonics ??= new SphericalHarmonicsDot9(CalculateSphericalHarmonics());

    public Color SampleNearest(Vector3f direction)
    {
        var (face, texel) = ProjectDirectionOnFaceTexel(direction);
        var image = GetImage(face);
        return image is null ? Color.Transparent : image.GetPixel(texel.X, texel.Y);
    }

    public Vector3f ProjectTexelOnCube(CubeMapFace face, int x, int y)
    {
        return ProjectTexelOnCubeLevel(face, x, y, 0u);
    }

    public Vector3f ProjectTexelOnCubeLevel(CubeMapFace face, int x, int y, uint level)
    {
        if (GetImage(face) is null || _width <= 0)
            return Vector3f.Zero;

        var width = Math.Max(_width >> checked((int)level), 1);
        var uv = new Vector2f((x + 0.5f) / width, (y + 0.5f) / width);
        return ProjectUVOnCube(face, uv);
    }

    public (CubeMapFace Face, Vector2i Texel) ProjectDirectionOnFaceTexel(Vector3f direction)
    {
        var (face, uv) = ProjectDirectionOnFace(direction);
        if (GetImage(face) is null || _width <= 0)
            return (face, Vector2i.Zero);

        var x = Math.Clamp((int)MathF.Floor(uv.X * _width), 0, _width);
        var y = Math.Clamp((int)MathF.Floor(uv.Y * _width), 0, _width);
        return (face, new Vector2i(x, y));
    }

    public static Vector3f ProjectUVOnCube(CubeMapFace face, Vector2f uv)
    {
        var u = uv.X * 2.0f - 1.0f;
        var v = 1.0f - uv.Y * 2.0f;
        return face switch
        {
            CubeMapFace.PositiveX => new Vector3f(1.0f, v, -u),
            CubeMapFace.NegativeX => new Vector3f(-1.0f, v, u),
            CubeMapFace.PositiveY => new Vector3f(u, 1.0f, -v),
            CubeMapFace.NegativeY => new Vector3f(u, -1.0f, v),
            CubeMapFace.PositiveZ => new Vector3f(u, v, 1.0f),
            CubeMapFace.NegativeZ => new Vector3f(-u, v, -1.0f),
            _ => Vector3f.Zero
        };
    }

    public static (CubeMapFace Face, Vector2f Uv) ProjectDirectionOnFace(Vector3f direction)
    {
        var normalized = direction.NormalizedOrDefault();
        var absX = Math.Abs(normalized.X);
        var absY = Math.Abs(normalized.Y);
        var absZ = Math.Abs(normalized.Z);

        if (absX >= absY && absX >= absZ)
        {
            if (normalized.X >= 0.0f)
                return (CubeMapFace.PositiveX, new Vector2f((1.0f - normalized.Z / absX) * 0.5f, (1.0f - normalized.Y / absX) * 0.5f));
            return (CubeMapFace.NegativeX, new Vector2f((normalized.Z / absX + 1.0f) * 0.5f, (1.0f - normalized.Y / absX) * 0.5f));
        }

        if (absY >= absX && absY >= absZ)
        {
            if (normalized.Y >= 0.0f)
                return (CubeMapFace.PositiveY, new Vector2f((normalized.X / absY + 1.0f) * 0.5f, (normalized.Z / absY + 1.0f) * 0.5f));
            return (CubeMapFace.NegativeY, new Vector2f((normalized.X / absY + 1.0f) * 0.5f, (1.0f - normalized.Z / absY) * 0.5f));
        }

        if (normalized.Z >= 0.0f)
            return (CubeMapFace.PositiveZ, new Vector2f((normalized.X / absZ + 1.0f) * 0.5f, (1.0f - normalized.Y / absZ) * 0.5f));
        return (CubeMapFace.NegativeZ, new Vector2f((1.0f - normalized.X / absZ) * 0.5f, (1.0f - normalized.Y / absZ) * 0.5f));
    }

    private void LinkSiblings()
    {
        Image? previous = null;
        foreach (var image in _faceImages)
        {
            if (image is null)
                continue;

            if (previous is not null)
                previous.SetNextSibling(image);
            previous = image;
        }

        if (previous is not null)
            previous.SetNextSibling(null);
    }

    private bool LoadSingleImageLayout(Image image, string layout)
    {
        var normalizedLayout = string.IsNullOrWhiteSpace(layout)
            ? "horizontal"
            : layout.Trim().ToLowerInvariant();

        return normalizedLayout switch
        {
            "horizontal" => SetHorizontalLayout(image),
            "horizontalnvidia" => SetHorizontalNvidiaLayout(image),
            "horizontalcross" => SetHorizontalCrossLayout(image),
            "verticalcross" => SetVerticalCrossLayout(image),
            "blender" => SetBlenderLayout(image),
            _ => false
        };
    }

    private bool SetHorizontalLayout(Image image)
    {
        var faceWidth = image.Width / 6;
        var faceHeight = image.Height;
        if (!ValidateTileLayout(faceWidth, faceHeight, image.Width, image.Height, 6, 1))
            return false;

        return SetFaceTiles(image,
            (CubeMapFace.PositiveZ, 0, 0),
            (CubeMapFace.PositiveX, 1, 0),
            (CubeMapFace.NegativeZ, 2, 0),
            (CubeMapFace.NegativeX, 3, 0),
            (CubeMapFace.PositiveY, 4, 0),
            (CubeMapFace.NegativeY, 5, 0));
    }

    private bool SetHorizontalNvidiaLayout(Image image)
    {
        var faceWidth = image.Width / 6;
        var faceHeight = image.Height;
        if (!ValidateTileLayout(faceWidth, faceHeight, image.Width, image.Height, 6, 1))
            return false;

        return SetFaceTiles(image,
            (CubeMapFace.PositiveX, 0, 0),
            (CubeMapFace.NegativeX, 1, 0),
            (CubeMapFace.PositiveY, 2, 0),
            (CubeMapFace.NegativeY, 3, 0),
            (CubeMapFace.PositiveZ, 4, 0),
            (CubeMapFace.NegativeZ, 5, 0));
    }

    private bool SetHorizontalCrossLayout(Image image)
    {
        var faceWidth = image.Width / 4;
        var faceHeight = image.Height / 3;
        if (!ValidateTileLayout(faceWidth, faceHeight, image.Width, image.Height, 4, 3))
            return false;

        return SetFaceTiles(image,
            (CubeMapFace.PositiveY, 1, 0),
            (CubeMapFace.NegativeX, 0, 1),
            (CubeMapFace.PositiveZ, 1, 1),
            (CubeMapFace.PositiveX, 2, 1),
            (CubeMapFace.NegativeZ, 3, 1),
            (CubeMapFace.NegativeY, 1, 2));
    }

    private bool SetVerticalCrossLayout(Image image)
    {
        var faceWidth = image.Width / 3;
        var faceHeight = image.Height / 4;
        if (!ValidateTileLayout(faceWidth, faceHeight, image.Width, image.Height, 3, 4))
            return false;

        if (!SetFaceTiles(image,
                (CubeMapFace.PositiveY, 1, 0),
                (CubeMapFace.NegativeX, 0, 1),
                (CubeMapFace.PositiveZ, 1, 1),
                (CubeMapFace.PositiveX, 2, 1),
                (CubeMapFace.NegativeY, 1, 2),
                (CubeMapFace.NegativeZ, 1, 3)))
            return false;

        var negativeZ = GetImage(CubeMapFace.NegativeZ);
        if (negativeZ is null)
            return false;

        negativeZ.FlipVertical();
        negativeZ.FlipHorizontal();
        return true;
    }

    private bool SetBlenderLayout(Image image)
    {
        var faceWidth = image.Width / 3;
        var faceHeight = image.Height / 2;
        if (!ValidateTileLayout(faceWidth, faceHeight, image.Width, image.Height, 3, 2))
            return false;

        return SetFaceTiles(image,
            (CubeMapFace.NegativeX, 0, 0),
            (CubeMapFace.NegativeZ, 1, 0),
            (CubeMapFace.PositiveX, 2, 0),
            (CubeMapFace.NegativeY, 0, 1),
            (CubeMapFace.PositiveY, 1, 1),
            (CubeMapFace.PositiveZ, 2, 1));
    }

    private bool SetFaceTiles(Image image, params (CubeMapFace Face, int TileX, int TileY)[] tiles)
    {
        var maxTileX = 1;
        var maxTileY = 1;
        foreach (var tile in tiles)
        {
            maxTileX = Math.Max(maxTileX, tile.TileX + 1);
            maxTileY = Math.Max(maxTileY, tile.TileY + 1);
        }

        var tileWidth = image.Width / maxTileX;
        var tileHeight = image.Height / maxTileY;
        foreach (var (face, tileX, tileY) in tiles)
        {
            var subImage = image.GetSubimage(new IntRect(tileX * tileWidth, tileY * tileHeight, (tileX + 1) * tileWidth, (tileY + 1) * tileHeight));
            if (subImage is null)
                return false;

            SetImage(face, subImage);
        }

        return true;
    }

    private static bool ValidateTileLayout(int faceWidth, int faceHeight, int imageWidth, int imageHeight, int layoutWidth, int layoutHeight)
    {
        return faceWidth > 0
            && faceHeight > 0
            && faceWidth == faceHeight
            && imageWidth == faceWidth * layoutWidth
            && imageHeight == faceHeight * layoutHeight;
    }
}
