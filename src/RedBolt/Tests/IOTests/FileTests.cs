using System.Text;
using RedBolt;

using RedBolt.IO;
namespace RedBolt.Tests;
using File = global::RedBolt.IO.File;
using FileMode = global::RedBolt.IO.FileMode;


public sealed class FileTests
{
    [Fact]
    public void File_and_file_mode_kinds_match_rbfx()
    {
        Assert.True(typeof(File).IsClass);
        Assert.False(typeof(File).IsAbstract);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(File)));
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(File)));

        Assert.True(typeof(FileMode).IsEnum);
        Assert.Equal(0, (int)FileMode.Read);
        Assert.Equal(1, (int)FileMode.Write);
        Assert.Equal(2, (int)FileMode.ReadWrite);

        Assert.True(typeof(File).GetMethod(nameof(File.ReadText), Type.EmptyTypes)!.IsVirtual);
        Assert.True(typeof(File).GetMethod(nameof(File.ReadText), [typeof(string).MakeByRefType()])!.IsVirtual);
    }

    [Fact]
    public void File_construction_names_and_disposal_match_rbfx()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), $"redbolt-file-{Guid.NewGuid():N}.bin");
        var fileName = Path.GetRelativePath(Environment.CurrentDirectory, absolutePath);
        try
        {
            AbstractFile file = new File(fileName, FileMode.Write);

            Assert.Equal(fileName, file.Name);
            Assert.Equal(fileName, file.AbsoluteName);
            Assert.True(file.IsOpen);

            file.Dispose();

            Assert.False(file.IsOpen);
            Assert.Equal(0u, file.Position);
            Assert.Equal(0u, file.Size);
        }
        finally
        {
            System.IO.File.Delete(absolutePath);
        }
    }

    [Fact]
    public void File_reads_writes_checksum_and_text()
    {
        var path = Path.Combine(Path.GetTempPath(), $"redbolt-file-{Guid.NewGuid():N}.bin");
        try
        {
            var writer = new File(path, FileMode.Write);
            var source = Encoding.UTF8.GetBytes("hello");
            Assert.Equal((uint)source.Length, writer.Write(source));
            writer.Flush();
            writer.Close();

            var reader = new File(path);
            Assert.Equal((uint)source.Length, reader.Size);
            Assert.Equal("hello", reader.ReadText());
            Assert.NotEqual(0u, reader.GetChecksum());
            Assert.Equal((uint)source.Length, reader.Position);
            reader.Close();
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void File_copies_from_read_file_to_write_file()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"redbolt-source-{Guid.NewGuid():N}.bin");
        var destinationPath = Path.Combine(Path.GetTempPath(), $"redbolt-destination-{Guid.NewGuid():N}.bin");
        try
        {
            var sourceWriter = new File(sourcePath, FileMode.Write);
            sourceWriter.Write(Encoding.UTF8.GetBytes("copy"));
            sourceWriter.Close();

            var source = new File(sourcePath);
            var destination = new File(destinationPath, FileMode.Write);
            Assert.True(destination.Copy(source));
            destination.Close();
            source.Close();

            var result = new File(destinationPath);
            Assert.Equal("copy", result.ReadText());
            result.Close();
        }
        finally
        {
            System.IO.File.Delete(sourcePath);
            System.IO.File.Delete(destinationPath);
        }
    }

    [Fact]
    public void File_binary_returns_are_independent_values()
    {
        var path = Path.Combine(Path.GetTempPath(), $"redbolt-file-{Guid.NewGuid():N}.bin");
        try
        {
            using (var writer = new File(path, FileMode.Write))
                writer.Write([1, 2, 3, 4]);

            using var reader = new File(path);
            var first = reader.ReadBinary();
            reader.Seek(0);
            var second = reader.ReadBinary();

            first[0] = 9;

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, second);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
