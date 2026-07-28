namespace RedBolt.Tests;

using System;
using System.IO;
using System.Threading;

using Xunit;

using RedBolt.IO;

public sealed class FileWatcherTests
{
    [Fact]
    public void File_change_types_and_watcher_class_match_rbfx()
    {
        Assert.True(typeof(FileChangeKind).IsEnum);
        Assert.Equal(0, (int)FileChangeKind.Added);
        Assert.Equal(1, (int)FileChangeKind.Removed);
        Assert.Equal(2, (int)FileChangeKind.Renamed);
        Assert.Equal(3, (int)FileChangeKind.Modified);

        Assert.True(typeof(FileChange).IsValueType);
        Assert.False(typeof(FileChange).IsClass);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(FileChange)));
        Assert.False(typeof(FileWatcher).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(FileWatcher)));
    }

    [Fact]
    public void File_changes_copy_independently_and_false_poll_preserves_destination()
    {
        using var watcher = new FileWatcher();
        watcher.SetDelay(0.0f);

        var source = new FileChange
        {
            Kind = FileChangeKind.Added,
            FileName = "file.txt",
            OldFileName = string.Empty
        };
        var clone = source.Clone();
        clone.FileName = "clone.txt";
        Assert.Equal("file.txt", source.FileName);

        watcher.AddChange(source);
        source.Kind = FileChangeKind.Modified;
        watcher.AddChange(source);

        var destination = new FileChange { FileName = "unchanged.txt" };
        Assert.True(watcher.GetNextChange(ref destination));
        Assert.Equal(FileChangeKind.Added, destination.Kind);
        Assert.Equal("file.txt", destination.FileName);

        Assert.False(watcher.GetNextChange(ref destination));
        Assert.Equal("file.txt", destination.FileName);
    }

    [Fact]
    public void FileWatcher_delivers_added_changes_after_delay()
    {
        var directory = CreateTempDirectory();
        using var watcher = new FileWatcher();
        watcher.SetDelay(0);

        try
        {
            Assert.True(watcher.StartWatching(directory, false));
            Assert.Equal(directory + Path.DirectorySeparatorChar, watcher.GetPath());

            System.IO.File.WriteAllText(Path.Combine(directory, "added.txt"), "content");
            Assert.True(TryGetChange(watcher, out var change));
            Assert.Equal(FileChangeKind.Added, change.Kind);
            Assert.Equal("added.txt", change.FileName);
            Assert.Equal(string.Empty, change.OldFileName);
        }
        finally
        {
            watcher.StopWatching();
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void FileWatcher_restarts_and_stops_cleanly()
    {
        var directory = CreateTempDirectory();
        using var watcher = new FileWatcher();

        try
        {
            Assert.False(watcher.StartWatching(Path.Combine(directory, "missing"), false));
            Assert.Equal(string.Empty, watcher.GetPath());
            Assert.Equal(1.0f, watcher.GetDelay());

            watcher.SetDelay(-1);
            Assert.Equal(0.0f, watcher.GetDelay());
            Assert.True(watcher.StartWatching(directory, true));
            watcher.StopWatching();
            Assert.Equal(string.Empty, watcher.GetPath());
        }
        finally
        {
            watcher.StopWatching();
            Directory.Delete(directory, true);
        }
    }

    private static bool TryGetChange(FileWatcher watcher, out FileChange change)
    {
        change = new FileChange();
        for (var i = 0; i < 20; ++i)
        {
            if (watcher.GetNextChange(ref change))
                return true;

            Thread.Sleep(50);
        }

        return false;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltFileWatcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
