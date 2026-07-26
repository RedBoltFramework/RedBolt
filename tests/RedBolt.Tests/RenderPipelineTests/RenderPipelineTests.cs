using System.Collections;
using System.Reflection;

using RedBolt.Bindings.Diligent;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderPipelineTests : IDisposable
{
    [Fact]
    public void Render_buffer_params_have_native_mutable_value_semantics()
    {
        var parameters = new RenderBufferParams();
        var copy = parameters;

        copy.Flags |= RenderBufferFlag.Persistent;

        Assert.True(typeof(RenderBufferParams).IsValueType);
        Assert.Equal(1, parameters.MultiSampleLevel);
        Assert.Equal(RenderBufferFlag.None, parameters.Flags);
        Assert.Equal(RenderBufferFlag.Persistent, copy.Flags);
        Assert.NotEqual(parameters, copy);
    }

    private readonly GraphicsEngineFixture _fixture;
    private bool _graphicsInitialized;

    public RenderPipelineTests(GraphicsEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Render_pipeline_enables_instancing_by_default()
    {
        var renderPipeline = new RenderPipeline();

        Assert.True(renderPipeline.Settings.InstancingBuffer.EnableInstancing);
    }

    [Fact]
    public void Render_pipeline_collects_render_path_parameters()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));

        renderPipeline.SetRenderPath(renderPath);

        Assert.Equal(1.25f, renderPipeline.GetRenderPathParameters()["Exposure"].GetFloat());
    }

    [Fact]
    public void Render_pipeline_serializes_render_path_and_pass_attributes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new TrackingRenderPass("First"));
        renderPath.AddPass(new TrackingRenderPass("Second"));
        renderPipeline.SetRenderPath(renderPath);

        var renderPathAttribute = renderPipeline.GetRenderPathAttr();
        Assert.Equal(renderPath.Name, renderPathAttribute.Name);

        var passAttributes = renderPipeline.GetRenderPassesAttr();
        Assert.Equal(4, passAttributes.Count);
        Assert.Equal("First", passAttributes[0].GetString());
        Assert.True(passAttributes[1].GetBool());
        Assert.Equal("Second", passAttributes[2].GetString());
        Assert.True(passAttributes[3].GetBool());

        renderPipeline.SetRenderPassesAttr(new VariantVector
        {
            new("First"),
            new(false),
            new("Second"),
            new(true)
        });

        Assert.Equal(
            [("First", false), ("Second", true)],
            renderPipeline.GetRenderPasses());
    }

    [Fact]
    public void Render_pipeline_copies_enabled_render_passes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetRenderPath((RenderPath?)null);
        (string Name, bool Enabled)[] renderPasses = [("First", true)];

        renderPipeline.SetRenderPasses(renderPasses);
        renderPasses[0] = ("Changed", false);

        Assert.Equal(("First", true), Assert.Single(renderPipeline.GetRenderPasses()));
    }

    [Fact]
    public void Render_pipeline_raises_events_for_settings_parameters_and_renderpath_changes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));

        var settingsEvents = 0;
        var parameterEvents = 0;
        RenderPath? changedRenderPath = null;

        renderPipeline.SettingsChanged += (_, settings) =>
        {
            ++settingsEvents;
            Assert.Equal(renderPipeline.Settings, settings);
        };
        renderPipeline.ParametersChanged += _ => ++parameterEvents;
        renderPipeline.RenderPathChanged += (_, newRenderPath) => changedRenderPath = newRenderPath;

        renderPipeline.SetSettings(new RenderPipelineSettings());
        renderPipeline.SetRenderPath(renderPath);
        renderPipeline.UpdateRenderPathParameter("Exposure", 2.0f);

        Assert.Equal(1, settingsEvents);
        Assert.Equal(1, parameterEvents);
        Assert.Same(renderPath, changedRenderPath);
    }

    [Fact]
    public void Render_pipeline_only_raises_parameter_event_on_actual_render_path_changes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));
        renderPipeline.SetRenderPath(renderPath);

        var parameterEvents = 0;
        renderPipeline.ParametersChanged += _ => ++parameterEvents;

        renderPipeline.UpdateRenderPathParameter("Missing", 2.0f);
        renderPipeline.UpdateRenderPathParameter("Exposure", 1.25f);

        Assert.Equal(0, parameterEvents);

        renderPipeline.UpdateRenderPathParameter("Exposure", 2.0f);

        Assert.Equal(1, parameterEvents);
        Assert.Equal(2.0f, renderPipeline.GetRenderPathParameters()["Exposure"].GetFloat());
    }

    [Fact]
    public void Render_pipeline_updates_existing_parameters_by_hash()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));
        renderPipeline.SetRenderPath(renderPath);

        var parameterEvents = 0;
        renderPipeline.ParametersChanged += _ => ++parameterEvents;

        using var parameters = new VariantMap
        {
            [new StringHash("Exposure")] = 2.0f,
            [new StringHash("Missing")] = 3.0f
        };
        renderPipeline.UpdateRenderPathParameters(parameters);

        Assert.Equal(1, parameterEvents);
        Assert.Equal(2.0f, renderPipeline.GetRenderPathParameters()["Exposure"].GetFloat());
        Assert.DoesNotContain("Missing", renderPipeline.GetRenderPathParameters().Keys);
    }

    [Fact]
    public void Render_pipeline_frame_shader_parameters_do_not_raise_parameter_event()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var parameterEvents = 0;
        renderPipeline.ParametersChanged += _ => ++parameterEvents;

        renderPipeline.SetFrameShaderParameter("Time", new Variant(1.0f));
        using var parameters = new StringVariantMap
        {
            ["FrameIndex"] = new Variant(4)
        };
        renderPipeline.SetFrameShaderParameters(parameters);

        Assert.Equal(0, parameterEvents);
        Assert.Equal(4, renderPipeline.GetFrameShaderParameters()["FrameIndex"].GetInt());
    }

    [Fact]
    public void Render_pipeline_updates_existing_frame_parameters_by_hash()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetFrameShaderParameter("Time", new Variant(1.0f));

        using var parameters = new VariantMap
        {
            [new StringHash("Time")] = new Variant(2.0f),
            [new StringHash("Missing")] = new Variant(3.0f)
        };
        renderPipeline.UpdateFrameShaderParameters(parameters);

        Assert.Equal(2.0f, renderPipeline.GetFrameShaderParameters()["Time"].GetFloat());
        Assert.DoesNotContain("Missing", renderPipeline.GetFrameShaderParameters().Keys);
    }

    [Fact]
    public void Render_pipeline_updates_single_existing_frame_parameter_by_hash()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetFrameShaderParameter("Time", new Variant(1.0f));

        renderPipeline.UpdateFrameShaderParameter(new StringHash("Time"), new Variant(2.0f));
        renderPipeline.UpdateFrameShaderParameter(new StringHash("Missing"), new Variant(3.0f));

        Assert.Equal(2.0f, renderPipeline.GetFrameShaderParameters()["Time"].GetFloat());
        Assert.DoesNotContain("Missing", renderPipeline.GetFrameShaderParameters().Keys);
    }

    [Fact]
    public void Frame_shader_parameters_and_view_descriptors_own_independent_variants()
    {
        var renderPipeline = new RenderPipeline();
        using var source = Variant.MakeCustomValue(new FrameParameterValue(1));
        using var parameters = new StringVariantMap
        {
            ["Value"] = source
        };
        renderPipeline.SetFrameShaderParameters(parameters);

        var parameterValue = parameters["Value"].GetCustom<FrameParameterValue>();
        var mapValue = renderPipeline.GetFrameShaderParameters()["Value"].GetCustom<FrameParameterValue>();
        Assert.NotSame(source.GetCustom<FrameParameterValue>(), mapValue);
        Assert.NotSame(parameterValue, mapValue);

        var view = new FrameParameterRenderPipelineView(renderPipeline);
        view.RefreshFrameParameters();
        var firstViewValue = view.GetFrameParameterValue();
        Assert.NotSame(mapValue, firstViewValue);

        using var replacement = Variant.MakeCustomValue(new FrameParameterValue(2));
        renderPipeline.SetFrameShaderParameter("Value", replacement);
        Assert.True(mapValue.IsDisposed);
        Assert.False(parameterValue.IsDisposed);
        Assert.False(firstViewValue.IsDisposed);

        view.RefreshFrameParameters();
        Assert.True(firstViewValue.IsDisposed);
        var secondViewValue = view.GetFrameParameterValue();
        Assert.Equal(2, secondViewValue.Value);

        view.Dispose();
        Assert.True(secondViewValue.IsDisposed);

        var replacementMapValue = renderPipeline.GetFrameShaderParameters()["Value"].GetCustom<FrameParameterValue>();
        renderPipeline.Dispose();
        Assert.True(replacementMapValue.IsDisposed);
        Assert.False(source.GetCustom<FrameParameterValue>().IsDisposed);
        Assert.False(replacement.GetCustom<FrameParameterValue>().IsDisposed);
    }

    [Fact]
    public void Render_path_parameters_own_independent_variants()
    {
        var renderPipeline = new RenderPipeline();
        using var source = Variant.MakeCustomValue(new FrameParameterValue(1));
        using var parameters = new StringVariantMap
        {
            ["Value"] = source
        };
        renderPipeline.SetRenderPathParameters(parameters);

        var parameterValue = parameters["Value"].GetCustom<FrameParameterValue>();
        var mapValue = renderPipeline.GetRenderPathParameters()["Value"].GetCustom<FrameParameterValue>();
        Assert.NotSame(source.GetCustom<FrameParameterValue>(), mapValue);
        Assert.NotSame(parameterValue, mapValue);

        using var replacement = Variant.MakeCustomValue(new FrameParameterValue(2));
        renderPipeline.UpdateRenderPathParameter(new StringHash("Value"), replacement);
        Assert.True(mapValue.IsDisposed);
        Assert.False(parameterValue.IsDisposed);

        var replacementMapValue = renderPipeline.GetRenderPathParameters()["Value"].GetCustom<FrameParameterValue>();
        Assert.Equal(2, replacementMapValue.Value);

        renderPipeline.Dispose();
        Assert.True(replacementMapValue.IsDisposed);
        Assert.False(source.GetCustom<FrameParameterValue>().IsDisposed);
        Assert.False(replacement.GetCustom<FrameParameterValue>().IsDisposed);
    }

    [Fact]
    public void Render_pipeline_set_render_pass_enabled_only_notifies_on_change()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new TrackingRenderPass("Tracking"));
        renderPipeline.SetRenderPath(renderPath);

        var parameterEvents = 0;
        renderPipeline.ParametersChanged += _ => ++parameterEvents;

        renderPipeline.SetRenderPassEnabled("Tracking", false);
        renderPipeline.SetRenderPassEnabled("Tracking", false);

        Assert.Equal(1, parameterEvents);
        Assert.Equal(("Tracking", false), Assert.Single(renderPipeline.GetRenderPasses()));
    }

    [Fact]
    public void Render_pipeline_view_updates_and_renders_enabled_passes()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RenderPipelineTests",
            Headless = false,
            Borderless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        try
        {
            using var scene = new Scene();
            scene.CreateComponent<Octree>();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            var renderPath = new RenderPath();
            renderPath.SetName("RenderPaths/Test.xml");
            var trackingPass = new TrackingRenderPass("Tracking");
            renderPath.AddPass(trackingPass);
            renderPipeline.SetRenderPath(renderPath);

            var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
            var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
            var view = renderPipeline.Instantiate();

            var beginUpdateCalls = 0;
            var endUpdateCalls = 0;
            var beginRenderCalls = 0;
            var buffersReadyCalls = 0;
            var endRenderCalls = 0;
            var beginRenderingCalls = 0;
            var endRenderingCalls = 0;
            var lifecycle = new List<string>();
            view.UpdateBegin += _ => lifecycle.Add("UpdateBeginSignal");
            view.UpdateEnd += _ => lifecycle.Add("UpdateEndSignal");
            view.RenderBegin += _ => lifecycle.Add("RenderBeginSignal");
            view.RenderEnd += _ => lifecycle.Add("RenderEndSignal");
            scene.SubscribeToEvent<GraphicsEvents.BeginViewUpdateEvent>((ref GraphicsEvents.BeginViewUpdateEvent eventData) =>
            {
                ++beginUpdateCalls;
                lifecycle.Add("BeginViewUpdateEvent");
                Assert.Same(view, eventData.RenderPipelineView);
                Assert.Null(eventData.Texture);
                Assert.Null(eventData.Surface);
                Assert.Same(scene, eventData.Scene);
                Assert.Same(camera, eventData.Camera);
                Assert.Same(_fixture.Renderer, scene.GetEventSender());
            });
            scene.SubscribeToEvent<GraphicsEvents.EndViewUpdateEvent>((ref GraphicsEvents.EndViewUpdateEvent _) =>
            {
                ++endUpdateCalls;
                lifecycle.Add("EndViewUpdateEvent");
            });
            scene.SubscribeToEvent<GraphicsEvents.BeginViewRenderEvent>((ref GraphicsEvents.BeginViewRenderEvent _) =>
            {
                ++beginRenderCalls;
                lifecycle.Add("BeginViewRenderEvent");
            });
            scene.SubscribeToEvent<GraphicsEvents.ViewBuffersReadyEvent>((ref GraphicsEvents.ViewBuffersReadyEvent _) =>
            {
                ++buffersReadyCalls;
                lifecycle.Add("ViewBuffersReadyEvent");
            });
            scene.SubscribeToEvent<GraphicsEvents.EndViewRenderEvent>((ref GraphicsEvents.EndViewRenderEvent _) =>
            {
                ++endRenderCalls;
                lifecycle.Add("EndViewRenderEvent");
            });
            scene.SubscribeToEvent<GraphicsEvents.BeginRenderingEvent>((ref GraphicsEvents.BeginRenderingEvent _) => ++beginRenderingCalls);
            scene.SubscribeToEvent<GraphicsEvents.EndRenderingEvent>((ref GraphicsEvents.EndRenderingEvent _) => ++endRenderingCalls);

            Assert.True(view.Define(null, viewport, _fixture.Graphics));

            view.Update(_fixture.Graphics);
            view.Render(_fixture.Graphics, MaterialQuality.High);

            Assert.Equal(1, trackingPass.UpdateCalls);
            Assert.Equal(1, trackingPass.RenderCalls);
            Assert.Equal(new Vector2i(64, 64), view.GetFrameInfo().ViewSize);
            Assert.Equal(0, view.GetStats().NumGeometries);
            Assert.Equal(1, beginUpdateCalls);
            Assert.Equal(1, endUpdateCalls);
            Assert.Equal(1, beginRenderCalls);
            Assert.Equal(1, buffersReadyCalls);
            Assert.Equal(1, endRenderCalls);
            Assert.Equal(0, beginRenderingCalls);
            Assert.Equal(0, endRenderingCalls);
            Assert.Equal(
            [
                "UpdateBeginSignal",
            "BeginViewUpdateEvent",
            "EndViewUpdateEvent",
            "UpdateEndSignal",
            "RenderBeginSignal",
            "BeginViewRenderEvent",
            "ViewBuffersReadyEvent",
            "RenderEndSignal",
            "EndViewRenderEvent"
            ], lifecycle);
        }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    [Fact]
    public void Render_pipeline_publishes_null_normal_buffer_for_forward_lighting()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RenderPipelineTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        try
        {
            var scene = new Scene();
            scene.CreateComponent<Octree>();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            var renderPath = new RenderPath();
            renderPath.SetName("RenderPaths/Test.xml");
            var capturePass = new BufferCaptureRenderPass("Capture", SharedRenderPassState.NormalBufferId);
            renderPath.AddPass(capturePass);
            renderPath.AddPass(new AmbientOcclusionPass());
            renderPipeline.SetRenderPath(renderPath);

            var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
            var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
            var view = renderPipeline.Instantiate();

            Assert.True(view.Define(null, viewport, _fixture.Graphics));

            view.Update(_fixture.Graphics);
            view.Render(_fixture.Graphics, MaterialQuality.High);

            Assert.True(capturePass.WasBufferPresent);
            Assert.True(capturePass.WasBufferNull);
        }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    [Fact]
    public void Render_pipeline_keeps_outline_buffer_published_during_update()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RenderPipelineTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        try
        {
            var scene = new Scene();
            scene.CreateComponent<Octree>();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            var renderPath = new RenderPath();
            renderPath.SetName("RenderPaths/Test.xml");
            renderPath.AddPass(new OutlineRenderPass());
            renderPipeline.SetRenderPath(renderPath);

            var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
            var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
            var view = renderPipeline.Instantiate();

            Assert.True(view.Define(null, viewport, _fixture.Graphics));

            view.Update(_fixture.Graphics);

            var sharedState = Assert.IsType<SharedRenderPassState>(GetPrivateField(view, "_state"));
            Assert.True(sharedState.RenderBuffers.TryGetValue(OutlineRenderPass.ColorBufferId, out var outlineBuffer));
            Assert.False(Assert.IsAssignableFrom<RenderBuffer>(outlineBuffer).IsEnabled);
        }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    [Fact]
    public void Render_buffer_manager_supports_secondary_color_buffer_when_requested()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 640, 360));

        var firstColor = manager.ColorOutput;
        Assert.NotNull(manager.SecondaryColorTexture);

        manager.SwapColorBuffers(synchronizeContents: false);

        Assert.NotSame(firstColor, manager.ColorOutput);
        Assert.Equal(new Vector2i(640, 360), manager.OutputSize);
    }

    [Fact]
    public void Render_buffer_manager_uses_offscreen_primary_buffer_for_color_read_write_frames()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearHDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 640, 360));

        Assert.NotNull(manager.ColorOutput!.GetTexture());
        Assert.NotNull(manager.DepthStencilOutput!.GetTexture());
        Assert.Equal(TextureFormat.TexFormatRgba16Float, manager.ColorOutput.GetTexture()!.GetParams().Format);
        Assert.NotNull(manager.SecondaryColorTexture);
        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_allocates_readable_depth_buffer_when_requested()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 640, 360));

        Assert.NotNull(manager.ColorOutput!.GetTexture());
        Assert.NotNull(manager.DepthStencilOutput!.GetTexture());
        Assert.Same(manager.DepthStencilOutput.GetTexture(), manager.DepthStencilTexture);
        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_shared_state_uses_readable_depth_buffer_when_requested()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 320, 200));

        Assert.NotNull(manager.DepthStencilOutput!.GetTexture());
        Assert.Same(manager.DepthStencilTexture, manager.DepthStencilOutput.GetTexture());
    }

    [Fact]
    public void Render_buffer_manager_uses_simple_primary_buffer_without_secondary_when_only_readable_color_is_requested()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableColor = true
        });
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 320, 200));

        Assert.NotNull(manager.ColorOutput!.GetTexture());
        Assert.Null(manager.SecondaryColorTexture);
        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_texture_output_without_secondary_when_only_readable_color_is_requested()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableColor = true
        });

        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        colorTarget.SetFilterMode(TextureFilterMode.Bilinear);
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        var output = GetRenderSurface(colorTarget, depthTarget);
        DefineAndBeginFrame(manager, output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.Null(manager.SecondaryColorTexture);
        Assert.IsType<ViewportColorRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_keeps_nearest_texture_output_when_only_readable_color_is_requested()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableColor = true
        });

        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        colorTarget.SetFilterMode(TextureFilterMode.Nearest);
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        var output = GetRenderSurface(colorTarget, depthTarget);
        DefineAndBeginFrame(manager, output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.Null(manager.SecondaryColorTexture);
        Assert.IsType<ViewportColorRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Graphics_clear_framebuffer_pipeline_settings_follow_requested_targets()
    {
        var method = typeof(Graphics).GetMethod("GetClearFramebufferPipelineSettings", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var depthSettings = method!.Invoke(null, [ClearTarget.Depth]);
        Assert.NotNull(depthSettings);
        Assert.False((bool)GetNamedValue(depthSettings!, "ColorWriteEnabled"));
        Assert.True((bool)GetNamedValue(depthSettings, "DepthWriteEnabled"));
        Assert.False((bool)GetNamedValue(depthSettings, "StencilEnabled"));

        var stencilSettings = method.Invoke(null, [ClearTarget.Stencil]);
        Assert.NotNull(stencilSettings);
        Assert.False((bool)GetNamedValue(stencilSettings!, "ColorWriteEnabled"));
        Assert.False((bool)GetNamedValue(stencilSettings, "DepthWriteEnabled"));
        Assert.True((bool)GetNamedValue(stencilSettings, "StencilEnabled"));
        Assert.Equal((byte)0xff, Assert.IsType<byte>(GetNamedValue(stencilSettings, "StencilWriteMask")));
        Assert.Equal(global::RedBolt.Bindings.Diligent.StencilOp.StencilOpReplace,
            Assert.IsType<global::RedBolt.Bindings.Diligent.StencilOp>(GetNamedValue(stencilSettings, "StencilPassOp")));
    }

    [Fact]
    public void Render_buffer_manager_derives_output_traits_from_render_target_texture()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.Optimized
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var renderTarget = new Texture2D { Name = "Textures/ViewportTarget" };
        Assert.True(renderTarget.SetSize(320, 200, TextureFormat.TexFormatRgba16Float, TextureFlag.BindRenderTarget));
        renderTarget.SetFilterMode(TextureFilterMode.Bilinear);
        renderTarget.SetLinear(true);

        DefineAndBeginFrame(manager, GetRenderSurface(renderTarget), new IntRect(0, 0, 320, 200));

        Assert.Equal(TextureFormat.TexFormatRgba16Float, manager.OutputColorFormat);
        Assert.Equal(1, manager.OutputMultiSample);
        Assert.True(manager.IsLinearColorSpace);
        Assert.True(manager.IsHDR);
        var colorTexture = manager.ColorOutput!.GetTexture();
        var secondaryColorTexture = manager.SecondaryColorTexture;
        Assert.NotNull(colorTexture);
        Assert.NotNull(secondaryColorTexture);
        Assert.Equal(TextureFormat.TexFormatRgba16Float, colorTexture!.GetParams().Format);
        Assert.Equal(TextureFormat.TexFormatRgba16Float, secondaryColorTexture!.GetParams().Format);
    }

    [Fact]
    public void Render_surface_derives_multisample_from_texture()
    {
        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget, 4));
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil, 4));

        var output = GetRenderSurface(colorTarget, depthTarget);

        Assert.Equal(4, RenderSurface.GetMultiSample(_fixture.Graphics, output));
        Assert.Equal(4, output.GetView().GetMultiSample());
        Assert.Equal(4, output.GetLinkedDepthStencil()?.GetView().GetMultiSample());
    }

    [Fact]
    public void Render_buffer_manager_refreshes_managed_output_buffers_with_requested_multisample()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            InheritMultiSampleLevel = false,
            MultiSampleLevel = 4,
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableColor = true
        });
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 320, 200));

        Assert.Equal(4, manager.OutputMultiSample);
        Assert.Equal(4, manager.ColorOutput!.GetTexture()!.GetParams().MultiSample);
        Assert.Equal(4, manager.DepthStencilTexture!.GetParams().MultiSample);
    }

    [Fact]
    public void Texture_render_buffer_propagates_no_auto_resolve_flag_into_managed_texture()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var buffer = new TextureRenderBuffer(pipeline, new RenderBufferParams(
            TextureFormat.TexFormatRgba8Unorm,
            4,
            RenderBufferFlag.NoMultiSampledAutoResolve));

        pipeline.RaiseRenderBegin(new CommonFrameInfo { ViewportRect = new IntRect(0, 0, 320, 200) });

        Assert.Equal(4, buffer.GetTexture()!.GetParams().MultiSample);
        Assert.True((buffer.GetTexture()!.GetParams().Flags & TextureFlag.NoMultiSampledAutoResolve) != 0);
    }

    [Fact]
    public void Texture_render_buffer_creates_depth_stencil_texture_for_all_depth_formats()
    {
        foreach (var format in new[]
                 {
                     TextureFormat.TexFormatD16Unorm,
                     TextureFormat.TexFormatD32Float,
                     TextureFormat.TexFormatD24UnormS8Uint,
                     TextureFormat.TexFormatD32FloatS8x24Uint
                 })
        {
            var pipeline = new TestRenderPipelineInterface();
            using var buffer = new TextureRenderBuffer(pipeline, new RenderBufferParams(format));

            pipeline.RaiseRenderBegin(new CommonFrameInfo { ViewportRect = new IntRect(0, 0, 320, 200) });

            var flags = buffer.GetTexture()!.GetParams().Flags;
            Assert.True((flags & TextureFlag.BindDepthStencil) != 0);
            Assert.True((flags & TextureFlag.BindRenderTarget) == 0);
        }
    }

    [Fact]
    public void Render_buffer_manager_respects_color_space_over_output_texture_format()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.GammaLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var renderTarget = new Texture2D { Name = "Textures/ViewportTarget" };
        Assert.True(renderTarget.SetSize(320, 200, TextureFormat.TexFormatRgba16Float, TextureFlag.BindRenderTarget));
        renderTarget.SetFilterMode(TextureFilterMode.Bilinear);
        renderTarget.SetLinear(true);
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        DefineAndBeginFrame(manager, GetRenderSurface(renderTarget, depthTarget), new IntRect(0, 0, 320, 200));

        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, manager.OutputColorFormat);
        Assert.True(manager.IsLinearColorSpace);
        Assert.False(manager.IsHDR);
        Assert.NotSame(renderTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, manager.ColorOutput.GetTexture()!.GetParams().Format);
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, manager.SecondaryColorTexture!.GetParams().Format);
        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_surface_preserves_srgb_parent_texture_metadata()
    {
        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        colorTarget.SetSRGB(true);
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));

        var output = GetRenderSurface(colorTarget);

        Assert.Same(colorTarget, output.GetParentTexture());
        Assert.True(output.GetParentTexture()!.GetSRGB());
    }

    [Fact]
    public void Render_surface_preserves_float_parent_texture_format()
    {
        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRg16Float, TextureFlag.BindRenderTarget));

        var output = GetRenderSurface(colorTarget);

        Assert.Same(colorTarget, output.GetParentTexture());
        Assert.Equal(TextureFormat.TexFormatRg16Float, output.GetParentTexture()!.Format);
    }

    [Fact]
    public void Render_buffer_manager_allocates_linear_ldr_intermediate_buffer_for_non_srgb_texture_output()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        DefineAndBeginFrame(manager, GetRenderSurface(colorTarget, depthTarget), new IntRect(0, 0, 320, 200));

        Assert.NotSame(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.Equal(TextureFormat.TexFormatRgba8UnormSrgb, manager.ColorOutput.GetTexture()!.GetParams().Format);
        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_srgb_texture_output_for_linear_ldr()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        colorTarget.SetSRGB(true);
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        DefineAndBeginFrame(manager, GetRenderSurface(colorTarget, depthTarget), new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.True(manager.IsLinearColorSpace);
        Assert.IsType<ViewportColorRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_marks_linear_ldr_color_buffers_as_srgb()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 320, 200));

        var primaryTexture = manager.ColorOutput!.GetTexture();
        var secondaryTexture = manager.SecondaryColorTexture;

        Assert.NotNull(primaryTexture);
        Assert.NotNull(secondaryTexture);
        Assert.True(manager.IsLinearColorSpace);
        Assert.False(manager.IsHDR);
        Assert.Equal(TextureFormat.TexFormatRgba8UnormSrgb, primaryTexture!.GetParams().Format);
        Assert.Equal(TextureFormat.TexFormatRgba8UnormSrgb, secondaryTexture!.GetParams().Format);
    }

    [Fact]
    public void Render_buffer_manager_treats_optimized_rg16f_output_as_linear_hdr()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.Optimized
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var renderTarget = new Texture2D { Name = "Textures/ViewportTarget" };
        Assert.True(renderTarget.SetSize(320, 200, TextureFormat.TexFormatRg16Float, TextureFlag.BindRenderTarget));
        renderTarget.SetFilterMode(TextureFilterMode.Bilinear);

        DefineAndBeginFrame(manager, GetRenderSurface(renderTarget), new IntRect(0, 0, 320, 200));

        Assert.Equal(TextureFormat.TexFormatRg16Float, manager.OutputColorFormat);
        Assert.True(manager.IsLinearColorSpace);
        Assert.True(manager.IsHDR);
        Assert.Equal(TextureFormat.TexFormatRg16Float, manager.ColorOutput!.GetTexture()!.GetParams().Format);
        Assert.Equal(TextureFormat.TexFormatRg16Float, manager.SecondaryColorTexture!.GetParams().Format);
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_texture_output_without_substitute_buffers()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        var output = GetRenderSurface(colorTarget, depthTarget);
        DefineAndBeginFrame(manager, output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.Same(depthTarget.GetRawTexture(), manager.DepthStencilTexture);
        Assert.IsType<ViewportColorRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_texture_output_as_primary_for_color_read_write_frames()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        var output = GetRenderSurface(colorTarget, depthTarget);
        DefineAndBeginFrame(manager, output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.NotNull(manager.SecondaryColorTexture);
        Assert.NotSame(colorTarget.GetRawTexture(), manager.SecondaryColorTexture);
        Assert.Same(depthTarget.GetRawTexture(), manager.DepthStencilTexture);
        Assert.IsType<ViewportColorRenderBuffer>(manager.ColorOutput);

        manager.SwapColorBuffers(synchronizeContents: false);

        Assert.NotSame(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.Same(colorTarget.GetRawTexture(), manager.SecondaryColorTexture);
        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Render_buffer_manager_allocates_substitute_buffers_for_partial_texture_output()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D { Name = "Textures/ViewportColor" };
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var depthTarget = new Texture2D { Name = "Textures/ViewportDepth" };
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.TexFormatD24UnormS8Uint, TextureFlag.BindDepthStencil));

        var output = GetRenderSurface(colorTarget, depthTarget);
        DefineAndBeginFrame(manager, output, new IntRect(0, 0, 160, 200));

        Assert.NotSame(colorTarget.GetRawTexture(), manager.ColorOutput!.GetTexture());
        Assert.NotSame(depthTarget.GetRawTexture(), manager.DepthStencilTexture);
        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
    }

    [Fact]
    public void Bloom_pass_allocates_scaled_intermediate_buffers()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var bloom = new BloomPass();
        using var parameters = new StringVariantMap();
        bloom.UpdateParameters(new RenderPipelineSettings(), parameters);

        var sharedState = new SharedRenderPassState
        {
            RenderBufferManager = manager
        };
        bloom.Update(sharedState);

        var textures = Assert.IsAssignableFrom<IEnumerable>(GetPrivateField(bloom, "_textures"));
        var caches = textures.Cast<object>().ToArray();
        Assert.Equal(5, caches.Length);

        var first = Assert.IsType<TextureRenderBuffer>(GetPublicProperty(caches[0], "Final"));
        var second = Assert.IsType<TextureRenderBuffer>(GetPublicProperty(caches[1], "Final"));
        var third = Assert.IsType<TextureRenderBuffer>(GetPublicProperty(caches[2], "Final"));

        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 128, 64));
        Assert.Equal(new Vector3i(128, 64, 1), first.GetTexture()!.GetParams().Size);
        Assert.Equal(new Vector3i(64, 32, 1), second.GetTexture()!.GetParams().Size);
        Assert.Equal(new Vector3i(32, 16, 1), third.GetTexture()!.GetParams().Size);
    }

    [Fact]
    public void Render_buffer_manager_resizes_dynamic_intermediate_buffers_with_viewport_changes()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var scaled = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm), new Vector2f(0.5f, 0.5f));
        var fixedSize = manager.CreateColorBuffer(
            new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm, 1, RenderBufferFlag.FixedTextureSize),
            new Vector2f(12, 6));

        var firstFrame = DefineAndBeginFrame(manager, null, new IntRect(0, 0, 128, 64));
        Assert.Equal(new Vector3i(64, 32, 1), scaled.GetTexture()!.GetParams().Size);
        Assert.Equal(new Vector3i(12, 6, 1), fixedSize.GetTexture()!.GetParams().Size);

        Assert.IsType<TestRenderPipelineInterface>(GetPrivateField(manager, "_renderPipeline")).EndFrame(firstFrame);
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 256, 128));

        Assert.Equal(new Vector3i(128, 64, 1), scaled.GetTexture()!.GetParams().Size);
        Assert.Equal(new IntRect(0, 0, 128, 64), scaled.GetViewportRect());
        Assert.Equal(new Vector3i(12, 6, 1), fixedSize.GetTexture()!.GetParams().Size);
        Assert.Equal(new IntRect(0, 0, 12, 6), fixedSize.GetViewportRect());
    }

    [Fact]
    public void Bloom_pass_cached_buffers_follow_viewport_resize()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var bloom = new BloomPass();
        using var parameters = new StringVariantMap();
        bloom.UpdateParameters(new RenderPipelineSettings(), parameters);
        var sharedState = new SharedRenderPassState
        {
            RenderBufferManager = manager
        };
        bloom.Update(sharedState);

        var textures = Assert.IsAssignableFrom<IEnumerable>(GetPrivateField(bloom, "_textures"));
        var caches = textures.Cast<object>().ToArray();
        var first = Assert.IsType<TextureRenderBuffer>(GetPublicProperty(caches[0], "Final"));
        var second = Assert.IsType<TextureRenderBuffer>(GetPublicProperty(caches[1], "Final"));

        var firstFrame = DefineAndBeginFrame(manager, null, new IntRect(0, 0, 128, 64));
        Assert.Equal(new Vector3i(128, 64, 1), first.GetTexture()!.GetParams().Size);
        Assert.Equal(new Vector3i(64, 32, 1), second.GetTexture()!.GetParams().Size);

        Assert.IsType<TestRenderPipelineInterface>(GetPrivateField(manager, "_renderPipeline")).EndFrame(firstFrame);
        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 256, 128));

        Assert.Equal(new Vector3i(256, 128, 1), first.GetTexture()!.GetParams().Size);
        Assert.Equal(new Vector3i(128, 64, 1), second.GetTexture()!.GetParams().Size);
    }

    [Fact]
    public void Render_buffer_manager_creates_cubemap_render_buffers_with_face_slice_views()
    {
        using var manager = CreateRenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var cubemap = manager.CreateColorBuffer(
            new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm, 1, RenderBufferFlag.CubeMap | RenderBufferFlag.FixedTextureSize),
            new Vector2f(32, 32));

        DefineAndBeginFrame(manager, null, new IntRect(0, 0, 128, 64));
        var cubeTexture = Assert.IsType<RawTexture>(cubemap.GetTexture());
        Assert.Equal(TextureType.TextureCube, cubeTexture.GetParams().Type);
        Assert.Equal(new Vector3i(32, 32, 1), cubeTexture.GetParams().Size);
        Assert.Equal(new IntRect(0, 0, 32, 32), cubemap.GetViewportRect());
        Assert.Equal(RenderTargetView.TextureSlice(cubeTexture, 0), cubemap.GetView());
        Assert.Equal(RenderTargetView.TextureSlice(cubeTexture, 5), cubemap.GetView(5));
    }

    [Fact]
    public void Render_pipeline_uses_default_renderpath_on_attach()
    {
        const string defaultRenderPathName = "RenderPaths/Default.renderpath";
        var previousContext = Context.Instance;
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPipelineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        _fixture.Resources.AddResourceDir(tempRoot);

        try
        {
            Context.SetInstance(_fixture.Context);
            System.IO.File.WriteAllText(Path.Combine(tempRoot, "RenderPaths", "Default.renderpath"), """
                {
                  "passes": [
                    {
                      "type": "FullScreenShaderPass",
                      "value": {
                        "attributes": [
                          { "name": "Pass Name", "type": "String", "value": "Postprocess: Default" },
                          {
                            "name": "Parameters",
                            "type": "StringVariantMap",
                            "value": [
                              { "key": "Exposure", "type": "Float", "value": 1.25 }
                            ]
                          }
                        ]
                      }
                    }
                  ]
                }
                """);

            _fixture.Resources.ReleaseResource<RenderPath>(defaultRenderPathName);

            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();

            Assert.NotNull(renderPipeline.GetRenderPath());
            Assert.Equal(defaultRenderPathName, renderPipeline.GetRenderPath()!.Name);
            Assert.Equal(1.25f, renderPipeline.GetRenderPathParameters()["Exposure"].GetFloat());
        }
        finally
        {
            Context.SetInstance(previousContext);
            _fixture.Resources.ReleaseResource<RenderPath>(defaultRenderPathName);
            _fixture.Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Render_pipeline_reacts_to_render_path_resource_reload()
    {
        const string renderPathName = "RenderPaths/Reload.renderpath";
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPipelineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        _fixture.Resources.AddResourceDir(tempRoot);

        try
        {
            var renderPathFile = Path.Combine(tempRoot, renderPathName);
            WriteRenderPath(renderPathFile, 1.25f, includeBloom: false);
            _fixture.Resources.ReleaseResource<RenderPath>(renderPathName);

            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            renderPipeline.SetRenderPath(_fixture.Resources.GetResource<RenderPath>(renderPathName));

            var renderPathChangedEvents = 0;
            renderPipeline.RenderPathChanged += (_, _) => ++renderPathChangedEvents;

            Assert.Equal(1.25f, renderPipeline.GetRenderPathParameters()["Exposure"].GetFloat());
            Assert.DoesNotContain("BloomStrength", renderPipeline.GetRenderPathParameters().Keys);

            WriteRenderPath(renderPathFile, 2.5f, includeBloom: true);
            Assert.True(_fixture.Resources.ReloadResource<RenderPath>(renderPathName));

            Assert.Equal(1, renderPathChangedEvents);
            Assert.Equal(1.25f, renderPipeline.GetRenderPathParameters()["Exposure"].GetFloat());
            Assert.Equal(0.8f, renderPipeline.GetRenderPathParameters()["BloomStrength"].GetFloat());
        }
        finally
        {
            _fixture.Resources.ReleaseResource<RenderPath>(renderPathName);
            _fixture.Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Render_pipeline_reload_event_observes_only_the_current_render_path()
    {
        var renderPipeline = new RenderPipeline();
        var firstRenderPath = new RenderPath();
        firstRenderPath.SetName("RenderPaths/First.renderpath");
        var secondRenderPath = new RenderPath();
        secondRenderPath.SetName("RenderPaths/Second.renderpath");
        renderPipeline.SetRenderPath(firstRenderPath);
        renderPipeline.SetRenderPath(secondRenderPath);
        var renderPathChangedEvents = 0;
        renderPipeline.RenderPathChanged += (_, _) => ++renderPathChangedEvents;

        firstRenderPath.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(0, renderPathChangedEvents);

        secondRenderPath.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(1, renderPathChangedEvents);
    }

    private sealed class ParameterRenderPass : RenderPass
    {
        private readonly string _parameterName;
        private readonly Variant _value;

        public ParameterRenderPass(string parameterName, Variant value)
        {
            _passName = parameterName;
            this._parameterName = parameterName;
            this._value = value.Clone();
        }

        public override void CollectParameters(StringVariantMap parameters)
        {
            DeclareParameter(_parameterName, _value, parameters);
        }

        public override RenderPass Clone() => new ParameterRenderPass(_parameterName, _value);

        public override void Dispose()
        {
            _value.Dispose();
            base.Dispose();
        }
    }

    private sealed class FrameParameterRenderPipelineView : RenderPipelineView
    {
        private FrameInfo _frameInfo;
        private RenderPipelineStats _stats;

        public FrameParameterRenderPipelineView(RenderPipeline renderPipeline)
            : base(renderPipeline)
        {
        }

        public void RefreshFrameParameters() => UpdateFrameParameters();

        public FrameParameterValue GetFrameParameterValue()
            => _frameParameters[0].Value.GetCustom<FrameParameterValue>();

        public override bool IsLinearColorSpace => false;

        public override bool Define(RenderSurface? renderTarget, Viewport viewport, Graphics graphics) => false;

        public override void Update(Graphics graphics)
        {
        }

        public override void Render(Graphics graphics, MaterialQuality materialQuality)
        {
        }

        public override ref readonly FrameInfo GetFrameInfo() => ref _frameInfo;

        public override ref readonly RenderPipelineStats GetStats() => ref _stats;

        public override void DrawDebugGeometries(bool depthTest)
        {
        }

        public override void DrawDebugLights(bool depthTest)
        {
        }
    }

    private sealed class FrameParameterValue(int value) : ICloneable, IDisposable
    {
        public int Value { get; } = value;

        public bool IsDisposed { get; private set; }

        public object Clone() => new FrameParameterValue(Value);

        public void Dispose() => IsDisposed = true;
    }

    private static object GetPrivateField(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{name}' was not found on '{instance.GetType().Name}'.");
        return field.GetValue(instance)
            ?? throw new InvalidOperationException($"Field '{name}' on '{instance.GetType().Name}' was null.");
    }

    private static object GetPublicProperty(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Property '{name}' was not found on '{instance.GetType().Name}'.");
        return property.GetValue(instance)
            ?? throw new InvalidOperationException($"Property '{name}' on '{instance.GetType().Name}' was null.");
    }

    private sealed class TrackingRenderPass : RenderPass
    {
        private readonly Counter _counter;

        public TrackingRenderPass(string passName)
            : this(passName, new Counter())
        {
        }

        private TrackingRenderPass(string passName, Counter counter)
        {
            _passName = passName;
            this._counter = counter;
        }

        public int UpdateCalls => _counter.UpdateCalls;

        public int RenderCalls => _counter.RenderCalls;

        public override void Update(SharedRenderPassState sharedState)
        {
            ++_counter.UpdateCalls;
        }

        public override void Render(SharedRenderPassState sharedState)
        {
            ++_counter.RenderCalls;
        }

        public override RenderPass Clone() => new TrackingRenderPass(GetPassName(), _counter);

        private sealed class Counter
        {
            public int UpdateCalls;
            public int RenderCalls;
        }
    }

    private static object GetNamedValue(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return property!.GetValue(instance) ?? throw new InvalidOperationException($"Property {propertyName} returned null.");
    }

    private RenderBufferManager CreateRenderBufferManager()
    {
        EnsureGraphics();
        return TestRenderPipelineInterface.CreateRenderBufferManager();
    }

    private static CommonFrameInfo DefineAndBeginFrame(
        RenderBufferManager manager, RenderSurface? renderTarget, IntRect viewportRect)
    {
        var pipeline = Assert.IsType<TestRenderPipelineInterface>(GetPrivateField(manager, "_renderPipeline"));
        return pipeline.BeginFrame(manager, renderTarget, viewportRect);
    }

    private RenderSurface GetRenderSurface(Texture2D colorTexture, Texture2D? depthTexture = null)
    {
        EnsureGraphics();
        var colorSurface = Assert.IsType<RenderSurface>(colorTexture.GetRenderSurface());
        if (depthTexture is not null)
            colorSurface.SetLinkedDepthStencil(Assert.IsType<RenderSurface>(depthTexture.GetRenderSurface()));
        return colorSurface;
    }

    private void EnsureGraphics()
    {
        if (_graphicsInitialized)
            return;

        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RenderPipelineTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        _graphicsInitialized = true;
    }

    public void Dispose()
    {
        if (!_graphicsInitialized)
            return;

            _fixture.ResetGraphics();
        RbfxTestData.RemoveResourceDirs(_fixture.Resources);
    }

    private sealed class BufferCaptureRenderPass : RenderPass
    {
        private readonly StringHash _bufferId;
        private readonly State _state;

        public BufferCaptureRenderPass(string passName, StringHash bufferId)
            : this(passName, bufferId, new State())
        {
        }

        private BufferCaptureRenderPass(string passName, StringHash bufferId, State state)
        {
            _passName = passName;
            this._bufferId = bufferId;
            this._state = state;
        }

        public bool WasBufferPresent => _state.WasBufferPresent;

        public bool WasBufferNull => _state.WasBufferNull;

        public TextureFormat CapturedFormat => _state.CapturedFormat;

        public Vector2i CapturedSize => _state.CapturedSize;

        public override void Render(SharedRenderPassState sharedState)
        {
            _state.WasBufferPresent = sharedState.RenderBuffers.TryGetValue(_bufferId, out var renderBuffer);
            _state.WasBufferNull = _state.WasBufferPresent && renderBuffer is null;
            if (!_state.WasBufferPresent || renderBuffer is null || renderBuffer.GetTexture() is not { } texture)
                return;

            _state.CapturedFormat = texture.GetParams().Format;
            _state.CapturedSize = new Vector2i(texture.GetParams().Size.X, texture.GetParams().Size.Y);
        }

        public override RenderPass Clone() => new BufferCaptureRenderPass(GetPassName(), _bufferId, _state);

        private sealed class State
        {
            public bool WasBufferPresent;
            public bool WasBufferNull;
            public TextureFormat CapturedFormat;
            public Vector2i CapturedSize;
        }
    }

    private static void WriteRenderPath(string fileName, float exposure, bool includeBloom)
    {
        var bloomEntry = includeBloom
            ? """
                                      ,
                                      { "key": "BloomStrength", "type": "Float", "value": 0.8 }
                """
            : string.Empty;

        System.IO.File.WriteAllText(fileName, $$"""
            {
              "passes": [
                {
                  "type": "FullScreenShaderPass",
                  "value": {
                    "attributes": [
                      { "name": "Pass Name", "type": "String", "value": "Postprocess: Reload" },
                      {
                        "name": "Parameters",
                        "type": "StringVariantMap",
                        "value": [
                          { "key": "Exposure", "type": "Float", "value": {{exposure}} }{{bloomEntry}}
                        ]
                      }
                    ]
                  }
                }
              ]
            }
            """);
    }
}
