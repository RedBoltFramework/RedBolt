using System.Collections.Generic;
using RedBolt;

using RedBolt.IO;
namespace RedBolt.Tests;
using FileMode = global::RedBolt.IO.FileMode;


public sealed class MountPointTests
{
    [Fact]
    public void MountPoint_types_are_extensible_noncopyable_classes()
    {
        Assert.True(typeof(MountPoint).IsClass);
        Assert.True(typeof(MountPoint).IsAbstract);
        Assert.False(typeof(MountPoint).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(MountPoint)));

        Assert.True(typeof(WatchableMountPoint).IsClass);
        Assert.True(typeof(WatchableMountPoint).IsAbstract);
        Assert.False(typeof(WatchableMountPoint).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(WatchableMountPoint)));
    }

    [Fact]
    public void MountPoint_default_implementations_match_rbfx()
    {
        using var mountPoint = new TestMountPoint();
        var existing = new FileIdentifier(string.Empty, "existing");
        var missing = new FileIdentifier(string.Empty, "missing");

        Assert.Equal(0u, mountPoint.GetLastModifiedTime(existing, false));
        Assert.Null(mountPoint.GetLastModifiedTime(missing, false));
        Assert.Equal(string.Empty, mountPoint.GetAbsoluteNameFromIdentifier(existing));
        Assert.Equal(FileIdentifier.Empty, mountPoint.GetIdentifierFromAbsoluteName("/tmp/missing"));
        Assert.False(mountPoint.IsWatching());

        mountPoint.SetWatching(true);
        Assert.False(mountPoint.IsWatching());
    }

    [Fact]
    public void WatchableMountPoint_starts_and_stops_only_on_state_changes()
    {
        using var mountPoint = new TestWatchableMountPoint();

        mountPoint.SetWatching(true);
        mountPoint.SetWatching(true);
        Assert.True(mountPoint.IsWatching());
        Assert.Equal(1, mountPoint.StartCount);
        Assert.Equal(0, mountPoint.StopCount);

        mountPoint.SetWatching(false);
        mountPoint.SetWatching(false);
        Assert.False(mountPoint.IsWatching());
        Assert.Equal(1, mountPoint.StartCount);
        Assert.Equal(1, mountPoint.StopCount);
    }

    private class TestMountPoint : MountPoint
    {
        public override bool AcceptsScheme(string scheme) => scheme == "test";

        public override bool Exists(FileIdentifier fileName) => fileName.FileName == "existing";

        public override AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode) => null;

        public override string GetName() => "Test";

        public override void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
        {
        }
    }

    private sealed class TestWatchableMountPoint : WatchableMountPoint
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public override bool AcceptsScheme(string scheme) => scheme == "test";

        public override bool Exists(FileIdentifier fileName) => fileName.FileName == "existing";

        public override AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode) => null;

        public override string GetName() => "Test";

        public override void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
        {
        }

        protected override void StartWatching()
        {
            ++StartCount;
        }

        protected override void StopWatching()
        {
            ++StopCount;
        }
    }
}
