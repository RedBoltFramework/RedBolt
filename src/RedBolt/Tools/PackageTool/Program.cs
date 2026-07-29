//
// Copyright (c) 2008-2022 the Urho3D project.
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

namespace RedBolt.Tools.PackageTool;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using RedBolt.IO;
using File = RedBolt.IO.File;
using FileMode = RedBolt.IO.FileMode;

public sealed class PackageTool
{
    private const uint CompressedBlockSize = 32768;

    private struct FileEntry
    {
        public string Name;
        public uint Offset;
        public uint Size;
        public uint Checksum;
    }

    private string _basePath = string.Empty;
    private readonly List<FileEntry> _entries = [];
    private uint _checksum;
    private bool _compress;
    private bool _quiet;
    private uint _blockSize = CompressedBlockSize;

    private static readonly string[] IgnoreExtensions =
    [
        ".bak",
        ".rule"
    ];

    public void Run(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2)
        {
            ErrorExit(
                "Usage: PackageTool <directory to process> <package name> [basepath] [options]\n"
                + "\n"
                + "Options:\n"
                + "-c      Enable package file LZ4 compression\n"
                + "-q      Enable quiet mode\n"
                + "\n"
                + "Basepath is an optional prefix that will be added to the file entries.\n\n"
                + "Alternative output usage: PackageTool <output option> <package name>\n"
                + "Output option:\n"
                + "-i      Output package file information\n"
                + "-l      Output file names (including their paths) contained in the package\n"
                + "-L      Similar to -l but also output compression ratio (compressed package file only)\n");
        }

        var dirName = arguments[0];
        var packageName = arguments[1];
        var isOutputMode = arguments[0].Length == 2 && arguments[0][0] == '-';
        if (arguments.Count > 2)
        {
            for (var i = 2; i < arguments.Count; ++i)
            {
                if (arguments[i][0] != '-')
                    _basePath = AddTrailingSlash(arguments[i]);
                else if (arguments[i].Length > 1)
                {
                    switch (arguments[i][1])
                    {
                        case 'c':
                            _compress = true;
                            break;
                        case 'q':
                            _quiet = true;
                            break;
                        default:
                            ErrorExit("Unrecognized option");
                            break;
                    }
                }
            }
        }

        if (!isOutputMode)
        {
            if (!_quiet)
                Console.WriteLine("Scanning directory " + dirName + " for files");

            var fileNames = ScanDirectory(dirName);
            if (fileNames.Count == 0)
                ErrorExit("No files found");

            for (var i = fileNames.Count - 1; i >= 0; --i)
            {
                var extension = Path.GetExtension(fileNames[i]);
                foreach (var ignoreExtension in IgnoreExtensions)
                {
                    if (extension == ignoreExtension)
                    {
                        fileNames.RemoveAt(i);
                        break;
                    }
                }
            }

            fileNames.Sort(StringComparer.Ordinal);

            if (System.IO.File.Exists(packageName))
            {
                var packageTime = System.IO.File.GetLastWriteTimeUtc(packageName);
                using var packageFile = new PackageFile(packageName);
                if (packageFile.GetNumFiles() == fileNames.Count)
                {
                    var filesOutOfDate = false;
                    foreach (var fileName in fileNames)
                    {
                        if (System.IO.File.GetLastWriteTimeUtc(Path.Combine(dirName, fileName)) > packageTime)
                        {
                            filesOutOfDate = true;
                            break;
                        }
                    }

                    if (!filesOutOfDate)
                    {
                        Console.WriteLine("Package " + packageName + " is up to date.");
                        return;
                    }
                }
            }

            foreach (var fileName in fileNames)
                ProcessFile(fileName, dirName);

            WritePackageFile(packageName, dirName);
        }
        else
        {
            using var packageFile = new PackageFile(packageName);
            var outputCompressionRatio = false;
            switch (arguments[0][1])
            {
                case 'i':
                    Console.WriteLine("Number of files: " + packageFile.GetNumFiles());
                    Console.WriteLine("File data size: " + packageFile.GetTotalDataSize());
                    Console.WriteLine("Package size: " + packageFile.GetTotalSize());
                    Console.WriteLine("Checksum: " + packageFile.GetChecksum());
                    Console.WriteLine("Compressed: " + (packageFile.IsCompressed() ? "yes" : "no"));
                    break;

                case 'L':
                    if (!packageFile.IsCompressed())
                        ErrorExit("Invalid output option: -L is applicable for compressed package file only");
                    outputCompressionRatio = true;
                    goto case 'l';

                case 'l':
                {
                    var entries = new List<KeyValuePair<string, PackageEntry>>(packageFile.GetEntries());
                    entries.Sort(static (lhs, rhs) => lhs.Value.Offset.CompareTo(rhs.Value.Offset));
                    for (var i = 0; i < entries.Count; ++i)
                    {
                        var current = entries[i];
                        var fileEntry = current.Key;
                        if (outputCompressionRatio)
                        {
                            var nextOffset = i + 1 == entries.Count
                                ? packageFile.GetTotalSize() - sizeof(uint)
                                : entries[i + 1].Value.Offset;
                            var compressedSize = nextOffset - current.Value.Offset;
                            var ratio = compressedSize != 0 ? (float)current.Value.Size / compressedSize : 0.0f;
                            fileEntry += string.Create(
                                CultureInfo.InvariantCulture,
                                $"\tin: {current.Value.Size}\tout: {compressedSize}\tratio: {ratio:F6}");
                        }
                        Console.WriteLine(fileEntry);
                    }
                    break;
                }

                default:
                    ErrorExit("Unrecognized output option");
                    break;
            }
        }
    }

    private void ProcessFile(string fileName, string rootDir)
    {
        var fullPath = rootDir + "/" + fileName;
        using var file = new File();
        if (!file.Open(fullPath))
            ErrorExit("Could not open file " + fileName);

        var newEntry = new FileEntry
        {
            Name = fileName,
            Offset = 0,
            Size = file.Size,
            Checksum = 0
        };
        _entries.Add(newEntry);
    }

    private void WritePackageFile(string fileName, string rootDir)
    {
        if (!_quiet)
            Console.WriteLine("Writing package");

        using var dest = new File();
        if (!dest.Open(fileName, FileMode.Write))
            ErrorExit("Could not open output file " + fileName);

        WriteHeader(dest);

        for (var i = 0; i < _entries.Count; ++i)
        {
            dest.WriteString(_basePath + _entries[i].Name);
            dest.WriteUInt(_entries[i].Offset);
            dest.WriteUInt(_entries[i].Size);
            dest.WriteUInt(_entries[i].Checksum);
        }

        var totalDataSize = 0u;

        for (var i = 0; i < _entries.Count; ++i)
        {
            var entry = _entries[i];
            var lastOffset = entry.Offset = dest.Size;
            var fileFullPath = rootDir + "/" + entry.Name;

            using var srcFile = new File(fileFullPath);
            if (!srcFile.IsOpen)
                ErrorExit("Could not open file " + fileFullPath);

            var dataSize = entry.Size;
            totalDataSize += dataSize;
            var buffer = new byte[checked((int)dataSize)];

            if (srcFile.Read(buffer) != dataSize)
                ErrorExit("Could not read file " + fileFullPath);
            srcFile.Close();

            foreach (var value in buffer)
            {
                _checksum = SdbmHash(_checksum, value);
                entry.Checksum = SdbmHash(entry.Checksum, value);
            }

            if (!_compress)
            {
                if (!_quiet)
                    Console.WriteLine(entry.Name + " size " + dataSize);
                dest.Write(buffer);
            }
            else
            {
                var compressBuffer = new byte[checked((int)Compression.EstimateCompressBound(_blockSize))];
                var position = 0;

                while (position < buffer.Length)
                {
                    var unpackedSize = Math.Min(checked((int)_blockSize), buffer.Length - position);
                    var packedSize = Compression.CompressData(
                        compressBuffer, buffer.AsSpan(position, unpackedSize), checked((uint)unpackedSize));
                    if (packedSize == 0)
                    {
                        ErrorExit(
                            "LZ4 compression failed for file " + entry.Name + " at offset " + position);
                    }

                    dest.WriteUShort(checked((ushort)unpackedSize));
                    dest.WriteUShort(checked((ushort)packedSize));
                    dest.Write(compressBuffer.AsSpan(0, checked((int)packedSize)));

                    position += unpackedSize;
                }

                if (!_quiet)
                {
                    var totalPackedBytes = dest.Size - lastOffset;
                    var ratio = totalPackedBytes != 0 ? (float)dataSize / totalPackedBytes : 0.0f;
                    Console.WriteLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{entry.Name}\tin: {dataSize}\tout: {totalPackedBytes}\tratio: {ratio:F6}"));
                }
            }

            _entries[i] = entry;
        }

        var currentSize = dest.Size;
        dest.WriteUInt(currentSize + sizeof(uint));

        dest.Seek(0);
        WriteHeader(dest);

        for (var i = 0; i < _entries.Count; ++i)
        {
            dest.WriteString(_basePath + _entries[i].Name);
            dest.WriteUInt(_entries[i].Offset);
            dest.WriteUInt(_entries[i].Size);
            dest.WriteUInt(_entries[i].Checksum);
        }

        if (!_quiet)
        {
            Console.WriteLine("Number of files: " + _entries.Count);
            Console.WriteLine("File data size: " + totalDataSize);
            Console.WriteLine("Package size: " + dest.Size);
            Console.WriteLine("Checksum: " + _checksum);
            Console.WriteLine("Compressed: " + (_compress ? "yes" : "no"));
        }
    }

    private void WriteHeader(File dest)
    {
        if (!_compress)
            dest.WriteFileID("UPAK");
        else
            dest.WriteFileID("ULZ4");
        dest.WriteUInt(checked((uint)_entries.Count));
        dest.WriteUInt(_checksum);
    }

    private static List<string> ScanDirectory(string directory)
    {
        var result = new List<string>();
        foreach (var fullPath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(directory, fullPath).Replace('\\', '/');
            if (!IsHiddenPath(relativePath))
                result.Add(relativePath);
        }
        return result;
    }

    private static bool IsHiddenPath(string path)
    {
        foreach (var part in path.Split('/'))
        {
            if (part.StartsWith(".", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string AddTrailingSlash(string path)
    {
        return path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
    }

    private static uint SdbmHash(uint hash, byte value)
    {
        return unchecked(value + (hash << 6) + (hash << 16) - hash);
    }

    private static void ErrorExit(string message)
    {
        throw new InvalidOperationException(message);
    }
}

internal static class Program
{
    private static int Main(string[] arguments)
    {
        try
        {
            new PackageTool().Run(arguments);
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
