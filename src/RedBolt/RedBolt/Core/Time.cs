namespace RedBolt.Core;

using System;
using System.Threading;

public sealed class Time : RedObject
{
    public const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly Timer _elapsedTime = new();
    private uint _frameNumber;
    private bool _isFrameInProgress;
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

    public bool IsFrameInProgress => _isFrameInProgress;

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

    public void BeginFrame(float elapsedTimeStep)
    {
        ++_frameNumber;
        if (_frameNumber == 0)
            ++_frameNumber;

        _timeStep = elapsedTimeStep;

        _isFrameInProgress = true;
        var eventData = new CoreEvents.BeginFrameEvent
        {
            FrameNumber = _frameNumber,
            TimeStep = _timeStep
        };
        SendEvent(ref eventData);
    }

    public void EndFrame()
    {
        SendEvent<CoreEvents.EndFrameEvent>();
        SendEvent<CoreEvents.EndFramePrivateEvent>();

        _isFrameInProgress = false;
    }

    public override void Dispose()
    {
        SetTimerPeriod(0);
        base.Dispose();
    }
}
