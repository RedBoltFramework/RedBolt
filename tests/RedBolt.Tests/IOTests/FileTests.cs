using System.Text;
using RedBolt;

namespace RedBolt.Tests;

public sealed class FileTests
{
    [Fact]
    public void File_and_file_mode_kinds_match_rbfx()
    {
        Assert.True(typeof(RedBolt.File).IsClass);
        Assert.False(typeof(RedBolt.File).IsAbstract);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(RedBolt.File)));
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(RedBolt.File)));

        Assert.True(typeof(FileMode).IsEnum);
        Assert.Equal(0, (int)FileMode.Read);
        Assert.Equal(1, (int)FileMode.Write);
        Assert.Equal(2, (int)FileMode.ReadWrite);

        Assert.True(typeof(RedBolt.File).GetMethod(nameof(RedBolt.File.ReadText), Type.EmptyTypes)!.IsVirtual);
        Assert.True(typeof(RedBolt.File).GetMethod(nameof(RedBolt.File.ReadText), [typeof(string).MakeByRefType()])!.IsVirtual);
    }

    [Fact]
    public void File_construction_names_and_disposal_match_rbfx()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), $"redbolt-file-{Guid.NewGuid():N}.bin");
        var fileName = Path.GetRelativePath(Environment.CurrentDirectory, absolutePath);
        try
        {
            AbstractFile file = new RedBolt.File(fileName, FileMode.Write);

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
            var writer = new RedBolt.File(path, RedBolt.FileMode.Write);
            var source = Encoding.UTF8.GetBytes("hello");
            Assert.Equal((uint)source.Length, writer.Write(source));
            writer.Flush();
            writer.Close();

            var reader = new RedBolt.File(path);
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
            var sourceWriter = new RedBolt.File(sourcePath, RedBolt.FileMode.Write);
            sourceWriter.Write(Encoding.UTF8.GetBytes("copy"));
            sourceWriter.Close();

            var source = new RedBolt.File(sourcePath);
            var destination = new RedBolt.File(destinationPath, RedBolt.FileMode.Write);
            Assert.True(destination.Copy(source));
            destination.Close();
            source.Close();

            var result = new RedBolt.File(destinationPath);
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
            using (var writer = new RedBolt.File(path, FileMode.Write))
                writer.Write([1, 2, 3, 4]);

            using var reader = new RedBolt.File(path);
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
