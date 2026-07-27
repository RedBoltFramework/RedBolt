using RedBolt.IO;
using RedBolt.Resource;
namespace RedBolt.Tests;

public sealed class ImageSaveTests
{
    [Fact]
    public void Save_writes_png_through_stb_and_round_trips()
    {
        var image = new Image();
        Assert.True(image.SetSize(2, 1, 4));
        image.SetData([255, 0, 0, 255, 0, 255, 0, 255]);

        using var stream = new VectorBuffer();
        Assert.True(image.Save(stream));
        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], stream.Buffer[..8].ToArray());

        stream.Seek(0);
        var loaded = new Image();
        Assert.True(loaded.BeginLoad(stream), GetStbiFailureReason());
        Assert.Equal(2, loaded.Width);
        Assert.Equal(1, loaded.Height);
    }

    [Fact]
    public void Save_bmp_writes_through_stb()
    {
        var image = new Image();
        Assert.True(image.SetSize(1, 1, 4));
        image.SetData([255, 0, 0, 255]);
        var fileName = Path.Combine(Path.GetTempPath(), $"RedBolt-{Guid.NewGuid():N}.bmp");

        try
        {
            Assert.True(image.SaveBMP(fileName));
            Assert.Equal([(byte)'B', (byte)'M'], System.IO.File.ReadAllBytes(fileName)[..2]);
        }
        finally
        {
            System.IO.File.Delete(fileName);
        }
    }

    [Fact]
    public void BeginLoad_preserves_source_component_count()
    {
        var source = new Image();
        Assert.True(source.SetSize(2, 1, 3));
        source.SetData([255, 0, 0, 0, 255, 0]);

        using var stream = new VectorBuffer();
        Assert.True(source.Save(stream));
        stream.Seek(0);

        var loaded = new Image();
        Assert.True(loaded.BeginLoad(stream), GetStbiFailureReason());
        Assert.Equal(3, loaded.Components);
        Assert.Equal(source.GetData(), loaded.GetData());
    }

    [Fact]
    public void Get_sdl_surface_uses_component_bit_depth()
    {
        var image = new Image();
        Assert.True(image.SetSize(1, 1, 3));
        image.SetData([255, 0, 0]);

        var surface = image.GetSDLSurface();
        Assert.NotNull(surface);
        try
        {
            Assert.Equal(24, (byte)surface.Format.BitsPerPixel);
        }
        finally
        {
            RedBolt.Bindings.Sdl.SdlNativeBindings.SdlFreeSurface(surface);
        }
    }

    [Fact]
    public void Save_tga_writes_through_stb_and_round_trips()
    {
        var image = new Image();
        Assert.True(image.SetSize(1, 1, 3));
        image.SetData([255, 0, 0]);
        var fileName = Path.Combine(Path.GetTempPath(), $"RedBolt-{Guid.NewGuid():N}.tga");

        try
        {
            Assert.True(image.SaveTGA(fileName));
            using var file = new VectorBuffer(System.IO.File.ReadAllBytes(fileName));
            var loaded = new Image();
            Assert.True(loaded.BeginLoad(file), GetStbiFailureReason());
            Assert.Equal(3, loaded.Components);
            Assert.Equal(image.GetData(), loaded.GetData());
        }
        finally
        {
            System.IO.File.Delete(fileName);
        }
    }

    [Fact]
    public void Load_color_lut_preserves_native_rgb_layout()
    {
        const int width = 16 * 16;
        const int height = 16;
        var data = new byte[width * height * 3];
        for (var i = 0; i < data.Length; ++i)
            data[i] = (byte)i;

        var source = new Image();
        Assert.True(source.SetSize(width, height, 3));
        source.SetData(data);
        using var stream = new VectorBuffer();
        Assert.True(source.Save(stream));
        stream.Seek(0);

        var lut = new Image();
        Assert.True(lut.LoadColorLUT(stream));
        Assert.Equal(16, lut.Width);
        Assert.Equal(16, lut.Height);
        Assert.Equal(16, lut.Depth);
        Assert.Equal(3, lut.Components);
        Assert.Equal(data[..(16 * 3)], lut.GetData()[..(16 * 3)]);
        Assert.Equal(
            data[(15 * 16 * 3)..(16 * 16 * 3)],
            lut.GetData()[(15 * 16 * 16 * 3)..(15 * 16 * 16 * 3 + 16 * 3)]);
    }

    private static string GetStbiFailureReason()
    {
        var reason = RedBolt.Bindings.Stb.StbVorbisNativeBindings.StbiFailureReason();
        var length = reason.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(length >= 0 ? reason[..length] : reason);
    }
}
