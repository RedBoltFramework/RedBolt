using System.Text;
using RedBolt;

namespace RedBolt.Tests;

public sealed class FileTests
{
    [Fact]
    public void File_reads_writes_checksum_and_text()
    {
        var path = Path.Combine(Path.GetTempPath(), $"redbolt-file-{Guid.NewGuid():N}.bin");
        try
        {
            var writer = new RedBolt.File(path, RedBolt.File.FileMode.Write);
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
            var sourceWriter = new RedBolt.File(sourcePath, RedBolt.File.FileMode.Write);
            sourceWriter.Write(Encoding.UTF8.GetBytes("copy"));
            sourceWriter.Close();

            var source = new RedBolt.File(sourcePath);
            var destination = new RedBolt.File(destinationPath, RedBolt.File.FileMode.Write);
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
}
