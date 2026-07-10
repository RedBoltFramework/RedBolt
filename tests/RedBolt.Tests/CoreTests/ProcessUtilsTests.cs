namespace RedBolt.Tests;

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
}
