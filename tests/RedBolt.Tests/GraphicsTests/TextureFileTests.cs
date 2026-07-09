using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TextureFileTests
{
    private readonly HeadlessEngineFixture _fixture;

    public TextureFileTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Repo_backed_rbfx_dds_textures_load_through_resource_cache()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var texture = Resources.GetResource<Texture2D>("Textures/StoneDiffuse.dds");

            Assert.True(texture.Width > 0);
            Assert.True(texture.Height > 0);
            Assert.Equal(TextureFormat.RGBA8, texture.Format);
            Assert.Equal(11u, texture.GetLevels());
            Assert.NotNull(texture.GetData(0));
            Assert.Equal(texture.Width * texture.Height * 4, texture.GetData(0)!.Length);
            Assert.NotNull(texture.GetData(1));
            Assert.Equal((texture.Width / 2) * (texture.Height / 2) * 4, texture.GetData(1)!.Length);
            Assert.Contains(texture.GetData(0)!, static value => value != 0);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }

    [Fact]
    public void Texture_cube_xml_loads_through_resource_cache()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.TextureCubeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "Textures"));
        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "Textures", "Cube.xml"), """
                <cubemap>
                    <face name="PosX.bmp" />
                    <face name="NegX.bmp" />
                    <face name="PosY.bmp" />
                    <face name="NegY.bmp" />
                    <face name="PosZ.bmp" />
                    <face name="NegZ.bmp" />
                </cubemap>
                """);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "PosX.bmp"), 2, 2, 255, 0, 0);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "NegX.bmp"), 2, 2, 0, 255, 0);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "PosY.bmp"), 2, 2, 0, 0, 255);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "NegY.bmp"), 2, 2, 255, 255, 0);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "PosZ.bmp"), 2, 2, 255, 0, 255);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "NegZ.bmp"), 2, 2, 0, 255, 255);
            Resources.AddResourceDir(tempRoot);

            var texture = Resources.GetResource<TextureCube>("Textures/Cube.xml");

            Assert.True(texture.Width > 0);
            Assert.Equal(texture.Width, texture.Height);
            Assert.Equal(TextureFormat.RGBA8, texture.Format);
            Assert.NotNull(texture.GetData(CubeMapFace.PositiveX, 0u));
            Assert.Equal(texture.Width * texture.Height * 4, texture.GetData(CubeMapFace.PositiveX, 0u)!.Length);
            Assert.Contains(texture.GetData(CubeMapFace.PositiveX, 0u)!, static value => value != 0);
        }
        finally
        {
            Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Texture_array_xml_loads_through_resource_cache()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.TextureArrayTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "Textures"));
        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "Textures", "Array.xml"), """
                <texturearray>
                    <layer name="Layer0.bmp" />
                    <layer name="Layer1.bmp" />
                </texturearray>
                """);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "Layer0.bmp"), 2, 2, 32, 64, 96);
            WriteSolidBmp(Path.Combine(tempRoot, "Textures", "Layer1.bmp"), 2, 2, 128, 160, 192);
            Resources.AddResourceDir(tempRoot);

            var texture = Resources.GetResource<Texture2DArray>("Textures/Array.xml");

            Assert.Equal(2u, texture.Layers);
            Assert.Equal(2, texture.Width);
            Assert.Equal(2, texture.Height);
            Assert.Equal(TextureFormat.RGBA8, texture.Format);
            Assert.NotNull(texture.GetData(0u, 0u));
            Assert.Equal(texture.Width * texture.Height * 4, texture.GetData(0u, 0u)!.Length);
            Assert.Contains(texture.GetData(0u, 0u)!, static value => value != 0);
        }
        finally
        {
            Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Texture3d_color_lut_xml_loads_through_resource_cache()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.Texture3DTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "Textures"));

        try
        {
            var imagePath = Path.Combine(tempRoot, "Textures", "Lut.bmp");
            var xmlPath = Path.Combine(tempRoot, "Textures", "Lut.xml");
            WriteColorLutBmp(imagePath);
            File.WriteAllText(xmlPath, "<texture3d><colorlut name=\"Lut.bmp\" /></texture3d>");

            Resources.AddResourceDir(tempRoot);
            var texture = Resources.GetResource<Texture3D>("Textures/Lut.xml");

            Assert.Equal(16, texture.Width);
            Assert.Equal(16, texture.Height);
            Assert.Equal(16, texture.Depth);
            Assert.Equal(TextureFormat.RGBA8, texture.Format);
            Assert.NotNull(texture.GetData(0u));
            Assert.Equal(16 * 16 * 16 * 4, texture.GetData(0u)!.Length);
            Assert.Contains(texture.GetData(0u)!, static value => value != 0);
        }
        finally
        {
            Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void WriteColorLutBmp(string fileName)
    {
        const int width = 16 * 16;
        const int height = 16;
        var pixels = new byte[width * height * 4];
        for (var z = 0; z < 16; ++z)
        {
            for (var y = 0; y < 16; ++y)
            {
                for (var x = 0; x < 16; ++x)
                {
                    var pixelIndex = ((y * width) + z * 16 + x) * 4;
                    pixels[pixelIndex + 0] = (byte)(x * 17);
                    pixels[pixelIndex + 1] = (byte)(y * 17);
                    pixels[pixelIndex + 2] = (byte)(z * 17);
                    pixels[pixelIndex + 3] = 255;
                }
            }
        }

        using var stream = File.Create(fileName);
        using var writer = new BinaryWriter(stream);
        WriteBmp(writer, width, height, pixels);
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

        using var stream = File.Create(fileName);
        using var writer = new BinaryWriter(stream);
        WriteBmp(writer, width, height, pixels);
    }

    private static void WriteBmp(BinaryWriter writer, int width, int height, byte[] pixels)
    {
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
