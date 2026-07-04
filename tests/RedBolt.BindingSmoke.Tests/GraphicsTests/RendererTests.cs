using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RendererTests
{
    private readonly HeadlessEngineFixture fixture;

    public RendererTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private Renderer Renderer => fixture.Renderer;

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
}
