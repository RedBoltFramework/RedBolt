using System.Collections;
using System.Reflection;

using RedBolt.Bindings.Diligent;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RenderPipelineTests(HeadlessEngineFixture fixture)
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
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));

        renderPipeline.SetRenderPath(renderPath);

        Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
    }

    [Fact]
    public void Render_pipeline_serializes_render_path_and_pass_attributes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
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
            [new RenderPassToggle("First", false), new RenderPassToggle("Second", true)],
            renderPipeline.GetRenderPasses());
    }

    [Fact]
    public void Render_pipeline_raises_events_for_settings_parameters_and_renderpath_changes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));

        var settingsEvents = 0;
        var parameterEvents = 0;
        RenderPath? changedRenderPath = null;

        renderPipeline.SettingsChanged += (_, settings) =>
        {
            ++settingsEvents;
            Assert.Same(renderPipeline.Settings, settings);
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
    public void Render_pipeline_only_marks_parameters_dirty_on_actual_render_path_changes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));
        renderPipeline.SetRenderPath(renderPath);

        var initialRevision = renderPipeline.Revision;

        renderPipeline.UpdateRenderPathParameter("Missing", 2.0f);
        renderPipeline.UpdateRenderPathParameter("Exposure", 1.25f);

        Assert.Equal(initialRevision, renderPipeline.Revision);

        renderPipeline.UpdateRenderPathParameter("Exposure", 2.0f);

        Assert.Equal(initialRevision + 1, renderPipeline.Revision);
        Assert.Equal(2.0f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
    }

    [Fact]
    public void Render_pipeline_updates_existing_parameters_by_hash()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));
        renderPipeline.SetRenderPath(renderPath);

        var parameterEvents = 0;
        renderPipeline.ParametersChanged += _ => ++parameterEvents;

        renderPipeline.UpdateRenderPathParameters(new Dictionary<StringHash, object?>
        {
            [new StringHash("Exposure")] = 2.0f,
            [new StringHash("Missing")] = 3.0f
        });

        Assert.Equal(1, parameterEvents);
        Assert.Equal(2.0f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
        Assert.DoesNotContain("Missing", renderPipeline.GetRenderPathParameters().Keys);
    }

    [Fact]
    public void Render_pipeline_frame_shader_parameters_do_not_bump_revision()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var initialRevision = renderPipeline.Revision;

        renderPipeline.SetFrameShaderParameter("Time", 1.0f);
        renderPipeline.SetFrameShaderParameters(
        [
            new KeyValuePair<string, object?>("FrameIndex", 4)
        ]);

        Assert.Equal(initialRevision, renderPipeline.Revision);
        Assert.Equal(4, Assert.IsType<int>(renderPipeline.GetFrameShaderParameters()["FrameIndex"]));
    }

    [Fact]
    public void Render_pipeline_updates_existing_frame_parameters_by_hash()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetFrameShaderParameter("Time", 1.0f);

        renderPipeline.UpdateFrameShaderParameters(new Dictionary<StringHash, object?>
        {
            [new StringHash("Time")] = 2.0f,
            [new StringHash("Missing")] = 3.0f
        });

        Assert.Equal(2.0f, Assert.IsType<float>(renderPipeline.GetFrameShaderParameters()["Time"]));
        Assert.DoesNotContain("Missing", renderPipeline.GetFrameShaderParameters().Keys);
    }

    [Fact]
    public void Render_pipeline_updates_single_existing_frame_parameter_by_hash()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetFrameShaderParameter("Time", 1.0f);

        renderPipeline.UpdateFrameShaderParameter(new StringHash("Time"), 2.0f);
        renderPipeline.UpdateFrameShaderParameter(new StringHash("Missing"), 3.0f);

        Assert.Equal(2.0f, Assert.IsType<float>(renderPipeline.GetFrameShaderParameters()["Time"]));
        Assert.DoesNotContain("Missing", renderPipeline.GetFrameShaderParameters().Keys);
    }

    [Fact]
    public void Render_pipeline_set_render_pass_enabled_only_notifies_on_change()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new TrackingRenderPass("Tracking"));
        renderPipeline.SetRenderPath(renderPath);

        var parameterEvents = 0;
        renderPipeline.ParametersChanged += _ => ++parameterEvents;

        renderPipeline.SetRenderPassEnabled("Tracking", false);
        renderPipeline.SetRenderPassEnabled("Tracking", false);

        Assert.Equal(1, parameterEvents);
        Assert.Equal(new RenderPassToggle("Tracking", false), Assert.Single(renderPipeline.GetRenderPasses()));
    }

    [Fact]
    public void Render_pipeline_view_updates_and_renders_enabled_passes()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
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
        using var beginUpdateSubscription = scene.SubscribeToEvent<GraphicsEvents.BeginViewUpdateEvent>((ref GraphicsEvents.BeginViewUpdateEvent eventData) =>
        {
            ++beginUpdateCalls;
            Assert.Same(view, eventData.RenderPipelineView);
            Assert.Same(scene, eventData.Scene);
            Assert.Same(camera, eventData.Camera);
        });
        using var endUpdateSubscription = scene.SubscribeToEvent<GraphicsEvents.EndViewUpdateEvent>((ref GraphicsEvents.EndViewUpdateEvent _) => ++endUpdateCalls);
        using var beginRenderSubscription = scene.SubscribeToEvent<GraphicsEvents.BeginViewRenderEvent>((ref GraphicsEvents.BeginViewRenderEvent _) => ++beginRenderCalls);
        using var buffersReadySubscription = scene.SubscribeToEvent<GraphicsEvents.ViewBuffersReadyEvent>((ref GraphicsEvents.ViewBuffersReadyEvent _) => ++buffersReadyCalls);
        using var endRenderSubscription = scene.SubscribeToEvent<GraphicsEvents.EndViewRenderEvent>((ref GraphicsEvents.EndViewRenderEvent _) => ++endRenderCalls);

        Assert.True(view.Define(null, viewport, _fixture.Graphics));

        view.Update(_fixture.Graphics);
        view.Render(_fixture.Graphics, MaterialQuality.High);

        Assert.Equal(1, trackingPass.UpdateCalls);
        Assert.Equal(1, trackingPass.RenderCalls);
        // Effective rect falls back to swap-chain / 1x1 when render target is null in the fixture.
        Assert.Equal(new Vector2i(1, 1), view.GetFrameInfo().ViewportSize);
        Assert.Equal(0u, view.GetStats().NumGeometries);
        Assert.Equal(1, beginUpdateCalls);
        Assert.Equal(1, endUpdateCalls);
        Assert.Equal(1, beginRenderCalls);
        Assert.Equal(1, buffersReadyCalls);
        Assert.Equal(1, endRenderCalls);
    }

    [Fact]
    public void Render_pipeline_does_not_publish_normal_buffer_for_forward_lighting()
    {
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        try
        {
            var scene = new Scene();
            scene.CreateComponent<Octree>();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            var renderPath = new RenderPath("RenderPaths/Test.xml");
            var capturePass = new BufferCaptureRenderPass("Capture", SharedRenderPassState.NormalBufferId);
            renderPath.AddPass(capturePass);
            renderPath.AddPass(new AmbientOcclusionPass("AmbientOcclusion"));
            renderPipeline.SetRenderPath(renderPath);

            var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
            var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
            var view = renderPipeline.Instantiate();

            Assert.True(view.Define(null, viewport, _fixture.Graphics));

            view.Update(_fixture.Graphics);
            view.Render(_fixture.Graphics, MaterialQuality.High);

            Assert.False(capturePass.WasBufferPresent);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    [Fact]
    public void Render_buffer_manager_supports_secondary_color_buffer_when_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        var firstColor = manager.GetColorOutput();
        Assert.NotNull(manager.GetSecondaryColorTexture());

        manager.SwapColorBuffers(synchronizeContents: false);

        Assert.NotSame(firstColor, manager.GetColorOutput());
        Assert.Equal(new Vector2i(640, 360), manager.GetOutputSize());
    }

    [Fact]
    public void Render_buffer_manager_rejects_color_buffer_swap_without_read_write_support()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        Assert.Throws<InvalidOperationException>(() => manager.SwapColorBuffers(synchronizeContents: false));
    }

    [Fact]
    public void Render_buffer_manager_uses_offscreen_primary_buffer_for_color_read_write_frames()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearHDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        Assert.NotNull(manager.GetColorOutput().GetTexture());
        Assert.NotNull(manager.GetDepthStencilOutput().GetTexture());
        Assert.Equal(TextureFormat.RGBA16F, manager.GetColorOutput().GetTexture()!.Format);
        Assert.NotNull(manager.GetSecondaryColorTexture());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_allocates_readable_depth_buffer_when_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        Assert.NotNull(manager.GetColorOutput().GetTexture());
        Assert.NotNull(manager.GetDepthStencilOutput().GetTexture());
        Assert.Same(manager.GetDepthStencilOutput().GetTexture(), manager.GetDepthStencilTexture());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_shared_state_uses_readable_depth_buffer_when_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        var sharedState = new SharedRenderPassState();
        manager.PopulateSharedState(sharedState);

        Assert.True(sharedState.RenderBuffers.TryGetValue("Viewport.Depth", out var depthBuffer));
        Assert.NotNull(depthBuffer.GetTexture());
        Assert.Same(manager.GetDepthStencilTexture(), depthBuffer.GetTexture());
    }

    [Fact]
    public void Render_buffer_manager_allocates_readable_depth_buffer_from_frame_traits()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableDepth = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        Assert.NotNull(manager.GetDepthStencilTexture());
    }

    [Fact]
    public void Render_buffer_manager_uses_simple_primary_buffer_without_secondary_when_only_readable_color_is_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableColor = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        Assert.NotNull(manager.GetColorOutput().GetTexture());
        Assert.Null(manager.GetSecondaryColorTexture());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_texture_output_without_secondary_when_only_readable_color_is_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableColor = true
        });

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        colorTarget.SetFilterMode(TextureFilterMode.Bilinear);
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        var output = RenderOutputTargetInfo.FromTexture(
            colorTarget,
            new IntRect(0, 0, 320, 200),
            depthTarget);
        manager.OnViewportDefined(output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.Null(manager.GetSecondaryColorTexture());
        Assert.False(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_keeps_nearest_texture_output_when_only_readable_color_is_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableColor = true
        });

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        colorTarget.SetFilterMode(TextureFilterMode.Nearest);
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        var output = RenderOutputTargetInfo.FromTexture(
            colorTarget,
            new IntRect(0, 0, 320, 200),
            depthTarget);
        manager.OnViewportDefined(output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.Null(manager.GetSecondaryColorTexture());
        Assert.False(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_maps_partial_clear_depth_to_opengl_clip_space()
    {
        // C++ ClearOutputRect: Depth = isOpenGL ? (depth * 2 - 1) : depth
        var method = typeof(RenderBufferManager).GetMethod("MapFullscreenQuadClearDepth",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(-1.0f, Assert.IsType<float>(method!.Invoke(null, [0.0f, true])!));
        Assert.Equal(0.0f, Assert.IsType<float>(method.Invoke(null, [0.5f, true])!));
        Assert.Equal(1.0f, Assert.IsType<float>(method.Invoke(null, [1.0f, true])!));
        // Non-OpenGL keeps [0, 1] depth.
        Assert.Equal(0.0f, Assert.IsType<float>(method.Invoke(null, [0.0f, false])!));
        Assert.Equal(0.5f, Assert.IsType<float>(method.Invoke(null, [0.5f, false])!));
        Assert.Equal(1.0f, Assert.IsType<float>(method.Invoke(null, [1.0f, false])!));
    }

    [Fact]
    public void Render_buffer_manager_clears_depth_without_binding_color_target()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        manager.ClearDepthStencil(_fixture.Graphics, manager.GetDepthStencilOutput(), ClearTarget.Depth | ClearTarget.Stencil, 0.5f, 7);

        // C++ ClearDepthStencil binds depth only via RenderContext (no color RTs).
        var renderContext = _fixture.RenderDevice.GetRenderContext();
        var outputDesc = renderContext.GetCurrentRenderTargetsDesc();
        Assert.Equal(0u, outputDesc.NumRenderTargets);
        Assert.NotEqual(TextureFormat.Unknown, outputDesc.DepthStencilFormat);
    }

    [Fact]
    public void Render_buffer_manager_uses_fullscreen_quad_for_partial_output_clears()
    {
        var method = typeof(RenderBufferManager).GetMethod("ShouldUseFullscreenQuadClear", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.False(Assert.IsType<bool>(method!.Invoke(null, [new IntRect(0, 0, 320, 200), new IntRect(0, 0, 320, 200)])!));
        Assert.True(Assert.IsType<bool>(method.Invoke(null, [new IntRect(16, 12, 160, 120), new IntRect(0, 0, 320, 200)])!));
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
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.Optimized
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var renderTarget = new Texture2D("Textures/ViewportTarget");
        Assert.True(renderTarget.SetSize(320, 200, TextureFormat.RGBA16F));
        renderTarget.SetFilterMode(TextureFilterMode.Bilinear);
        renderTarget.SetLinear(true);

        manager.OnViewportDefined(RenderOutputTargetInfo.FromTexture(renderTarget, new IntRect(0, 0, 320, 200)), new IntRect(0, 0, 320, 200));

        Assert.Equal(TextureFormat.RGBA16F, manager.GetOutputColorFormat());
        Assert.Equal(1, manager.GetOutputMultiSample());
        Assert.True(manager.IsLinearColorSpace());
        Assert.True(manager.IsHDR());
        var colorTexture = manager.GetColorOutput().GetTexture();
        var secondaryColorTexture = manager.GetSecondaryColorTexture();
        Assert.NotNull(colorTexture);
        Assert.NotNull(secondaryColorTexture);
        Assert.Equal(TextureFormat.RGBA16F, colorTexture!.Format);
        Assert.Equal(TextureFormat.RGBA16F, secondaryColorTexture!.Format);
    }

    [Fact]
    public void Render_output_target_info_derives_multisample_from_texture()
    {
        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8, 4));
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8, 4));

        var output = RenderOutputTargetInfo.FromTexture(colorTarget, new IntRect(0, 0, 320, 200), depthTarget);

        Assert.Equal(4, output.MultiSample);
        Assert.Equal(4, output.ColorTarget.GetMultiSample());
        Assert.Equal(4, output.DepthStencilTarget?.GetMultiSample());
    }

    [Fact]
    public void Render_buffer_manager_refreshes_managed_output_buffers_with_requested_multisample()
    {
        var manager = new RenderBufferManager();
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
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        Assert.Equal(4, manager.GetOutputMultiSample());
        Assert.Equal(4, manager.GetColorOutput().GetTexture()!.MultiSample);
        Assert.Equal(4, manager.GetDepthStencilTexture()!.MultiSample);
    }

    [Fact]
    public void Texture_render_buffer_propagates_no_auto_resolve_flag_into_managed_texture()
    {
        var buffer = new TextureRenderBuffer(new RenderBufferParams(
            TextureFormat.RGBA8,
            4,
            RenderBufferFlag.NoMultiSampledAutoResolve));

        buffer.RefreshForViewport(new IntRect(0, 0, 320, 200));

        Assert.Equal(4, buffer.Texture.MultiSample);
        Assert.True(buffer.Texture.NoMultiSampledAutoResolve);
    }

    [Fact]
    public void Texture_render_buffer_creates_depth_stencil_texture_for_all_depth_formats()
    {
        foreach (var format in new[]
                 {
                     TextureFormat.Depth16,
                     TextureFormat.Depth32,
                     TextureFormat.Depth24Stencil8,
                     TextureFormat.Depth32Stencil8
                 })
        {
            var buffer = new TextureRenderBuffer(new RenderBufferParams(format));

            buffer.RefreshForViewport(new IntRect(0, 0, 320, 200));

            var flags = buffer.Texture.GetRawTexture()!.GetParams().Flags;
            Assert.True((flags & TextureFlag.BindDepthStencil) != 0);
            Assert.True((flags & TextureFlag.BindRenderTarget) == 0);
        }
    }

    [Fact]
    public void Render_buffer_manager_respects_color_space_over_output_texture_format()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.GammaLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var renderTarget = new Texture2D("Textures/ViewportTarget");
        Assert.True(renderTarget.SetSize(320, 200, TextureFormat.RGBA16F));
        renderTarget.SetFilterMode(TextureFilterMode.Bilinear);
        renderTarget.SetLinear(true);
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        manager.OnViewportDefined(
            RenderOutputTargetInfo.FromTexture(renderTarget, new IntRect(0, 0, 320, 200), depthTarget),
            new IntRect(0, 0, 320, 200));

        Assert.Equal(TextureFormat.RGBA8, manager.GetOutputColorFormat());
        Assert.False(manager.IsLinearColorSpace());
        Assert.False(manager.IsHDR());
        Assert.NotSame(renderTarget, manager.GetColorOutput().GetTexture());
        Assert.Equal(TextureFormat.RGBA8, manager.GetColorOutput().GetTexture()!.Format);
        Assert.Equal(TextureFormat.RGBA8, manager.GetSecondaryColorTexture()!.Format);
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_output_target_info_treats_srgb_texture_as_linear_color_space_output()
    {
        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        colorTarget.SetSRGB(true);

        var output = RenderOutputTargetInfo.FromTexture(colorTarget, new IntRect(0, 0, 320, 200));

        Assert.True(output.LinearColorSpace);
        Assert.False(output.HDR);
    }

    [Fact]
    public void Render_output_target_info_treats_rg16f_texture_as_linear_color_space_output()
    {
        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RG16F));

        var output = RenderOutputTargetInfo.FromTexture(colorTarget, new IntRect(0, 0, 320, 200));

        Assert.True(output.LinearColorSpace);
        Assert.True(output.HDR);
    }

    [Fact]
    public void Render_buffer_manager_allocates_linear_ldr_intermediate_buffer_for_non_srgb_texture_output()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        manager.OnViewportDefined(
            RenderOutputTargetInfo.FromTexture(colorTarget, new IntRect(0, 0, 320, 200), depthTarget),
            new IntRect(0, 0, 320, 200));

        Assert.NotSame(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.True(manager.GetColorOutput().GetTexture()!.GetSRGB());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_srgb_texture_output_for_linear_ldr()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        colorTarget.SetSRGB(true);
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        manager.OnViewportDefined(
            RenderOutputTargetInfo.FromTexture(colorTarget, new IntRect(0, 0, 320, 200), depthTarget),
            new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.True(manager.IsLinearColorSpace());
        Assert.False(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_marks_linear_ldr_color_buffers_as_srgb()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.LinearLDR
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        var primaryTexture = manager.GetColorOutput().GetTexture();
        var secondaryTexture = manager.GetSecondaryColorTexture();

        Assert.NotNull(primaryTexture);
        Assert.NotNull(secondaryTexture);
        Assert.True(manager.IsLinearColorSpace());
        Assert.False(manager.IsHDR());
        Assert.True(primaryTexture!.GetSRGB());
        Assert.False(primaryTexture.GetLinear());
        Assert.True(secondaryTexture!.GetSRGB());
        Assert.False(secondaryTexture.GetLinear());
    }

    [Fact]
    public void Render_buffer_manager_treats_optimized_rg16f_output_as_linear_hdr()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ColorSpace = RenderPipelineColorSpace.Optimized
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var renderTarget = new Texture2D("Textures/ViewportTarget");
        Assert.True(renderTarget.SetSize(320, 200, TextureFormat.RG16F));
        renderTarget.SetFilterMode(TextureFilterMode.Bilinear);

        manager.OnViewportDefined(RenderOutputTargetInfo.FromTexture(renderTarget, new IntRect(0, 0, 320, 200)), new IntRect(0, 0, 320, 200));

        Assert.Equal(TextureFormat.RG16F, manager.GetOutputColorFormat());
        Assert.True(manager.IsLinearColorSpace());
        Assert.True(manager.IsHDR());
        Assert.True(manager.GetColorOutput().GetTexture()!.GetLinear());
        Assert.True(manager.GetSecondaryColorTexture()!.GetLinear());
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_texture_output_without_substitute_buffers()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        var output = RenderOutputTargetInfo.FromTexture(
            colorTarget,
            new IntRect(0, 0, 320, 200),
            depthTarget);
        manager.OnViewportDefined(output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.Same(depthTarget, manager.GetDepthStencilTexture());
        Assert.False(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_texture_output_for_frame_readable_depth_requests()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableDepth = true
        });

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        var output = RenderOutputTargetInfo.FromTexture(
            colorTarget,
            new IntRect(0, 0, 320, 200),
            depthTarget);
        manager.OnViewportDefined(output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.Same(depthTarget, manager.GetDepthStencilTexture());
        Assert.False(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_uses_matching_texture_output_as_primary_for_color_read_write_frames()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        var output = RenderOutputTargetInfo.FromTexture(
            colorTarget,
            new IntRect(0, 0, 320, 200),
            depthTarget);
        manager.OnViewportDefined(output, new IntRect(0, 0, 320, 200));

        Assert.Same(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.NotNull(manager.GetSecondaryColorTexture());
        Assert.NotSame(colorTarget, manager.GetSecondaryColorTexture());
        Assert.Same(depthTarget, manager.GetDepthStencilTexture());
        Assert.False(manager.NeedsViewportResolve());

        manager.SwapColorBuffers(synchronizeContents: false);

        Assert.NotSame(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.Same(colorTarget, manager.GetSecondaryColorTexture());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_allocates_substitute_buffers_for_partial_texture_output()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings
        {
            ReadableDepth = true
        });
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        var output = RenderOutputTargetInfo.FromTexture(
            colorTarget,
            new IntRect(0, 0, 320, 200),
            depthTarget);
        manager.OnViewportDefined(output, new IntRect(0, 0, 160, 200));

        Assert.NotSame(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.NotSame(depthTarget, manager.GetDepthStencilTexture());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Render_buffer_manager_allocates_substitute_depth_for_partial_frame_readable_depth_output()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            ReadableDepth = true
        });

        var colorTarget = new Texture2D("Textures/ViewportColor");
        Assert.True(colorTarget.SetSize(320, 200, TextureFormat.RGBA8));
        var depthTarget = new Texture2D("Textures/ViewportDepth");
        Assert.True(depthTarget.SetSize(320, 200, TextureFormat.Depth24Stencil8));

        var output = RenderOutputTargetInfo.FromTexture(
            colorTarget,
            new IntRect(0, 0, 320, 200),
            depthTarget);
        manager.OnViewportDefined(output, new IntRect(0, 0, 160, 200));

        Assert.NotSame(colorTarget, manager.GetColorOutput().GetTexture());
        Assert.NotSame(depthTarget, manager.GetDepthStencilTexture());
        Assert.True(manager.NeedsViewportResolve());
    }

    [Fact]
    public void Bloom_pass_allocates_scaled_intermediate_buffers()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var bloom = new BloomPass("Bloom");
        bloom.UpdateParameters(new RenderPipelineSettings(), new Dictionary<string, object?>());

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

        Assert.Equal((128, 64), (first.Texture.Width, first.Texture.Height));
        Assert.Equal((64, 32), (second.Texture.Width, second.Texture.Height));
        Assert.Equal((32, 16), (third.Texture.Width, third.Texture.Height));
    }

    [Fact]
    public void Render_buffer_manager_resizes_dynamic_intermediate_buffers_with_viewport_changes()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var scaled = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RGBA8), new Vector2f(0.5f, 0.5f));
        var fixedSize = manager.CreateColorBuffer(
            new RenderBufferParams(TextureFormat.RGBA8, 1, RenderBufferFlag.FixedTextureSize),
            new Vector2f(12, 6));

        Assert.Equal((64, 32), (scaled.Texture.Width, scaled.Texture.Height));
        Assert.Equal((12, 6), (fixedSize.Texture.Width, fixedSize.Texture.Height));

        manager.OnViewportDefined(null, new IntRect(0, 0, 256, 128));

        Assert.Equal((128, 64), (scaled.Texture.Width, scaled.Texture.Height));
        Assert.Equal(new IntRect(0, 0, 128, 64), scaled.GetViewportRect());
        Assert.Equal((12, 6), (fixedSize.Texture.Width, fixedSize.Texture.Height));
        Assert.Equal(new IntRect(0, 0, 12, 6), fixedSize.GetViewportRect());
    }

    [Fact]
    public void Bloom_pass_cached_buffers_follow_viewport_resize()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var bloom = new BloomPass("Bloom");
        bloom.UpdateParameters(new RenderPipelineSettings(), new Dictionary<string, object?>());
        var sharedState = new SharedRenderPassState
        {
            RenderBufferManager = manager
        };
        bloom.Update(sharedState);

        var textures = Assert.IsAssignableFrom<IEnumerable>(GetPrivateField(bloom, "_textures"));
        var caches = textures.Cast<object>().ToArray();
        var first = Assert.IsType<TextureRenderBuffer>(GetPublicProperty(caches[0], "Final"));
        var second = Assert.IsType<TextureRenderBuffer>(GetPublicProperty(caches[1], "Final"));

        Assert.Equal((128, 64), (first.Texture.Width, first.Texture.Height));
        Assert.Equal((64, 32), (second.Texture.Width, second.Texture.Height));

        manager.OnViewportDefined(null, new IntRect(0, 0, 256, 128));

        Assert.Equal((256, 128), (first.Texture.Width, first.Texture.Height));
        Assert.Equal((128, 64), (second.Texture.Width, second.Texture.Height));
    }

    [Fact]
    public void Render_buffer_manager_creates_cubemap_render_buffers_with_face_slice_views()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings());
        manager.OnViewportDefined(null, new IntRect(0, 0, 128, 64));

        var cubemap = manager.CreateColorBuffer(
            new RenderBufferParams(TextureFormat.RGBA8, 1, RenderBufferFlag.CubeMap | RenderBufferFlag.FixedTextureSize),
            new Vector2f(32, 32));

        var cubeTexture = Assert.IsType<TextureCube>(cubemap.Texture);
        Assert.Equal((32, 32), (cubeTexture.Width, cubeTexture.Height));
        Assert.Equal(new IntRect(0, 0, 32, 32), cubemap.GetViewportRect());
        Assert.Equal(new RenderTargetView(cubeTexture, RenderTargetViewType.ResourceSlice, 0u), cubemap.GetView());
        Assert.Equal(new RenderTargetView(cubeTexture, RenderTargetViewType.ResourceSlice, 5u), cubemap.GetView(5u));
    }

    [Fact]
    public void Render_pipeline_uses_default_renderpath_on_attach()
    {
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

            _fixture.Resources.ReleaseResource<RenderPath>(RenderPipeline.DefaultRenderPathName);

            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();

            Assert.NotNull(renderPipeline.GetRenderPath());
            Assert.Equal(RenderPipeline.DefaultRenderPathName, renderPipeline.GetRenderPath()!.Name);
            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
        }
        finally
        {
            Context.SetInstance(previousContext);
            _fixture.Resources.ReleaseResource<RenderPath>(RenderPipeline.DefaultRenderPathName);
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

            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
            Assert.DoesNotContain("BloomStrength", renderPipeline.GetRenderPathParameters().Keys);

            WriteRenderPath(renderPathFile, 2.5f, includeBloom: true);
            Assert.True(_fixture.Resources.ReloadResource<RenderPath>(renderPathName));

            Assert.Equal(1, renderPathChangedEvents);
            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
            Assert.Equal(0.8f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["BloomStrength"]));
        }
        finally
        {
            _fixture.Resources.ReleaseResource<RenderPath>(renderPathName);
            _fixture.Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class ParameterRenderPass : RenderPass
    {
        private readonly string _parameterName;
        private readonly object? _value;

        public ParameterRenderPass(string parameterName, object? value)
            : base(parameterName)
        {
            this._parameterName = parameterName;
            this._value = value;
        }

        public override void CollectParameters(IDictionary<string, object?> parameters)
        {
            DeclareParameter(_parameterName, _value, parameters);
        }

        public override RenderPass Clone() => new ParameterRenderPass(_parameterName, _value);
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
            : base(passName)
        {
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

    private sealed class BufferCaptureRenderPass : RenderPass
    {
        private readonly StringHash _bufferId;
        private readonly State _state;

        public BufferCaptureRenderPass(string passName, StringHash bufferId)
            : this(passName, bufferId, new State())
        {
        }

        private BufferCaptureRenderPass(string passName, StringHash bufferId, State state)
            : base(passName)
        {
            this._bufferId = bufferId;
            this._state = state;
        }

        public bool WasBufferPresent => _state.WasBufferPresent;

        public TextureFormat CapturedFormat => _state.CapturedFormat;

        public Vector2i CapturedSize => _state.CapturedSize;

        public override void Render(SharedRenderPassState sharedState)
        {
            _state.WasBufferPresent = sharedState.RenderBuffers.TryGetValue(_bufferId, out var renderBuffer);
            if (!_state.WasBufferPresent || renderBuffer is null || renderBuffer.GetTexture() is not Texture texture)
                return;

            _state.CapturedFormat = texture.Format;
            _state.CapturedSize = new Vector2i(texture.Width, texture.Height);
        }

        public override RenderPass Clone() => new BufferCaptureRenderPass(GetPassName(), _bufferId, _state);

        private sealed class State
        {
            public bool WasBufferPresent;
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
