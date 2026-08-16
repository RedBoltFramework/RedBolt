// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using System.Diagnostics;

public sealed class Timer
{
    private uint _startTime;

    public Timer()
    {
        Reset();
    }

    public uint GetMSec(bool reset = false)
    {
        var currentTime = Time.GetSystemTime();
        var elapsedTime = currentTime - _startTime;
        if (reset)
            _startTime = currentTime;

        return elapsedTime;
    }

    public void Reset()
    {
        _startTime = Time.GetSystemTime();
    }
}

public sealed class HiresTimer
{
    private long _startTime;

    public HiresTimer()
    {
        Reset();
    }

    public long GetUSec(bool reset = false)
    {
        var currentTime = Stopwatch.GetTimestamp();
        var elapsedTime = System.Math.Max(0, currentTime - _startTime);

        if (reset)
            _startTime = currentTime;

        return elapsedTime * 1_000_000L / Stopwatch.Frequency;
    }

    public void Reset()
    {
        _startTime = Stopwatch.GetTimestamp();
    }

    public static bool IsSupported() => Stopwatch.IsHighResolution;

    public static long GetFrequency() => Stopwatch.Frequency;
}

public sealed class TimedCounter
{
    private readonly Timer _timer = new();
    private readonly int _intervalLength;
    private int _current;
    private float _accumulator;
    private readonly float[] _data;

    public TimedCounter(int intervalCount, uint intervalLengthMs)
    {
        _intervalLength = unchecked((int)intervalLengthMs);
        _current = intervalCount - 1;
        _data = new float[intervalCount];
    }

    public int GetCurrent() => _current;

    public IReadOnlyList<float> GetData() => _data;

    public void AddSample(float value)
    {
        RotateIfNeeded();
        _accumulator += value;
    }

    public float GetAverage()
    {
        RotateIfNeeded();
        var sum = 0.0f;
        foreach (var value in _data)
            sum += value;

        return sum / _data.Length;
    }

    public float GetLast()
    {
        RotateIfNeeded();
        return _data[_current];
    }

    private void RotateIfNeeded()
    {
        if (_timer.GetMSec(false) >= unchecked((uint)_intervalLength))
        {
            _timer.Reset();
            _current = (_current + 1) % _data.Length;
            _data[_current] = _accumulator;
            _accumulator = 0.0f;
        }
    }
}
