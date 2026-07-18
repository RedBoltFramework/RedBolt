using System;
using System.Collections.Generic;
using System.IO;

namespace RedBolt;

/// <summary>Stores files of a directory tree sequentially for convenient access.</summary>
public class MountedDirectory : WatchableMountPoint
{
    /// <summary>Construct and open.</summary>
    public MountedDirectory(string directory, string scheme = "")
    {
        _scheme = scheme;
        _directory = SanitizeDirName(directory);
        _name = string.IsNullOrEmpty(_scheme) ? _directory : _scheme + "://" + _directory;
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool AcceptsScheme(string scheme)
    {
        return string.Equals(scheme, _scheme, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool Exists(FileIdentifier fileName)
    {
        // File system directory only reacts on specific scheme.
        if (!AcceptsScheme(fileName.Scheme))
            return false;

        return System.IO.File.Exists(_directory + fileName.FileName);
    }

    /// <summary>Implement MountPoint.</summary>
    public override AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode)
    {
        // File system directory only reacts on specific scheme.
        if (!AcceptsScheme(fileName.Scheme))
            return null;

        var needRead = mode == FileMode.Read || mode == FileMode.ReadWrite;
        var needWrite = mode == FileMode.Write || mode == FileMode.ReadWrite;
        var fullPath = _directory + fileName.FileName;

        if (needRead && !System.IO.File.Exists(fullPath))
            return null;

        if (needWrite)
        {
            var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (IOException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }
        }

        var file = new File(fullPath, mode);
        if (!file.IsOpen)
        {
            file.Dispose();
            return null;
        }

        file.SetName(fileName.ToUri());
        return file;
    }

    /// <summary>Implement MountPoint.</summary>
    public override uint? GetLastModifiedTime(FileIdentifier fileName, bool creationIsModification)
    {
        if (!Exists(fileName))
            return null;

        var fullPath = _directory + fileName.FileName;
        var modifiedTime = System.IO.File.GetLastWriteTimeUtc(fullPath);
        if (OperatingSystem.IsWindows() && creationIsModification)
        {
            var creationTime = System.IO.File.GetCreationTimeUtc(fullPath);
            if (creationTime > modifiedTime)
                modifiedTime = creationTime;
        }

        var seconds = new DateTimeOffset(modifiedTime).ToUnixTimeSeconds();
        return seconds > 0 ? unchecked((uint)seconds) : 0u;
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetName()
    {
        return _name;
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetAbsoluteNameFromIdentifier(FileIdentifier fileName)
    {
        if (Exists(fileName))
            return _directory + fileName.FileName;

        return string.Empty;
    }

    /// <summary>Implement MountPoint.</summary>
    public override FileIdentifier GetIdentifierFromAbsoluteName(string absoluteFileName)
    {
        if (absoluteFileName.StartsWith(_directory, StringComparison.Ordinal))
            return new FileIdentifier(_scheme, absoluteFileName[_directory.Length..]);

        return FileIdentifier.Empty;
    }

    /// <summary>Implement MountPoint.</summary>
    public override void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
    {
        var root = _directory + pathName;
        filter = string.IsNullOrEmpty(filter) ? "*" : filter;
        if ((flags & ScanFlags.ScanAppend) != ScanFlags.ScanAppend)
            result.Clear();

        if (!Directory.Exists(root))
            return;

        var options = (flags & ScanFlags.ScanRecursive) == ScanFlags.ScanRecursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        if ((flags & ScanFlags.ScanFiles) == ScanFlags.ScanFiles)
        {
            foreach (var file in Directory.EnumerateFiles(root, filter, options))
            {
                if ((flags & ScanFlags.ScanHidden) != ScanFlags.ScanHidden
                    && (System.IO.File.GetAttributes(file) & FileAttributes.Hidden) != 0)
                    continue;
                result.Add(Path.GetRelativePath(_directory, file).Replace('\\', '/'));
            }
        }

        if ((flags & ScanFlags.ScanDirs) == ScanFlags.ScanDirs)
        {
            foreach (var directory in Directory.EnumerateDirectories(root, filter, options))
            {
                if ((flags & ScanFlags.ScanHidden) != ScanFlags.ScanHidden
                    && (System.IO.File.GetAttributes(directory) & FileAttributes.Hidden) != 0)
                    continue;
                result.Add(Path.GetRelativePath(_directory, directory).Replace('\\', '/'));
            }
        }
    }

    /// <summary>Get mounted directory path.</summary>
    public string GetDirectory()
    {
        return _directory;
    }

    protected string SanitizeDirName(string name)
    {
        var fixedPath = name.Replace('\\', '/');
        if (!fixedPath.EndsWith("/", StringComparison.Ordinal))
            fixedPath += "/";
        if (!Path.IsPathFullyQualified(fixedPath))
        {
            var currentDirectory = Directory.GetCurrentDirectory().Replace('\\', '/');
            if (!currentDirectory.EndsWith("/", StringComparison.Ordinal))
                currentDirectory += "/";
            fixedPath = currentDirectory + fixedPath;
        }

        fixedPath = fixedPath.Replace("/./", "/");
        return fixedPath.Trim();
    }

    protected override void StartWatching()
    {
        if (_fileWatcher is null)
            _fileWatcher = new FileWatcher();

        _fileWatcher.StartWatching(_directory, true);

        SubscribeToEvent<CoreEvents.BeginFrameEvent>((ref CoreEvents.BeginFrameEvent _) => ProcessUpdates());
    }

    protected override void StopWatching()
    {
        if (_fileWatcher is not null)
            _fileWatcher.StopWatching();

        UnsubscribeFromEvent<CoreEvents.BeginFrameEvent>();
    }

    private void ProcessUpdates()
    {
        if (_fileWatcher is null)
            return;

        while (_fileWatcher.GetNextChange(out var change))
        {
            var eventData = new ResourceEvents.FileChanged
            {
                FileName = _fileWatcher.GetPath() + change.FileName,
                ResourceName = new FileIdentifier(_scheme, change.FileName).ToUri()
            };
            SendEvent(ref eventData);
        }
    }

    private readonly string _scheme;
    private readonly string _directory;
    private readonly string _name;
    private FileWatcher? _fileWatcher;
}
