namespace RedBolt.Tests;

using System.Text;

using RedBolt.Bindings.RmlUi;
using RedBolt.RmlUI;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RmlFileTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RmlFileTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public unsafe void RmlFile_matches_resource_file_behavior()
    {
        var tempDir = Directory.CreateTempSubdirectory("redbolt-rml-file-").FullName;
        var dataDir = Path.Combine(tempDir, "Data");
        Directory.CreateDirectory(dataDir);
        System.IO.File.WriteAllText(Path.Combine(dataDir, "rml-file.txt"), "abcdef");
        _fixture.Resources.AddResourceDir(tempDir);
        try
        {
            using var path = new Std.String();
            using var fileInterface = new RmlFile();
            path.Assign("Data/rml-file.txt");
            var file = fileInterface.Open(path);
            Assert.NotEqual(0u, file);
            try
            {
                Assert.True(fileInterface.IsResourceLoaded("Data/rml-file.txt"));
                Assert.Equal(6u, fileInterface.Length(file));
                Assert.Equal(0u, fileInterface.Tell(file));

                Span<byte> buffer = stackalloc byte[3];
                fixed (byte* bufferPointer = buffer)
                    Assert.Equal(3u, fileInterface.Read((nint)bufferPointer, (nuint)buffer.Length, file));
                Assert.Equal("abc", Encoding.UTF8.GetString(buffer));
                Assert.Equal(3u, fileInterface.Tell(file));

                Assert.True(fileInterface.Seek(file, 1, (int)SeekOrigin.Current));
                Assert.Equal(4u, fileInterface.Tell(file));
                Assert.True(fileInterface.Seek(file, 1, (int)SeekOrigin.End));
                Assert.Equal(5u, fileInterface.Tell(file));
                Assert.True(fileInterface.Seek(file, 0, (int)SeekOrigin.Begin));
                Assert.False(fileInterface.Seek(file, -1, (int)SeekOrigin.Begin));
            }
            finally
            {
                fileInterface.Close(file);
            }

            fileInterface.AddResourceLoaded("Data/manual.rml");
            Assert.True(fileInterface.IsResourceLoaded("Data/manual.rml"));
            fileInterface.ClearLoadedResources();
            Assert.False(fileInterface.IsResourceLoaded("Data/rml-file.txt"));
            Assert.False(fileInterface.IsResourceLoaded("Data/manual.rml"));

            path.Assign("Data/missing.rml");
            Assert.Equal(0u, fileInterface.Open(path));
        }
        finally
        {
            _fixture.Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
