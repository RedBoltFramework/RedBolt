using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ResourceCacheTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ResourceCacheTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

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
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
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
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
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
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "cache-test.txt"), "content");
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
    public void LoadFromCache_returns_resource_from_context_cache()
    {
        var resourceName = $"Data/load-from-cache-{Guid.NewGuid():N}.bin";
        var resource = new BinaryFile(resourceName);
        var type = new StringHash(typeof(BinaryFile).GetClassName());

        try
        {
            Assert.True(Resources.AddManualResource(resource));
            Assert.Same(resource, Resource.LoadFromCache(_fixture.Context, type, resourceName));
            Assert.Null(Resource.LoadFromCache(_fixture.Context, type, string.Empty));
        }
        finally
        {
            Resources.ReleaseResource(type, resourceName, force: true);
        }
    }

    [Fact]
    public void TempResource_does_not_reuse_cached()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
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
    public void TempResource_with_empty_name_returns_null()
    {
        Assert.Null(_fixture.Resources.GetTempResource<BinaryFile>(string.Empty, false));
    }

    [Fact]
    public void ExistingResource_finds_cached()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
            Resources.AddResourceDir(tempDir);
            var cached = Resources.GetResource<BinaryFile>("Data/hello.txt");
            Assert.Same(cached, Resources.GetExistingResource<BinaryFile>("Data/hello.txt"));
            Assert.Same(cached, Resources.GetExistingResource(typeof(BinaryFile), "Data/hello.txt"));
            Assert.Contains(cached, Resources.GetResources(typeof(BinaryFile)));
            Assert.True(Resources.GetAllResources().TryGetValue(typeof(BinaryFile), out var group));
            Assert.Same(cached, group!.Resources[cached.GetNameHash()]);
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void StringHash_resource_overloads_find_release_and_report_groups()
    {
        using var cache = new ResourceCache();
        var resourceName = $"Data/hash-{Guid.NewGuid():N}.bin";
        var resource = new BinaryFile(resourceName);
        resource.SetMemoryUse(3);
        var type = new StringHash(typeof(BinaryFile).GetClassName());
        cache.SetMemoryBudget(type, 8);
        try
        {
            Assert.True(cache.AddManualResource(resource));
            Assert.Same(resource, cache.GetExistingResource(type, resourceName));
            Assert.Contains(resource, cache.GetResources(type));
            Assert.Equal(8ul, cache.GetMemoryBudget(type));
            Assert.Equal(3ul, cache.GetMemoryUse(type));
            Assert.Same(resource, cache.GetResource(type, resourceName));

            cache.ReleaseResource(type, resourceName);

            Assert.Null(cache.GetExistingResource(type, resourceName));
        }
        finally
        {
            cache.SetMemoryBudget(type, 0);
            cache.ReleaseResources(type, force: true);
        }
    }

    [Fact]
    public void BackgroundLoad_finishes_successfully()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "bg.txt"), "background");
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
    public void BackgroundLoad_failure_sends_failure_and_completion_events()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "background-failure.dat"), "failure");
            Resources.AddResourceDir(tempDir);
            Resources.SetReturnFailedResources(false);

            string? failedName = null;
            string? completedName = null;
            var completedSuccessfully = true;
            using var failedSubscription = Resources.SubscribeToEvent<ResourceEvents.LoadFailed>(
                (ref ResourceEvents.LoadFailed eventData) => failedName = eventData.ResourceName);
            using var completedSubscription = Resources.SubscribeToEvent<ResourceEvents.ResourceBackgroundLoaded>(
                (ref ResourceEvents.ResourceBackgroundLoaded eventData) =>
                {
                    completedName = eventData.ResourceName;
                    completedSuccessfully = eventData.Success;
                });

            Assert.True(Resources.BackgroundLoadResource<FailedResource>("Data/background-failure.dat"));
            Resources.FinishBackgroundResources(1000);

            Assert.Equal("Data/background-failure.dat", failedName);
            Assert.Equal("Data/background-failure.dat", completedName);
            Assert.False(completedSuccessfully);
            Assert.Null(Resources.GetExistingResource<FailedResource>("Data/background-failure.dat"));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void File_changed_reloads_resource_after_reload_suspension_is_lifted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var fileName = Path.Combine(tempDir, "Data", "reload.dat");
            System.IO.File.WriteAllText(fileName, "first");
            Resources.AddResourceDir(tempDir);
            var resource = Resources.GetResource<ReloadableResource>("Data/reload.dat");
            Assert.Equal(1, resource.ReloadCount);

            Resources.SetResourceReloadSuspended(true);
            var fileChangedEventData = new ResourceEvents.FileChanged
            {
                FileName = fileName,
                ResourceName = "Data/reload.dat"
            };
            Resources.SendEvent(ref fileChangedEventData);
            Assert.Equal(1, resource.ReloadCount);

            Resources.SetResourceReloadSuspended(false);
            Assert.Equal(2, resource.ReloadCount);
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resource_routers_apply_in_order_to_resolved_identifiers()
    {
        var cache = new ResourceCache();
        var second = new PrefixRouter("Final/");
        var first = new PrefixRouter("Intermediate/");
        cache.AddResourceRouter(second);
        cache.AddResourceRouter(first, addAsFirst: true);

        var resolved = cache.GetResolvedIdentifier(new FileIdentifier("Data/file.txt"));

        Assert.Equal("Final/Intermediate/Data/file.txt", resolved.FileName);
        Assert.Same(first, cache.GetResourceRouter(0));
        cache.RemoveResourceRouter(first);
        Assert.Same(second, cache.GetResourceRouter(0));
    }

    [Fact]
    public void Resource_routing_canonicalizes_only_before_routers_and_blocks_reentrancy()
    {
        var cache = new ResourceCache();
        var reentrant = new ReentrantRouter(cache);
        cache.AddResourceRouter(reentrant);
        cache.AddResourceRouter(new PrefixRouter("../"));

        var resolved = cache.GetResolvedIdentifier(new FileIdentifier(string.Empty, "Folder/../Data/file.txt"));

        Assert.Equal("../Folder/Data/file.txt", resolved.FileName);
        Assert.Equal(1, reentrant.NumRoutes);
    }

    [Fact]
    public void Dependency_reload_scan_and_diagnostics_work()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "main.txt"), "before");
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "dependency.txt"), "dependency");
            Directory.CreateDirectory(Path.Combine(tempDir, "Data", "Nested"));
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "Nested", "other.txt"), "other");
            Resources.AddResourceDir(tempDir);
            var resource = Resources.GetResource<BinaryFile>("Data/main.txt");
            resource.SetMemoryUse(64);
            Resources.StoreResourceDependency(resource, "Data/dependency.txt");
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "main.txt"), "after");

            Resources.ReloadResourceWithDependencies("Data/dependency.txt");
            Assert.Equal("after", resource.GetText());

            var files = new List<string>();
            Resources.Scan(files, "Data", "*.txt", ScanFlags.ScanFiles | ScanFlags.ScanRecursive);
            Assert.Contains("Data/main.txt", files);
            Assert.Contains("Data/Nested/other.txt", files);
            Assert.Contains("BinaryFile", Resources.PrintMemoryUsage(), StringComparison.Ordinal);
            Assert.Contains("Data/main.txt", Resources.PrintResources(nameof(BinaryFile)), StringComparison.Ordinal);
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
    public void Memory_budget_releases_oldest_resource()
    {
        Resources.SetMemoryBudget<ReloadableResource>(4);
        var oldestName = $"Data/old-{Guid.NewGuid():N}.bin";
        var newestName = $"Data/new-{Guid.NewGuid():N}.bin";
        try
        {
            var oldest = new ReloadableResource(oldestName);
            oldest.SetMemoryUse(3);
            Assert.True(Resources.AddManualResource(oldest));
            oldest.ResetUseTimer();
            System.Threading.Thread.Sleep(20);

            var newest = new ReloadableResource(newestName);
            newest.SetMemoryUse(3);
            Assert.True(Resources.AddManualResource(newest));

            Assert.Null(Resources.GetExistingResource<ReloadableResource>(oldestName));
            Assert.NotNull(Resources.GetExistingResource<ReloadableResource>(newestName));
        }
        finally
        {
            Resources.SetMemoryBudget<ReloadableResource>(0);
            Resources.ReleaseResources<ReloadableResource>(force: true);
        }
    }

    [Fact]
    public void Resource_groups_expose_cached_resources_and_budget()
    {
        var resource = new ReloadableResource($"Data/group-{Guid.NewGuid():N}.bin");
        resource.SetMemoryUse(3);
        Resources.SetMemoryBudget<ReloadableResource>(8);
        try
        {
            Assert.True(Resources.AddManualResource(resource));

            var groups = Resources.GetAllResources();
            Assert.True(groups.TryGetValue(typeof(ReloadableResource), out var group));
            Assert.Equal(8ul, group!.MemoryBudget);
            Assert.Equal(3ul, group.MemoryUse);
            Assert.Same(resource, group.Resources[resource.GetNameHash()]);
        }
        finally
        {
            Resources.SetMemoryBudget<ReloadableResource>(0);
            Resources.ReleaseResources<ReloadableResource>(force: true);
        }
    }

    [Fact]
    public void Removing_resource_reflection_releases_cached_resources()
    {
        var resource = new ReloadableResource($"Data/reflection-{Guid.NewGuid():N}.bin");
        Assert.True(Resources.AddManualResource(resource));
        Assert.Same(resource, Resources.GetExistingResource<ReloadableResource>(resource.Name));

        _fixture.Context.AddReflection<ReloadableResource>();
        _fixture.Context.RemoveReflection<ReloadableResource>();

        Assert.Null(Resources.GetExistingResource<ReloadableResource>(resource.Name));
    }

    [Fact]
    public void ReleasePackageResources_removes_resources_named_by_package_entries()
    {
        var packageBytes = new VectorBuffer();
        var resourceName = $"Data/package-{Guid.NewGuid():N}.bin";
        var content = System.Text.Encoding.UTF8.GetBytes("package-data");
        packageBytes.WriteFileID("UPAK");
        packageBytes.WriteUInt(1);
        packageBytes.WriteUInt(0x1234);
        packageBytes.WriteString(resourceName);
        packageBytes.WriteUInt(packageBytes.Size + 12);
        packageBytes.WriteUInt((uint)content.Length);
        packageBytes.WriteUInt(0x55);
        packageBytes.Write(content);

        var packagePath = Path.Combine(Path.GetTempPath(), $"redbolt-package-{Guid.NewGuid():N}.pak");
        try
        {
            System.IO.File.WriteAllBytes(packagePath, packageBytes.GetBuffer());
            using var packageFile = new PackageFile(packagePath);

            var resource = new BinaryFile(resourceName);
            resource.SetText("manual");
            Assert.True(Resources.AddManualResource(resource));
            Assert.NotNull(Resources.GetExistingResource<BinaryFile>(resourceName));
            Assert.True(Resources.GetAllResources().TryGetValue(typeof(BinaryFile), out var group));
            Assert.Contains(resource.GetNameHash(), group!.Resources.Keys);

            Resources.ReleasePackageResources(packageFile);

            Assert.Null(Resources.GetExistingResource<BinaryFile>(resourceName));
            Assert.DoesNotContain(resource.GetNameHash(), group.Resources.Keys);
        }
        finally
        {
            System.IO.File.Delete(packagePath);
        }
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
        r.SetMemoryUse(123);
        r.SetAsyncLoadState(AsyncLoadState.Queued);
        r.SetAbsoluteFileName("/tmp/test.bin");
        Assert.Equal("Data/renamed.bin", r.GetName());
        Assert.Equal(StringHash.Calculate("Data/renamed.bin"), r.GetNameHash().Value);
        Assert.Equal(123, r.GetMemoryUse());
        Assert.Equal(AsyncLoadState.Queued, r.GetAsyncLoadState());
        Assert.Equal("/tmp/test.bin", r.GetAbsoluteFileName());
    }

    [Fact]
    public void GetFile_opens_resource_stream()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
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
    public void Missing_resource_file_sends_not_found_event()
    {
        string? observedName = null;
        using var subscription = Resources.SubscribeToEvent<ResourceEvents.ResourceNotFound>(
            (ref ResourceEvents.ResourceNotFound eventData) => observedName = eventData.ResourceName);

        Assert.Null(Resources.GetFile("Data/does-not-exist.txt"));
        Assert.Equal("Data/does-not-exist.txt", observedName);
    }

    [Fact]
    public void Failed_resource_load_sends_load_failed_event()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "failure.dat"), "failure");
            Resources.AddResourceDir(tempDir);
            Resources.SetReturnFailedResources(false);

            string? observedName = null;
            using var subscription = Resources.SubscribeToEvent<ResourceEvents.LoadFailed>(
                (ref ResourceEvents.LoadFailed eventData) => observedName = eventData.ResourceName);

            Assert.Throws<InvalidOperationException>(() => Resources.GetResource<FailedResource>("Data/failure.dat"));
            Assert.Equal("Data/failure.dat", observedName);
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
        System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "hello.txt"), "hello");
        Resources.AddResourceDir(tempDir);
        Assert.True(Resources.RemoveResourceDir(tempDir));
        Assert.False(Resources.Exists("Data/hello.txt"));
        Directory.Delete(tempDir, recursive: true);
    }
}

internal sealed class PrefixRouter(string prefix) : ResourceRouter
{
    public override void Route(ref FileIdentifier name)
    {
        name.FileName = prefix + name.FileName;
    }
}

internal sealed class ReentrantRouter(ResourceCache cache) : ResourceRouter
{
    public int NumRoutes { get; private set; }

    public override void Route(ref FileIdentifier name)
    {
        ++NumRoutes;
        name = cache.GetResolvedIdentifier(name);
    }
}

public sealed class FailedResource(string name) : Resource(name)
{
    public override bool BeginLoad(Stream source) => false;
}

public sealed class ReloadableResource(string name) : Resource(name)
{
    public int ReloadCount { get; private set; }

    public override bool BeginLoad(Stream source)
    {
        ReloadCount++;
        return true;
    }
}
