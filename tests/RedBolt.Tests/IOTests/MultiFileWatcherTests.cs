using System;
using System.IO;
using System.Threading;
using Xunit;

namespace RedBolt.Tests;

public sealed class MultiFileWatcherTests
{
    [Fact]
    public void MultiFileWatcher_forwards_delay_and_changes_in_watcher_order()
    {
        var firstDirectory = CreateTempDirectory();
        var secondDirectory = CreateTempDirectory();
        using var watcher = new MultiFileWatcher();

        try
        {
            watcher.SetDelay(0);
            Assert.Equal(0.0f, watcher.GetDelay());
            Assert.True(watcher.StartWatching(firstDirectory, false));
            Assert.True(watcher.StartWatching(secondDirectory, false));

            System.IO.File.WriteAllText(Path.Combine(firstDirectory, "first.txt"), "first");
            System.IO.File.WriteAllText(Path.Combine(secondDirectory, "second.txt"), "second");

            Assert.True(TryGetChange(watcher, out var change));
            Assert.Equal("first.txt", change.FileName);

            watcher.StopWatching();
            Assert.False(watcher.GetNextChange(out _));
        }
        finally
        {
            watcher.StopWatching();
            Directory.Delete(firstDirectory, true);
            Directory.Delete(secondDirectory, true);
        }
    }

    [Fact]
    public void MultiFileWatcher_does_not_retain_failed_watchers()
    {
        using var watcher = new MultiFileWatcher();
        Assert.False(watcher.StartWatching(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), false));
        Assert.False(watcher.GetNextChange(out _));
    }

    private static bool TryGetChange(MultiFileWatcher watcher, out FileChange change)
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
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltMultiFileWatcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
