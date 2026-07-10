namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class OutlineGroupTests
{
    private readonly HeadlessEngineFixture _fixture;

    public OutlineGroupTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Outline_group_caches_outline_material_and_updates_cached_state()
    {
        var referenceMaterial = Resources.Get<Material>("Materials/FirstCube");
        var outlineGroup = new OutlineGroup();
        outlineGroup.SetColor(new Color(0.2f, 0.4f, 0.6f, 1.0f));
        outlineGroup.SetRenderOrder(17u);

        var outlineMaterial = outlineGroup.GetOutlineMaterial(referenceMaterial);
        var cachedMaterial = outlineGroup.GetOutlineMaterial(referenceMaterial);

        Assert.Same(outlineMaterial, cachedMaterial);
        Assert.Equal(17u, outlineMaterial.GetRenderOrder());
        Assert.Equal(new Vector4f(0.2f, 0.4f, 0.6f, 1.0f), Assert.IsType<Vector4f>(outlineMaterial.GetShaderParameter("OutlineColor")));
        Assert.True(outlineMaterial.ShaderParameters["OutlineColor"].IsCustom);

        outlineGroup.SetColor(new Color(0.8f, 0.3f, 0.1f, 1.0f));
        outlineGroup.SetRenderOrder(23u);

        Assert.Equal(new Vector4f(0.8f, 0.3f, 0.1f, 1.0f), Assert.IsType<Vector4f>(outlineMaterial.GetShaderParameter("OutlineColor")));
        Assert.Equal(23u, outlineMaterial.GetRenderOrder());
    }

    [Fact]
    public void Outline_group_rewrites_outlineable_passes_to_outline_shader()
    {
        var referenceMaterial = Resources.Get<Material>("Materials/Mushroom.xml");
        var outlineMaterial = new OutlineGroup().GetOutlineMaterial(referenceMaterial);
        var technique = outlineMaterial.FindTechnique(MaterialQuality.High);
        var pass = technique?.GetPass("base") ?? technique?.GetPass("alpha");

        Assert.NotNull(pass);
        Assert.Equal("M_OutlinePixel", pass!.PixelShader);
        Assert.Equal(CompareMode.Always, pass.DepthTestMode);
        Assert.False(pass.DepthWrite);
        Assert.Equal(BlendMode.Replace, pass.BlendMode);
        Assert.Equal(CullMode.None, pass.CullMode);
        Assert.False(outlineMaterial.GetAlphaToCoverage());
        Assert.Equal(FillMode.Solid, outlineMaterial.GetFillMode());
    }

    [Fact]
    public void Outline_group_tracks_drawables()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Drawable");
        var drawable = node.CreateComponent<StaticModel>();
        var outlineGroup = scene.CreateComponent<OutlineGroup>();

        Assert.False(outlineGroup.HasDrawables());
        Assert.True(outlineGroup.AddDrawable(drawable));
        Assert.True(outlineGroup.HasDrawable(drawable));
        Assert.True(outlineGroup.ContainsDrawable(drawable));
        Assert.Single(outlineGroup.GetDrawables());
        Assert.True(outlineGroup.RemoveDrawable(drawable));
        Assert.False(outlineGroup.HasDrawables());
    }
}
