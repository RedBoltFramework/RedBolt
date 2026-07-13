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
}
