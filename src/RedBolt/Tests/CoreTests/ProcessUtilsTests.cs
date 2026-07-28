namespace RedBolt.Tests;

using RedBolt.Core;

public class ProcessUtilsTests
{
    [Fact]
    public void PlatformInformationIsConsistent()
    {
        var platform = ProcessUtils.GetPlatform();
        var platformName = ProcessUtils.GetPlatformName();

        Assert.NotEqual(PlatformId.Unknown, platform);
        Assert.NotEqual("(?)", platformName);
    }

    [Fact]
    public void CpuCountsArePositiveAndLogicalIsNotLessThanPhysical()
    {
        var physical = ProcessUtils.GetNumPhysicalCPUs();
        var logical = ProcessUtils.GetNumLogicalCPUs();

        Assert.True(physical >= 1);
        Assert.True(logical >= 1);
        Assert.True(logical >= physical);
    }

    [Fact]
    public void MiniDumpDirectoryRoundTripsWithTrailingSlash()
    {
        ProcessUtils.SetMiniDumpDir("/tmp/minidumps");

        Assert.Equal($"/tmp/minidumps{Path.DirectorySeparatorChar}", ProcessUtils.GetMiniDumpDir());

        ProcessUtils.SetMiniDumpDir(string.Empty);

        Assert.Equal(string.Empty, ProcessUtils.GetMiniDumpDir());
    }

    [Fact]
    public void SystemInformationAndIdentifiersReturnUsableValues()
    {
        Assert.True(ProcessUtils.GetTotalMemory() > 0);
        Assert.False(string.IsNullOrWhiteSpace(ProcessUtils.GetLoginName()));
        Assert.False(string.IsNullOrWhiteSpace(ProcessUtils.GetHostName()));
        Assert.False(string.IsNullOrWhiteSpace(ProcessUtils.GetOSVersion()));
        Assert.Equal(Environment.ProcessId, (int)ProcessUtils.GetCurrentProcessID());

        var uuid = ProcessUtils.GenerateUUID();
        Assert.True(Guid.TryParse(uuid, out _));
    }

    [Fact]
    public void ParseArgumentsPreservesQuotedArguments()
    {
        var arguments = ProcessUtils.ParseArguments("program \"two words\" plain", skipFirstArgument: true);

        Assert.Equal(["two words", "plain"], arguments);
        Assert.Same(arguments, ProcessUtils.GetArguments());
    }

    [Fact]
    public void ParseArgumentListSkipsExecutableName()
    {
        var arguments = ProcessUtils.ParseArguments(new[] { "program", "--scene", "StaticScene" });

        Assert.Equal(["--scene", "StaticScene"], arguments);
    }
}
