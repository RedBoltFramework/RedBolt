using System.Collections.Generic;

namespace RedBolt;

/// <summary>Access to engine file system mount point.</summary>
public abstract class MountPoint : Object
{
    /// <summary>Construct.</summary>
    public MountPoint()
    {
    }

    /// <summary>Destruct.</summary>
    ~MountPoint()
    {
    }

    /// <summary>Checks if mount point accepts scheme.</summary>
    public abstract bool AcceptsScheme(string scheme);

    /// <summary>
    /// Check if a file exists within the mount point.
    /// The file name may be be case-insensitive on Windows and case-sensitive on other platforms.
    /// </summary>
    public abstract bool Exists(FileIdentifier fileName);

    /// <summary>
    /// Open file in a virtual file system. Returns null if file not found.
    /// The file name may be be case-insensitive on Windows and case-sensitive on other platforms.
    /// </summary>
    public abstract AbstractFile? OpenFile(FileIdentifier fileName, File.FileMode mode);

    /// <summary>
    /// Return modification time, or 0 if not supported.
    /// Return nullopt if file does not exist.
    /// </summary>
    public virtual uint? GetLastModifiedTime(FileIdentifier fileName, bool creationIsModification)
    {
        if (Exists(fileName))
            return 0u;
        else
            return null;
    }

    /// <summary>Returns human-readable name of the mount point.</summary>
    public abstract string GetName();

    /// <summary>Return absolute file name for <c>existing</c> identifier in this mount point, if supported.</summary>
    public virtual string GetAbsoluteNameFromIdentifier(FileIdentifier fileName)
    {
        return string.Empty;
    }

    /// <summary>
    /// Return identifier in this mount point for absolute file name, if supported.
    /// Works even if the file does not exist.
    /// </summary>
    public virtual FileIdentifier GetIdentifierFromAbsoluteName(string absoluteFileName)
    {
        return FileIdentifier.Empty;
    }

    /// <summary>Enable or disable FileChanged events.</summary>
    public virtual void SetWatching(bool enable)
    {
    }

    /// <summary>Returns true if FileChanged events are enabled.</summary>
    public virtual bool IsWatching()
    {
        return false;
    }

    /// <summary>Enumerate objects in the mount point. Only files enumeration is guaranteed to be supported.</summary>
    public abstract void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags);
}

/// <summary>Base implementation of watchable mount point.</summary>
public abstract class WatchableMountPoint : MountPoint
{
    /// <summary>Construct.</summary>
    public WatchableMountPoint()
    {
    }

    /// <summary>Destruct.</summary>
    ~WatchableMountPoint()
    {
    }

    /// <summary>Implement MountPoint.</summary>
    public override void SetWatching(bool enable)
    {
        if (_isWatching != enable)
        {
            _isWatching = enable;
            if (_isWatching)
                StartWatching();
            else
                StopWatching();
        }
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool IsWatching()
    {
        return _isWatching;
    }

    /// <summary>Start file watcher.</summary>
    protected abstract void StartWatching();

    /// <summary>Stop file watcher.</summary>
    protected abstract void StopWatching();

    private bool _isWatching;
}
