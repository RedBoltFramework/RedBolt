// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IO;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>Lightweight mount point that provides read-only access to the externally managed memory.</summary>
public class MountedRoot : MountPoint
{
    /// <summary>Construct.</summary>
    public MountedRoot()
    {
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool AcceptsScheme(string scheme)
    {
        return string.Equals(scheme, "file", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Implement MountPoint.</summary>
    public override bool Exists(FileIdentifier fileName)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return false;

        return Path.IsPathFullyQualified(fileName.FileName)
            && (System.IO.File.Exists(fileName.FileName) || Directory.Exists(fileName.FileName));
    }

    /// <summary>Implement MountPoint.</summary>
    public override AbstractFile? OpenFile(FileIdentifier fileName, FileMode mode)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return null;

        if (!Path.IsPathFullyQualified(fileName.FileName))
            return null;

        var needRead = mode == FileMode.Read || mode == FileMode.ReadWrite;
        var needWrite = mode == FileMode.Write || mode == FileMode.ReadWrite;

        if (needRead && !System.IO.File.Exists(fileName.FileName))
            return null;

        if (needWrite)
        {
            var directory = Path.GetDirectoryName(fileName.FileName) ?? string.Empty;
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

        var file = new File(fileName.FileName, mode);
        if (!file.IsOpen)
        {
            file.Dispose();
            return null;
        }

        file.SetName(fileName.ToUri());
        return file;
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetName()
    {
        return "file://";
    }

    /// <summary>Implement MountPoint.</summary>
    public override string GetAbsoluteNameFromIdentifier(FileIdentifier fileName)
    {
        if (!AcceptsScheme(fileName.Scheme))
            return string.Empty;

        if (Path.IsPathFullyQualified(fileName.FileName) && System.IO.File.Exists(fileName.FileName))
            return fileName.FileName;

        return string.Empty;
    }

    /// <summary>Implement MountPoint.</summary>
    public override FileIdentifier GetIdentifierFromAbsoluteName(string absoluteFileName)
    {
        return new FileIdentifier("file", absoluteFileName);
    }

    /// <summary>Implement MountPoint.</summary>
    public override void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
    {
        // Disable Scan for the root until we add scheme filtering.
    }
}
