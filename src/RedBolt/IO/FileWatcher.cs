using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace RedBolt;

/// <summary>File change kind.</summary>
public enum FileChangeKind
{
    /// <summary>New file was created.</summary>
    Added,
    /// <summary>File was deleted.</summary>
    Removed,
    /// <summary>File was renamed.</summary>
    Renamed,
    /// <summary>File was modified.</summary>
    Modified
}

/// <summary>File change information.</summary>
public struct FileChange
{
    public FileChange()
    {
        Kind = FileChangeKind.Added;
        FileName = string.Empty;
        OldFileName = string.Empty;
    }

    /// <summary>File change kind.</summary>
    public FileChangeKind Kind;
    /// <summary>Name of modified file name. Always set.</summary>
    public string FileName = string.Empty;
    /// <summary>Previous file name in case of FILECHANGE_MODIFIED event. Empty otherwise.</summary>
    public string OldFileName = string.Empty;
}

/// <summary>Watches a directory and its subdirectories for files being modified.</summary>
public sealed class FileWatcher : Object, IDisposable
{
    /// <summary>Construct.</summary>
    public FileWatcher()
    {
        _delay = 1.0f;
        _watchSubDirs = false;
    }

    /// <summary>Directory watching loop.</summary>
    public void ThreadFunction()
    {
        // TODO: port C++ platform thread loop. FileSystemWatcher delivers the same changes asynchronously.
    }

    /// <summary>Start watching a directory. Return true if successful.</summary>
    public bool StartWatching(string pathName, bool watchSubDirs)
    {
        StopWatching();

        if (!Directory.Exists(pathName))
            return false;

        try
        {
            var watcher = new FileSystemWatcher(pathName)
            {
                IncludeSubdirectories = watchSubDirs,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
            };

            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;

            _fileWatcher = watcher;
            _path = Path.EndsInDirectorySeparator(pathName) ? pathName : pathName + Path.DirectorySeparatorChar;
            _watchSubDirs = watchSubDirs;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Stop watching the directory.</summary>
    public void StopWatching()
    {
        if (_fileWatcher is null)
            return;

        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Created -= OnCreated;
        _fileWatcher.Deleted -= OnDeleted;
        _fileWatcher.Changed -= OnChanged;
        _fileWatcher.Renamed -= OnRenamed;
        _fileWatcher.Dispose();
        _fileWatcher = null;
        _path = string.Empty;
    }

    /// <summary>Set the delay in seconds before file changes are notified.</summary>
    public void SetDelay(float interval)
    {
        _delay = Math.Max(interval, 0.0f);
    }

    /// <summary>Add a file change into the changes queue.</summary>
    public void AddChange(FileChange change)
    {
        lock (_changesMutex)
        {
            if (!_changes.TryGetValue(change.FileName, out var timedChange))
            {
                _changes[change.FileName] = new TimedFileChange(change, Environment.TickCount64);
            }
            else
            {
                // Reset the timer associated with the filename. Will be notified once timer exceeds the delay
                timedChange.Timestamp = Environment.TickCount64;
                _changes[change.FileName] = timedChange;
            }
        }
    }

    /// <summary>Return a file change (true if was found, false if not).</summary>
    public bool GetNextChange(out FileChange dest)
    {
        lock (_changesMutex)
        {
            var delayMsec = (long)(_delay * 1000.0f);
            if (_changes.Count == 0)
            {
                dest = default;
                return false;
            }

            foreach (var pair in _changes)
            {
                if (Environment.TickCount64 - pair.Value.Timestamp >= delayMsec)
                {
                    dest = pair.Value.Change;
                    _changes.Remove(pair.Key);
                    return true;
                }
            }

            dest = default;
            return false;
        }
    }

    /// <summary>Return the path being watched, or empty if not watching.</summary>
    public string GetPath()
    {
        return _path;
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
        GC.SuppressFinalize(this);
    }

    private void OnCreated(object sender, FileSystemEventArgs args)
    {
        AddChange(new FileChange { Kind = FileChangeKind.Added, FileName = GetInternalPath(args.FullPath), OldFileName = string.Empty });
    }

    private void OnDeleted(object sender, FileSystemEventArgs args)
    {
        AddChange(new FileChange { Kind = FileChangeKind.Removed, FileName = GetInternalPath(args.FullPath), OldFileName = string.Empty });
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        AddChange(new FileChange { Kind = FileChangeKind.Modified, FileName = GetInternalPath(args.FullPath), OldFileName = string.Empty });
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        AddChange(new FileChange
        {
            Kind = FileChangeKind.Renamed,
            FileName = GetInternalPath(args.FullPath),
            OldFileName = GetInternalPath(args.OldFullPath)
        });
    }

    private string GetInternalPath(string fullPath)
    {
        return Path.GetRelativePath(_path, fullPath).Replace('\\', '/');
    }

    private readonly Dictionary<string, TimedFileChange> _changes = new();
    private readonly object _changesMutex = new();
    private FileSystemWatcher? _fileWatcher;
    private string _path = string.Empty;
    private float _delay;
    private bool _watchSubDirs;

    private struct TimedFileChange
    {
        public TimedFileChange(FileChange change, long timestamp)
        {
            Change = change;
            Timestamp = timestamp;
        }

        /// <summary>File change information.</summary>
        public FileChange Change;
        /// <summary>Timer used to filter out repeated events when file is being written.</summary>
        public long Timestamp;
    }
}
