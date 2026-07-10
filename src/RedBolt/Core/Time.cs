using System;
using System.Threading;

namespace RedBolt;

public sealed class Time : Object
{
    public const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly Timer _elapsedTime = new();
    private uint _frameNumber;
    private float _timeStep;
    private uint _timerPeriod;

    public uint FrameNumber
    {
        get => _frameNumber;
        private set => _frameNumber = value;
    }

    public float TimeStep
    {
        get => _timeStep;
    }

    public float ElapsedTime => GetElapsedTime();

    public uint TimerPeriod => _timerPeriod;

    public uint GetFrameNumber() => FrameNumber;

    public float GetTimeStep() => TimeStep;

    public float GetElapsedTime() => _elapsedTime.GetMSec(false) / 1000.0f;

    public float GetFramesPerSecond() => 1.0f / _timeStep;

    public void SetTimerPeriod(uint milliseconds)
    {
        _timerPeriod = milliseconds;
    }

    public uint GetTimerPeriod() => _timerPeriod;

    public static uint GetSystemTime()
    {
        return (uint)Environment.TickCount64;
    }

    public static uint GetTimeSinceEpoch()
    {
        return (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static string GetTimeStamp(string format = null!)
    {
        return GetTimeStamp(GetTimeSinceEpoch(), format);
    }

    public static string GetTimeStamp(uint timestamp, string format = null!)
    {
        format ??= DefaultDateTimeFormat;
        return DateTimeOffset
            .FromUnixTimeSeconds(timestamp)
            .ToLocalTime()
            .ToString(format);
    }

    public static void Sleep(uint milliseconds)
    {
        var remaining = milliseconds;
        while (remaining > int.MaxValue)
        {
            Thread.Sleep(int.MaxValue);
            remaining -= int.MaxValue;
        }

        Thread.Sleep((int)remaining);
    }

    internal void BeginFrame(float elapsedTimeStep)
    {
        ++_frameNumber;
        if (_frameNumber == 0)
            ++_frameNumber;

        _timeStep = elapsedTimeStep;

        var eventData = new CoreEvents.BeginFrameEvent
        {
            FrameNumber = _frameNumber,
            TimeStep = _timeStep
        };
        SendEvent(ref eventData);
    }

    internal void EndFrame()
    {
        SendEvent<CoreEvents.EndFrameEvent>();
        SendEvent<CoreEvents.EndFramePrivateEvent>();
    }
}
