using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderDeviceTests
{
    private readonly HeadlessEngineFixture fixture;

    public RenderDeviceTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Render_device_is_registered_as_engine_subsystem()
    {
        Assert.Same(fixture.RenderDevice, fixture.Context.GetSubsystem<RenderDevice>());
    }

    [Fact]
    public void Render_device_maps_engine_parameters_to_render_and_window_settings()
    {
        var previousContext = Context.TryGetInstance();
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = true,
            RenderBackend = GraphicsBackend.OpenGL,
            GPUDebug = true,
            RenderAdapterId = 7,
            ExternalWindow = (nint)1234,
            FullScreen = true,
            Monitor = 2,
            RefreshRate = 144,
            MultiSample = 4,
            WindowWidth = 1600,
            WindowHeight = 900,
            WindowTitle = "Render Device Window",
            WindowResizable = true,
            Orientations = "LandscapeLeft Portrait"
        };

        try
        {
            using var engine = new EngineHandle();
            engine.Engine.Initialize(parameters);
            var renderDevice = engine.Engine.Context.GetSubsystem<RenderDevice>();
            Assert.NotNull(renderDevice);

            var deviceSettings = renderDevice!.GetDeviceSettings();
            var windowSettings = renderDevice.GetWindowSettings();

            Assert.Equal(RenderBackend.OpenGL, renderDevice.GetBackend());
            Assert.Equal(RenderBackend.OpenGL, deviceSettings.Backend);
            Assert.Equal((nint)1234, deviceSettings.ExternalWindowHandle);
            Assert.True(deviceSettings.GpuDebug);
            Assert.Equal((uint)7, deviceSettings.AdapterId);

            Assert.Equal(WindowMode.Fullscreen, windowSettings.Mode);
            Assert.Equal(new IntVector2(1600, 900), windowSettings.Size);
            Assert.Equal("Render Device Window", windowSettings.Title);
            Assert.True(windowSettings.Resizable);
            Assert.Equal(2, windowSettings.Monitor);
            Assert.Equal(144, windowSettings.RefreshRate);
            Assert.Equal(4, windowSettings.MultiSample);
            Assert.Equal(["LandscapeLeft", "Portrait"], windowSettings.Orientations);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Device_object_receives_invalidate_restore_and_destroy_events()
    {
        var previousContext = Context.TryGetInstance();
        TrackingDeviceObject? deviceObject;

        try
        {
            using var engine = new EngineHandle();
            engine.Engine.Initialize(new EngineParameters
            {
                ApplicationName = "RenderDeviceTests",
                Headless = true,
                RenderBackend = GraphicsBackend.OpenGL
            });

            var renderDevice = engine.Engine.Context.GetSubsystem<RenderDevice>();
            Assert.NotNull(renderDevice);

            deviceObject = new TrackingDeviceObject();
            Assert.Same(renderDevice, deviceObject.GetRenderDevice());

            Assert.True(renderDevice!.EmulateLossAndRestore());
            Assert.Equal(1, deviceObject.InvalidateCalls);
            Assert.Equal(1, deviceObject.RestoreCalls);
            Assert.Equal(0, deviceObject.DestroyCalls);
            Assert.False(deviceObject.IsDataLost());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }

        Assert.NotNull(deviceObject);
        Assert.Equal(1, deviceObject!.DestroyCalls);
    }

    [Fact]
    public void Pipeline_state_uses_device_object_debug_name_surface()
    {
        var pipelineState = new PipelineState
        {
            DebugName = "TestPipeline"
        };

        Assert.IsAssignableFrom<DeviceObject>(pipelineState);
        Assert.Equal("TestPipeline", pipelineState.DebugName);
        Assert.Equal("TestPipeline", pipelineState.GetDebugName());
        Assert.True(pipelineState.IsValid);
    }

    private sealed class TrackingDeviceObject : DeviceObject
    {
        public int InvalidateCalls { get; private set; }
        public int RestoreCalls { get; private set; }
        public int DestroyCalls { get; private set; }

        public override void Invalidate()
        {
            base.Invalidate();
            ++InvalidateCalls;
        }

        public override void Restore()
        {
            base.Restore();
            ++RestoreCalls;
        }

        public override void Destroy()
        {
            base.Destroy();
            ++DestroyCalls;
        }
    }

    private sealed class EngineHandle : IDisposable
    {
        public Engine Engine { get; } = new();

        public void Dispose()
        {
            Engine.Shutdown();
        }
    }
}
