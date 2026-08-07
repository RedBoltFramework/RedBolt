//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using RedBolt.Core;
using RedBolt.IO;

/// <summary>Description of file system entry (file or directory) with hierarchy information.</summary>
public class FileSystemEntry : ICloneable
{
    public FileSystemReflection? Owner;
    public FileSystemEntry? Parent;

    public string ResourceName = string.Empty;
    public string AbsolutePath = string.Empty;
    public bool IsDirectory;
    public bool IsFile;
    public int DirectoryIndex;

    public string LocalName = string.Empty;
    public bool IsFileAmbiguous;

    public List<FileSystemEntry> Children = [];

    public static bool ComparePathFilesFirst(string lhs, string rhs)
    {
        return CompareEntries(lhs, rhs, true);
    }

    public static bool ComparePathDirectoriesFirst(string lhs, string rhs)
    {
        return CompareEntries(lhs, rhs, false);
    }

    public static bool CompareFilesFirst(FileSystemEntry lhs, FileSystemEntry rhs)
    {
        return ComparePathFilesFirst(lhs.ResourceName, rhs.ResourceName);
    }

    public static bool CompareDirectoriesFirst(FileSystemEntry lhs, FileSystemEntry rhs)
    {
        return ComparePathDirectoriesFirst(lhs.ResourceName, rhs.ResourceName);
    }

    public void ForEach(Action<FileSystemEntry> callback)
    {
        callback(this);
        foreach (var child in Children)
            child.ForEach(callback);
    }

    public void FillParents()
    {
        foreach (var child in Children)
        {
            child.Parent = this;
            child.FillParents();
        }
    }

    public FileSystemEntry? FindChild(string name)
    {
        foreach (var child in Children)
        {
            if (child.LocalName == name)
                return child;
        }
        return null;
    }

    public FileSystemEntry Clone()
    {
        var clone = new FileSystemEntry
        {
            Owner = Owner,
            Parent = Parent,
            ResourceName = ResourceName,
            AbsolutePath = AbsolutePath,
            IsDirectory = IsDirectory,
            IsFile = IsFile,
            DirectoryIndex = DirectoryIndex,
            LocalName = LocalName,
            IsFileAmbiguous = IsFileAmbiguous,
        };
        foreach (var child in Children)
            clone.Children.Add(child.Clone());
        return clone;
    }

    object ICloneable.Clone() => Clone();

    private static bool CompareEntries(string lhs, string rhs, bool filesFirst)
    {
        var lhsBytes = Encoding.UTF8.GetBytes(lhs);
        var rhsBytes = Encoding.UTF8.GetBytes(rhs);
        var length = Math.Min(lhsBytes.Length, rhsBytes.Length);
        for (var index = 0; index < length; ++index)
        {
            var lhsGroup = (lhsBytes[index] != (byte)'/') != filesFirst;
            var rhsGroup = (rhsBytes[index] != (byte)'/') != filesFirst;
            if (lhsGroup != rhsGroup)
                return !lhsGroup;

            var lhsCharacter = unchecked((sbyte)lhsBytes[index]);
            var rhsCharacter = unchecked((sbyte)rhsBytes[index]);
            if (lhsCharacter != rhsCharacter)
                return lhsCharacter < rhsCharacter;
        }
        return lhsBytes.Length < rhsBytes.Length;
    }
}

/// <summary>Utility class that watches all files in ResourceCache.</summary>
public class FileSystemReflection : RedObject
{
    /// <summary>Called whenever resource file is modified or new resource is added.</summary>
    /// <remarks>Can be used to invalidate whatever per-resource caches there are.</remarks>
    public event Action<FileSystemReflection, FileSystemEntry>? OnResourceUpdated;

    public event Action<FileSystemReflection>? OnListUpdated;

    public FileSystemReflection(IEnumerable<string> directories)
    {
        _directories.AddRange(directories);
        foreach (var directory in _directories)
            _fileWatcher.StartWatching(directory, true);
    }

    public override void Dispose()
    {
        _index.Clear();
        _root = new FileSystemEntry();
        _updatedResources.Clear();
        _fileWatcher.Dispose();
        _directories.Clear();
        OnListUpdated = null;
        OnResourceUpdated = null;
        base.Dispose();
    }

    public void Update()
    {
        var change = new FileChange();
        while (_fileWatcher.GetNextChange(ref change))
        {
            _updatedResources.Add(change.FileName);
            if (change.Kind is FileChangeKind.Added or FileChangeKind.Removed or FileChangeKind.Renamed)
                _treeDirty = true;
        }

        if (_treeDirty)
            UpdateEntryTree();

        foreach (var resourceName in _updatedResources)
        {
            var entry = FindEntry(resourceName);
            if (entry is not null)
                OnResourceUpdated?.Invoke(this, entry);
        }
        _updatedResources.Clear();
    }

    public FileSystemEntry Root => _root;

    public FileSystemEntry? FindEntry(string name)
    {
        return _index.GetValueOrDefault(name);
    }

    private void UpdateEntryTree()
    {
        var entries = new List<FileSystemEntry>();
        var index = 0;
        foreach (var resourceDirectory in _directories)
            ScanRootDirectory(resourceDirectory, entries, index++);

        CollectAddedFiles(_root, entries);

        entries.Sort(static (lhs, rhs) =>
            FileSystemEntry.CompareDirectoriesFirst(lhs, rhs)
                ? -1
                : FileSystemEntry.CompareDirectoriesFirst(rhs, lhs) ? 1 : 0);
        var mergedEntries = MergeEntries(entries);

        var root = new FileSystemEntry { Owner = this };
        foreach (var entry in mergedEntries)
            AppendEntry(root, entry);

        _root = root;
        _root.FillParents();
        _treeDirty = false;

        _index.Clear();
        _root.ForEach(entry => _index[entry.ResourceName] = entry);

        OnListUpdated?.Invoke(this);
    }

    private void ScanRootDirectory(string resourceDirectory, List<FileSystemEntry> entries, int index)
    {
        if (!Directory.Exists(resourceDirectory))
            return;

        ScanDirectory(resourceDirectory, string.Empty);

        void ScanDirectory(string directory, string relativeDirectory)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var localName = Path.GetFileName(path);
                if (localName.StartsWith(".", StringComparison.Ordinal))
                    continue;

                var resourceName = relativeDirectory + localName;
                var entry = new FileSystemEntry
                {
                    Owner = this,
                    AbsolutePath = resourceDirectory + resourceName,
                    ResourceName = resourceName,
                    IsFile = System.IO.File.Exists(path),
                    IsDirectory = Directory.Exists(path),
                    DirectoryIndex = index,
                };

                if (entry.IsFile || entry.IsDirectory)
                    entries.Add(entry);
                if (entry.IsDirectory)
                    ScanDirectory(path, resourceName + "/");
            }
        }
    }

    private void CollectAddedFiles(FileSystemEntry oldRoot, List<FileSystemEntry> entries)
    {
        var previousResources = new HashSet<string>(StringComparer.Ordinal);
        oldRoot.ForEach(entry => previousResources.Add(entry.ResourceName));

        foreach (var entry in entries)
        {
            if (entry.IsFile && !previousResources.Contains(entry.ResourceName))
                _updatedResources.Add(entry.ResourceName);
        }
    }

    private List<FileSystemEntry> MergeEntries(List<FileSystemEntry> entries)
    {
        var mergedEntries = new List<FileSystemEntry>();
        foreach (var entry in entries)
        {
            if (mergedEntries.Count != 0 && mergedEntries[^1].ResourceName == entry.ResourceName)
            {
                var existingEntry = mergedEntries[^1];
                if (entry.IsFile && existingEntry.IsFile)
                    existingEntry.IsFileAmbiguous = true;
                if (entry.IsFile)
                    existingEntry.IsFile = true;
                if (entry.IsDirectory)
                    existingEntry.IsDirectory = true;
                if (existingEntry.DirectoryIndex > entry.DirectoryIndex)
                {
                    existingEntry.DirectoryIndex = entry.DirectoryIndex;
                    existingEntry.AbsolutePath = entry.AbsolutePath;
                }
            }
            else
            {
                mergedEntries.Add(entry.Clone());
            }
        }
        return mergedEntries;
    }

    private void AppendEntry(FileSystemEntry root, FileSystemEntry entry)
    {
        var pathParts = entry.ResourceName.Split('/');

        var currentEntry = root;
        foreach (var localName in pathParts)
        {
            if (currentEntry.Children.Count == 0 || currentEntry.Children[^1].LocalName != localName)
            {
                var entryCopy = entry.Clone();
                entryCopy.LocalName = localName;
                currentEntry.Children.Add(entryCopy);
            }

            currentEntry = currentEntry.Children[^1];
        }
    }

    private static void ForEach(FileSystemEntry entry, Action<FileSystemEntry> callback)
    {
        callback(entry);
        foreach (var child in entry.Children)
            ForEach(child, callback);
    }

    private readonly List<string> _directories = [];
    private readonly MultiFileWatcher _fileWatcher = new();

    private bool _treeDirty = true;
    private readonly HashSet<string> _updatedResources = new(StringComparer.Ordinal);

    private FileSystemEntry _root = new();
    private readonly Dictionary<string, FileSystemEntry> _index = new(StringComparer.Ordinal);
}
