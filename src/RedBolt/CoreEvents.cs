namespace RedBolt;

public static class CoreEvents
{
    public struct BeginFrameEvent
    {
        public int FrameNumber;
        public float TimeStep;
    }

    public struct InputReadyEvent
    {
        public float TimeStep;
    }

    public struct UpdateEvent
    {
        public float TimeStep;
    }

    public struct PostUpdateEvent
    {
        public float TimeStep;
    }

    public struct RenderUpdateEvent
    {
        public float TimeStep;
    }

    public struct PostRenderUpdateEvent
    {
        public float TimeStep;
    }

    public struct EndFrameEvent
    {
    }

    public struct EndFramePrivateEvent
    {
    }
}
