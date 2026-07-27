namespace RedBolt.Tests;

public sealed class GraphicsEventsTests
{
    [Fact]
    public void Graphics_events_preserve_payload_shapes()
    {
        var screenMode = new GraphicsEvents.ScreenModeEvent
        {
            Width = 1280,
            Height = 720,
            Fullscreen = true,
            Borderless = false,
            Resizable = true,
            Monitor = 1,
            RefreshRate = 60
        };
        var windowPos = new GraphicsEvents.WindowPosEvent { X = 12, Y = 34 };
        var defaultRenderPath = new GraphicsEvents.RenderPathEvent();
        var renderPath = new GraphicsEvents.RenderPathEvent { Name = "Default" };
        var gpuResourceReleased = new GraphicsEvents.GpuResourceReleasedEvent { Object = 1234 };
        var gpuResourceReleasedCopy = gpuResourceReleased;

        Assert.Equal(1280, screenMode.Width);
        Assert.True(screenMode.Fullscreen);
        Assert.Equal(12, windowPos.X);
        Assert.Equal(string.Empty, defaultRenderPath.Name);
        Assert.Equal("Default", renderPath.Name);
        Assert.Equal((nint)1234, gpuResourceReleased.Object);
        Assert.Equal(gpuResourceReleased.Object, gpuResourceReleasedCopy.Object);
    }

    [Fact]
    public void Graphics_events_include_empty_lifecycle_events()
    {
        _ = new GraphicsEvents.RenderSurfaceUpdateEvent();
        _ = new GraphicsEvents.BeginRenderingEvent();
        _ = new GraphicsEvents.EndRenderingEvent();
        _ = new GraphicsEvents.EndAllViewsRenderEvent();
        _ = new GraphicsEvents.DeviceLostEvent();
        _ = new GraphicsEvents.DeviceResetEvent();
    }
}
