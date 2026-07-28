namespace RedBolt.Tests;

using System.Threading;

using RedBolt.Core;

public class ProfilerTests
{
    [Fact]
    public void Profiler_constants_match_rbfx()
    {
        Assert.Equal(0xb26d19u, Profiler.ColorEvents);
        Assert.Equal(0x006b82u, Profiler.ColorResources);
    }

    [Fact]
    public void Profiler_sets_current_thread_name_when_unset()
    {
        string? threadName = null;

        var thread = new Thread(() =>
        {
            Profiler.SetProfilerThreadName("WorkerThread");
            threadName = Thread.CurrentThread.Name;
        });

        thread.Start();
        thread.Join();

        Assert.Equal("WorkerThread", threadName);
    }

    [Fact]
    public void Profiler_does_not_overwrite_existing_thread_name()
    {
        string? threadName = null;

        var thread = new Thread(() =>
        {
            Profiler.SetProfilerThreadName("InitialName");
            Profiler.SetProfilerThreadName("UpdatedName");
            threadName = Thread.CurrentThread.Name;
        });

        thread.Start();
        thread.Join();

        Assert.Equal("InitialName", threadName);
    }
}
