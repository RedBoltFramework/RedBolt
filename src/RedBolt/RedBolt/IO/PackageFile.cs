using System;
using System.Collections.Generic;

using RedBolt.Container;
using RedBolt.Input;
namespace RedBolt.IO;

/// <summary>File entry within the package file.</summary>
public struct PackageEntry
{
    /// <summary>Offset from the beginning.</summary>
    public uint Offset;

    /// <summary>File size.</summary>
    public uint Size;

    /// <summary>File checksum.</summary>
    public uint Checksum;
}

/// <summary>Stores files of a directory tree sequentially for convenient access.</summary>
public class PackageFile : MountPoint
{
    /// <summary>Construct.</summary>
    public PackageFile()
    {
    }

    /// <summary>Construct and open.</summary>
    public PackageFile(string fileName, uint startOffset = 0)
    {
        Open(fileName, startOffset);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _fileName = string.Empty;
        _entries.Clear();
        base.Dispose();
    }

    /// <summary>Open the package file. Return true if successful.</summary>
    public bool Open(string fileName, uint startOffset = 0)
    {
        using var file = new File(fileName);
        if (!file.IsOpen)
            return false;

        file.Seek(startOffset);
        var id = file.ReadFileID();
        if (id != "UPAK" && id != "ULZ4" && id != "RPAK" && id != "RLZ4")
        {
            if (startOffset == 0)
            {
                var fileSize = file.Size;
                file.Seek(fileSize - sizeof(uint));
                var newStartOffset = fileSize - file.ReadUInt();
                if (newStartOffset < fileSize)
                {
                    startOffset = newStartOffset;
                    file.Seek(startOffset);
                    id = file.ReadFileID();
                }
            }

            if (id != "UPAK" && id != "ULZ4" && id != "RPAK" && id != "RLZ4")
                return false;
        }

        _fileName = fileName;
        _nameHash = new StringHash(fileName);
        _totalSize = file.Size;
        _compressed = id == "ULZ4" || id == "RLZ4";
        var numFiles = file.ReadUInt();
        _checksum = file.ReadUInt();

        if (id == "RPAK" || id == "RLZ4")
        {
            var version = file.ReadUInt();
            System.Diagnostics.Debug.Assert(version == 0);
            var fileListOffset = file.ReadInt64();
            file.Seek(unchecked((uint)fileListOffset));
        }

        for (var i = 0u; i < numFiles; ++i)
        {
            var entryName = file.ReadString();
            var newEntry = new PackageEntry();
            newEntry.Offset = file.ReadUInt() + startOffset;
            _totalDataSize += newEntry.Size = file.ReadUInt();
            newEntry.Checksum = file.ReadUInt();
            if (!_compressed && newEntry.Offset + newEntry.Size > _totalSize)
                return false;

            _entries[entryName] = newEntry;
        }

        return true;
    }

    /// <summary>Check if a file exists within the package file. This will be case-insensitive on Windows and case-sensitive on other platforms.</summary>
    public bool Exists(string fileName)
    {
        if (_entries.ContainsKey(fileName))
            return true;

        if (OperatingSystem.IsWindows())
        {
            foreach (var entryName in _entries.Keys)
            {
                if (string.Equals(entryName, fileName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Return the file entry corresponding to the name, or null if not found. This will be case-insensitive on Windows and case-sensitive on other platforms.</summary>
    public PackageEntry? GetEntry(string fileName)
    {
        if (_entries.TryGetValue(fileName, out var entry))
            return entry;

        if (OperatingSystem.IsWindows())
        {
            foreach (var pair in _entries)
            {
                if (string.Equals(pair.Key, fileName, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
        }

        return null;
    }

    /// <summary>Return all file entries.</summary>
    public IReadOnlyDictionary<string, PackageEntry> GetEntries()
    {
        return _entries;
    }

    /// <summary>Return hash of the package file name.</summary>
    public StringHash GetNameHash()
    {
        return _nameHash;
    }

    /// <summary>Return number of files.</summary>
    public uint GetNumFiles()
    {
        return (uint)_entries.Count;
    }

    /// <summary>Return total size of the package file.</summary>
    public uint GetTotalSize()
    {
        return _totalSize;
    }

    /// <summary>Return total data size from all the file entries in the package file.</summary>
    public uint GetTotalDataSize()
    {
        return _totalDataSize;
    }

    /// <summary>Return checksum of the package file contents.</summary>
    public uint GetChecksum()
    {
        return _checksum;
    }

    /// <summary>Return whether the files are compressed.</summary>
    public bool IsCompressed()
    {
        return _compressed;
    }

    /// <summary>Return list of file names in the package.</summary>
    public List<string> GetEntryNames()
    {
        return new List<string>(_entries.Keys);
    }

    /// <summary>Return a file name in the package at the specified index.</summary>
    public string GetEntryName(uint index)
    {
        var currentIndex = 0u;
        foreach (var entry in _entries)
        {
            if (currentIndex == index)
                return entry.Key;
            ++currentIndex;
        }

        return string.Empty;
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool AcceptsScheme(string scheme)
    {
        return string.IsNullOrEmpty(scheme) || string.Equals(scheme, GetName(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Check if a file exists within the mount point.</summary>
    public override bool Exists(FileIdentifier fileName)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return false;

        return Exists(fileName.FileName);
    }

    /// <summary>Open package file. Returns null if file not found.</summary>
    public override AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode)
    {
        if (mode != FileMode.Read)
            return null;

        if (!string.IsNullOrEmpty(fileName.Scheme) && fileName.Scheme != GetName())
            return null;

        if (!Exists(fileName.FileName))
            return null;

        var file = new File(this, fileName.FileName);
        file.SetName(fileName.ToUri());
        return file;
    }

    /// <summary>Implement MountPoint.</summary>
    public override uint? GetLastModifiedTime(FileIdentifier fileName, bool creationIsModification)
    {
        if (!Exists(fileName))
            return null;

        var modifiedTime = System.IO.File.GetLastWriteTimeUtc(_fileName);
        if (OperatingSystem.IsWindows() && creationIsModification)
        {
            var creationTime = System.IO.File.GetCreationTimeUtc(_fileName);
            if (creationTime > modifiedTime)
                modifiedTime = creationTime;
        }

        var seconds = new DateTimeOffset(modifiedTime).ToUnixTimeSeconds();
        return seconds > 0 ? unchecked((uint)seconds) : 0u;
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetName()
    {
        return _fileName;
    }

    /// <summary>Implement MountPoint.</summary>
    public override void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
    {
        if ((flags & ScanFlags.ScanAppend) != ScanFlags.ScanAppend)
            result.Clear();

        var recursive = (flags & ScanFlags.ScanRecursive) == ScanFlags.ScanRecursive;
        var filterExtension = string.Empty;
        var dotPosition = filter.LastIndexOf('.');
        if (dotPosition >= 0)
        {
            filterExtension = filter[dotPosition..];
            if (filterExtension.Contains('*', StringComparison.Ordinal))
                filterExtension = string.Empty;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var entryName in GetEntryNames())
        {
            if (!entryName.StartsWith(pathName, comparison))
                continue;

            if (entryName.Length > pathName.Length && entryName[pathName.Length] != '/')
                continue;

            if (filterExtension.Length != 0 && !entryName.EndsWith(filterExtension, comparison))
                continue;

            if (!recursive)
            {
                var relativeFileName = entryName[pathName.Length..];
                if (relativeFileName.StartsWith("/", StringComparison.Ordinal))
                    relativeFileName = relativeFileName[1..];

                if (relativeFileName.Contains('/', StringComparison.Ordinal))
                    continue;
            }

            if (pathName.Length >= entryName.Length)
                continue;

            var trimmedName = entryName[pathName.Length..];
            if (trimmedName.StartsWith("/", StringComparison.Ordinal))
                trimmedName = trimmedName[1..];
            result.Add(trimmedName);
        }
    }

    private readonly Dictionary<string, PackageEntry> _entries = new();
    private string _fileName = string.Empty;
    private StringHash _nameHash;
    private uint _totalSize;
    private uint _totalDataSize;
    private uint _checksum;
    private bool _compressed;
}
