using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RendererTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RendererTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private Renderer Renderer => _fixture.Renderer;

    [Fact]
    public void Renderer_types_match_cpp_value_and_reference_categories()
    {
        Assert.False(typeof(Renderer).IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Renderer)));
        Assert.True(typeof(FrameStatistics).IsValueType);
        Assert.True(typeof(CommonFrameInfo).IsValueType);
        Assert.Equal(9, (int)LightVsVariation.MaxLightVsVariations);
        Assert.Equal(5, (int)VertexLightVsVariation.MaxVertexLightVsVariations);
        Assert.Equal(16, (int)LightPsVariation.MaxLightPsVariations);
        Assert.Equal(4, (int)DeferredLightVsVariation.MaxDeferredLightVsVariations);
        Assert.Equal(48, (int)DeferredLightPsVariation.MaxDeferredLightPsVariations);
    }

    [Fact]
    public void Viewport_registration_works()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));

        Renderer.SetNumViewports(2u);
        Renderer.SetViewport(1u, viewport);
        Renderer.SetViewport(0u, viewport);

        Assert.Equal(2u, Renderer.GetNumViewports());
        Assert.Equal(0u, Renderer.GetNumViews());
        Assert.Same(viewport, Renderer.GetViewportForScene(scene, 1u));
    }

    [Fact]
    public void Main_viewport_camera_mouse_position_is_normalized_at_input_end()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(100, 50, 500, 250));
        var texture = new Texture2D { Name = "Textures/MousePositionTarget" };
        Assert.True(texture.SetSize(640, 480, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var renderSurface = new RenderSurface(texture, 0);
        var mousePositionProperty = typeof(Input).GetProperty(nameof(Input.MousePosition));
        Assert.NotNull(mousePositionProperty);
        var oldMousePosition = _fixture.Input.MousePosition;
        mousePositionProperty!.SetValue(_fixture.Input, new Vector2i(300, 150));
        try
        {
            Renderer.SetNumViewports(1);
            Renderer.SetViewport(0, viewport);
            Renderer.SetBackbufferRenderSurface(renderSurface);

            var method = typeof(Renderer).GetMethod("UpdateMousePositionsForMainViewports",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(Renderer, null);

            Assert.InRange(Math.Abs(camera.MousePosition.X - 200.0f / 399.0f), 0.0f, 0.00001f);
            Assert.InRange(Math.Abs(camera.MousePosition.Y - 100.0f / 199.0f), 0.0f, 0.00001f);
        }
        finally
        {
            Renderer.SetBackbufferRenderSurface(null);
            mousePositionProperty.SetValue(_fixture.Input, oldMousePosition);
        }
    }

    [Fact]
    public void Render_surface_viewport_queue_deduplicates_pairs_and_accepts_backbuffer_entries()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var texture = new Texture2D { Name = "Textures/QueueTarget" };
        Assert.True(texture.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var renderSurface = new RenderSurface(texture, 0);

        Renderer.QueueViewport(renderSurface, viewport);
        Renderer.QueueViewport(renderSurface, viewport);
        Renderer.QueueViewport(null, null);

        var field = typeof(Renderer).GetField("_queuedViewports", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var queuedViewports = Assert.IsAssignableFrom<System.Collections.ICollection>(field!.GetValue(Renderer));
        Assert.Equal(1, queuedViewports.Count);

        Assert.IsAssignableFrom<System.Collections.IList>(queuedViewports).Clear();
    }

    [Fact]
    public void Backbuffer_render_surface_replacement_clears_previous_viewports()
    {
        var firstTexture = new Texture2D { Name = "Textures/FirstBackbuffer" };
        var firstSurface = new RenderSurface(firstTexture, 0);
        firstSurface.SetNumViewports(1);

        Renderer.SetBackbufferRenderSurface(firstSurface);
        Renderer.SetBackbufferRenderSurface(null);

        Assert.Equal(0, firstSurface.GetNumViewports());
    }

    [Fact]
    public void Update_skips_backbuffer_viewport_assignment_when_graphics_is_uninitialized()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var texture = new Texture2D { Name = "Textures/BackbufferViewports" };
        Assert.True(texture.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        var renderSurface = new RenderSurface(texture, 0);

        Renderer.SetNumViewports(1u);
        Renderer.SetViewport(0u, viewport);
        Renderer.SetBackbufferRenderSurface(renderSurface);

        Renderer.Update(0.0f);

        Assert.Null(renderSurface.GetViewport(0));
        renderSurface.SetNumViewports(0);
        Renderer.SetBackbufferRenderSurface(null);
        Renderer.SetNumViewports(0);
    }

    [Fact]
    public void Update_skips_pipeline_allocation_when_graphics_is_uninitialized()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));

        Renderer.SetViewport(0u, viewport);

        Renderer.Update(0.0f);

        Assert.Null(scene.GetComponent<RenderPipeline>());
        Assert.Null(viewport.GetRenderPipelineView());
        Renderer.SetNumViewports(0);
    }

    [Fact]
    public void Headless_render_target_texture_does_not_queue_surface_on_render_surface_update_event()
    {
        var texture = new Texture2D { Name = "Textures/EventTarget" };
        Assert.True(texture.SetSize(320, 200, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget));
        Assert.Null(texture.GetRenderSurface());

        _fixture.Context.SendEvent<GraphicsEvents.RenderSurfaceUpdateEvent>();

        var field = typeof(Renderer).GetField("_queuedViewports", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var queuedViewports = Assert.IsAssignableFrom<System.Collections.ICollection>(field!.GetValue(Renderer));
        Assert.Equal(0, queuedViewports.Count);

        Assert.IsAssignableFrom<System.Collections.IList>(queuedViewports).Clear();
    }

    [Fact]
    public void Texture_quality_settings_roundtrip()
    {
        Renderer.SetTextureAnisotropy(0);
        Renderer.SetTextureFilterMode(TextureFilterMode.Trilinear);
        Renderer.SetTextureQuality(MaterialQuality.Medium);

        Assert.Equal(1, Renderer.GetTextureAnisotropy());
        Assert.Equal(TextureFilterMode.Trilinear, Renderer.GetTextureFilterMode());
        Assert.Equal(MaterialQuality.Medium, Renderer.GetTextureQuality());

        Renderer.SetTextureQuality((MaterialQuality)uint.MaxValue);
        Assert.Equal(MaterialQuality.High, Renderer.GetTextureQuality());

        Renderer.SetTextureQuality((MaterialQuality)uint.MinValue);
        Assert.Equal(MaterialQuality.Low, Renderer.GetTextureQuality());

        Renderer.SetTextureQuality(MaterialQuality.High);
    }

    [Fact]
    public void Uninitialized_renderer_does_not_subscribe_to_end_frame()
    {
        ref var statistics = ref Renderer.GetMutableFrameStats();
        statistics.Animations = 4;
        statistics.ChangedAnimations = 2;
        var initialized = Renderer.GetQuadGeometry() is not null;

        _fixture.Context.SendEvent<CoreEvents.EndFrameEvent>();

        Assert.Equal(initialized ? 0u : 4u, statistics.Animations);
        Assert.Equal(initialized ? 0u : 2u, statistics.ChangedAnimations);
    }

    [Fact]
    public void Skinning_mode_helpers_work()
    {
        Renderer.SetSkinningMode(SkinningMode.Hardware);
        Renderer.SetNumSoftwareSkinningBones(24);

        Assert.True(Renderer.GetUseHardwareSkinning());
        Assert.Equal(SkinningMode.Hardware, Renderer.GetSkinningMode());
        Assert.Equal(24u, Renderer.GetNumSoftwareSkinningBones());
    }

    [Fact]
    public void Default_zone_is_available()
    {
        var defaultZone = Renderer.GetDefaultZone();

        Assert.NotNull(defaultZone);
        Assert.Same(defaultZone, Renderer.GetDefaultZone());
    }

    [Fact]
    public void Default_technique_can_be_overridden()
    {
        var technique = new Technique { Name = "Techniques/RendererDefault" };

        Renderer.SetDefaultTechnique(technique);

        Assert.Same(technique, Renderer.GetDefaultTechnique());
    }

    [Fact]
    public void Default_material_and_black_cube_map_follow_renderer_initialization()
    {
        var material = Renderer.GetDefaultMaterial();
        var blackCubeMap = Renderer.GetBlackCubeMap();

        Assert.Same(material, Renderer.GetDefaultMaterial());
        Assert.Same(blackCubeMap, Renderer.GetBlackCubeMap());
        if (Renderer.GetQuadGeometry() is null)
        {
            Assert.Null(material);
            Assert.Null(blackCubeMap);
        }
        else
        {
            Assert.NotNull(material);
            Assert.NotNull(blackCubeMap);
            Assert.Equal(new Vector2i(1, 1), blackCubeMap.GetSize());
            Assert.Equal(TextureFilterMode.Nearest, blackCubeMap.GetFilterMode());
            var data = new byte[4];
            Assert.False(blackCubeMap.GetData(CubeMapFace.PositiveX, 0, data));
        }
    }

    [Fact]
    public void Frame_statistics_expose_mutable_renderer_counters()
    {
        ref var stats = ref Renderer.GetMutableFrameStats();
        stats.Animations = 3;
        stats.ChangedAnimations = 2;

        ref readonly var readOnlyStats = ref Renderer.GetFrameStats();
        Assert.Equal(stats, readOnlyStats);
        Assert.Equal(3u, Renderer.GetFrameStats().Animations);
        Assert.Equal(2u, Renderer.GetFrameStats().ChangedAnimations);

        var copy = readOnlyStats;
        copy.Animations = 10;
        Assert.Equal(3u, Renderer.GetFrameStats().Animations);
    }

    [Fact]
    public void Frame_info_exposes_read_only_value_state()
    {
        ref readonly var rendererFrameInfo = ref Renderer.GetFrameInfo();
        var frameInfo = rendererFrameInfo;
        frameInfo.FrameNumber = rendererFrameInfo.FrameNumber + 1;
        frameInfo.TimeStep = rendererFrameInfo.TimeStep + 0.25f;

        Assert.NotEqual(frameInfo.FrameNumber, Renderer.GetFrameInfo().FrameNumber);
        Assert.NotEqual(frameInfo.TimeStep, Renderer.GetFrameInfo().TimeStep);
    }

    [Fact]
    public void Common_frame_info_copy_has_independent_camera_slots()
    {
        var scene = new Scene();
        var first = scene.CreateChild("First").CreateComponent<Camera>();
        var second = scene.CreateChild("Second").CreateComponent<Camera>();
        var replacement = scene.CreateChild("Replacement").CreateComponent<Camera>();
        var frameInfo = new CommonFrameInfo { Cameras = (first, second) };

        var copy = frameInfo;
        copy.Cameras.First = replacement;

        Assert.Same(first, frameInfo.Cameras.First);
        Assert.Same(second, frameInfo.Cameras.Second);
        Assert.Same(replacement, copy.Cameras.First);
    }

    [Fact]
    public void Frame_info_types_match_native_pod_shapes()
    {
        Assert.True(typeof(CommonFrameInfo).IsValueType);
        Assert.True(typeof(FrameInfo).IsValueType);
        Assert.Null(typeof(CommonFrameInfo).GetField("Scene"));
        Assert.Null(typeof(CommonFrameInfo).GetField("Camera"));
        Assert.Null(typeof(CommonFrameInfo).GetField("Octree"));
        Assert.NotNull(typeof(FrameInfo).GetField("Scene"));
        Assert.NotNull(typeof(FrameInfo).GetField("Camera"));
        Assert.NotNull(typeof(FrameInfo).GetField("Octree"));
        Assert.NotNull(typeof(FrameInfo).GetField("ReflectionProbeManager"));
    }

    [Fact]
    public void Uninitialized_renderer_does_not_create_light_geometries()
    {
        var directionalLight = new Light();
        directionalLight.SetLightType(LightType.Directional);
        var spotLight = new Light();
        spotLight.SetLightType(LightType.Spot);
        var pointLight = new Light();
        pointLight.SetLightType(LightType.Point);

        if (Renderer.GetQuadGeometry() is not null)
        {
            Assert.Same(Renderer.GetQuadGeometry(), Renderer.GetLightGeometry(directionalLight));
            Assert.Equal(6u, Renderer.GetLightGeometry(directionalLight)!.GetIndexCount());
            Assert.Equal(36u, Renderer.GetLightGeometry(spotLight)!.GetIndexCount());
            Assert.Equal(132u, Renderer.GetLightGeometry(pointLight)!.GetIndexCount());
        }
        else
        {
            Assert.Null(Renderer.GetQuadGeometry());
            Assert.Null(Renderer.GetLightGeometry(directionalLight));
            Assert.Null(Renderer.GetLightGeometry(spotLight));
            Assert.Null(Renderer.GetLightGeometry(pointLight));
        }
    }

    [Fact]
    public void Set_viewport_accepts_null_and_preserves_viewport_slot()
    {
        Renderer.SetViewport(2u, null);

        Assert.Equal(3u, Renderer.GetNumViewports());
        Assert.Null(Renderer.GetViewport(2));
    }

    [Fact]
    public void Get_viewport_for_null_scene_returns_null()
    {
        Assert.Null(Renderer.GetViewportForScene(null));
    }
}

[Collection(nameof(GraphicsEngineCollection))]
public sealed class GraphicsRendererTests
{
    private readonly GraphicsEngineFixture _fixture;

    public GraphicsRendererTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Update_dispatches_render_surface_update_event_from_renderer()
    {
        using var receiver = new TestEventReceiver();
        RedObject? eventSender = null;
        receiver.SubscribeToEvent<GraphicsEvents.RenderSurfaceUpdateEvent>(
            (ref GraphicsEvents.RenderSurfaceUpdateEvent _) => eventSender = receiver.GetEventSender());

        _fixture.Renderer.Update(0.0f);

        Assert.Same(_fixture.Renderer, eventSender);
    }

    [Fact]
    public void Render_dispatches_end_all_views_event()
    {
        using var receiver = new TestEventReceiver();
        var eventCount = 0;
        RedObject? eventSender = null;
        receiver.SubscribeToEvent<GraphicsEvents.EndAllViewsRenderEvent>(
            (ref GraphicsEvents.EndAllViewsRenderEvent _) =>
            {
                ++eventCount;
                eventSender = receiver.GetEventSender();
            });

        _fixture.Renderer.Render();

        Assert.Equal(1, eventCount);
        Assert.Same(_fixture.Renderer, eventSender);
    }
}
