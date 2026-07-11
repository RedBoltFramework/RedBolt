using System;
using System.IO;
using System.Threading;
using Xunit;

namespace RedBolt.Tests;

public sealed class FileWatcherTests
{
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
        for (var i = 0; i < 20; ++i)
        {
            if (watcher.GetNextChange(out change))
                return true;

            Thread.Sleep(50);
        }

        change = default;
        return false;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltFileWatcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
