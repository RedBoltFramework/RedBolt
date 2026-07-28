namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Core;
using Timer = global::RedBolt.Core.Timer;
using RedBolt.Math;

public sealed class TimerTests
{
    [Fact]
    public void Timer_elapsed_and_reset_work()
    {
        var timer = new Timer();
        Time.Sleep(2);
        Assert.NotEqual(0u, timer.GetMSec(reset: true));
    }

    [Fact]
    public void HiresTimer_elapsed_and_frequency_work()
    {
        var timer = new HiresTimer();
        Time.Sleep(2);
        Assert.True(timer.GetUSec(reset: true) > 0);
        Assert.True(HiresTimer.GetFrequency() > 0);
    }

    [Fact]
    public void TimedCounter_interval_accumulation_works()
    {
        var counter = new TimedCounter(intervalCount: 2, intervalLengthMs: 50u);
        counter.AddSample(2.0f);
        counter.AddSample(4.0f);
        Time.Sleep(60);
        Assert.True(MathDefs.Equals(counter.GetLast(), 6.0f));

        counter.AddSample(10.0f);
        Time.Sleep(60);
        Assert.True(MathDefs.Equals(counter.GetLast(), 10.0f));
        Assert.True(MathDefs.Equals(counter.GetAverage(), 8.0f));
        Assert.Equal(2, counter.GetData().Count);
    }
}
