using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace RedBolt.Tests;

public sealed class VirtualFileSystemTests
{
    [Fact]
    public void VirtualFileSystem_mounts_in_reverse_order_and_resolves_aliases()
    {
        var firstDirectory = CreateTempDirectory();
        var secondDirectory = CreateTempDirectory();
        System.IO.File.WriteAllText(Path.Combine(firstDirectory, "value.txt"), "first");
        System.IO.File.WriteAllText(Path.Combine(secondDirectory, "value.txt"), "second");

        try
        {
            var virtualFileSystem = new VirtualFileSystem();
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
    public void MountPointGuard_mounts_and_unmounts_the_mount_point()
    {
        var directory = CreateTempDirectory();
        System.IO.File.WriteAllText(Path.Combine(directory, "guard.txt"), "guard");
        var previousContext = Context.TryGetInstance();
        var context = new Context();

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

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltVirtualFileSystem-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
