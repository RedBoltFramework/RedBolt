using System;
using System.Collections.Generic;
using System.IO;
using RedBolt;

namespace RedBolt.Tests;

public sealed class MountedAliasRootTests
{
    [Fact]
    public void MountedAliasRoot_forwards_aliases_to_the_selected_mount_point()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"redbolt-mounted-alias-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "Textures"));
        var fileName = Path.Combine(directory, "Textures", "Box.txt");
        System.IO.File.WriteAllText(fileName, "box");

        try
        {
            var mountedDirectory = new MountedDirectory(directory, "assets");
            var aliasRoot = new MountedAliasRoot();
            aliasRoot.AddAlias("Data", "assets", mountedDirectory);
            var identifier = new FileIdentifier("alias", "Data:/Textures/Box.txt");

            Assert.True(aliasRoot.AcceptsScheme("ALIAS"));
            Assert.Equal("alias://", aliasRoot.GetName());
            Assert.True(aliasRoot.Exists(identifier));
            Assert.NotNull(aliasRoot.GetLastModifiedTime(identifier, false));
            Assert.Equal(fileName, aliasRoot.GetAbsoluteNameFromIdentifier(identifier));

            var found = aliasRoot.FindMountPoint(identifier.FileName);
            Assert.Same(mountedDirectory, found.MountPoint);
            Assert.Equal("Data", found.Alias);
            Assert.Equal("assets", found.Scheme);

            var input = aliasRoot.OpenFile(identifier, FileMode.Read);
            try
            {
                Assert.NotNull(input);
                Assert.Equal(3u, input.Read(new byte[3]));
            }
            finally
            {
                input?.Close();
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MountedAliasRoot_removes_aliases_and_does_not_scan()
    {
        var mountedDirectory = new MountedDirectory(Path.GetTempPath(), "assets");
        var aliasRoot = new MountedAliasRoot();
        aliasRoot.AddAlias("Data", "assets", mountedDirectory);
        aliasRoot.AddAlias("Other", "assets", mountedDirectory);

        var result = new List<string> { "existing" };
        aliasRoot.Scan(result, string.Empty, "*", ScanFlags.ScanFiles);
        Assert.Equal(new[] { "existing" }, result);

        aliasRoot.RemoveAliases(mountedDirectory);
        Assert.False(aliasRoot.Exists(new FileIdentifier("alias", "Data:/file.txt")));
        Assert.Equal((MountPoint?)null, aliasRoot.FindMountPoint("Data:/file.txt").MountPoint);
        Assert.Equal(FileIdentifier.Empty, aliasRoot.GetIdentifierFromAbsoluteName("/tmp/file.txt"));
    }
}
