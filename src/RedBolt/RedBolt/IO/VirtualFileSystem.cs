namespace RedBolt.IO;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using RedBolt.Core;
using RedBolt.Logging;

/// <summary>Subsystem for virtual file system.</summary>
public class VirtualFileSystem : RedObject
{
    /// <summary>Construct.</summary>
    public VirtualFileSystem()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        var aliasMountPoint = _aliasMountPoint;
        var aliasIsMounted = false;
        for (var i = 0; i < _mountPoints.Count; ++i)
        {
            if (ReferenceEquals(_mountPoints[i], aliasMountPoint))
            {
                aliasIsMounted = true;
                break;
            }
        }

        _aliasMountPoint = null;
        if (aliasMountPoint is not null && !aliasIsMounted)
            aliasMountPoint.Dispose();

        for (var i = _mountPoints.Count - 1; i >= 0; --i)
            _mountPoints[i].Dispose();
        _mountPoints.Clear();
        base.Dispose();
    }

    /// <summary>Mount alias root as alias:// scheme. Alias root will be mounted automatically if alias is created.</summary>
    public MountPoint MountAliasRoot()
    {
        lock (_mountMutex)
        {
            return GetOrCreateAliasRoot();
        }
    }

    /// <summary>Mount file system root as file:// scheme.</summary>
    public MountPoint MountRoot()
    {
        var mountPoint = new MountedRoot();
        Mount(mountPoint);
        return mountPoint;
    }

    /// <summary>Mount real folder into virtual file system.</summary>
    public MountPoint MountDir(string path)
    {
        return MountDir(string.Empty, path);
    }

    /// <summary>Mount real folder into virtual file system under the scheme.</summary>
    public MountPoint MountDir(string scheme, string path)
    {
        var mountPoint = new MountedDirectory(path, scheme);
        Mount(mountPoint);
        return mountPoint;
    }

    /// <summary>Mount subfolders and pak files from real folder into virtual file system.</summary>
    public void AutomountDir(string path)
    {
        AutomountDir(string.Empty, path);
    }

    /// <summary>Mount subfolders and pak files from real folder into virtual file system under the scheme.</summary>
    public void AutomountDir(string scheme, string path)
    {
        if (!Directory.Exists(path))
            return;

        // Add all the subdirs (non-recursive) as resource directory
        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            var directoryName = Path.GetFileName(directory);
            if (directoryName.StartsWith(".", StringComparison.Ordinal))
                continue;

            var pathWithSlash = Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
            var autoResourceDir = pathWithSlash + directoryName;
            MountDir(scheme, autoResourceDir);
        }

        // Add all the found package files (non-recursive)
        foreach (var packageFile in Directory.EnumerateFiles(path, "*.pak", SearchOption.TopDirectoryOnly))
        {
            var packageFileName = Path.GetFileName(packageFile);
            if (packageFileName.StartsWith(".", StringComparison.Ordinal))
                continue;

            var pathWithSlash = Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
            var autoPackageName = pathWithSlash + packageFileName;
            MountPackageFile(autoPackageName);
        }
    }

    /// <summary>Mount package file into virtual file system.</summary>
    public MountPoint? MountPackageFile(string path)
    {
        var packageFile = new PackageFile();
        if (packageFile.Open(path, 0u))
        {
            Mount(packageFile);
            return packageFile;
        }

        packageFile.Dispose();
        return null;
    }

    /// <summary>Mount virtual or real folder into virtual file system.</summary>
    public void Mount(MountPoint mountPoint)
    {
        lock (_mountMutex)
        {
            for (var i = 0; i < _mountPoints.Count; ++i)
            {
                if (ReferenceEquals(_mountPoints[i], mountPoint))
                    return;
            }

            _mountPoints.Add(mountPoint);
            mountPoint.SetWatching(_isWatching);

            if (mountPoint is MountedAliasRoot mountedAliasRoot)
            {
                if (_aliasMountPoint is not null)
                    Log.GetLogger().LogWarning("Mounted alias root when one already exists, overwriting.");
                _aliasMountPoint = mountedAliasRoot;
            }
        }
    }

    /// <summary>Mount alias to another mount point.</summary>
    public void MountAlias(string alias, MountPoint? mountPoint, string scheme = "")
    {
        lock (_mountMutex)
        {
            GetOrCreateAliasRoot().AddAlias(alias, scheme, mountPoint);
        }
    }

    /// <summary>Remove mount point from the virtual file system.</summary>
    public void Unmount(MountPoint? mountPoint)
    {
        lock (_mountMutex)
        {
            _aliasMountPoint?.RemoveAliases(mountPoint);

            for (var i = 0; i < _mountPoints.Count; ++i)
            {
                if (ReferenceEquals(_mountPoints[i], mountPoint))
                {
                    // Erase the slow way because order of the mount points matters.
                    _mountPoints.RemoveAt(i);
                    if (!ReferenceEquals(_aliasMountPoint, mountPoint))
                        mountPoint?.Dispose();
                    break;
                }
            }
        }
    }

    /// <summary>Remove all mount points.</summary>
    public void UnmountAll()
    {
        lock (_mountMutex)
        {
            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                if (!ReferenceEquals(_mountPoints[i], _aliasMountPoint))
                    _mountPoints[i].Dispose();
            }
            _mountPoints.Clear();
            _aliasMountPoint?.Dispose();
            _aliasMountPoint = null;
        }
    }

    /// <summary>Get number of mount points.</summary>
    public uint NumMountPoints()
    {
        return checked((uint)_mountPoints.Count);
    }

    /// <summary>Get mount point by index.</summary>
    public MountPoint? GetMountPoint(uint index)
    {
        lock (_mountMutex)
        {
            return index < (uint)_mountPoints.Count ? _mountPoints[(int)index] : null;
        }
    }

    /// <summary>Mount all existing packages for each combination of prefix path and relative path.</summary>
    public void MountExistingPackages(StringVector prefixPaths, StringVector relativePaths)
    {
        foreach (var prefixPath in prefixPaths)
        {
            foreach (var relativePath in relativePaths)
            {
                var packagePath = prefixPath + relativePath;
                if (System.IO.File.Exists(packagePath))
                {
                    var mountPoint = MountPackageFile(packagePath);
                    if (mountPoint is not null)
                        MountAlias($"res:{relativePath}", mountPoint);
                }
            }
        }
    }

    /// <summary>Mount all existing directories and packages for each combination of prefix path and relative path.</summary>
    /// <remarks>Package is preferred over directory if both exist.</remarks>
    public void MountExistingDirectoriesOrPackages(StringVector prefixPaths, StringVector relativePaths)
    {
        foreach (var prefixPath in prefixPaths)
        {
            foreach (var relativePath in relativePaths)
            {
                var packagePath = prefixPath + relativePath + ".pak";
                var directoryPath = prefixPath + relativePath;
                if (System.IO.File.Exists(packagePath))
                {
                    var mountPoint = MountPackageFile(packagePath);
                    if (mountPoint is not null)
                        MountAlias($"res:{relativePath}", mountPoint);
                }
                else if (Directory.Exists(directoryPath))
                {
                    var mountPoint = MountDir(directoryPath);
                    if (mountPoint is not null)
                        MountAlias($"res:{relativePath}", mountPoint);
                }
            }
        }
    }

    /// <summary>Check if a file exists in the virtual file system.</summary>
    public bool Exists(FileIdentifier fileName)
    {
        lock (_mountMutex)
        {
            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                if (_mountPoints[i].Exists(fileName))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Open file in the virtual file system. Returns null if file not found.</summary>
    public AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode)
    {
        if (fileName.IsEmpty())
            return null;

        lock (_mountMutex)
        {
            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                var result = _mountPoints[i].OpenFile(fileName, mode);
                if (result is not null)
                    return result;
            }
        }

        return null;
    }

    /// <summary>Read text file from the virtual file system. Returns empty string if file not found.</summary>
    public string ReadAllText(FileIdentifier fileName)
    {
        var file = OpenFile(fileName, FileMode.Read);
        if (file is null)
            return string.Empty;

        try
        {
            var dataSize = file.Size;
            var buffer = new byte[checked((int)dataSize)];
            file.Read(buffer);
            return Encoding.UTF8.GetString(buffer);
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>Write text file to the virtual file system. Returns true if file is written successfully.</summary>
    public bool WriteAllText(FileIdentifier fileName, string text)
    {
        var file = OpenFile(fileName, FileMode.Write);
        if (file is null)
            return false;

        try
        {
            var data = Encoding.UTF8.GetBytes(text);
            return file.Write(data) == data.Length;
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>Return modification time. Return 0 if not supported or file doesn't exist.</summary>
    public uint GetLastModifiedTime(FileIdentifier fileName, bool creationIsModification)
    {
        lock (_mountMutex)
        {
            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                var result = _mountPoints[i].GetLastModifiedTime(fileName, creationIsModification);
                if (result.HasValue)
                    return result.Value;
            }
        }

        return 0;
    }

    /// <summary>Return absolute file name for <c>existing</c> identifier in this mount point, if supported.</summary>
    public string GetAbsoluteNameFromIdentifier(FileIdentifier fileName)
    {
        lock (_mountMutex)
        {
            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                var result = _mountPoints[i].GetAbsoluteNameFromIdentifier(fileName);
                if (!string.IsNullOrEmpty(result))
                    return result;
            }
        }

        return string.Empty;
    }

    /// <summary>Return canonical file identifier, if possible.</summary>
    public FileIdentifier GetCanonicalIdentifier(FileIdentifier fileName)
    {
        var result = fileName;

        // .. is not supported
        result.FileName = result.FileName.Replace("../", string.Empty, StringComparison.Ordinal);
        result.FileName = result.FileName.Replace("./", string.Empty, StringComparison.Ordinal);
        result.FileName = result.FileName.Trim();

        // Attempt to go from "file" scheme to local schemes
        if (result.Scheme == "file")
        {
            var betterName = GetIdentifierFromAbsoluteName(result.FileName);
            if (!betterName.IsEmpty())
                result = betterName;
        }

        return result;
    }

    /// <summary>Works even if the file does not exist.</summary>
    public FileIdentifier GetIdentifierFromAbsoluteName(string absoluteFileName)
    {
        lock (_mountMutex)
        {
            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                var result = _mountPoints[i].GetIdentifierFromAbsoluteName(absoluteFileName);
                if (!result.IsEmpty())
                    return result;
            }
        }

        return FileIdentifier.Empty;
    }

    /// <summary>Return relative file name of the file, or empty if not found.</summary>
    public FileIdentifier GetIdentifierFromAbsoluteName(string scheme, string absoluteFileName)
    {
        lock (_mountMutex)
        {
            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                if (!_mountPoints[i].AcceptsScheme(scheme))
                    continue;

                var result = _mountPoints[i].GetIdentifierFromAbsoluteName(absoluteFileName);
                if (!result.IsEmpty())
                    return result;
            }
        }

        return FileIdentifier.Empty;
    }

    /// <summary>Enable or disable file watchers.</summary>
    public void SetWatching(bool enable)
    {
        if (_isWatching != enable)
        {
            lock (_mountMutex)
            {
                _isWatching = enable;
                for (var i = _mountPoints.Count - 1; i >= 0; --i)
                    _mountPoints[i].SetWatching(_isWatching);
            }
        }
    }

    /// <summary>Returns true if the file watchers are enabled.</summary>
    public bool IsWatching()
    {
        return _isWatching;
    }

    /// <summary>Scan for specified files.</summary>
    public void Scan(ICollection<string> result, string scheme, string pathName, string filter, ScanFlags flags)
    {
        lock (_mountMutex)
        {
            if ((flags & ScanFlags.ScanAppend) == 0)
                result.Clear();

            for (var i = _mountPoints.Count - 1; i >= 0; --i)
            {
                if (_mountPoints[i].AcceptsScheme(scheme))
                    _mountPoints[i].Scan(result, pathName, filter, flags | ScanFlags.ScanAppend);
            }
        }
    }

    /// <summary>Scan for specified files.</summary>
    public void Scan(ICollection<string> result, FileIdentifier pathName, string filter, ScanFlags flags)
    {
        Scan(result, pathName.Scheme, pathName.FileName, filter, flags);
    }

    /// <summary>Return or create internal alias:// mount point.</summary>
    private MountedAliasRoot GetOrCreateAliasRoot()
    {
        if (_aliasMountPoint is null)
        {
            _aliasMountPoint = new MountedAliasRoot();
            _mountPoints.Add(_aliasMountPoint);
        }

        return _aliasMountPoint;
    }

    private readonly object _mountMutex = new();
    private readonly List<MountPoint> _mountPoints = new();
    private MountedAliasRoot? _aliasMountPoint;
    private bool _isWatching;
}

/// <summary>Helper class to mount and unmount an object automatically.</summary>
public class MountPointGuard : IDisposable
{
    public MountPointGuard(MountPoint? mountPoint)
    {
        _mountPoint = mountPoint;
        if (_mountPoint is not null)
        {
            var vfs = _mountPoint.Context.GetSubsystem<VirtualFileSystem>();
            vfs!.Mount(_mountPoint);
        }
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    public MountPointGuard(MountPointGuard other)
    {
        MoveAssign(other);
    }

    public MountPointGuard MoveAssign(MountPointGuard other)
    {
        Release();
        _mountPoint = other._mountPoint;
        other._mountPoint = null;
        return this;
    }

    public void Release()
    {
        if (_mountPoint is not null)
        {
            var vfs = _mountPoint.Context.GetSubsystem<VirtualFileSystem>();
            vfs!.Unmount(_mountPoint);
            _mountPoint = null;
        }
    }

    public MountPoint? Get()
    {
        return _mountPoint;
    }

    private MountPoint? _mountPoint;
}
