using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderDeviceTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RenderDeviceTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Render_device_is_registered_as_engine_subsystem()
    {
        Assert.Same(_fixture.RenderDevice, _fixture.Context.GetSubsystem<RenderDevice>());
        Assert.Same(_fixture.RenderContext, _fixture.RenderDevice.GetRenderContext());
        Assert.NotNull(_fixture.RenderDevice.GetRenderPool());
        Assert.NotNull(_fixture.RenderDevice.GetDefaultQueue());
        Assert.Same(_fixture.Graphics.GetNativeRenderDevice(), _fixture.RenderDevice.GetRenderDevice());
        Assert.Same(_fixture.Graphics.GetImmediateContext(), _fixture.RenderDevice.GetImmediateContext());
        Assert.Same(_fixture.Graphics.GetNativeSwapChain(), _fixture.RenderDevice.GetSwapChain());
    }

    [Fact]
    public void Render_device_post_initialize_creates_default_raw_textures()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            _fixture.RenderDevice.PostInitialize();

            foreach (var type in Enum.GetValues<TextureType>())
            {
                var texture = _fixture.RenderDevice.GetDefaultTexture(type);
                Assert.NotNull(texture);
                Assert.True(texture!.IsValid());
                Assert.NotNull(texture.GetHandles().ShaderResourceView);
            }
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Render_device_loss_restore_recreates_defaults_and_resets_context_state()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            _fixture.RenderDevice.PostInitialize();
            var oldTexture = _fixture.RenderDevice.GetDefaultTexture(TextureType.Texture2D);
            _fixture.RenderContext.SetClipPlaneMask(0x5Au);

            Assert.True(_fixture.RenderDevice.EmulateLossAndRestore());

            var newTexture = _fixture.RenderDevice.GetDefaultTexture(TextureType.Texture2D);
            Assert.NotSame(oldTexture, newTexture);
            Assert.NotNull(newTexture);
            Assert.True(newTexture!.IsValid());
            var clipPlaneMask = (uint)typeof(RenderContext)
                .GetField("_clipPlaneMask", global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.NonPublic)!
                .GetValue(_fixture.RenderContext)!;
            Assert.Equal(0u, clipPlaneMask);
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Render_device_maps_engine_parameters_to_render_and_window_settings()
    {
        var previousContext = Context.Instance;
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
            Assert.NotNull(deviceSettings.Vulkan);
            Assert.NotNull(deviceSettings.D3D12);
            Assert.Equal((int)RedBolt.Bindings.Diligent.QueryType.QueryTypeNumTypes, deviceSettings.Vulkan.QueryPoolSizes.Length);
            Assert.Equal((int)RedBolt.Bindings.Diligent.QueryType.QueryTypeNumTypes, deviceSettings.D3D12.QueryPoolSizes.Length);

            Assert.Equal(WindowMode.Fullscreen, windowSettings.Mode);
            Assert.Equal(new Vector2i(1600, 900), windowSettings.Size);
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
        var previousContext = Context.Instance;
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
        Assert.False(pipelineState.IsValid);
    }

    [Fact]
    public void Render_pool_exposes_rbfx_style_default_settings_surface()
    {
        var settings = _fixture.RenderDevice.GetRenderPool().Settings;

        Assert.Equal(1u, settings.TextureCacheMinFrames);
        Assert.Equal(1000u, settings.TextureCacheMaxFrames);
        Assert.Equal(0.1f, settings.TextureCacheMinLoadFactor);
        Assert.Equal(1000u, settings.LogPeriod);
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
