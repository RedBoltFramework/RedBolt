using RedBolt;

namespace RedBolt.Tests;

public sealed class FileIdentifierTests
{
    [Fact]
    public void FromUri_handles_empty_scheme_relative_and_absolute_file_paths()
    {
        var relative = FileIdentifier.FromUri("Textures/Box.png");
        Assert.Equal(string.Empty, relative.Scheme);
        Assert.Equal("Textures/Box.png", relative.FileName);

        var unixAbsolute = FileIdentifier.FromUri("/tmp/example.txt");
        Assert.Equal("file", unixAbsolute.Scheme);
        Assert.Equal("/tmp/example.txt", unixAbsolute.FileName);

        var windowsAbsolute = FileIdentifier.FromUri("C:/Temp/example.txt");
        Assert.Equal("file", windowsAbsolute.Scheme);
        Assert.Equal("C:/Temp/example.txt", windowsAbsolute.FileName);
    }

    [Fact]
    public void FromUri_handles_file_scheme_and_other_schemes_like_rbfx()
    {
        var unixFile = FileIdentifier.FromUri("file:///tmp/example.txt");
        Assert.Equal("file", unixFile.Scheme);
        Assert.Equal("/tmp/example.txt", unixFile.FileName);

        var windowsFile = FileIdentifier.FromUri("file:///C:/Temp/example.txt");
        Assert.Equal("file", windowsFile.Scheme);
        Assert.Equal("C:/Temp/example.txt", windowsFile.FileName);

        var aliasFile = FileIdentifier.FromUri("alias://CoreData/Textures/Box.png");
        Assert.Equal("alias", aliasFile.Scheme);
        Assert.Equal("CoreData/Textures/Box.png", aliasFile.FileName);
    }

    [Fact]
    public void ToUri_and_append_path_match_rbfx_contract()
    {
        var relative = new FileIdentifier(string.Empty, "Textures/Box.png");
        Assert.Equal("Textures/Box.png", relative.ToUri());

        var unixAbsolute = new FileIdentifier("file", "/tmp/example.txt");
        Assert.Equal("file:///tmp/example.txt", unixAbsolute.ToUri());

        var windowsAbsolute = new FileIdentifier("file", "C:/Temp/example.txt");
        Assert.Equal("file:///C:/Temp/example.txt", windowsAbsolute.ToUri());

        var aliasFile = new FileIdentifier("alias", "CoreData");
        aliasFile.AppendPath("/Textures/Box.png");
        Assert.Equal("alias://CoreData/Textures/Box.png", aliasFile.ToUri());
    }

    [Fact]
    public void SanitizeFileName_collapses_dot_segments()
    {
        Assert.Equal("Textures/Box.png", FileIdentifier.SanitizeFileName("Textures/./Boxes/../Box.png"));
        Assert.Equal("/tmp/example.txt", FileIdentifier.SanitizeFileName("/tmp/./content/../example.txt"));
        Assert.Equal("C:/Temp/example.txt", FileIdentifier.SanitizeFileName("C:/Temp/content/../example.txt"));
    }
}
