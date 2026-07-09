using System.Diagnostics;
namespace RedBolt;

public sealed class Timer
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public uint GetMSec(bool reset = false)
    {
        var elapsed = ToUIntMilliseconds(_stopwatch.ElapsedMilliseconds);
        if (reset)
            Reset();

        return elapsed;
    }

    public void Reset()
    {
        _stopwatch.Restart();
    }

    private static uint ToUIntMilliseconds(long milliseconds)
    {
        return (uint)Math.Min(uint.MaxValue, Math.Max(0, milliseconds));
    }
}

public sealed class HiresTimer
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public long GetUSec(bool reset = false)
    {
        var elapsed = ToMicroseconds(_stopwatch.ElapsedTicks);
        if (reset)
            Reset();

        return elapsed;
    }

    public void Reset()
    {
        _stopwatch.Restart();
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
    private readonly Timer _timer = new();
    private readonly float[] _data;
    private readonly uint _intervalLengthMs;
    private int _current;
    private float _accumulator;

    public TimedCounter(int intervalCount, uint intervalLengthMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalCount);
        _data = new float[intervalCount];
        this._intervalLengthMs = intervalLengthMs;
        _current = intervalCount - 1;
    }

    public int Current => _current;

    public IReadOnlyList<float> Data => _data;

    public int GetCurrent() => Current;

    public IReadOnlyList<float> GetData() => Data;

    public void AddSample(float value)
    {
        RotateIfNeeded();
        _accumulator += value;
    }

    public float GetAverage()
    {
        RotateIfNeeded();
        return _data.Sum() / _data.Length;
    }

    public float GetLast()
    {
        RotateIfNeeded();
        return _data[_current];
    }

    private void RotateIfNeeded()
    {
        if (_timer.GetMSec() < _intervalLengthMs)
            return;

        _timer.Reset();
        _current = (_current + 1) % _data.Length;
        _data[_current] = _accumulator;
        _accumulator = 0.0f;
    }
}
