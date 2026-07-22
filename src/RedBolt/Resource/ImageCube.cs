namespace RedBolt;

/// <summary>Cube texture resource.</summary>
public class ImageCube : Resource
{
    /// <summary>Face images.</summary>
    private readonly List<Image?> _faceImages = [];
    /// <summary>Parameter file.</summary>
    private XMLFile? _parametersXml;
    /// <summary>Cube width.</summary>
    private int _width;
    /// <summary>Precalculated spherical harmonics.</summary>
    private SphericalHarmonicsDot9? _cachedSphericalHarmonics;

    /// <summary>Construct.</summary>
    public ImageCube()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _cachedSphericalHarmonics = null;
        _width = 0;
        _parametersXml = null;
        _faceImages.Clear();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddFactoryReflection<ImageCube>();
    }

    private static Image? GetTileImage(Image source, int tileX, int tileY, int tileWidth, int tileHeight)
    {
        return source.GetSubimage(new IntRect(
            tileX * tileWidth, tileY * tileHeight, (tileX + 1) * tileWidth, (tileY + 1) * tileHeight));
    }

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var cache = GetSubsystem<ResourceCache>()!;

        cache.ResetDependencies(this);

        var normalizedName = Name.Replace('\\', '/');
        var separatorIndex = normalizedName.LastIndexOf('/');
        var texturePath = separatorIndex >= 0 ? normalizedName[..(separatorIndex + 1)] : string.Empty;

        _parametersXml = new XMLFile();
        if (!_parametersXml.Load(source))
        {
            _parametersXml = null;
            return false;
        }

        _faceImages.Clear();

        var textureElement = _parametersXml.Root;
        var imageElement = textureElement.GetChild("image");
        // Single image and multiple faces with layout
        if (imageElement.NotNull)
        {
            var name = imageElement.GetAttribute("name");
            // If path is empty, add the XML file path
            if (!name.Contains('/') && !name.Contains('\\'))
                name = texturePath + name;

            var image = cache.GetTempResource<Image>(name);
            if (image is null)
                return false;

            int faceWidth;
            int faceHeight;
            for (var i = 0; i < (int)CubeMapFace.MaxCubeMapFaces; ++i)
                _faceImages.Add(null);

            if (image.IsCubemap())
            {
                _faceImages[(int)CubeMapFace.PositiveX] = image;
                _faceImages[(int)CubeMapFace.NegativeX] = _faceImages[(int)CubeMapFace.PositiveX]!.GetNextSibling();
                _faceImages[(int)CubeMapFace.PositiveY] = _faceImages[(int)CubeMapFace.NegativeX]!.GetNextSibling();
                _faceImages[(int)CubeMapFace.NegativeY] = _faceImages[(int)CubeMapFace.PositiveY]!.GetNextSibling();
                _faceImages[(int)CubeMapFace.PositiveZ] = _faceImages[(int)CubeMapFace.NegativeY]!.GetNextSibling();
                _faceImages[(int)CubeMapFace.NegativeZ] = _faceImages[(int)CubeMapFace.PositiveZ]!.GetNextSibling();
            }
            else
            {
                var layout = imageElement.GetAttribute("layout").ToLowerInvariant() switch
                {
                    "horizontalnvidia" => CubeMapLayout.HorizontalNvidia,
                    "horizontalcross" => CubeMapLayout.HorizontalCross,
                    "verticalcross" => CubeMapLayout.VerticalCross,
                    "blender" => CubeMapLayout.Blender,
                    _ => CubeMapLayout.Horizontal
                };

                switch (layout)
                {
                    case CubeMapLayout.Horizontal:
                        faceWidth = image.Width / (int)CubeMapFace.MaxCubeMapFaces;
                        faceHeight = image.Height;
                        _faceImages[(int)CubeMapFace.PositiveZ] = GetTileImage(image, 0, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveX] = GetTileImage(image, 1, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeZ] = GetTileImage(image, 2, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeX] = GetTileImage(image, 3, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveY] = GetTileImage(image, 4, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeY] = GetTileImage(image, 5, 0, faceWidth, faceHeight);
                        break;

                    case CubeMapLayout.HorizontalNvidia:
                        faceWidth = image.Width / (int)CubeMapFace.MaxCubeMapFaces;
                        faceHeight = image.Height;
                        for (var i = 0; i < (int)CubeMapFace.MaxCubeMapFaces; ++i)
                            _faceImages[i] = GetTileImage(image, i, 0, faceWidth, faceHeight);
                        break;

                    case CubeMapLayout.HorizontalCross:
                        faceWidth = image.Width / 4;
                        faceHeight = image.Height / 3;
                        _faceImages[(int)CubeMapFace.PositiveY] = GetTileImage(image, 1, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeX] = GetTileImage(image, 0, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveZ] = GetTileImage(image, 1, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveX] = GetTileImage(image, 2, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeZ] = GetTileImage(image, 3, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeY] = GetTileImage(image, 1, 2, faceWidth, faceHeight);
                        break;

                    case CubeMapLayout.VerticalCross:
                        faceWidth = image.Width / 3;
                        faceHeight = image.Height / 4;
                        _faceImages[(int)CubeMapFace.PositiveY] = GetTileImage(image, 1, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeX] = GetTileImage(image, 0, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveZ] = GetTileImage(image, 1, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveX] = GetTileImage(image, 2, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeY] = GetTileImage(image, 1, 2, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeZ] = GetTileImage(image, 1, 3, faceWidth, faceHeight);
                        if (_faceImages[(int)CubeMapFace.NegativeZ] is not null)
                        {
                            _faceImages[(int)CubeMapFace.NegativeZ]!.FlipVertical();
                            _faceImages[(int)CubeMapFace.NegativeZ]!.FlipHorizontal();
                        }
                        break;

                    case CubeMapLayout.Blender:
                        faceWidth = image.Width / 3;
                        faceHeight = image.Height / 2;
                        _faceImages[(int)CubeMapFace.NegativeX] = GetTileImage(image, 0, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeZ] = GetTileImage(image, 1, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveX] = GetTileImage(image, 2, 0, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.NegativeY] = GetTileImage(image, 0, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveY] = GetTileImage(image, 1, 1, faceWidth, faceHeight);
                        _faceImages[(int)CubeMapFace.PositiveZ] = GetTileImage(image, 2, 1, faceWidth, faceHeight);
                        break;
                }
            }
        }
        // Face per image
        else
        {
            var faceElement = textureElement.GetChild("face");
            while (faceElement.NotNull)
            {
                var name = faceElement.GetAttribute("name");

                // If path is empty, add the XML file path
                if (!name.Contains('/') && !name.Contains('\\'))
                    name = texturePath + name;

                _faceImages.Add(cache.GetTempResource<Image>(name));
                cache.StoreResourceDependency(this, name);

                faceElement = faceElement.GetNext("face");
            }
        }

        // Load spherical harmonics, if present.
        _cachedSphericalHarmonics = null;
        var shElement = textureElement.GetChild("sh");
        if (shElement.NotNull)
        {
            _cachedSphericalHarmonics = new SphericalHarmonicsDot9
            {
                Ar = shElement.GetVector4("ar"),
                Ag = shElement.GetVector4("ag"),
                Ab = shElement.GetVector4("ab"),
                Br = shElement.GetVector4("br"),
                Bg = shElement.GetVector4("bg"),
                Bb = shElement.GetVector4("bb"),
                C = shElement.GetVector4("c")
            };
        }

        // Precalculate mip levels if async loading
        if (AsyncLoadState == AsyncLoadState.Loading)
        {
            foreach (var faceImage in _faceImages)
                faceImage?.PrecalculateLevels();
        }

        // Calculate width and memory use
        var memoryUse = 0;
        _width = 0;
        foreach (var faceImage in _faceImages)
        {
            if (faceImage is not null)
            {
                memoryUse += faceImage.MemoryUse;

                if (_width == 0)
                    _width = faceImage.Width;

                if (faceImage.Width != _width || faceImage.Height != _width)
                    return false;
            }
        }

        MemoryUse = memoryUse;
        return true;
    }

    /// <summary>Return face images.</summary>
    public IReadOnlyList<Image?> GetImages() => _faceImages;

    /// <summary>Return parameters XML.</summary>
    public XMLFile? GetParametersXML() => _parametersXml;

    /// <summary>Return image data from a face's zero mip level.</summary>
    public Image? GetImage(CubeMapFace face) => _faceImages[(int)face];

    /// <summary>Return mip level used for SH calculation.</summary>
    public int GetSphericalHarmonicsMipLevel()
    {
        var maxLevel = (int)MathDefs.LogBaseTwo((uint)_width);
        var bestLevelCountedFromSmallest = Math.Min(maxLevel, (int)MathDefs.LogBaseTwo(8)); // 8x8 should be enough for SH
        return maxLevel - bestLevelCountedFromSmallest;
    }

    /// <summary>Return decompressed cube image mip level.</summary>
    public ImageCube GetDecompressedImageLevel(int index)
    {
        var copy = new ImageCube();

        copy._parametersXml = _parametersXml;
        copy._faceImages.AddRange(_faceImages);
        copy._width = Math.Max(1, _width >> index);

        for (var i = 0; i < copy._faceImages.Count; ++i)
        {
            if (copy._faceImages[i] is not null)
                copy._faceImages[i] = copy._faceImages[i]!.GetDecompressedImageLevel(index);
        }

        return copy;
    }

    /// <summary>Return decompressed cube image.</summary>
    public ImageCube GetDecompressedImage() => GetDecompressedImageLevel(0);

    /// <summary>Return nearest pixel color at given direction.</summary>
    public Color SampleNearest(Vector3f direction)
    {
        var projection = ProjectDirectionOnFaceTexel(direction);
        var texel = projection.Texel;
        return _faceImages[(int)projection.Face]!.GetPixel(texel.X, texel.Y);
    }

    /// <summary>Return offset from the center of the unit cube for given texel.</summary>
    public Vector3f ProjectTexelOnCubeLevel(CubeMapFace face, int x, int y, int level)
    {
        var u = (x + 0.5f) / (_width >> level);
        var v = (y + 0.5f) / (_width >> level);
        return ProjectUVOnCube(face, new Vector2f(u, v));
    }

    /// <summary>Return offset from the center of the unit cube for given texel (assuming zero mip level).</summary>
    public Vector3f ProjectTexelOnCube(CubeMapFace face, int x, int y)
    {
        return ProjectTexelOnCubeLevel(face, x, y, 0);
    }

    /// <summary>Project direction on texel of cubemap face.</summary>
    public (CubeMapFace Face, Vector2i Texel) ProjectDirectionOnFaceTexel(Vector3f direction)
    {
        var faceUv = ProjectDirectionOnFace(direction);
        var uv = faceUv.Uv;
        var x = Math.Clamp((int)MathF.Floor(uv.X * _width), 0, _width);
        var y = Math.Clamp((int)MathF.Floor(uv.Y * _width), 0, _width);
        return (faceUv.Face, new Vector2i(x, y));
    }

    /// <summary>Calculate spherical harmonics for the cube map.</summary>
    public SphericalHarmonicsColor9 CalculateSphericalHarmonics()
    {
        var result = new SphericalHarmonicsColor9();
        var weightSum = 0.0f;

        for (var i = 0; i < 6; ++i)
        {
            var face = (CubeMapFace)i;

            var faceImage = GetImage(face);
            if (faceImage is null)
                continue;

            var maxLevel = faceImage.IsCompressed() ? faceImage.NumCompressedLevels - 1 : int.MaxValue;
            var bestLevel = Math.Min(maxLevel, GetSphericalHarmonicsMipLevel());
            var bestLevelWidth = _width >> bestLevel;
            var decompressedImage = faceImage.GetDecompressedImageLevel(bestLevel);
            if (decompressedImage is null)
                continue;

            for (var y = 0; y < bestLevelWidth; ++y)
            {
                for (var x = 0; x < bestLevelWidth; ++x)
                {
                    var sample = decompressedImage.GetPixel(x, y).GammaToLinear();
                    var offset = ProjectTexelOnCubeLevel(face, x, y, bestLevel);
                    var distance = offset.Length;
                    var weight = 1.0f / (distance * distance * distance);
                    var direction = offset / distance;

                    result += new SphericalHarmonicsColor9(direction, sample) * weight;
                    weightSum += weight;
                }
            }
        }

        if (weightSum > 0.0f)
            result *= 4.0f * MathF.PI / weightSum;
        return result;
    }

    /// <summary>Return cached spherical harmonics or recalculate from image if cache is not available.</summary>
    public SphericalHarmonicsDot9 GetOrCreateSphericalHarmonics()
    {
        if (_cachedSphericalHarmonics is not null)
            return _cachedSphericalHarmonics.Value;
        return new SphericalHarmonicsDot9(CalculateSphericalHarmonics());
    }

    /// <summary>Project UV onto cube.</summary>
    public static Vector3f ProjectUVOnCube(CubeMapFace face, Vector2f uv)
    {
        // Convert from [0, 1] to [-1, 1]
        var u = uv.X * 2.0f - 1.0f;
        var v = uv.Y * 2.0f - 1.0f;

        return face switch
        {
            CubeMapFace.PositiveX => new Vector3f(1, -v, -u),
            CubeMapFace.NegativeX => new Vector3f(-1, -v, u),
            CubeMapFace.PositiveY => new Vector3f(u, 1, v),
            CubeMapFace.NegativeY => new Vector3f(u, -1, -v),
            CubeMapFace.PositiveZ => new Vector3f(u, -v, 1),
            CubeMapFace.NegativeZ => new Vector3f(-u, -v, -1),
            _ => Vector3f.Zero
        };
    }

    /// <summary>Project direction onto cubemap.</summary>
    public static (CubeMapFace Face, Vector2f Uv) ProjectDirectionOnFace(Vector3f direction)
    {
        var x = direction.X;
        var y = direction.Y;
        var z = direction.Z;

        static bool TestAxis(float axis, float a, float b)
            => 2 * axis + MathDefs.LargeEpsilon > Math.Abs(a) + Math.Abs(b);

        (CubeMapFace Face, Vector2f Uv) result;
        if (TestAxis(x, y, z))
            result = (CubeMapFace.PositiveX, new Vector2f(-z / x, -y / x)); // x =  1, y = -v, z = -u
        else if (TestAxis(-x, y, z))
            result = (CubeMapFace.NegativeX, new Vector2f(z / -x, -y / -x)); // x = -1, y = -v, z =  u
        else if (TestAxis(y, x, z))
            result = (CubeMapFace.PositiveY, new Vector2f(x / y, z / y)); // x =  u, y =  1, z =  v
        else if (TestAxis(-y, x, z))
            result = (CubeMapFace.NegativeY, new Vector2f(x / -y, -z / -y)); // x =  u, y = -1, z = -v
        else if (TestAxis(z, x, y))
            result = (CubeMapFace.PositiveZ, new Vector2f(x / y, -y / z)); // x =  u, y = -v, z =  1
        else
            result = (CubeMapFace.NegativeZ, new Vector2f(-x / -y, -y / -z)); // x = -u, y = -v, z = -1

        // Convert from [-1, 1] to [0, 1]
        result.Uv.X = result.Uv.X * 0.5f + 0.5f;
        result.Uv.Y = result.Uv.Y * 0.5f + 0.5f;
        return result;
    }
}
