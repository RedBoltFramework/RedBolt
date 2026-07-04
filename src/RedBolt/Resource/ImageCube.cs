namespace RedBolt;

public sealed class ImageCube : Resource
{
    private readonly Image?[] faceImages = new Image[6];

    public ImageCube(string name)
        : base(name)
    {
    }

    public override bool BeginLoad(Stream source) => false;

    public IReadOnlyList<Image?> GetImages() => faceImages;

    public XMLFile? GetParametersXML() => null;

    public Image? GetImage(CubeMapFace face) => faceImages[(int)face];

    public void SetImage(CubeMapFace face, Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        image.Cubemap = true;
        faceImages[(int)face] = image;
        LinkSiblings();
    }

    public uint GetSphericalHarmonicsMipLevel() => 0u;

    public ImageCube GetDecompressedImageLevel(uint index)
    {
        var cube = new ImageCube(Name + "#level");
        foreach (CubeMapFace face in Enum.GetValues<CubeMapFace>())
        {
            var image = GetImage(face);
            if (image is null)
                continue;

            var mip = image;
            for (var i = 0u; i < index; ++i)
                mip = mip.GetNextLevel();
            cube.SetImage(face, mip);
        }

        return cube;
    }

    public ImageCube GetDecompressedImage() => GetDecompressedImageLevel(0u);

    public Color SampleNearest(Vector3 direction)
    {
        var (face, texel) = ProjectDirectionOnFaceTexel(direction);
        var image = GetImage(face);
        return image is null ? Color.Transparent : image.GetPixel(texel.X, texel.Y);
    }

    public Vector3 ProjectTexelOnCube(CubeMapFace face, int x, int y)
    {
        return ProjectTexelOnCubeLevel(face, x, y, 0u);
    }

    public Vector3 ProjectTexelOnCubeLevel(CubeMapFace face, int x, int y, uint level)
    {
        var image = GetImage(face);
        if (image is null)
            return Vector3.Zero;

        var width = Math.Max(image.GetWidth() >> (int)level, 1);
        var uv = new Vector2((x + 0.5f) / width, (y + 0.5f) / width);
        return ProjectUVOnCube(face, uv);
    }

    public (CubeMapFace Face, IntVector2 Texel) ProjectDirectionOnFaceTexel(Vector3 direction)
    {
        var (face, uv) = ProjectDirectionOnFace(direction);
        var image = GetImage(face);
        if (image is null || image.GetWidth() <= 0)
            return (face, IntVector2.Zero);

        var width = image.GetWidth();
        var x = Math.Clamp((int)(uv.X * width), 0, width - 1);
        var y = Math.Clamp((int)(uv.Y * width), 0, width - 1);
        return (face, new IntVector2(x, y));
    }

    public static Vector3 ProjectUVOnCube(CubeMapFace face, Vector2 uv)
    {
        var u = uv.X * 2.0f - 1.0f;
        var v = 1.0f - uv.Y * 2.0f;
        return face switch
        {
            CubeMapFace.PositiveX => new Vector3(1.0f, v, -u),
            CubeMapFace.NegativeX => new Vector3(-1.0f, v, u),
            CubeMapFace.PositiveY => new Vector3(u, 1.0f, -v),
            CubeMapFace.NegativeY => new Vector3(u, -1.0f, v),
            CubeMapFace.PositiveZ => new Vector3(u, v, 1.0f),
            CubeMapFace.NegativeZ => new Vector3(-u, v, -1.0f),
            _ => Vector3.Zero
        };
    }

    public static (CubeMapFace Face, Vector2 Uv) ProjectDirectionOnFace(Vector3 direction)
    {
        var normalized = direction.NormalizedOrDefault;
        var absX = Math.Abs(normalized.X);
        var absY = Math.Abs(normalized.Y);
        var absZ = Math.Abs(normalized.Z);

        if (absX >= absY && absX >= absZ)
        {
            if (normalized.X >= 0.0f)
                return (CubeMapFace.PositiveX, new Vector2((1.0f - normalized.Z / absX) * 0.5f, (1.0f - normalized.Y / absX) * 0.5f));
            return (CubeMapFace.NegativeX, new Vector2((normalized.Z / absX + 1.0f) * 0.5f, (1.0f - normalized.Y / absX) * 0.5f));
        }

        if (absY >= absX && absY >= absZ)
        {
            if (normalized.Y >= 0.0f)
                return (CubeMapFace.PositiveY, new Vector2((normalized.X / absY + 1.0f) * 0.5f, (normalized.Z / absY + 1.0f) * 0.5f));
            return (CubeMapFace.NegativeY, new Vector2((normalized.X / absY + 1.0f) * 0.5f, (1.0f - normalized.Z / absY) * 0.5f));
        }

        if (normalized.Z >= 0.0f)
            return (CubeMapFace.PositiveZ, new Vector2((normalized.X / absZ + 1.0f) * 0.5f, (1.0f - normalized.Y / absZ) * 0.5f));
        return (CubeMapFace.NegativeZ, new Vector2((1.0f - normalized.X / absZ) * 0.5f, (1.0f - normalized.Y / absZ) * 0.5f));
    }

    private void LinkSiblings()
    {
        Image? previous = null;
        foreach (var image in faceImages)
        {
            if (previous is not null)
                previous.SetNextSibling(image);
            previous = image;
        }

        if (previous is not null)
            previous.SetNextSibling(null);
    }
}
