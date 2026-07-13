namespace RedBolt;

public static class GraphicsEvents
{
    /// <summary>New screen mode set.</summary>
    public struct ScreenModeEvent
    {
        public int Width;
        public int Height;
        public bool Fullscreen;
        public bool Borderless;
        public bool Resizable;
        public int Monitor;
        public int RefreshRate;
    }

    /// <summary>Window position changed.</summary>
    public struct WindowPosEvent
    {
        public int X;
        public int Y;
    }

    /// <summary>Request for queuing rendersurfaces either in manual or always-update mode.</summary>
    public struct RenderSurfaceUpdateEvent
    {
    }

    public struct BeginRenderingEvent
    {
    }

    public struct EndRenderingEvent
    {
    }

    /// <summary>Update of a view started.</summary>
    public struct BeginViewUpdateEvent
    {
        public RenderPipelineView? RenderPipelineView;
        public Texture? Texture;
        public RenderSurface? Surface;
        public Scene? Scene;
        public Camera? Camera;
    }

    /// <summary>Update of a view ended.</summary>
    public struct EndViewUpdateEvent
    {
        public RenderPipelineView? RenderPipelineView;
        public Texture? Texture;
        public RenderSurface? Surface;
        public Scene? Scene;
        public Camera? Camera;
    }

    /// <summary>Render of a view started.</summary>
    public struct BeginViewRenderEvent
    {
        public RenderPipelineView? RenderPipelineView;
        public Texture? Texture;
        public RenderSurface? Surface;
        public Scene? Scene;
        public Camera? Camera;
    }

    /// <summary>A view has allocated its screen buffers for rendering. They can be accessed now with View.FindNamedTexture().</summary>
    public struct ViewBuffersReadyEvent
    {
        public RenderPipelineView? RenderPipelineView;
        public Texture? Texture;
        public RenderSurface? Surface;
        public Scene? Scene;
        public Camera? Camera;
    }

    /// <summary>A view has set global shader parameters for a new combination of vertex/pixel shaders. Custom global parameters can now be set.</summary>
    public struct ViewGlobalShaderParametersEvent
    {
        public RenderPipelineView? RenderPipelineView;
        public Texture? Texture;
        public RenderSurface? Surface;
        public Scene? Scene;
        public Camera? Camera;
    }

    /// <summary>Render of a view ended. Its screen buffers are still accessible if needed.</summary>
    public struct EndViewRenderEvent
    {
        public RenderPipelineView? RenderPipelineView;
        public Texture? Texture;
        public RenderSurface? Surface;
        public Scene? Scene;
        public Camera? Camera;
    }

    /// <summary>Render of all views is finished for the frame.</summary>
    public struct EndAllViewsRenderEvent
    {
    }

    /// <summary>A render path event has occurred.</summary>
    public struct RenderPathEvent
    {
        public string Name;
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
        public Object? Object;
    }
}
