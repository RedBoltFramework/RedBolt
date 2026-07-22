using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ImageTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ImageTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Pixel_roundtrip_and_clear_work()
    {
        var image = new Image();
        Assert.True(image.SetSize(2, 2, 4));
        image.Clear(Color.TransparentBlack);
        image.SetPixel(0, 0, new Color(1.0f, 0.5f, 0.25f, 1.0f));

        Assert.True(image.GetPixel(0, 0).Equals(new Color(1.0f, 127.0f / 255.0f, 63.0f / 255.0f, 1.0f), 0.001f));
        Assert.Equal(image.GetPixel(0, 0).ToUInt(), image.GetPixelInt(0, 0));
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, image.GetGPUFormat());
    }

    [Fact]
    public void Gpu_format_matches_native_component_mapping()
    {
        using var image = new Image();

        Assert.True(image.SetSize(2, 2, 1));
        Assert.Equal(TextureFormat.TexFormatR8Unorm, image.GetGPUFormat());

        Assert.True(image.SetSize(2, 2, 2));
        Assert.Equal(TextureFormat.TexFormatRg8Unorm, image.GetGPUFormat());

        Assert.True(image.SetSize(2, 2, 3));
        Assert.Equal(TextureFormat.TexFormatUnknown, image.GetGPUFormat());

        Assert.True(image.SetSize(2, 2, 4));
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, image.GetGPUFormat());
    }

    [Fact]
    public void Bilinear_resize_and_mip_helpers_work()
    {
        var image = new Image();
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
    public void Mip_levels_are_owned_only_after_precalculation()
    {
        var image = new Image();
        Assert.True(image.SetSize(4, 4, 1));
        image.SetData(Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray());

        var first = image.GetNextLevel();
        var second = image.GetNextLevel();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);

        var levels = new List<Image> { first! };
        image.GetLevels(levels);
        Assert.Single(levels);
        Assert.Same(image, levels[0]);

        image.PrecalculateLevels();
        var cached = image.GetNextLevel();
        Assert.Same(cached, image.GetNextLevel());
        image.GetLevels(levels);
        Assert.Equal(3, levels.Count);
    }

    [Fact]
    public void Compressed_level_is_mutable_value_data_with_borrowed_pixels()
    {
        var data = new byte[4];
        var level = new CompressedLevel
        {
            Data = data,
            Format = TextureFormat.TexFormatRgba8Unorm,
            Width = 1,
            Height = 1,
            Depth = 1,
            DataSize = data.Length
        };
        var copy = level;

        copy.Width = 2;
        copy.Data.Span[0] = 42;

        Assert.Equal(1, level.Width);
        Assert.Equal(42, level.Data.Span[0]);
    }

    [Fact]
    public void Compressed_level_decompresses_etc2_through_texture_format()
    {
        var level = new CompressedLevel
        {
            Data = new byte[8],
            Format = TextureFormat.TexFormatEtc2Rgb8Unorm,
            Width = 4,
            Height = 4,
            Depth = 1,
            DataSize = 8
        };

        Assert.True(level.Decompress(new byte[4 * 4 * 4]));
    }

    [Fact]
    public void Dds_header_types_preserve_native_pod_layout_and_format_mapping()
    {
        Assert.Equal(8, System.Runtime.InteropServices.Marshal.SizeOf<DDColorKey>());
        Assert.Equal(32, System.Runtime.InteropServices.Marshal.SizeOf<DDPixelFormat>());
        Assert.Equal(16, System.Runtime.InteropServices.Marshal.SizeOf<DDSCaps2>());
        Assert.Equal(20, System.Runtime.InteropServices.Marshal.SizeOf<DDSHeader10>());
        Assert.Equal(124, System.Runtime.InteropServices.Marshal.SizeOf<DDSurfaceDesc2>());

        var pixelFormat = new DDPixelFormat
        {
            RgbBitCount = 32,
            RBitMask = 0x000000ff,
            GBitMask = 0x0000ff00,
            BBitMask = 0x00ff0000,
            RgbAlphaBitMask = 0xff000000
        };
        Assert.Equal(pixelFormat.RgbBitCount, pixelFormat.YuvBitCount);
        Assert.True(ImageDds.AreTextureComponentsMatching(pixelFormat, TextureFormat.TexFormatRgba8Unorm));

        var copy = pixelFormat;
        copy.RBitMask = 0;
        Assert.NotEqual(copy.RBitMask, pixelFormat.RBitMask);

        Assert.Equal(0x31545844u, ImageDds.MakeFourCC('D', 'X', 'T', '1'));
        Assert.Equal(TextureFormat.TexFormatRgba16Unorm, ImageDds.PickTextureFormat(default, 11));
        Assert.Equal(TextureFormat.TexFormatBc1UnormSrgb, ImageDds.PickTextureFormat(default, 72));
    }

    [Fact]
    public void Pixel_changes_and_resize_preserve_precalculated_mip_ownership()
    {
        var image = new Image();
        Assert.True(image.SetSize(4, 4, 1));
        image.PrecalculateLevels();
        var cached = image.GetNextLevel();

        image.SetPixelInt(0, 0, 255);
        Assert.Same(cached, image.GetNextLevel());

        Assert.True(image.Resize(8, 8));
        Assert.Same(cached, image.GetNextLevel());

        image.SetData(new byte[8 * 8]);
        Assert.NotSame(cached, image.GetNextLevel());
    }

    [Fact]
    public void Compressed_block_flips_are_reversible()
    {
        var image = new Image();
        Assert.True(image.LoadFile(Path.Combine(RbfxTestData.Data, "Textures", "StoneDiffuse.dds")));
        var original = image.GetData().ToArray();

        Assert.True(image.FlipHorizontal());
        Assert.True(image.FlipHorizontal());
        Assert.Equal(original, image.GetData());

        Assert.True(image.FlipVertical());
        Assert.True(image.FlipVertical());
        Assert.Equal(original, image.GetData());
    }

    [Fact]
    public void Trilinear_and_rgba_conversion_work()
    {
        var image = new Image();
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
        Assert.Equal(4, rgba!.GetComponents());
        Assert.True(rgba.HasAlphaChannel());
    }

    [Fact]
    public void Png_save_subimage_and_level_management_work()
    {
        var source = new Image();
        Assert.True(source.SetSize(2, 2, 4));
        source.Clear(new Color(1, 0, 0, 1));
        var destination = new Image();
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
        var cube = LoadImageCube(
            [(255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 0), (255, 0, 255), (0, 255, 255)],
            out var tempRoot);
        try
        {
            Assert.True(cube.SampleNearest(Vector3f.Right).Equals(new Color(1, 0, 0), 0.001f));
            Assert.True(cube.SampleNearest(Vector3f.Forward).Equals(new Color(1, 0, 1), 0.001f));

            var projected = ImageCube.ProjectUVOnCube(CubeMapFace.PositiveZ, new Vector2f(0.5f, 0.5f)).NormalizedOrDefault();
            Assert.True(projected.ApproximatelyEquivalent(Vector3f.Forward, 0.001f));

            var (face, uv) = ImageCube.ProjectDirectionOnFace(Vector3f.Up);
            Assert.Equal(CubeMapFace.PositiveY, face);
            Assert.True(uv.ApproximatelyEquivalent(new Vector2f(0.5f, 0.5f), 0.001f));

            var texel = cube.ProjectDirectionOnFaceTexel(Vector3f.Forward);
            Assert.Equal(CubeMapFace.PositiveZ, texel.Face);
            Assert.Equal(new Vector2i(0, 1), texel.Texel);
            Assert.True(cube.ProjectTexelOnCube(CubeMapFace.PositiveZ, 1, 1).Z > 0.0f);
        }
        finally
        {
            cube.Dispose();
            _fixture.Resources.RemoveResourceDir(tempRoot);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Image_encoders_and_cube_spherical_harmonics_work()
    {
        var image = new Image();
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

            var cube = LoadImageCube(
                [(128, 64, 32), (128, 64, 32), (128, 64, 32), (128, 64, 32), (128, 64, 32), (128, 64, 32)],
                out var cubeRoot);
            try
            {
                var evaluated = cube.GetOrCreateSphericalHarmonics().Evaluate(Vector3f.Forward);
                Assert.True(evaluated.X > 0 && evaluated.Y > 0 && evaluated.Z > 0);
            }
            finally
            {
                cube.Dispose();
                _fixture.Resources.RemoveResourceDir(cubeRoot);
                Directory.Delete(cubeRoot, recursive: true);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Repo_backed_rbfx_dds_image_loads_and_decompresses_through_image_resource()
    {
        var image = new Image();

        Assert.True(image.LoadFile(Path.Combine(RbfxTestData.Data, "Textures", "StoneDiffuse.dds")));
        Assert.True(image.IsCompressed());
        Assert.Equal(TextureFormat.TexFormatBc1Unorm, image.CompressedFormat);
        Assert.Equal(TextureFormat.TexFormatBc1Unorm, image.GetCompressedFormat());
        Assert.Equal(TextureFormat.TexFormatBc1Unorm, image.GetGPUFormat());
        Assert.Equal(1024, image.Width);
        Assert.Equal(1024, image.Height);
        Assert.Equal(11, image.NumCompressedLevels);

        var rgba = image.GetDecompressedImage();
        Assert.NotNull(rgba);
        Assert.Equal(4, rgba!.Components);
        Assert.Equal(1024 * 1024 * 4, rgba.GetData().Length);
        Assert.Contains(rgba.GetData(), static value => value != 0);

        var mip = image.GetDecompressedImageLevel(1);
        Assert.NotNull(mip);
        Assert.Equal(512, mip!.Width);
        Assert.Equal(512, mip.Height);
        Assert.Equal(512 * 512 * 4, mip.GetData().Length);
    }

    [Fact]
    public void Dds_dxgi_rgba16_unorm_preserves_native_texture_format_and_data()
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

        var image = new Image();
        Assert.True(image.BeginLoad(new MemoryStream(dds)));
        Assert.Equal(TextureFormat.TexFormatRgba16Unorm, image.CompressedFormat);
        Assert.Equal(4, image.Components);

        var level = image.GetCompressedLevel(0);
        Assert.Equal(8, level.DataSize);
        Assert.Equal(dds[148..156], level.Data.ToArray());
        Assert.Null(image.GetDecompressedImage());
    }

    private static void WriteUInt32(byte[] data, int offset, uint value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);

    [Fact]
    public void Image_cube_xml_loads_through_image_cube_resource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.ImageCubeTests", Guid.NewGuid().ToString("N"));
        var textureDir = Path.Combine(tempRoot, "Textures");
        Directory.CreateDirectory(textureDir);
        var resources = _fixture.Resources;
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

    private ImageCube LoadImageCube(
        IReadOnlyList<(byte R, byte G, byte B)> colors, out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.ImageCube", Guid.NewGuid().ToString("N"));
        var relativeDirectory = Guid.NewGuid().ToString("N");
        var cubeDirectory = Path.Combine(tempRoot, relativeDirectory);
        Directory.CreateDirectory(cubeDirectory);
        _fixture.Resources.AddResourceDir(tempRoot);

        System.IO.File.WriteAllText(Path.Combine(cubeDirectory, "Cube.xml"), """
            <cubemap>
                <face name="Face0.bmp" />
                <face name="Face1.bmp" />
                <face name="Face2.bmp" />
                <face name="Face3.bmp" />
                <face name="Face4.bmp" />
                <face name="Face5.bmp" />
            </cubemap>
            """);
        for (var i = 0; i < colors.Count; ++i)
        {
            var color = colors[i];
            WriteSolidBmp(Path.Combine(cubeDirectory, $"Face{i}.bmp"), 2, 2, color.R, color.G, color.B);
        }

        var cube = new ImageCube();
        cube.SetName($"{relativeDirectory}/Cube.xml");
        using var source = System.IO.File.OpenRead(Path.Combine(cubeDirectory, "Cube.xml"));
        Assert.True(cube.BeginLoad(source));
        return cube;
    }
}
