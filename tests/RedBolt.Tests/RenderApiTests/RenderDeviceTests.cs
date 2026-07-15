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
            WindowMaximize = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            Assert.Equal(_fixture.RenderDevice.GetCaps().MaxTextureSize,
                _fixture.Graphics.GetRenderDeviceCaps().MaxTextureSize);

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
    public void Render_device_default_texture_settings_match_native_behavior()
    {
        var filterMode = _fixture.RenderDevice.GetDefaultTextureFilterMode();
        var anisotropy = _fixture.RenderDevice.GetDefaultTextureAnisotropy();
        try
        {
            Assert.Equal(TextureFilterMode.Trilinear, filterMode);
            Assert.Equal(4, anisotropy);

            _fixture.RenderDevice.SetDefaultTextureFilterMode(TextureFilterMode.Bilinear);
            _fixture.RenderDevice.SetDefaultTextureAnisotropy(0);

            Assert.Equal(TextureFilterMode.Bilinear, _fixture.RenderDevice.GetDefaultTextureFilterMode());
            Assert.Equal(1, _fixture.RenderDevice.GetDefaultTextureAnisotropy());
            Assert.Throws<ArgumentException>(() => _fixture.RenderDevice.SetDefaultTextureFilterMode(TextureFilterMode.Default));
        }
        finally
        {
            _fixture.RenderDevice.SetDefaultTextureFilterMode(filterMode);
            _fixture.RenderDevice.SetDefaultTextureAnisotropy(anisotropy);
        }
    }

    [Fact]
    public void Render_device_dpi_scale_matches_equal_window_and_swap_chain_sizes()
    {
        Assert.Equal(0.0f, _fixture.RenderDevice.GetDpiScale());
    }

    [Fact]
    public void Render_device_stats_expose_render_context_counters()
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
            var stats = _fixture.RenderContext.GetMutableStats();
            stats.NumPrimitives = 12;
            stats.NumDraws = 3;
            stats.NumDispatches = 2;

            _fixture.RenderDevice.Present();

            Assert.Equal(12u, _fixture.RenderDevice.GetStats().NumPrimitives);
            Assert.Equal(3u, _fixture.RenderDevice.GetStats().NumDraws);
            Assert.Equal(2u, _fixture.RenderDevice.GetStats().NumDispatches);
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Render_device_present_advances_frame_index()
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
            var firstFrame = _fixture.RenderDevice.GetFrameIndex();

            _fixture.RenderDevice.Present();

            Assert.Equal((long)firstFrame + 1, (long)_fixture.RenderDevice.GetFrameIndex());
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Render_device_reports_native_swap_chain_and_window_sizes_separately()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 48
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            using var description = _fixture.RenderDevice.GetSwapChain()!.GetDesc();

            Assert.NotNull(_fixture.RenderDevice.GetFactory());
            Assert.NotNull(_fixture.RenderDevice.GetSDLWindow());
            Assert.Equal(nint.Zero, _fixture.RenderDevice.GetMetalView());
            Assert.Equal(new Vector2i((int)description.Width, (int)description.Height),
                _fixture.RenderDevice.GetSwapChainSize());
            Assert.Equal(new Vector2i(_fixture.Graphics.Width, _fixture.Graphics.Height),
                _fixture.RenderDevice.GetWindowSize());
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Render_device_updates_swap_chain_size_from_native_window()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 48
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            _fixture.RenderDevice.UpdateSwapChainSize();

            Assert.Equal(new Vector2i(_fixture.Graphics.Width, _fixture.Graphics.Height),
                _fixture.RenderDevice.GetWindowSize());
            using var description = _fixture.RenderDevice.GetSwapChain()!.GetDesc();
            Assert.Equal(new Vector2i((int)description.Width, (int)description.Height),
                _fixture.RenderDevice.GetSwapChainSize());
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Graphics_takes_screenshot_into_image()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 48
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            var image = new Image("RenderDeviceTests.Screenshot");

            Assert.True(_fixture.Graphics.TakeScreenShot(image));
            Assert.Equal(_fixture.Graphics.Width, image.Width);
            Assert.Equal(_fixture.Graphics.Height, image.Height);
            Assert.Equal(4u, image.Components);
            Assert.Equal(image.Width * image.Height * 4, image.GetData().Length);
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Render_device_updates_native_window_settings()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = false,
            WindowMaximize = false,
            WindowWidth = 64,
            WindowHeight = 48
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        try
        {
            var settings = _fixture.RenderDevice.GetWindowSettings();
            settings.Size = new Vector2i(80, 60);
            settings.Title = "Updated Render Device Window";
            settings.Resizable = false;
            settings.VSync = false;

            _fixture.RenderDevice.UpdateWindowSettings(settings);

            var updatedSettings = _fixture.RenderDevice.GetWindowSettings();
            Assert.Equal(new Vector2i(80, 60), updatedSettings.Size);
            Assert.Equal("Updated Render Device Window", updatedSettings.Title);
            Assert.False(updatedSettings.Resizable);
            Assert.False(updatedSettings.VSync);
            Assert.Equal(new Vector2i(80, 60), _fixture.RenderDevice.GetWindowSize());
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
        }
    }

    [Fact]
    public void Graphics_exposes_native_window_and_display_queries()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RenderDeviceTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 48
        };

        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        var originalTitle = _fixture.Graphics.GetWindowTitle();
        var originalPosition = _fixture.Graphics.GetWindowPosition();
        try
        {
            var windowPosEventCount = 0;
            var reportedWindowPosition = Vector2i.Zero;
            using var windowPosSubscription = _fixture.Graphics.SubscribeToEvent<GraphicsEvents.WindowPosEvent>((ref GraphicsEvents.WindowPosEvent eventData) =>
            {
                ++windowPosEventCount;
                reportedWindowPosition = new Vector2i(eventData.X, eventData.Y);
            });

            _fixture.Graphics.SetWindowTitle("Graphics Window API");
            var movedPosition = originalPosition + new Vector2i(1, 1);
            _fixture.Graphics.SetWindowPosition(movedPosition);
            _fixture.Graphics.OnWindowMoved();

            Assert.Equal("Graphics Window API", _fixture.Graphics.GetWindowTitle());
            Assert.Equal(movedPosition, _fixture.Graphics.GetWindowPosition());
            Assert.Equal(1, windowPosEventCount);
            Assert.Equal(movedPosition, reportedWindowPosition);
            Assert.Equal(new Vector2i(_fixture.Graphics.Width, _fixture.Graphics.Height), _fixture.Graphics.GetSize());
            Assert.Equal(_fixture.RenderDevice.GetWindowSettings().MultiSample, _fixture.Graphics.GetMultiSample());
            Assert.Equal(_fixture.RenderDevice.GetWindowSettings().Mode == WindowMode.Fullscreen, _fixture.Graphics.GetFullscreen());
            Assert.Equal(_fixture.RenderDevice.GetWindowSettings().VSync, _fixture.Graphics.GetVSync());
            Assert.Equal(RenderBackend.OpenGL, _fixture.Graphics.GetRenderBackend());
            Assert.Equal("OpenGL", _fixture.Graphics.GetApiName());
            Assert.Equal(nint.Zero, _fixture.Graphics.GetExternalWindow());
            Assert.Equal(TextureFormat.Unknown, _fixture.Graphics.GetDummyColorFormat());
            Assert.Equal(TextureFormat.Unknown, _fixture.Graphics.GetShadowMapFormat());
            Assert.Equal(TextureFormat.Unknown, _fixture.Graphics.GetHiresShadowMapFormat());
            Assert.True(_fixture.Graphics.GetInstancingSupport());
            Assert.True(_fixture.Graphics.GetHardwareShadowSupport());
            Assert.True(_fixture.Graphics.GetMonitorCount() > 0);
            Assert.True(_fixture.Graphics.GetCurrentMonitor() >= 0);
            Assert.NotEmpty(_fixture.Graphics.GetResolutions(_fixture.Graphics.GetCurrentMonitor()));
            Assert.True(_fixture.Graphics.GetDesktopResolution(_fixture.Graphics.GetCurrentMonitor()).X > 0);
            Assert.True(_fixture.Graphics.GetDisplayDPI(_fixture.Graphics.GetCurrentMonitor()).X >= 0);

            var currentSettings = _fixture.Graphics.GetWindowSettings();
            Assert.True(_fixture.Graphics.SetWindowModes(currentSettings, currentSettings));
            Assert.Equal(currentSettings.Mode, _fixture.Graphics.GetWindowSettings().Mode);
            Assert.True(_fixture.Graphics.SetMode(currentSettings.Size.X, currentSettings.Size.Y));
            Assert.True(_fixture.Graphics.BeginFrame());
            _fixture.Graphics.ResetRenderTargets();
            _fixture.Graphics.Clear(ClearTarget.Color);
            _fixture.Graphics.OnWindowResized();
            _fixture.Graphics.OnWindowMoved();
            _fixture.Graphics.EndFrame();
            Assert.Equal(Vector2i.Zero, _fixture.Graphics.GetRenderTargetDimensions());
        }
        finally
        {
            _fixture.Graphics.SetWindowTitle(originalTitle);
            _fixture.Graphics.SetWindowPosition(originalPosition);
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
            var oldUniformBuffer = _fixture.RenderDevice.GetRenderPool().GetUniformBuffer(1u, 64u);
            _fixture.RenderContext.SetClipPlaneMask(0x5Au);

            Assert.True(_fixture.RenderDevice.EmulateLossAndRestore());

            var newTexture = _fixture.RenderDevice.GetDefaultTexture(TextureType.Texture2D);
            var newUniformBuffer = _fixture.RenderDevice.GetRenderPool().GetUniformBuffer(1u, 64u);
            Assert.NotSame(oldTexture, newTexture);
            Assert.NotSame(oldUniformBuffer, newUniformBuffer);
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
    public void Render_device_exposes_separate_device_state_lifecycle()
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
            renderDevice!.InvalidateDeviceState();
            Assert.Equal(1, deviceObject.InvalidateCalls);
            Assert.True(deviceObject.IsDataLost());

            renderDevice.RestoreDeviceState();
            Assert.Equal(1, deviceObject.RestoreCalls);
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
        var pipelineState = new PipelineState(_fixture.PipelineStateCache, new PipelineStateDesc())
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

    [Fact]
    public void Render_device_selects_closest_fullscreen_mode_like_native_implementation()
    {
        var modes = new[]
        {
            new FullscreenMode(new Vector2i(1280, 720), 60),
            new FullscreenMode(new Vector2i(1280, 720), 144),
            new FullscreenMode(new Vector2i(1920, 1080), 60)
        };

        Assert.Equal(1u, RenderDevice.GetClosestFullscreenModeIndex(modes,
            new FullscreenMode(new Vector2i(1280, 720), 144)));
        Assert.Equal(1u, RenderDevice.GetClosestFullscreenModeIndex(modes,
            new FullscreenMode(new Vector2i(1280, 720), 120)));
        Assert.Equal(2u, RenderDevice.GetClosestFullscreenModeIndex(modes,
            new FullscreenMode(new Vector2i(1600, 900), 60)));
        Assert.Equal(2u, RenderDevice.GetClosestFullscreenModeIndex(modes,
            new FullscreenMode(new Vector2i(2560, 1440), 240)));
        Assert.Equal(modes[2], RenderDevice.GetClosestFullscreenMode(modes,
            new FullscreenMode(new Vector2i(1600, 900), 60)));
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
