namespace RedBolt.Tests;

public sealed class ImageSaveTests
{
    [Fact]
    public void Save_writes_png_through_stb_and_round_trips()
    {
        var image = new Image("Test");
        Assert.True(image.SetSize(2, 1, 4));
        image.SetData([255, 0, 0, 255, 0, 255, 0, 255]);

        using var stream = new MemoryStream();
        Assert.True(image.Save(stream));
        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], stream.ToArray()[..8]);

        stream.Position = 0;
        var loaded = new Image("Loaded");
        Assert.True(loaded.BeginLoad(stream));
        Assert.Equal(2, loaded.Width);
        Assert.Equal(1, loaded.Height);
    }

    [Fact]
    public void Save_bmp_writes_through_stb()
    {
        var image = new Image("Test");
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
}
