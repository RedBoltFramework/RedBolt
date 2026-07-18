using System;
using System.Collections.Generic;

namespace RedBolt;

/// <summary>
/// Lightweight mount point that provides read-only access to the externally managed memory.
/// </summary>
public class MountedExternalMemory : MountPoint
{
    public MountedExternalMemory(string scheme)
    {
        _scheme = scheme;
    }

    public void LinkMemory(string fileName, MemoryBuffer memory)
    {
        _files[fileName] = memory;
    }

    public void LinkMemory(string fileName, string content)
    {
        LinkMemory(fileName, new MemoryBuffer(content));
    }

    public void UnlinkMemory(string fileName)
    {
        _files.Remove(fileName);
    }

    public void SendFileChangedEvent(string fileName)
    {
        var eventData = new ResourceEvents.FileChanged
        {
            FileName = string.Empty,
            ResourceName = new FileIdentifier(_scheme, fileName).ToUri()
        };
        SendEvent(ref eventData);
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool AcceptsScheme(string scheme)
    {
        return string.Equals(scheme, _scheme, StringComparison.Ordinal);
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool Exists(FileIdentifier fileName)
    {
        return AcceptsScheme(fileName.Scheme) && _files.ContainsKey(fileName.FileName);
    }

    /// <summary>Implement MountPoint.</summary>
    public override AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode)
    {
        if (mode == FileMode.Write || mode == FileMode.ReadWrite)
            return null;

        if (!_files.TryGetValue(fileName.FileName, out var memory))
            return null;

        return new MemoryBuffer(memory.GetData(), memory.Size);
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetName()
    {
        return _scheme;
    }

    /// <summary>Implement MountPoint.</summary>
    public override void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
    {
        if ((flags & ScanFlags.ScanAppend) != ScanFlags.ScanAppend)
            result.Clear();

        var filterExtension = string.Empty;
        var dotPosition = filter.LastIndexOf('.');
        if (dotPosition >= 0)
        {
            filterExtension = filter[dotPosition..];
            if (filterExtension.Contains('*', StringComparison.Ordinal))
                filterExtension = string.Empty;
        }

        var recursive = (flags & ScanFlags.ScanRecursive) == ScanFlags.ScanRecursive;
        foreach (var name in _files.Keys)
        {
            if (!name.StartsWith(pathName, StringComparison.Ordinal))
                continue;

            if (name.Length > pathName.Length && name[pathName.Length] != '/')
                continue;

            if (filterExtension.Length != 0 && !name.EndsWith(filterExtension, StringComparison.Ordinal))
                continue;

            if (!recursive)
            {
                var relativeFileName = name[pathName.Length..];
                if (relativeFileName.StartsWith("/", StringComparison.Ordinal))
                    relativeFileName = relativeFileName[1..];

                if (relativeFileName.Contains('/', StringComparison.Ordinal))
                    continue;
            }

            if (pathName.Length >= name.Length)
                continue;

            var trimmedName = name[pathName.Length..];
            if (trimmedName.StartsWith("/", StringComparison.Ordinal))
                trimmedName = trimmedName[1..];
            result.Add(trimmedName);
        }
    }

    private readonly string _scheme;
    private readonly Dictionary<string, MemoryBuffer> _files = new();
}
