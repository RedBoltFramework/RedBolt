using System;
using System.IO;
using System.Text;
using RedBolt;

namespace RedBolt.Tests;

public sealed class MountedRootTests
{
    [Fact]
    public void MountedRoot_matches_file_scheme_and_absolute_path_behavior()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"redbolt-mounted-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var fileName = Path.Combine(directory, "data.txt");
        System.IO.File.WriteAllText(fileName, "data");

        try
        {
            var mountPoint = new MountedRoot();

            Assert.True(mountPoint.AcceptsScheme("FILE"));
            Assert.False(mountPoint.AcceptsScheme("other"));
            Assert.True(mountPoint.Exists(new FileIdentifier("file", fileName)));
            Assert.True(mountPoint.Exists(new FileIdentifier("file", directory)));
            Assert.False(mountPoint.Exists(new FileIdentifier(string.Empty, fileName)));
            Assert.Equal(fileName, mountPoint.GetAbsoluteNameFromIdentifier(new FileIdentifier("file", fileName)));
            Assert.Equal(string.Empty, mountPoint.GetAbsoluteNameFromIdentifier(new FileIdentifier("file", directory)));
            Assert.Equal("file://", mountPoint.GetName());
            Assert.Equal(new FileIdentifier("file", fileName), mountPoint.GetIdentifierFromAbsoluteName(fileName));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MountedRoot_opens_read_and_write_files()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"redbolt-mounted-root-{Guid.NewGuid():N}");
        var fileName = Path.Combine(directory, "Nested", "data.txt");

        try
        {
            var mountPoint = new MountedRoot();
            var identifier = new FileIdentifier("file", fileName);

            Assert.Null(mountPoint.OpenFile(identifier, FileMode.Read));

            var output = mountPoint.OpenFile(identifier, FileMode.Write);
            try
            {
                Assert.NotNull(output);
                Assert.Equal(identifier.ToUri(), output.Name);
                Assert.Equal(5u, output.Write("hello"u8));
            }
            finally
            {
                output?.Close();
            }

            var input = mountPoint.OpenFile(identifier, FileMode.Read);
            try
            {
                Assert.NotNull(input);
                var data = new byte[5];
                Assert.Equal(5u, input.Read(data));
                Assert.Equal("hello", Encoding.UTF8.GetString(data));
            }
            finally
            {
                input?.Close();
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
