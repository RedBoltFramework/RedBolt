namespace RedBolt;

public static class GraphicsEvents
{
    /// <summary>New screen mode set.</summary>
    public struct ScreenModeEvent
    {
        public int Width; // int
        public int Height; // int
        public bool Fullscreen; // bool
        public bool Borderless; // bool
        public bool Resizable; // bool
        public int Monitor; // int
        public int RefreshRate; // int
    }

    /// <summary>Window position changed.</summary>
    public struct WindowPosEvent
    {
        public int X; // int
        public int Y; // int
    }

    /// <summary>Request for queuing rendersurfaces either in manual or always-update mode.</summary>
    public struct RenderSurfaceUpdateEvent
    {
    }

    /// <summary>Frame rendering started.</summary>
    public struct BeginRenderingEvent
    {
    }

    /// <summary>Frame rendering ended.</summary>
    public struct EndRenderingEvent
    {
    }

    /// <summary>Update of a view started.</summary>
    public struct BeginViewUpdateEvent
    {
        public RenderPipelineView? RenderPipelineView; // RenderPipelineView pointer
        public Texture? Texture; // Texture pointer
        public RenderSurface? Surface; // RenderSurface pointer
        public Scene? Scene; // Scene pointer
        public Camera? Camera; // Camera pointer
    }

    /// <summary>Update of a view ended.</summary>
    public struct EndViewUpdateEvent
    {
        public RenderPipelineView? RenderPipelineView; // RenderPipelineView pointer
        public Texture? Texture; // Texture pointer
        public RenderSurface? Surface; // RenderSurface pointer
        public Scene? Scene; // Scene pointer
        public Camera? Camera; // Camera pointer
    }

    /// <summary>Render of a view started.</summary>
    public struct BeginViewRenderEvent
    {
        public RenderPipelineView? RenderPipelineView; // RenderPipelineView pointer
        public Texture? Texture; // Texture pointer
        public RenderSurface? Surface; // RenderSurface pointer
        public Scene? Scene; // Scene pointer
        public Camera? Camera; // Camera pointer
    }

    /// <summary>A view has allocated its screen buffers for rendering. They can be accessed now with View.FindNamedTexture().</summary>
    public struct ViewBuffersReadyEvent
    {
        public RenderPipelineView? RenderPipelineView; // RenderPipelineView pointer
        public Texture? Texture; // Texture pointer
        public RenderSurface? Surface; // RenderSurface pointer
        public Scene? Scene; // Scene pointer
        public Camera? Camera; // Camera pointer
    }

    /// <summary>A view has set global shader parameters for a new combination of vertex/pixel shaders. Custom global parameters can now be set.</summary>
    public struct ViewGlobalShaderParametersEvent
    {
        public RenderPipelineView? RenderPipelineView; // RenderPipelineView pointer
        public Texture? Texture; // Texture pointer
        public RenderSurface? Surface; // RenderSurface pointer
        public Scene? Scene; // Scene pointer
        public Camera? Camera; // Camera pointer
    }

    /// <summary>Render of a view ended. Its screen buffers are still accessible if needed.</summary>
    public struct EndViewRenderEvent
    {
        public RenderPipelineView? RenderPipelineView; // RenderPipelineView pointer
        public Texture? Texture; // Texture pointer
        public RenderSurface? Surface; // RenderSurface pointer
        public Scene? Scene; // Scene pointer
        public Camera? Camera; // Camera pointer
    }

    /// <summary>Render of all views is finished for the frame.</summary>
    public struct EndAllViewsRenderEvent
    {
    }

    /// <summary>A render path event has occurred.</summary>
    public struct RenderPathEvent
    {
        public string Name = string.Empty; // String

        public RenderPathEvent() { }
    }

    /// <summary>Graphics context has been lost. Some or all (depending on the API) GPU objects have lost their contents.</summary>
    public struct DeviceLostEvent
    {
    }

    /// <summary>Graphics context has been recreated after being lost. GPU objects in the "data lost" state can be restored now.</summary>
    public struct DeviceResetEvent
    {
    }

    public struct GpuResourceReleasedEvent
    {
        public nint Object; // Ptr
    }
}
