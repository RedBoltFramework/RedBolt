namespace RedBolt;

public sealed class EngineTime
{
    private uint timerPeriod;

    public ulong FrameNumber { get; private set; }

    public float TimeStep { get; private set; }

    public double ElapsedTime { get; private set; }

    public uint TimerPeriod => timerPeriod;

    public ulong GetFrameNumber() => FrameNumber;

    public float GetTimeStep() => TimeStep;

    public float GetElapsedTime() => (float)ElapsedTime;

    public float GetFramesPerSecond() => TimeStep > 0.0f ? 1.0f / TimeStep : float.PositiveInfinity;

    public void SetTimerPeriod(uint milliseconds)
    {
        timerPeriod = milliseconds;
    }

    public uint GetTimerPeriod() => timerPeriod;

    internal void Advance(float timeStep)
    {
        TimeStep = timeStep;
        ElapsedTime += timeStep;
        ++FrameNumber;
    }
}
