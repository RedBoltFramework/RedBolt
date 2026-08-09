namespace RedBolt.IO;

using System;
using System.Collections.Generic;
using SystemMath = global::System.Math;

using RedBolt.Core;

/// <summary>Watches a set of directories for files being modified.</summary>
public class MultiFileWatcher : RedObject
{
    /// <summary>Construct.</summary>
    public MultiFileWatcher()
    {
    }

    /// <summary>Start watching a directory. Return true if successful.</summary>
    public bool StartWatching(string pathName, bool watchSubDirs)
    {
        var watcher = new FileWatcher();
        watcher.SetDelay(_delay);
        if (!watcher.StartWatching(pathName, watchSubDirs))
        {
            watcher.Dispose();
            return false;
        }

        _watchers.Add(watcher);
        return true;
    }

    /// <summary>Stop watching all the directories.</summary>
    public void StopWatching()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();

        _watchers.Clear();
    }

    /// <summary>Set the delay in seconds before file changes are notified.</summary>
    public void SetDelay(float interval)
    {
        _delay = SystemMath.Max(interval, 0.0f);
        foreach (var watcher in _watchers)
            watcher.SetDelay(_delay);
    }

    /// <summary>Return a file change (true if was found, false if not).</summary>
    public bool GetNextChange(ref FileChange dest)
    {
        foreach (var watcher in _watchers)
        {
            if (watcher.GetNextChange(ref dest))
                return true;
        }

        return false;
    }

    /// <summary>Return the delay in seconds for notifying file changes.</summary>
    public float GetDelay()
    {
        return _delay;
    }

    public override void Dispose()
    {
        StopWatching();
        base.Dispose();
    }

    private readonly List<FileWatcher> _watchers = new();
    private float _delay = 1.0f;
}
