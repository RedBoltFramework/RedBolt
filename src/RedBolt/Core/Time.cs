using System;
using System.Globalization;
using System.Threading;

namespace RedBolt;

public sealed class Time : Object
{
    public const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly Timer elapsedTime = new();
    private uint frameNumber;
    private float timeStep;
    private uint timerPeriod;

    public uint FrameNumber
    {
        get => frameNumber;
        private set => frameNumber = value;
    }

    public float TimeStep
    {
        get => timeStep;
        private set => timeStep = value;
    }

    public double ElapsedTime => elapsedTime.GetMSec(false) / 1000.0;

    public uint TimerPeriod => timerPeriod;

    public uint GetFrameNumber() => FrameNumber;

    public float GetTimeStep() => TimeStep;

    public float GetElapsedTime() => (float)ElapsedTime;

    public float GetFramesPerSecond() => TimeStep > 0.0f ? 1.0f / TimeStep : float.PositiveInfinity;

    public void SetTimerPeriod(uint milliseconds) => timerPeriod = milliseconds;

    public uint GetTimerPeriod() => timerPeriod;

    public static uint GetSystemTime()
    {
        return (uint)Environment.TickCount64;
    }

    public static uint GetTimeSinceEpoch()
    {
        return (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static string GetTimeStamp(string? format = null)
    {
        return GetTimeStamp(DateTimeOffset.Now, format);
    }

    public static string GetTimeStamp(uint timestamp, string? format = null)
    {
        return GetTimeStamp(DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime(), format);
    }

    public static string GetTimeStamp(DateTimeOffset timestamp, string? format = null)
    {
        return timestamp.ToString(string.IsNullOrEmpty(format) ? DefaultDateTimeFormat : format, CultureInfo.InvariantCulture);
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
        ++FrameNumber;
        if (FrameNumber == 0)
            ++FrameNumber;

        TimeStep = elapsedTimeStep;
    }

    internal void EndFrame()
    {
    }
}
