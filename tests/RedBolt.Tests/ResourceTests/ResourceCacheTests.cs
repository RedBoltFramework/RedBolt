using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ResourceCacheTests
{
    private readonly HeadlessEngineFixture fixture;

    public ResourceCacheTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RedBolt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Data"));
        return dir;
    }

    [Fact]
    public void AddResourceDir_registers_file_resources()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
            Assert.True(Resources.AddResourceDir(tempDir));
            Assert.True(Resources.Exists<BinaryFile>("Data/hello.txt"));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Settings_roundtrip()
    {
        Resources.SetMemoryBudget<BinaryFile>(4096ul);
        Resources.SetReturnFailedResources(true);
        Resources.SetSearchPackagesFirst(true);
        Resources.SetFinishBackgroundResourcesMs(0);

        Assert.Equal(4096ul, Resources.GetMemoryBudget<BinaryFile>());
        Assert.True(Resources.GetReturnFailedResources());
        Assert.True(Resources.GetSearchPackagesFirst());
        Assert.Equal(1, Resources.GetFinishBackgroundResourcesMs());
    }

    [Fact]
    public void SanitateResourceName_strips_absolute_path()
    {
        var tempDir = CreateTempDir();
        try
        {
            Resources.AddResourceDir(tempDir);
            var fullPath = Path.Combine(tempDir, "Data", "hello.txt");
            Assert.Equal("Data/hello.txt", Resources.SanitateResourceName(fullPath));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void BinaryFile_GetText_returns_file_content()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
            Resources.AddResourceDir(tempDir);
            var bf = Resources.GetResource<BinaryFile>("Data/hello.txt");
            Assert.Equal("hello", bf.GetText());
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Caching_returns_same_instance()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "cache-test.txt"), "content");
            Resources.AddResourceDir(tempDir);
            var first = Resources.GetResource<BinaryFile>("Data/cache-test.txt");
            Assert.Same(first, Resources.GetResource<BinaryFile>("Data/cache-test.txt"));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TempResource_does_not_reuse_cached()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
            Resources.AddResourceDir(tempDir);
            var cached = Resources.GetResource<BinaryFile>("Data/hello.txt");
            Assert.NotSame(cached, Resources.GetTempResource<BinaryFile>("Data/hello.txt"));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ExistingResource_finds_cached()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
            Resources.AddResourceDir(tempDir);
            var cached = Resources.GetResource<BinaryFile>("Data/hello.txt");
            Assert.Same(cached, Resources.GetExistingResource<BinaryFile>("Data/hello.txt"));
            Assert.True(Resources.GetAllResources().ContainsKey((typeof(BinaryFile), "Data/hello.txt")));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void BackgroundLoad_finishes_successfully()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "bg.txt"), "background");
            Resources.AddResourceDir(tempDir);
            Assert.True(Resources.BackgroundLoadResource<BinaryFile>("Data/bg.txt"));
            Resources.FinishBackgroundResources(1000);
            var bg = Resources.GetExistingResource<BinaryFile>("Data/bg.txt");
            Assert.NotNull(bg);
            Assert.Equal("background", bg.GetText());
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ManualResource_add_and_release_work()
    {
        var manual = new BinaryFile("Data/manual.bin");
        manual.SetText("manual");
        Assert.True(Resources.AddManualResource(manual));
        Resources.ReleaseResource("Data/manual.bin");
        Assert.Null(Resources.GetExistingResource<BinaryFile>("Data/manual.bin"));
    }

    [Fact]
    public void LoadFile_SaveFile_roundtrip()
    {
        var tempDir = CreateTempDir();
        try
        {
            var savePath = Path.Combine(tempDir, "Data", "saved.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            var saved = new BinaryFile("Data/saved.bin");
            saved.SetText("saved through resource helper");
            Assert.True(saved.SaveFile(savePath));

            var reloaded = new BinaryFile("Data/reloaded.bin");
            Assert.True(reloaded.LoadFile(savePath));
            Assert.Equal("saved through resource helper", reloaded.GetText());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resource_property_helpers_work()
    {
        var r = new BinaryFile("Data/test.bin");
        r.SetName("Data/renamed.bin");
        r.SetMemoryUse(123u);
        r.SetAsyncLoadState(AsyncLoadState.Queued);
        r.SetAbsoluteFileName("/tmp/test.bin");
        Assert.Equal("Data/renamed.bin", r.GetName());
        Assert.Equal(StringHash.Calculate("Data/renamed.bin"), r.GetNameHash().Value);
        Assert.Equal(123u, r.GetMemoryUse());
        Assert.Equal(AsyncLoadState.Queued, r.GetAsyncLoadState());
        Assert.Equal("/tmp/test.bin", r.GetAbsoluteFileName());
    }

    [Fact]
    public void GetFile_opens_resource_stream()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
            Resources.AddResourceDir(tempDir);
            using var file = Resources.GetFile("Data/hello.txt");
            Assert.NotNull(file);
            Assert.True(file.Length > 0);
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void RemoveResourceDir_unregisters_resources()
    {
        var tempDir = CreateTempDir();
        File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
        Resources.AddResourceDir(tempDir);
        Assert.True(Resources.RemoveResourceDir(tempDir));
        Assert.False(Resources.Exists("Data/hello.txt"));
        Directory.Delete(tempDir, recursive: true);
    }
}
