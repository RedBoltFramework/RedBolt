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

    [Fact]
    public void Frame_progress_matches_begin_and_end_lifecycle()
    {
        var context = new Context();
        Context.SetInstance(context);
        var time = new RedBolt.Time();

        Assert.False(time.IsFrameInProgress);
        time.BeginFrame(1.0f / 60.0f);
        Assert.True(time.IsFrameInProgress);
        time.EndFrame();
        Assert.False(time.IsFrameInProgress);
        time.Dispose();
    }
}
