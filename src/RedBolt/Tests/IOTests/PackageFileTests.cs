using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using Xunit;

using RedBolt;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;

namespace RedBolt.Tests;

public sealed class PackageFileTests
{
    [Fact]
    public void Package_types_match_native_class_and_pod_kinds()
    {
        Assert.True(typeof(PackageEntry).IsValueType);
        Assert.False(typeof(PackageEntry).IsByRefLike);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PackageEntry>());
        Assert.Equal(typeof(uint), typeof(PackageEntry).GetField(nameof(PackageEntry.Offset))!.FieldType);
        Assert.Equal(typeof(uint), typeof(PackageEntry).GetField(nameof(PackageEntry.Size))!.FieldType);
        Assert.Equal(typeof(uint), typeof(PackageEntry).GetField(nameof(PackageEntry.Checksum))!.FieldType);

        Assert.True(typeof(PackageFile).IsClass);
        Assert.False(typeof(PackageFile).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(PackageFile)));
    }

    [Fact]
    public void PackageFile_parses_entries_scans_and_opens_package_data()
    {
        var packageBytes = new VectorBuffer();
        var content = Encoding.UTF8.GetBytes("package-data");
        packageBytes.WriteFileID("UPAK");
        packageBytes.WriteUInt(1);
        packageBytes.WriteUInt(0x1234);
        packageBytes.WriteString("folder/item.txt");
        packageBytes.WriteUInt(packageBytes.Size + 12);
        packageBytes.WriteUInt((uint)content.Length);
        packageBytes.WriteUInt(0x55);
        packageBytes.Write(content);

        var packagePath = Path.Combine(Path.GetTempPath(), $"redbolt-package-{Guid.NewGuid():N}.pak");
        try
        {
            System.IO.File.WriteAllBytes(packagePath, packageBytes.GetBuffer());

            using var packageFile = new PackageFile(packagePath);
            Assert.Equal(packagePath, packageFile.GetName());
            Assert.Equal(1u, packageFile.GetNumFiles());
            Assert.Equal(packageBytes.Size, packageFile.GetTotalSize());
            Assert.Equal((uint)content.Length, packageFile.GetTotalDataSize());
            Assert.Equal(0x1234u, packageFile.GetChecksum());
            Assert.False(packageFile.IsCompressed());
            Assert.True(packageFile.Exists("folder/item.txt"));
            Assert.True(packageFile.Exists(new FileIdentifier(string.Empty, "folder/item.txt")));
            Assert.False(packageFile.Exists(new FileIdentifier("other", "folder/item.txt")));

            var entry = packageFile.GetEntry("folder/item.txt");
            Assert.True(entry.HasValue);
            Assert.Equal((uint)content.Length, entry.Value.Size);
            Assert.Equal(0x55u, entry.Value.Checksum);

            var result = new List<string> { "old" };
            packageFile.Scan(result, "folder", "*.txt", ScanFlags.ScanRecursive);
            Assert.Equal(new[] { "item.txt" }, result);

            var file = packageFile.OpenFile(new FileIdentifier(string.Empty, "folder/item.txt"), FileMode.Read);
            Assert.NotNull(file);
            var readBuffer = new byte[content.Length];
            Assert.Equal((uint)content.Length, file!.Read(readBuffer));
            Assert.Equal(content, readBuffer);
            file.Dispose();

            Assert.Null(packageFile.OpenFile(new FileIdentifier(string.Empty, "folder/item.txt"), FileMode.Write));
            Assert.Null(packageFile.OpenFile(new FileIdentifier("other", "folder/item.txt"), FileMode.Read));
            Assert.NotNull(packageFile.GetLastModifiedTime(new FileIdentifier(string.Empty, "folder/item.txt"), false));
        }
        finally
        {
            System.IO.File.Delete(packagePath);
        }
    }

    [Fact]
    public void PackageFile_reads_compressed_package_entries()
    {
        var content = Encoding.UTF8.GetBytes(new string('x', 100));
        var compressed = new byte[128];
        var compressedSize = Compression.CompressData(compressed, content, (uint)content.Length);
        Assert.NotEqual(0u, compressedSize);

        var packageBytes = new VectorBuffer();
        packageBytes.WriteFileID("ULZ4");
        packageBytes.WriteUInt(1);
        packageBytes.WriteUInt(0);
        packageBytes.WriteString("compressed.txt");
        packageBytes.WriteUInt(packageBytes.Size + 12);
        packageBytes.WriteUInt((uint)content.Length);
        packageBytes.WriteUInt(0);
        packageBytes.WriteUShort((ushort)content.Length);
        packageBytes.WriteUShort((ushort)compressedSize);
        packageBytes.Write(compressed.AsSpan(0, checked((int)compressedSize)));

        var packagePath = Path.Combine(Path.GetTempPath(), $"redbolt-package-{Guid.NewGuid():N}.pak");
        try
        {
            System.IO.File.WriteAllBytes(packagePath, packageBytes.GetBuffer());

            using var packageFile = new PackageFile(packagePath);
            Assert.True(packageFile.IsCompressed());
            var file = packageFile.OpenFile(new FileIdentifier(string.Empty, "compressed.txt"), FileMode.Read);
            Assert.NotNull(file);
            var readBuffer = new byte[content.Length];
            Assert.Equal((uint)content.Length, file!.Read(readBuffer));
            Assert.Equal(content, readBuffer);
            Assert.Equal(0u, file.Seek(0));
            file.Dispose();
        }
        finally
        {
            System.IO.File.Delete(packagePath);
        }
    }

    [Fact]
    public void PackageFile_truncates_signed_file_list_offset_like_cpp()
    {
        using var packageBytes = new VectorBuffer();
        packageBytes.WriteFileID("RPAK");
        packageBytes.WriteUInt(0);
        packageBytes.WriteUInt(0);
        packageBytes.WriteUInt(0);
        packageBytes.WriteInt64(-1);

        var packagePath = Path.Combine(Path.GetTempPath(), $"redbolt-package-{Guid.NewGuid():N}.pak");
        try
        {
            System.IO.File.WriteAllBytes(packagePath, packageBytes.GetBuffer());
            using var packageFile = new PackageFile();
            Assert.True(packageFile.Open(packagePath));
        }
        finally
        {
            System.IO.File.Delete(packagePath);
        }
    }
}
