namespace RedBolt;

public sealed class EngineTime
{
    public ulong FrameNumber { get; private set; }

    public float TimeStep { get; private set; }

    public double ElapsedTime { get; private set; }

    internal void Advance(float timeStep)
    {
        TimeStep = timeStep;
        ElapsedTime += timeStep;
        ++FrameNumber;
    }
}
