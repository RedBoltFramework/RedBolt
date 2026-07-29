namespace RedBolt.Tests;

using System.Diagnostics;
using System.Text;

using RedBolt.IO;

public sealed class PackageToolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PackageTool_writes_package_readable_by_PackageFile(bool compress)
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(), $"redbolt-package-tool-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(temporaryDirectory, "Data");
        var packagePath = Path.Combine(temporaryDirectory, "Data.pak");
        var firstContent = Encoding.UTF8.GetBytes("first package entry");
        var secondContent = Encoding.UTF8.GetBytes(new string('x', 65536));

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Folder"));
            Directory.CreateDirectory(Path.Combine(sourceDirectory, ".hidden"));
            System.IO.File.WriteAllBytes(Path.Combine(sourceDirectory, "First.txt"), firstContent);
            System.IO.File.WriteAllBytes(Path.Combine(sourceDirectory, "Folder", "Second.bin"), secondContent);
            System.IO.File.WriteAllText(Path.Combine(sourceDirectory, "Ignored.rule"), "ignored");
            System.IO.File.WriteAllText(Path.Combine(sourceDirectory, ".hidden", "Ignored.txt"), "ignored");

            var arguments = new List<string> { sourceDirectory, packagePath, "Prefix", "-q" };
            if (compress)
                arguments.Add("-c");
            RunPackageTool(arguments);

            using var package = new PackageFile(packagePath);
            Assert.Equal(compress, package.IsCompressed());
            Assert.Equal(2u, package.GetNumFiles());
            Assert.True(package.Exists("Prefix/First.txt"));
            Assert.True(package.Exists("Prefix/Folder/Second.bin"));
            Assert.False(package.Exists("Prefix/Ignored.rule"));
            Assert.False(package.Exists("Prefix/.hidden/Ignored.txt"));
            AssertPackageEntry(package, "Prefix/First.txt", firstContent);
            AssertPackageEntry(package, "Prefix/Folder/Second.bin", secondContent);

            RunPackageTool(["-i", packagePath]);
            RunPackageTool(["-l", packagePath]);
            if (compress)
                RunPackageTool(["-L", packagePath]);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }
    }

    private static void RunPackageTool(IEnumerable<string> arguments)
    {
        var packageToolPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "tools", "RedBolt.PackageTool.dll"));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        var nativeDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "runtimes", "linux-x64", "native"));
        startInfo.Environment["LD_LIBRARY_PATH"] = nativeDirectory;
        startInfo.ArgumentList.Add(packageToolPath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process!.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"PackageTool exited with code {process.ExitCode}.{Environment.NewLine}{standardOutput}{standardError}");
    }

    private static void AssertPackageEntry(PackageFile package, string name, byte[] expected)
    {
        using var file = package.OpenFile(new FileIdentifier(string.Empty, name), FileMode.Read);
        Assert.NotNull(file);
        var actual = new byte[expected.Length];
        Assert.Equal((uint)actual.Length, file!.Read(actual));
        Assert.Equal(expected, actual);
    }
}
