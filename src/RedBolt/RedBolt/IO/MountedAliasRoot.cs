using System;
using System.Collections.Generic;

namespace RedBolt;

/// <summary>Mount point that provides named aliases to other mount points.</summary>
public class MountedAliasRoot : MountPoint
{
    /// <summary>Construct.</summary>
    public MountedAliasRoot()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _aliases.Clear();
        base.Dispose();
    }

    /// <summary>Add alias to another mount point.</summary>
    public void AddAlias(string path, string scheme, MountPoint? mountPoint)
    {
        _aliases[path] = (mountPoint is not null ? new WeakReference<MountPoint>(mountPoint) : null, scheme);
    }

    /// <summary>Remove all aliases to the mount point.</summary>
    public void RemoveAliases(MountPoint? mountPoint)
    {
        var aliasesToRemove = new List<string>();
        foreach (var pair in _aliases)
        {
            MountPoint? target = null;
            pair.Value.MountPoint?.TryGetTarget(out target);
            if (ReferenceEquals(target, mountPoint))
                aliasesToRemove.Add(pair.Key);
        }

        foreach (var alias in aliasesToRemove)
            _aliases.Remove(alias);
    }

    /// <summary>
    /// Find mount point and its alias for the specified file name.
    /// Returns mount point, alias and recommended scheme.
    /// </summary>
    public (MountPoint? MountPoint, string Alias, string Scheme) FindMountPoint(string fileName)
    {
        const string aliasSeparator = ":/";
        var separatorPosition = fileName.IndexOf(aliasSeparator, StringComparison.Ordinal);
        if (separatorPosition < 0)
            return (null, string.Empty, string.Empty);

        var alias = fileName[..separatorPosition];
        if (!_aliases.TryGetValue(alias, out var value) || value.MountPoint is null
            || !value.MountPoint.TryGetTarget(out var mountPoint))
            return (null, string.Empty, string.Empty);

        return (mountPoint, alias, value.Scheme);
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool AcceptsScheme(string scheme)
    {
        return string.Equals(scheme, "alias", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool Exists(FileIdentifier fileName)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return false;

        var (mountPoint, alias, scheme) = FindMountPoint(fileName.FileName);
        if (mountPoint is null)
            return false;

        var resolvedFileName = StripFileIdentifier(fileName, alias, scheme);
        return mountPoint.Exists(resolvedFileName);
    }

    /// <summary>Implement MountPoint.</summary>
    public override AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return null;

        var (mountPoint, alias, scheme) = FindMountPoint(fileName.FileName);
        if (mountPoint is null)
            return null;

        var resolvedFileName = StripFileIdentifier(fileName, alias, scheme);
        return mountPoint.OpenFile(resolvedFileName, mode);
    }

    /// <summary>Implement MountPoint.</summary>
    public override uint? GetLastModifiedTime(FileIdentifier fileName, bool creationIsModification)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return null;

        var (mountPoint, alias, scheme) = FindMountPoint(fileName.FileName);
        if (mountPoint is null)
            return null;

        var resolvedFileName = StripFileIdentifier(fileName, alias, scheme);
        return mountPoint.GetLastModifiedTime(resolvedFileName, creationIsModification);
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetName()
    {
        return "alias://";
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetAbsoluteNameFromIdentifier(FileIdentifier fileName)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return string.Empty;

        var (mountPoint, alias, scheme) = FindMountPoint(fileName.FileName);
        if (mountPoint is null)
            return string.Empty;

        var resolvedFileName = StripFileIdentifier(fileName, alias, scheme);
        return mountPoint.GetAbsoluteNameFromIdentifier(resolvedFileName);
    }

    /// <summary>Implement MountPoint.</summary>
    public override FileIdentifier GetIdentifierFromAbsoluteName(string absoluteFileName)
    {
        // This operation is not supported, actual mount points should do this work.
        return FileIdentifier.Empty;
    }

    /// <summary>Implement MountPoint.</summary>
    public override void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
    {
        // Scanning is not supported for aliases.
    }

    private static FileIdentifier StripFileIdentifier(FileIdentifier fileName, string alias, string scheme)
    {
        const string aliasSeparator = ":/";
        var offset = alias.Length + aliasSeparator.Length;
        var strippedFileName = offset < fileName.FileName.Length ? fileName.FileName[offset..] : string.Empty;
        return new FileIdentifier(scheme, strippedFileName);
    }

    private readonly Dictionary<string, (WeakReference<MountPoint>? MountPoint, string Scheme)> _aliases = new();
}
