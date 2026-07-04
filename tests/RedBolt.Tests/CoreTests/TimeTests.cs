using RedBolt;

namespace RedBolt.Tests;

public sealed class TimeTests
{
    [Fact]
    public void Static_helpers_return_valid_values()
    {
        var systemTime = RedBolt.Time.GetSystemTime();
        RedBolt.Time.Sleep(1);
        Assert.True(RedBolt.Time.GetSystemTime() - systemTime < 1000u);
        Assert.True(RedBolt.Time.GetTimeSinceEpoch() > 1_700_000_000u);
        Assert.False(string.IsNullOrWhiteSpace(RedBolt.Time.GetTimeStamp()));
    }
}
