namespace RedBolt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

using RedBolt;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;

public sealed class MountedExternalMemoryTests
{
    [Fact]
    public void MountedExternalMemory_links_opens_and_unlinks_read_only_memory()
    {
        var mountPoint = new MountedExternalMemory("memory");
        var linkedMemory = new MemoryBuffer("linked");
        mountPoint.LinkMemory("linked.txt", linkedMemory);
        linkedMemory.Dispose();
        mountPoint.LinkMemory("content.txt", "content");

        Assert.True(mountPoint.AcceptsScheme("memory"));
        Assert.False(mountPoint.AcceptsScheme("MEMORY"));
        Assert.True(mountPoint.Exists(new FileIdentifier("memory", "linked.txt")));
        Assert.False(mountPoint.Exists(new FileIdentifier("other", "linked.txt")));
        Assert.Equal("memory", mountPoint.GetName());

        var file = mountPoint.OpenFile(new FileIdentifier("other", "linked.txt"), FileMode.Read);
        Assert.NotNull(file);
        Span<byte> buffer = stackalloc byte[6];
        Assert.Equal(6u, file!.Read(buffer));
        Assert.Equal("linked", System.Text.Encoding.UTF8.GetString(buffer));
        file.Close();

        Assert.Null(mountPoint.OpenFile(new FileIdentifier("memory", "linked.txt"), FileMode.Write));
        Assert.Null(mountPoint.OpenFile(new FileIdentifier("memory", "linked.txt"), FileMode.ReadWrite));

        mountPoint.UnlinkMemory("linked.txt");
        Assert.False(mountPoint.Exists(new FileIdentifier("memory", "linked.txt")));
        Assert.True(mountPoint.Exists(new FileIdentifier("memory", "content.txt")));
    }

    [Fact]
    public void MountedExternalMemory_scans_paths_and_preserves_append_results()
    {
        var mountPoint = new MountedExternalMemory("memory");
        mountPoint.LinkMemory("root/a.txt", "a");
        mountPoint.LinkMemory("root/sub/b.txt", "b");
        mountPoint.LinkMemory("root/c.bin", "c");

        var result = new List<string> { "old" };
        mountPoint.Scan(result, "root", "*.txt", (ScanFlags)0);
        Assert.Equal(new[] { "a.txt" }, result.OrderBy(static value => value));

        mountPoint.Scan(result, "root", "*.txt", ScanFlags.ScanRecursive | ScanFlags.ScanAppend);
        Assert.Equal(new[] { "a.txt", "a.txt", "sub/b.txt" }, result.OrderBy(static value => value));

        mountPoint.SendFileChangedEvent("root/a.txt");
    }
}
