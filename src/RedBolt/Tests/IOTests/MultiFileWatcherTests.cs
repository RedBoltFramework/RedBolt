using System;
using System.IO;
using System.Threading;

using Xunit;

using RedBolt.IO;

namespace RedBolt.Tests;

public sealed class MultiFileWatcherTests
{
    [Fact]
    public void MultiFileWatcher_is_an_extensible_noncopyable_class()
    {
        Assert.True(typeof(MultiFileWatcher).IsClass);
        Assert.False(typeof(MultiFileWatcher).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(MultiFileWatcher)));
    }

    [Fact]
    public void MultiFileWatcher_forwards_delay_and_changes()
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

            Assert.True(TryGetChange(watcher, out var firstChange));
            Assert.True(TryGetChange(watcher, out var secondChange));
            Assert.Equal(
                new HashSet<string> { "first.txt", "second.txt" },
                new HashSet<string> { firstChange.FileName, secondChange.FileName });

            watcher.StopWatching();
            var noChange = new FileChange();
            Assert.False(watcher.GetNextChange(ref noChange));
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
        var change = new FileChange();
        Assert.False(watcher.GetNextChange(ref change));
    }

    private static bool TryGetChange(MultiFileWatcher watcher, out FileChange change)
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
        var directory = Path.Combine(Path.GetTempPath(), "RedBoltMultiFileWatcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
