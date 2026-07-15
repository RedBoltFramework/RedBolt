using System;
using System.Threading;

namespace RedBolt;

// TODO: port constructor - C++ code:
// Time::Time(Context* context) :
//     Object(context),
//     frameNumber_(0),
//     timeStep_(0.0f),
//     timerPeriod_(0)
// {
// #ifdef _WIN32
//     LARGE_INTEGER frequency;
//     if (QueryPerformanceFrequency(&frequency))
//     {
//         HiresTimer::frequency = frequency.QuadPart;
//         HiresTimer::supported = true;
//     }
// #else
//     HiresTimer::frequency = 1000000;
//     HiresTimer::supported = true;
// #endif
// }

public sealed class Time : Object
{
    public const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    // C++: Timer elapsedTime_;
    private readonly Timer _elapsedTime = new();
    // C++: unsigned frameNumber_{};
    private uint _frameNumber;
    // TODO: port - C++ code:
    // bool isFrameInProgress_{};
    // private bool _isFrameInProgress;
    // C++: float timeStep_{};
    private float _timeStep;
    // C++: unsigned timerPeriod_{};
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

    // TODO: port - C++ code:
    // bool IsFrameInProgress() const { return isFrameInProgress_; }
    // public bool IsFrameInProgress() => _isFrameInProgress;

    public uint GetFrameNumber() => FrameNumber;

    public float GetTimeStep() => TimeStep;

    public float GetElapsedTime() => _elapsedTime.GetMSec(false) / 1000.0f;

    public float GetFramesPerSecond() => 1.0f / _timeStep;

    public void SetTimerPeriod(uint milliseconds)
    {
        // C++: #if defined(_WIN32) && !defined(UWP)
        // C++:     if (timerPeriod_ > 0)
        // C++:         timeEndPeriod(timerPeriod_);
        // C++:     timerPeriod_ = mSec;
        // C++:     if (timerPeriod_ > 0)
        // C++:         timeBeginPeriod(timerPeriod_);
        // C++: #endif
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

        // C++: isFrameInProgress_ = true;
        // C++: URHO3D_PROFILE("BeginFrame");

        var eventData = new CoreEvents.BeginFrameEvent
        {
            FrameNumber = _frameNumber,
            TimeStep = _timeStep
        };
        SendEvent(ref eventData);
    }

    public void EndFrame()
    {
        // C++: URHO3D_PROFILE("EndFrame");

        SendEvent<CoreEvents.EndFrameEvent>();
        SendEvent<CoreEvents.EndFramePrivateEvent>();

        // C++: isFrameInProgress_ = false;
    }

    // TODO: port - C++ code:
    // ~Time() override { SetTimerPeriod(0); }
    // public void Dispose() { SetTimerPeriod(0); }
}
