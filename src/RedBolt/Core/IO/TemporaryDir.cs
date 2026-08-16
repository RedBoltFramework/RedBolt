// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IO;

using System;
using System.IO;

/// <summary>Helper class to create and destory temporary directory.</summary>
public sealed class TemporaryDir : IDisposable
{
    public TemporaryDir(string path)
    {
        Path = AddTrailingSlash(path);
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        _disposed = true;
    }

    /// <summary>Return the path.</summary>
    public string Path { get; }

    private bool _disposed;

    private static string AddTrailingSlash(string path)
    {
        return System.IO.Path.EndsInDirectorySeparator(path) ? path : path + System.IO.Path.DirectorySeparatorChar;
    }
}
