using RedBolt;
using RedBolt.IO;

namespace RedBolt.Tests;

public sealed class FileIdentifierTests
{
    [Fact]
    public void FileIdentifier_is_a_copyable_non_pod_value_struct()
    {
        Assert.True(typeof(FileIdentifier).IsValueType);
        Assert.False(typeof(FileIdentifier).IsClass);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(FileIdentifier)));
        Assert.Equal(typeof(string), typeof(FileIdentifier).GetField(nameof(FileIdentifier.Scheme))!.FieldType);
        Assert.Equal(typeof(string), typeof(FileIdentifier).GetField(nameof(FileIdentifier.FileName))!.FieldType);

        var empty = new FileIdentifier();
        Assert.Equal(FileIdentifier.Empty, empty);
        Assert.Equal(FileIdentifier.Empty, default(FileIdentifier));
        Assert.False((bool)FileIdentifier.Empty);
        Assert.True(!FileIdentifier.Empty);
    }

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

        Assert.Equal(FileIdentifier.Empty, FileIdentifier.FromUri("file:path/to/file"));
        Assert.Equal(FileIdentifier.Empty, FileIdentifier.FromUri("file:////path/to/file"));
        Assert.Equal(new FileIdentifier("conf", "config.json"), FileIdentifier.FromUri("conf:config.json"));
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

        Assert.Equal("path/to", (new FileIdentifier(string.Empty, "path") + "to").FileName);
        Assert.Equal("path/to", (new FileIdentifier(string.Empty, "path/") + "/to").FileName);
        Assert.Equal("path/to/file/", (new FileIdentifier(string.Empty, "path") + "to/file/").FileName);
    }

    [Fact]
    public void Copies_and_operator_add_are_independent_values()
    {
        var source = new FileIdentifier("res", "Data");
        var assigned = source;
        var appended = source + "Textures";
        var cloned = (FileIdentifier)source.Clone();

        assigned.AppendPath("Models");
        cloned.AppendPath("Shaders");

        Assert.Equal("Data", source.FileName);
        Assert.Equal("Data/Models", assigned.FileName);
        Assert.Equal("Data/Textures", appended.FileName);
        Assert.Equal("Data/Shaders", cloned.FileName);
    }

    [Fact]
    public void SanitizeFileName_collapses_dot_segments()
    {
        Assert.Equal("Textures/Box.png", FileIdentifier.SanitizeFileName("Textures/./Boxes/../Box.png"));
        Assert.Equal("/tmp/example.txt", FileIdentifier.SanitizeFileName("/tmp/./content/../example.txt"));
        Assert.Equal("C:/Temp/example.txt", FileIdentifier.SanitizeFileName("C:/Temp/content/../example.txt"));
        Assert.Equal("outside.txt", FileIdentifier.SanitizeFileName("../../outside.txt"));
        Assert.Equal("outside.txt", FileIdentifier.SanitizeFileName("/../outside.txt"));
        Assert.Equal("path/to", FileIdentifier.SanitizeFileName(" path//to "));
        Assert.Equal("path/to", FileIdentifier.SanitizeFileName("path/to/"));
        Assert.Equal("/", FileIdentifier.SanitizeFileName("/"));
    }
}
