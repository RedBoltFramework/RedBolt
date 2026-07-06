using System.Collections;
using System.Reflection;

using RedBolt.Bindings.Diligent;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineTests
{
    private readonly HeadlessEngineFixture fixture;

    public RenderPipelineTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
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
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        var trackingPass = new TrackingRenderPass("Tracking");
        renderPath.AddPass(trackingPass);
        renderPipeline.SetRenderPath(renderPath);

        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var view = renderPipeline.Instantiate();

        Assert.True(view.Define(viewport, fixture.Graphics));

        view.Update(fixture.Graphics);
        view.Render(fixture.Graphics, MaterialQuality.High);

        Assert.Equal(1, trackingPass.UpdateCalls);
        Assert.Equal(1, trackingPass.RenderCalls);
        Assert.Equal(new IntVector2(320, 200), view.GetFrameInfo().ViewportSize);
        Assert.Equal(0u, view.GetStats().NumGeometries);
    }

    [Fact]
    public void Render_pipeline_publishes_normal_buffer_when_ssao_is_enabled()
    {
        RbfxTestData.AddResourceDirs(fixture.Resources);
        try
        {
            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            var renderPath = new RenderPath("RenderPaths/Test.xml");
            var capturePass = new BufferCaptureRenderPass("Capture", SharedRenderPassState.NormalBufferId);
            renderPath.AddPass(capturePass);
            renderPath.AddPass(new AmbientOcclusionPass("AmbientOcclusion"));
            renderPipeline.SetRenderPath(renderPath);

            var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
            var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
            var view = renderPipeline.Instantiate();

            Assert.True(view.Define(viewport, fixture.Graphics));

            view.Update(fixture.Graphics);
            view.Render(fixture.Graphics, MaterialQuality.High);

            Assert.True(capturePass.WasBufferPresent);
            Assert.Equal(TextureFormat.RGBA8, capturePass.CapturedFormat);
            Assert.Equal(new IntVector2(320, 200), capturePass.CapturedSize);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(fixture.Resources);
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
        Assert.Equal(new IntVector2(640, 360), manager.GetOutputSize());
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
        Assert.Equal(TextureFormat.Depth24Stencil8, manager.GetDepthStencilOutput().GetTexture()!.Format);
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
        Assert.Equal(TextureFormat.Depth24Stencil8, manager.GetDepthStencilOutput().GetTexture()!.Format);
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
        Assert.Equal(TextureFormat.Depth24Stencil8, manager.GetDepthStencilTexture()!.Format);
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
        var method = typeof(RenderBufferManager).GetMethod("GetFullscreenQuadClearDepth", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(-1.0f, Assert.IsType<float>(method!.Invoke(null, [0.0f])!));
        Assert.Equal(0.0f, Assert.IsType<float>(method.Invoke(null, [0.5f])!));
        Assert.Equal(1.0f, Assert.IsType<float>(method.Invoke(null, [1.0f])!));
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

        manager.ClearDepthStencil(fixture.Graphics, manager.GetDepthStencilOutput(), ClearTarget.Depth | ClearTarget.Stencil, 0.5f, 7);

        var currentRenderTargetCountField = typeof(Graphics).GetField("currentRenderTargetCount", BindingFlags.NonPublic | BindingFlags.Instance);
        var renderTargetsField = typeof(Graphics).GetField("renderTargets", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(currentRenderTargetCountField);
        Assert.NotNull(renderTargetsField);
        Assert.Equal(0, Assert.IsType<int>(currentRenderTargetCountField!.GetValue(fixture.Graphics)));
        Assert.Null(Assert.IsType<ITextureView?[]>(renderTargetsField!.GetValue(fixture.Graphics))[0]);
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
        Assert.True(manager.IsLinearColorSpace());
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

        var textures = Assert.IsAssignableFrom<IEnumerable>(GetPrivateField(bloom, "textures"));
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

        var scaled = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RGBA8), new Vector2(0.5f, 0.5f));
        var fixedSize = manager.CreateColorBuffer(
            new RenderBufferParams(TextureFormat.RGBA8, 1, RenderBufferFlag.FixedTextureSize),
            new Vector2(12, 6));

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

        var textures = Assert.IsAssignableFrom<IEnumerable>(GetPrivateField(bloom, "textures"));
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
    public void Render_pipeline_uses_default_renderpath_on_attach()
    {
        var previousContext = Context.TryGetInstance();
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPipelineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        fixture.Resources.AddResourceDir(tempRoot);

        try
        {
            Context.SetInstance(fixture.Context);
            File.WriteAllText(Path.Combine(tempRoot, "RenderPaths", "Default.renderpath"), """
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

            fixture.Resources.ReleaseResource<RenderPath>(RenderPipeline.DefaultRenderPathName);

            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();

            Assert.NotNull(renderPipeline.GetRenderPath());
            Assert.Equal(RenderPipeline.DefaultRenderPathName, renderPipeline.GetRenderPath()!.Name);
            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
        }
        finally
        {
            Context.SetInstance(previousContext);
            fixture.Resources.ReleaseResource<RenderPath>(RenderPipeline.DefaultRenderPathName);
            fixture.Resources.RemoveResourceDir(tempRoot);
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
        fixture.Resources.AddResourceDir(tempRoot);

        try
        {
            var renderPathFile = Path.Combine(tempRoot, renderPathName);
            WriteRenderPath(renderPathFile, 1.25f, includeBloom: false);
            fixture.Resources.ReleaseResource<RenderPath>(renderPathName);

            var scene = new Scene();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            renderPipeline.SetRenderPath(fixture.Resources.GetResource<RenderPath>(renderPathName));

            var renderPathChangedEvents = 0;
            renderPipeline.RenderPathChanged += (_, _) => ++renderPathChangedEvents;

            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
            Assert.DoesNotContain("BloomStrength", renderPipeline.GetRenderPathParameters().Keys);

            WriteRenderPath(renderPathFile, 2.5f, includeBloom: true);
            Assert.True(fixture.Resources.ReloadResource<RenderPath>(renderPathName));

            Assert.Equal(1, renderPathChangedEvents);
            Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
            Assert.Equal(0.8f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["BloomStrength"]));
        }
        finally
        {
            fixture.Resources.ReleaseResource<RenderPath>(renderPathName);
            fixture.Resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class ParameterRenderPass : RenderPass
    {
        private readonly string parameterName;
        private readonly object? value;

        public ParameterRenderPass(string parameterName, object? value)
            : base(parameterName)
        {
            this.parameterName = parameterName;
            this.value = value;
        }

        public override void CollectParameters(IDictionary<string, object?> parameters)
        {
            DeclareParameter(parameterName, value, parameters);
        }

        public override RenderPass Clone() => new ParameterRenderPass(parameterName, value);
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
        private readonly Counter counter;

        public TrackingRenderPass(string passName)
            : this(passName, new Counter())
        {
        }

        private TrackingRenderPass(string passName, Counter counter)
            : base(passName)
        {
            this.counter = counter;
        }

        public int UpdateCalls => counter.UpdateCalls;

        public int RenderCalls => counter.RenderCalls;

        public override void Update(SharedRenderPassState sharedState)
        {
            ++counter.UpdateCalls;
        }

        public override void Render(SharedRenderPassState sharedState)
        {
            ++counter.RenderCalls;
        }

        public override RenderPass Clone() => new TrackingRenderPass(GetPassName(), counter);

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
        private readonly StringHash bufferId;
        private readonly State state;

        public BufferCaptureRenderPass(string passName, StringHash bufferId)
            : this(passName, bufferId, new State())
        {
        }

        private BufferCaptureRenderPass(string passName, StringHash bufferId, State state)
            : base(passName)
        {
            this.bufferId = bufferId;
            this.state = state;
        }

        public bool WasBufferPresent => state.WasBufferPresent;

        public TextureFormat CapturedFormat => state.CapturedFormat;

        public IntVector2 CapturedSize => state.CapturedSize;

        public override void Render(SharedRenderPassState sharedState)
        {
            state.WasBufferPresent = sharedState.RenderBuffers.TryGetValue(bufferId, out var renderBuffer);
            if (!state.WasBufferPresent || renderBuffer is null || renderBuffer.GetTexture() is not Texture texture)
                return;

            state.CapturedFormat = texture.Format;
            state.CapturedSize = new IntVector2(texture.Width, texture.Height);
        }

        public override RenderPass Clone() => new BufferCaptureRenderPass(GetPassName(), bufferId, state);

        private sealed class State
        {
            public bool WasBufferPresent;
            public TextureFormat CapturedFormat;
            public IntVector2 CapturedSize;
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

        File.WriteAllText(fileName, $$"""
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
