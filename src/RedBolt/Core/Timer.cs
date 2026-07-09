using System.Diagnostics;
namespace RedBolt;

public sealed class Timer
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public uint GetMSec(bool reset = false)
    {
        var elapsed = ToUIntMilliseconds(stopwatch.ElapsedMilliseconds);
        if (reset)
            Reset();

        return elapsed;
    }

    public void Reset()
    {
        stopwatch.Restart();
    }

    private static uint ToUIntMilliseconds(long milliseconds)
    {
        return (uint)Math.Min(uint.MaxValue, Math.Max(0, milliseconds));
    }
}

public sealed class HiresTimer
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public long GetUSec(bool reset = false)
    {
        var elapsed = ToMicroseconds(stopwatch.ElapsedTicks);
        if (reset)
            Reset();

        return elapsed;
    }

    public void Reset()
    {
        stopwatch.Restart();
    }

    public static bool IsSupported() => Stopwatch.IsHighResolution;

    public static long GetFrequency() => Stopwatch.Frequency;

    private static long ToMicroseconds(long ticks)
    {
        return Math.Max(0L, (long)(ticks * (1_000_000.0 / Stopwatch.Frequency)));
    }
}

public sealed class TimedCounter
{
    private readonly Timer timer = new();
    private readonly float[] data;
    private readonly uint intervalLengthMs;
    private int current;
    private float accumulator;

    public TimedCounter(int intervalCount, uint intervalLengthMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalCount);
        data = new float[intervalCount];
        this.intervalLengthMs = intervalLengthMs;
        current = intervalCount - 1;
    }

    public int Current => current;

    public IReadOnlyList<float> Data => data;

    public int GetCurrent() => Current;

    public IReadOnlyList<float> GetData() => Data;

    public void AddSample(float value)
    {
        RotateIfNeeded();
        accumulator += value;
    }

    public float GetAverage()
    {
        RotateIfNeeded();
        return data.Sum() / data.Length;
    }

    public float GetLast()
    {
        RotateIfNeeded();
        return data[current];
    }

    private void RotateIfNeeded()
    {
        if (timer.GetMSec() < intervalLengthMs)
            return;

        timer.Reset();
        current = (current + 1) % data.Length;
        data[current] = accumulator;
        accumulator = 0.0f;
    }
}
