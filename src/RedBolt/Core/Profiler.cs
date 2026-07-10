using System.Threading;

namespace RedBolt;

public static class Profiler
{
    public const uint ColorEvents = 0xb26d19;
    public const uint ColorResources = 0x006b82;

    /// <summary>
    /// Set profiler-visible name for the current thread.
    /// </summary>
    public static void SetProfilerThreadName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var currentThread = Thread.CurrentThread;
        if (currentThread.Name is null)
            currentThread.Name = name;
    }
}
