namespace RedBolt;

public static class CoreEvents
{
    public static readonly StringHash BeginFrame = "BeginFrame";
    public static readonly StringHash InputReady = "InputReady";
    public static readonly StringHash Update = "Update";
    public static readonly StringHash PostUpdate = "PostUpdate";
    public static readonly StringHash RenderUpdate = "RenderUpdate";
    public static readonly StringHash PostRenderUpdate = "PostRenderUpdate";
    public static readonly StringHash EndFrame = "EndFrame";
    public static readonly StringHash EndFramePrivate = "EndFramePrivate";

    public static readonly StringHash FrameNumber = "FrameNumber";
    public static readonly StringHash TimeStep = "TimeStep";
}
