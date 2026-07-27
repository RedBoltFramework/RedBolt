using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.Core;
using Timer = global::RedBolt.Core.Timer;
using RedBolt.Input;
using RedBolt.Logging;

namespace RedBolt.IO;

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
public struct FileChange : ICloneable
{
    public FileChange()
    {
    }

    /// <summary>File change kind.</summary>
    public FileChangeKind Kind;
    /// <summary>Name of modified file name. Always set.</summary>
    public string FileName = string.Empty;
    /// <summary>Previous file name in case of FILECHANGE_MODIFIED event. Empty otherwise.</summary>
    public string OldFileName = string.Empty;

    public readonly FileChange Clone()
    {
        return new FileChange
        {
            Kind = Kind,
            FileName = FileName,
            OldFileName = OldFileName
        };
    }

    readonly object ICloneable.Clone() => Clone();
}

/// <summary>Watches a directory and its subdirectories for files being modified.</summary>
public class FileWatcher : RedObject
{
    /// <summary>Construct.</summary>
    public FileWatcher()
    {
        _delay = 1.0f;
        _watchSubDirs = false;
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        StopWatching();
        lock (_changesMutex)
            _changes.Clear();
        base.Dispose();
    }

    /// <summary>Directory watching loop.</summary>
    public void ThreadFunction()
    {
    }

    /// <summary>Start watching a directory. Return true if successful.</summary>
    public bool StartWatching(string pathName, bool watchSubDirs)
    {
        // Stop any previous watching
        StopWatching();

        // TODO: port SetName("Watcher for " + pathName);

        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(pathName)
            {
                IncludeSubdirectories = watchSubDirs,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
            };

            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;

            _path = Path.EndsInDirectorySeparator(pathName) ? pathName : pathName + Path.DirectorySeparatorChar;
            _watchSubDirs = watchSubDirs;
            _fileWatcher = watcher;
            watcher.EnableRaisingEvents = true;

            Log.GetLogger().LogDebug("Started watching path {PathName}", pathName);
            return true;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (ArgumentException)
        {
        }

        watcher?.Dispose();
        _fileWatcher = null;
        _path = string.Empty;
        Log.GetLogger().LogError("Failed to start watching path {PathName}", pathName);
        return false;
    }

    /// <summary>Stop watching the directory.</summary>
    public void StopWatching()
    {
        if (_fileWatcher is not null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Created -= OnCreated;
            _fileWatcher.Deleted -= OnDeleted;
            _fileWatcher.Changed -= OnChanged;
            _fileWatcher.Renamed -= OnRenamed;
            _fileWatcher.Dispose();
            _fileWatcher = null;

            Log.GetLogger().LogDebug("Stopped watching path {Path}", _path);
            _path = string.Empty;
        }
    }

    /// <summary>Set the delay in seconds before file changes are notified. This (hopefully) avoids notifying when a file save is still in progress. Default 1 second.</summary>
    public void SetDelay(float interval)
    {
        _delay = SystemMath.Max(interval, 0.0f);
    }

    /// <summary>Add a file change into the changes queue.</summary>
    public void AddChange(in FileChange change)
    {
        lock (_changesMutex)
        {
            ref var timedChange = ref CollectionsMarshal.GetValueRefOrAddDefault(
                _changes, change.FileName, out var exists);
            if (!exists)
                timedChange = new TimedFileChange(change, Environment.TickCount64);
            else
                // Reset the timer associated with the filename. Will be notified once timer exceeds the delay
                timedChange.Timestamp = Environment.TickCount64;
        }
    }

    /// <summary>Return a file change (true if was found, false if not).</summary>
    public bool GetNextChange(ref FileChange dest)
    {
        lock (_changesMutex)
        {
            var delayMsec = unchecked((uint)(_delay * 1000.0f));

            if (_changes.Count == 0)
                return false;

            foreach (var pair in _changes)
            {
                if (Environment.TickCount64 - pair.Value.Timestamp >= delayMsec)
                {
                    dest = pair.Value.Change.Clone();
                    _changes.Remove(pair.Key);
                    return true;
                }
            }

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

    private void OnCreated(object sender, FileSystemEventArgs args)
    {
        AddChange(new FileChange
        {
            Kind = FileChangeKind.Added,
            FileName = GetInternalPath(args.FullPath),
            OldFileName = string.Empty
        });
    }

    private void OnDeleted(object sender, FileSystemEventArgs args)
    {
        AddChange(new FileChange
        {
            Kind = FileChangeKind.Removed,
            FileName = GetInternalPath(args.FullPath),
            OldFileName = string.Empty
        });
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        AddChange(new FileChange
        {
            Kind = FileChangeKind.Modified,
            FileName = GetInternalPath(args.FullPath),
            OldFileName = string.Empty
        });
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

    private struct TimedFileChange
    {
        public TimedFileChange(in FileChange change, long timestamp)
        {
            Change = change.Clone();
            Timestamp = timestamp;
        }

        /// <summary>File change information.</summary>
        public FileChange Change;
        /// <summary>Timer used to filter out repeated events when file is being written.</summary>
        public long Timestamp;
    }

    /// <summary>The path being watched.</summary>
    private string _path = string.Empty;
    /// <summary>Pending changes. These will be returned and removed from the list when their timer has exceeded the delay.</summary>
    private readonly Dictionary<string, TimedFileChange> _changes = new();
    /// <summary>Mutex for the change buffer.</summary>
    private readonly object _changesMutex = new();
    /// <summary>Delay in seconds for notifying changes.</summary>
    private float _delay;
    /// <summary>Watch subdirectories flag.</summary>
    private bool _watchSubDirs;
    /// <summary>Platform file watcher for the path being watched.</summary>
    private FileSystemWatcher? _fileWatcher;
}
