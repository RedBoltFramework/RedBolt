using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

using RedBolt.Core;
using RedBolt.IO;
namespace RedBolt.Tests;

public sealed class VirtualFileSystemTests
{
    [Fact]
    public void VirtualFileSystem_types_match_native_non_pod_class_kinds()
    {
        Assert.True(typeof(VirtualFileSystem).IsClass);
        Assert.False(typeof(VirtualFileSystem).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(VirtualFileSystem)));

        Assert.True(typeof(MountPointGuard).IsClass);
        Assert.False(typeof(MountPointGuard).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(MountPointGuard)));
    }

    [Fact]
    public void VirtualFileSystem_mounts_in_reverse_order_and_resolves_aliases()
    {
        var firstDirectory = CreateTempDirectory();
        var secondDirectory = CreateTempDirectory();
        System.IO.File.WriteAllText(Path.Combine(firstDirectory, "value.txt"), "first");
        System.IO.File.WriteAllText(Path.Combine(secondDirectory, "value.txt"), "second");

        try
        {
            using var virtualFileSystem = new VirtualFileSystem();
            var firstMount = virtualFileSystem.MountDir("res", firstDirectory);
            var secondMount = virtualFileSystem.MountDir("res", secondDirectory);
            var fileName = new FileIdentifier("res", "value.txt");

            Assert.Equal("second", virtualFileSystem.ReadAllText(fileName));
            Assert.Equal(secondDirectory + Path.DirectorySeparatorChar + "value.txt",
                virtualFileSystem.GetAbsoluteNameFromIdentifier(fileName));

            virtualFileSystem.MountAlias("assets", firstMount, "res");
            Assert.Equal("first", virtualFileSystem.ReadAllText(new FileIdentifier("alias", "assets:/value.txt")));
            Assert.Equal(new FileIdentifier("res", "value.txt"),
                virtualFileSystem.GetCanonicalIdentifier(new FileIdentifier("file", firstDirectory + "/value.txt")));

            var result = new List<string>();
            virtualFileSystem.Scan(result, new FileIdentifier("res", string.Empty), "*.txt", ScanFlags.ScanFiles);
            Assert.Contains("value.txt", result);

            virtualFileSystem.Unmount(secondMount);
            Assert.Equal("first", virtualFileSystem.ReadAllText(fileName));
        }
        finally
        {
            Directory.Delete(firstDirectory, true);
            Directory.Delete(secondDirectory, true);
        }
    }

    [Fact]
    public void VirtualFileSystem_releases_owned_mount_points()
    {
        var virtualFileSystem = new VirtualFileSystem();
        var unmounted = new TrackingMountedRoot();
        var retained = new TrackingMountedRoot();
        virtualFileSystem.Mount(unmounted);
        virtualFileSystem.Mount(retained);

        virtualFileSystem.Unmount(unmounted);
        Assert.Equal(1, unmounted.DisposeCount);
        Assert.Equal(0, retained.DisposeCount);

        virtualFileSystem.Dispose();
        Assert.Equal(1, unmounted.DisposeCount);
        Assert.Equal(1, retained.DisposeCount);
    }

    [Fact]
    public void MountPointGuard_mounts_and_unmounts_the_mount_point()
    {
        var directory = CreateTempDirectory();
        System.IO.File.WriteAllText(Path.Combine(directory, "guard.txt"), "guard");
        var previousContext = Context.Instance;
        using var context = new Context();

        try
        {
            Context.SetInstance(context);
            var virtualFileSystem = new VirtualFileSystem();
            context.RegisterSubsystem(virtualFileSystem);
            var mountPoint = new MountedDirectory(directory);
            var fileName = new FileIdentifier(string.Empty, "guard.txt");
            var guard = new MountPointGuard(mountPoint);

            try
            {
                Assert.Same(mountPoint, guard.Get());
                Assert.True(virtualFileSystem.Exists(fileName));
            }
            finally
            {
                guard.Dispose();
            }

            Assert.Null(guard.Get());
            Assert.False(virtualFileSystem.Exists(fileName));
        }
        finally
        {
            Context.SetInstance(previousContext);
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MountPointGuard_move_assignment_releases_then_transfers_ownership()
    {
        var previousContext = Context.Instance;
        using var context = new Context();

        try
        {
            Context.SetInstance(context);
            using var virtualFileSystem = new VirtualFileSystem();
            context.RegisterSubsystem(virtualFileSystem);
            var first = new TrackingMountedRoot();
            var second = new TrackingMountedRoot();
            using var firstGuard = new MountPointGuard(first);
            using var secondGuard = new MountPointGuard(second);

            Assert.Same(secondGuard, secondGuard.MoveAssign(firstGuard));
            Assert.Null(firstGuard.Get());
            Assert.Same(first, secondGuard.Get());
            Assert.Equal(0, first.DisposeCount);
            Assert.Equal(1, second.DisposeCount);

            secondGuard.Release();
            Assert.Equal(1, first.DisposeCount);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    private sealed class TrackingMountedRoot : MountedRoot
    {
        public int DisposeCount { get; private set; }

        public override void Dispose()
        {
            ++DisposeCount;
            base.Dispose();
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltVirtualFileSystem-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
