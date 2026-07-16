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
    public void Viewport_registration_works()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));

        Renderer.SetNumViewports(2u);
        Renderer.SetViewport(1u, viewport);
        Renderer.SetViewport(0u, viewport);

        Assert.Equal(2u, Renderer.GetNumViewports());
        Assert.Equal(2u, Renderer.GetNumViews());
        Assert.Same(viewport, Renderer.GetViewportForScene(scene, 1u));
    }

    [Fact]
    public void Render_surface_viewport_queue_deduplicates_pairs_and_accepts_backbuffer_entries()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var texture = new Texture2D("Textures/QueueTarget");
        Assert.True(texture.SetSize(320, 200, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
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
        var firstTexture = new Texture2D("Textures/FirstBackbuffer");
        var firstSurface = new RenderSurface(firstTexture, 0);
        firstSurface.SetNumViewports(1);

        Renderer.SetBackbufferRenderSurface(firstSurface);
        Renderer.SetBackbufferRenderSurface(null);

        Assert.Equal(0u, firstSurface.GetNumViewports());
    }

    [Fact]
    public void Backbuffer_render_surface_receives_registered_viewports_on_render()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var texture = new Texture2D("Textures/BackbufferViewports");
        Assert.True(texture.SetSize(320, 200, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        var renderSurface = new RenderSurface(texture, 0);

        Renderer.SetNumViewports(1u);
        Renderer.SetViewport(0u, viewport);
        Renderer.SetBackbufferRenderSurface(renderSurface);

        var renderFrame = typeof(Renderer).GetMethod("RenderFrame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(renderFrame);
        renderFrame!.Invoke(Renderer, [_fixture.Graphics, null]);

        Assert.Same(viewport, renderSurface.GetViewport(0));
        renderSurface.SetNumViewports(0);
        Renderer.SetBackbufferRenderSurface(null);
        Renderer.ClearViewports();
    }

    [Fact]
    public void Render_allocates_automatic_render_pipeline_for_viewport()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));

        Renderer.SetViewport(0u, viewport);

        var renderFrame = typeof(Renderer).GetMethod("RenderFrame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(renderFrame);
        renderFrame!.Invoke(Renderer, [_fixture.Graphics, null]);

        Assert.NotNull(scene.Root.GetComponent<RenderPipeline>());
        Assert.NotNull(viewport.GetRenderPipelineView());
        Renderer.ClearViewports();
    }

    [Fact]
    public void Render_dispatches_end_all_views_event()
    {
        var eventCount = 0;
        using var subscription = Renderer.SubscribeToEvent<GraphicsEvents.EndAllViewsRenderEvent>(
            (ref GraphicsEvents.EndAllViewsRenderEvent _) => ++eventCount);

        var renderFrame = typeof(Renderer).GetMethod("RenderFrame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(renderFrame);
        renderFrame!.Invoke(Renderer, [_fixture.Graphics, null]);

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Render_target_texture_queues_surface_on_render_surface_update_event()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var texture = new Texture2D("Textures/EventTarget");
        Assert.True(texture.SetSize(320, 200, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        var renderSurface = texture.GetRenderSurface()!;
        renderSurface.SetViewport(0, viewport);
        renderSurface.QueueUpdate();

        _fixture.Context.SendEvent<GraphicsEvents.RenderSurfaceUpdateEvent>();

        var field = typeof(Renderer).GetField("_queuedViewports", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var queuedViewports = Assert.IsAssignableFrom<System.Collections.ICollection>(field!.GetValue(Renderer));
        Assert.Equal(1, queuedViewports.Count);

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
        var technique = new Technique("Techniques/RendererDefault");

        Renderer.SetDefaultTechnique(technique);

        Assert.Same(technique, Renderer.GetDefaultTechnique());
    }

    [Fact]
    public void Default_material_and_black_cube_map_are_cached()
    {
        var material = Renderer.GetDefaultMaterial();
        var blackCubeMap = Renderer.GetBlackCubeMap();

        Assert.Same(material, Renderer.GetDefaultMaterial());
        Assert.Same(blackCubeMap, Renderer.GetBlackCubeMap());
        Assert.Equal(new Vector2i(1, 1), blackCubeMap.GetSize());
        Assert.Equal(TextureFilterMode.Nearest, blackCubeMap.GetFilterMode());
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, blackCubeMap.GetData(CubeMapFace.PositiveX, 0));
    }

    [Fact]
    public void Frame_statistics_expose_mutable_renderer_counters()
    {
        var stats = Renderer.GetMutableFrameStats();
        stats.Animations = 3;
        stats.ChangedAnimations = 2;

        Assert.Same(stats, Renderer.GetFrameStats());
        Assert.Equal(3u, Renderer.GetFrameStats().Animations);
        Assert.Equal(2u, Renderer.GetFrameStats().ChangedAnimations);
    }

    [Fact]
    public void Frame_info_exposes_mutable_renderer_frame_state()
    {
        var frameInfo = Renderer.GetFrameInfo();
        frameInfo.FrameNumber = 7;
        frameInfo.TimeStep = 0.25f;

        Assert.Same(frameInfo, Renderer.GetFrameInfo());
        Assert.Equal(7u, Renderer.GetFrameInfo().FrameNumber);
        Assert.Equal(0.25f, Renderer.GetFrameInfo().TimeStep);
    }

    [Fact]
    public void Light_and_quad_geometry_match_renderer_geometry_contract()
    {
        var directionalLight = new Light();
        directionalLight.SetLightType(LightType.Directional);
        var spotLight = new Light();
        spotLight.SetLightType(LightType.Spot);
        var pointLight = new Light();
        pointLight.SetLightType(LightType.Point);

        Assert.Same(Renderer.GetQuadGeometry(), Renderer.GetLightGeometry(directionalLight));
        Assert.Equal(6u, Renderer.GetLightGeometry(directionalLight)!.GetIndexCount());
        Assert.Equal(36u, Renderer.GetLightGeometry(spotLight)!.GetIndexCount());
        Assert.Equal(132u, Renderer.GetLightGeometry(pointLight)!.GetIndexCount());
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
