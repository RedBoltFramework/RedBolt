using RedBolt;

namespace RedBolt.Tests;

public sealed class ImageTests
{
    [Fact]
    public void Pixel_roundtrip_and_clear_work()
    {
        var image = new Image("Images/Test");
        Assert.True(image.SetSize(2, 2, 4));
        image.Clear(Color.TransparentBlack);
        image.SetPixel(0, 0, new Color(1.0f, 0.5f, 0.25f, 1.0f));

        Assert.True(image.GetPixel(0, 0).Equals(new Color(1.0f, 127.0f / 255.0f, 63.0f / 255.0f, 1.0f), 0.001f));
        Assert.Equal(image.GetPixel(0, 0).ToUInt(), image.GetPixelInt(0, 0));
        Assert.Equal(TextureFormat.RGBA8, image.GetGPUFormat());
    }

    [Fact]
    public void Gpu_format_matches_native_component_mapping()
    {
        using var image = new Image("Images/GpuFormat");

        Assert.True(image.SetSize(2, 2, 1));
        Assert.Equal(TextureFormat.R8, image.GetGPUFormat());

        Assert.True(image.SetSize(2, 2, 2));
        Assert.Equal(TextureFormat.RG8, image.GetGPUFormat());

        Assert.True(image.SetSize(2, 2, 3));
        Assert.Equal(TextureFormat.Unknown, image.GetGPUFormat());

        Assert.True(image.SetSize(2, 2, 4));
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
        Assert.True(center.Equals(new Color(0.5f, 0.5f, 0.5f, 1.0f), 0.01f));
        Assert.Equal(new Color(1, 0, 0), image.GetPixelBilinear(0.25f, 0.25f));

        var mip = image.GetNextLevel();
        Assert.Equal(new Vector3i(1, 1, 1), mip.GetSize());
        Assert.True(mip.GetPixel(0, 0).Equals(center, 0.01f));

        var subimage = image.GetSubimage(new IntRect(1, 0, 2, 1));
        Assert.NotNull(subimage);
        Assert.Equal(new Vector3i(1, 1, 1), subimage!.GetSize());
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
        Assert.True(sampled.Equals(new Color(0.5f, 0.5f, 0.5f, 1.0f), 0.02f));

        var rgba = image.ConvertToRGBA();
        Assert.Equal(4u, rgba.GetComponents());
        Assert.True(rgba.HasAlphaChannel());
    }

    [Fact]
    public void Png_save_subimage_and_level_management_work()
    {
        var source = new Image("Source");
        Assert.True(source.SetSize(2, 2, 4));
        source.Clear(new Color(1, 0, 0, 1));
        var destination = new Image("Destination");
        Assert.True(destination.SetSize(4, 4, 4));
        destination.Clear(Color.TransparentBlack);
        Assert.True(destination.SetSubimage(source, new IntRect(1, 1, 3, 3)));
        Assert.True(destination.GetPixel(1, 1).Equals(new Color(1, 0, 0, 1), 0.001f));

        destination.PrecalculateLevels();
        var levels = new List<Image>();
        destination.GetLevels(levels);
        Assert.Equal(3, levels.Count);
        destination.CleanupLevels();
        levels.Clear();
        destination.GetLevels(levels);
        Assert.Single(levels);

        using var encoded = new MemoryStream();
        Assert.True(destination.Save(encoded));
        Assert.True(encoded.ToArray().AsSpan().StartsWith(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }));
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

        Assert.True(cube.SampleNearest(Vector3f.Right).Equals(new Color(1, 0, 0), 0.001f));
        Assert.True(cube.SampleNearest(Vector3f.Forward).Equals(new Color(1, 0, 1), 0.001f));

        var projected = ImageCube.ProjectUVOnCube(CubeMapFace.PositiveZ, new Vector2f(0.5f, 0.5f)).NormalizedOrDefault();
        Assert.True(projected.ApproximatelyEquivalent(Vector3f.Forward, 0.001f));

        var (face, uv) = ImageCube.ProjectDirectionOnFace(Vector3f.Up);
        Assert.Equal(CubeMapFace.PositiveY, face);
        Assert.True(uv.ApproximatelyEquivalent(new Vector2f(0.5f, 0.5f), 0.001f));

        var texel = cube.ProjectDirectionOnFaceTexel(Vector3f.Forward);
        Assert.Equal(CubeMapFace.PositiveZ, texel.Face);
        Assert.Equal(new Vector2i(1, 1), texel.Texel);
        Assert.True(cube.ProjectTexelOnCube(CubeMapFace.PositiveZ, 1, 1).Z > 0.0f);
    }

    [Fact]
    public void Image_encoders_and_cube_spherical_harmonics_work()
    {
        var image = new Image("Solid");
        Assert.True(image.SetSize(2, 2, 4));
        image.Clear(new Color(0.5f, 0.25f, 0.125f, 1));
        var directory = Path.Combine(Path.GetTempPath(), "RedBolt.ImageEncoders", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.True(image.SaveBMP(Path.Combine(directory, "image.bmp")));
            Assert.True(image.SaveTGA(Path.Combine(directory, "image.tga")));
            Assert.True(image.SavePNG(Path.Combine(directory, "image.png")));
            Assert.True(image.SaveJPG(Path.Combine(directory, "image.jpg"), 90));
            var ddsFileName = Path.Combine(directory, "image.dds");
            Assert.True(image.SaveDDS(ddsFileName));
            Assert.Equal("DDS ", System.Text.Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(ddsFileName), 0, 4));

            var cube = new ImageCube("Cube");
            for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; face = (CubeMapFace)((int)face + 1)) cube.SetImage(face, image);
            var evaluated = cube.GetOrCreateSphericalHarmonics().Evaluate(Vector3f.Forward);
            Assert.True(evaluated.X > 0 && evaluated.Y > 0 && evaluated.Z > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Repo_backed_rbfx_dds_image_loads_and_decompresses_through_image_resource()
    {
        var image = new Image("Textures/StoneDiffuse.dds");

        Assert.True(image.LoadFile(Path.Combine(RbfxTestData.Data, "Textures", "StoneDiffuse.dds")));
        Assert.True(image.IsCompressed());
        Assert.Equal(CompressedFormat.Dxt1, image.CompressedFormat);
        Assert.Equal(TextureFormat.BC1, image.GetCompressedFormat());
        Assert.Equal(TextureFormat.BC1, image.GetGPUFormat());
        Assert.Equal(1024, image.Width);
        Assert.Equal(1024, image.Height);
        Assert.Equal(11u, image.NumCompressedLevels);

        var rgba = image.GetDecompressedImage();
        Assert.NotNull(rgba);
        Assert.Equal(4u, rgba!.Components);
        Assert.Equal(1024 * 1024 * 4, rgba.GetData().Length);
        Assert.Contains(rgba.GetData(), static value => value != 0);

        var mip = image.GetDecompressedImageLevel(1u);
        Assert.NotNull(mip);
        Assert.Equal(512, mip!.Width);
        Assert.Equal(512, mip.Height);
        Assert.Equal(512 * 512 * 4, mip.GetData().Length);
    }

    [Fact]
    public void Dds_dxgi_rgba16_unorm_is_converted_to_rgba_image_data()
    {
        var dds = new byte[148 + 8];
        WriteUInt32(dds, 0, 0x20534444u);
        WriteUInt32(dds, 4, 124u);
        WriteUInt32(dds, 12, 1u);
        WriteUInt32(dds, 16, 1u);
        WriteUInt32(dds, 24, 1u);
        WriteUInt32(dds, 28, 1u);
        WriteUInt32(dds, 76, 32u);
        WriteUInt32(dds, 84, 0x30315844u);
        WriteUInt32(dds, 128, 11u);
        dds[148..156].AsSpan().Clear();
        dds[148] = 0xff;
        dds[149] = 0xff;
        dds[150] = 0x00;
        dds[151] = 0x80;
        dds[154] = 0xff;
        dds[155] = 0xff;

        var image = new Image("Rgba16");
        Assert.True(image.BeginLoad(new MemoryStream(dds)));
        Assert.Equal(CompressedFormat.Rgba, image.CompressedFormat);

        var rgba = image.GetDecompressedImage();
        Assert.NotNull(rgba);
        Assert.Equal(new byte[] { 255, 128, 0, 255 }, rgba!.GetData());
    }

    private static void WriteUInt32(byte[] data, int offset, uint value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);

    [Fact]
    public void Image_cube_xml_loads_through_image_cube_resource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.ImageCubeTests", Guid.NewGuid().ToString("N"));
        var textureDir = Path.Combine(tempRoot, "Textures");
        Directory.CreateDirectory(textureDir);
        var resources = new ResourceCache();
        resources.AddResourceDir(tempRoot);

        try
        {
            System.IO.File.WriteAllText(Path.Combine(textureDir, "Cube.xml"), """
                <cubemap>
                    <face name="Face0.bmp" />
                    <face name="Face1.bmp" />
                    <face name="Face2.bmp" />
                    <face name="Face3.bmp" />
                    <face name="Face4.bmp" />
                    <face name="Face5.bmp" />
                </cubemap>
                """);
            WriteSolidBmp(Path.Combine(textureDir, "Face0.bmp"), 2, 2, 255, 0, 0);
            WriteSolidBmp(Path.Combine(textureDir, "Face1.bmp"), 2, 2, 0, 255, 0);
            WriteSolidBmp(Path.Combine(textureDir, "Face2.bmp"), 2, 2, 0, 0, 255);
            WriteSolidBmp(Path.Combine(textureDir, "Face3.bmp"), 2, 2, 255, 255, 0);
            WriteSolidBmp(Path.Combine(textureDir, "Face4.bmp"), 2, 2, 255, 0, 255);
            WriteSolidBmp(Path.Combine(textureDir, "Face5.bmp"), 2, 2, 0, 255, 255);

            var cube = resources.GetResource<ImageCube>("Textures/Cube.xml");

            Assert.NotNull(cube.GetParametersXML());

            for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; face = (CubeMapFace)((int)face + 1))
            {
                var image = cube.GetImage(face);
                Assert.NotNull(image);
                Assert.True(image!.Width > 0);
                Assert.Equal(image.Width, image.Height);
            }
        }
        finally
        {
            resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void WriteSolidBmp(string fileName, int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        using var stream = System.IO.File.Create(fileName);
        using var writer = new BinaryWriter(stream);

        const int fileHeaderSize = 14;
        const int dibHeaderSize = 40;
        var pixelDataSize = width * height * 4;
        var fileSize = fileHeaderSize + dibHeaderSize + pixelDataSize;

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(fileHeaderSize + dibHeaderSize);
        writer.Write(dibHeaderSize);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            writer.Write(pixels[i + 2]);
            writer.Write(pixels[i + 1]);
            writer.Write(pixels[i + 0]);
            writer.Write(pixels[i + 3]);
        }
    }
}
