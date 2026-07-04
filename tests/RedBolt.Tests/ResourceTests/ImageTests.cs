using RedBolt;

namespace RedBolt.Tests;

public sealed class ImageTests
{
    [Fact]
    public void Pixel_roundtrip_and_clear_work()
    {
        var image = new Image("Images/Test");
        Assert.True(image.SetSize(2, 2, 4));
        image.Clear(Color.Transparent);
        image.SetPixel(0, 0, new Color(1.0f, 0.5f, 0.25f, 1.0f));

        Assert.True(image.GetPixel(0, 0).ApproximatelyEquivalent(new Color(1.0f, 127.0f / 255.0f, 63.0f / 255.0f, 1.0f), 0.001f));
        Assert.Equal(image.GetPixel(0, 0).ToUInt(), image.GetPixelInt(0, 0));
        Assert.Equal(TextureFormat.RGBA8, image.GetGPUFormat());
    }

    [Fact]
    public void Bilinear_resize_and_mip_helpers_work()
    {
        var image = new Image("Images/Test");
        Assert.True(image.SetSize(2, 2, 4));
        image.SetPixel(0, 0, new Color(1, 0, 0));
        image.SetPixel(1, 0, new Color(0, 1, 0));
        image.SetPixel(0, 1, new Color(0, 0, 1));
        image.SetPixel(1, 1, new Color(1, 1, 1));

        var center = image.GetPixelBilinear(0.5f, 0.5f);
        Assert.True(center.ApproximatelyEquivalent(new Color(0.5f, 0.5f, 0.5f, 1.0f), 0.01f));

        var mip = image.GetNextLevel();
        Assert.Equal(new IntVector3(1, 1, 1), mip.GetSize());
        Assert.True(mip.GetPixel(0, 0).ApproximatelyEquivalent(center, 0.01f));

        var subimage = image.GetSubimage(new IntRect(1, 0, 2, 1));
        Assert.NotNull(subimage);
        Assert.Equal(new IntVector3(1, 1, 1), subimage!.GetSize());
        Assert.True(image.Resize(4, 4));
        Assert.Equal(4, image.GetWidth());
        Assert.Equal(4, image.GetHeight());
    }

    [Fact]
    public void Trilinear_and_rgba_conversion_work()
    {
        var image = new Image("Images/Volume");
        Assert.True(image.SetSize(2, 2, 2, 1));
        image.SetPixelInt(0, 0, 0, 0x00000000u);
        image.SetPixelInt(1, 0, 0, 0x000000ffu);
        image.SetPixelInt(0, 1, 0, 0x000000ffu);
        image.SetPixelInt(1, 1, 0, 0x00000000u);
        image.SetPixelInt(0, 0, 1, 0x000000ffu);
        image.SetPixelInt(1, 0, 1, 0x00000000u);
        image.SetPixelInt(0, 1, 1, 0x00000000u);
        image.SetPixelInt(1, 1, 1, 0x000000ffu);

        var sampled = image.GetPixelTrilinear(0.5f, 0.5f, 0.5f);
        Assert.True(sampled.ApproximatelyEquivalent(new Color(0.5f, 0.5f, 0.5f, 1.0f), 0.02f));

        var rgba = image.ConvertToRGBA();
        Assert.Equal(4u, rgba.GetComponents());
        Assert.True(rgba.HasAlphaChannel());
    }

    [Fact]
    public void ImageCube_projection_and_sampling_work()
    {
        var cube = new ImageCube("Images/Cube");
        foreach (var pair in new[]
                 {
                     (CubeMapFace.PositiveX, new Color(1, 0, 0)),
                     (CubeMapFace.NegativeX, new Color(0, 1, 0)),
                     (CubeMapFace.PositiveY, new Color(0, 0, 1)),
                     (CubeMapFace.NegativeY, new Color(1, 1, 0)),
                     (CubeMapFace.PositiveZ, new Color(1, 0, 1)),
                     (CubeMapFace.NegativeZ, new Color(0, 1, 1))
                 })
        {
            var image = new Image(pair.Item1.ToString());
            Assert.True(image.SetSize(2, 2, 4));
            image.Clear(pair.Item2);
            cube.SetImage(pair.Item1, image);
        }

        Assert.True(cube.SampleNearest(Vector3.Right).ApproximatelyEquivalent(new Color(1, 0, 0), 0.001f));
        Assert.True(cube.SampleNearest(Vector3.Forward).ApproximatelyEquivalent(new Color(1, 0, 1), 0.001f));

        var projected = ImageCube.ProjectUVOnCube(CubeMapFace.PositiveZ, new Vector2(0.5f, 0.5f)).NormalizedOrDefault;
        Assert.True(projected.ApproximatelyEquivalent(Vector3.Forward, 0.001f));

        var (face, uv) = ImageCube.ProjectDirectionOnFace(Vector3.Up);
        Assert.Equal(CubeMapFace.PositiveY, face);
        Assert.True(uv.ApproximatelyEquivalent(new Vector2(0.5f, 0.5f), 0.001f));

        var texel = cube.ProjectDirectionOnFaceTexel(Vector3.Forward);
        Assert.Equal(CubeMapFace.PositiveZ, texel.Face);
        Assert.Equal(new IntVector2(1, 1), texel.Texel);
        Assert.True(cube.ProjectTexelOnCube(CubeMapFace.PositiveZ, 1, 1).Z > 0.0f);
    }
}
