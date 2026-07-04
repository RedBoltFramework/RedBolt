using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class MaterialTests
{
    private readonly HeadlessEngineFixture fixture;

    public MaterialTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Render_state_helpers_roundtrip()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetCullMode(CullMode.Clockwise);
        material.SetShadowCullMode(CullMode.None);
        material.SetFillMode(FillMode.Wireframe);

        Assert.Equal(CullMode.Clockwise, material.GetCullMode());
        Assert.Equal(CullMode.None, material.GetShadowCullMode());
        Assert.Equal(FillMode.Wireframe, material.GetFillMode());
    }

    [Fact]
    public void Depth_and_render_order_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetDepthBias(new BiasParameters(1.0f, 2.0f));
        material.SetRenderOrder(42u);

        Assert.Equal(42u, material.GetRenderOrder());
        Assert.Equal(new BiasParameters(1.0f, 2.0f), material.GetDepthBias());
        Assert.NotEqual(0u, material.GetShaderParameterHash());
    }

    [Fact]
    public void Shader_parameter_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetShaderParameter("CustomValue", Material.ParseShaderParameterValue("1 2 3"), isCustom: true);

        var value = material.GetShaderParameter("CustomValue");
        Assert.IsType<Vector3>(value);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), (Vector3)value);
        Assert.True(material.ShaderParameters["CustomValue"].IsCustom);
    }

    [Fact]
    public void Uv_transform_metadata_works()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetUVTransform(new Vector2(0.25f, 0.5f), 90.0f, new Vector2(2.0f, 4.0f));

        Assert.True(material.UVOffset.ApproximatelyEquivalent(new Vector2(0.25f, 0.5f)));
        Assert.True(material.UVRepeat.ApproximatelyEquivalent(new Vector2(2.0f, 4.0f)));
    }

    [Fact]
    public void Texture_slot_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var albedo = new Texture2D("Textures/Albedo");
        albedo.SetSize(2, 2, TextureFormat.RGBA8);
        var reflection = new TextureCube("Textures/Reflection");
        reflection.SetSize(2, TextureFormat.RGBA8);

        material.SetTexture("diffuse", albedo);
        material.SetTexture("zone", reflection);

        Assert.Same(albedo, material.GetTexture("Albedo"));
        Assert.Same(reflection, material.GetTexture(new StringHash("Reflection1")));
        Assert.Equal(2, material.GetTextures().Count);

        var clone = material.Clone("Materials/TextureClone");
        Assert.Same(albedo, clone.GetTexture("Albedo"));

        material.SetTexture("diffuse", null);
        Assert.Null(material.GetTexture("Albedo"));
    }

    [Fact]
    public void Technique_entry_management_works()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var t0 = new Technique("Techniques/Smoke");
        t0.CreatePass("alpha");
        var t1 = new Technique("Techniques/Xml");
        t1.CreatePass("base");

        material.SetTechnique(0, t0, MaterialQuality.Low, 0.0f);
        material.SetTechnique(1, t1, MaterialQuality.High, 25.0f);

        Assert.Equal(2u, material.GetNumTechniques());
        Assert.Equal(25.0f, material.GetTechniqueEntry(0).LodDistance);
        Assert.Equal(MaterialQuality.High, material.GetTechniqueEntry(0).QualityLevel);
        Assert.NotNull(material.GetTechnique(0)?.GetPass("base"));
        Assert.NotNull(material.GetTechnique(1)?.GetPass("alpha"));
    }

    [Fact]
    public void Shader_define_application_works()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var technique = new Technique("Techniques/Smoke");
        technique.CreatePass("base");
        material.SetTechnique(0, technique);
        material.SetVertexShaderDefines("MATERIAL_VS");
        material.SetPixelShaderDefines("MATERIAL_PS");

        Assert.Contains("MATERIAL_VS",
            material.GetTechnique(0)?.GetPass("base")?.VertexShaderDefines ?? "", StringComparison.Ordinal);
        Assert.Contains("MATERIAL_PS",
            material.GetTechnique(0)?.GetPass("base")?.PixelShaderDefines ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceWithMetadata_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.AddMetadata("Category", "Smoke");
        Assert.True(material.HasMetadata());
        Assert.Equal("Smoke", material.GetMetadata("Category"));
        Assert.Contains("Category", material.GetMetadataKeys());

        var clone = material.Clone("Materials/Clone");
        Assert.Equal("Smoke", clone.GetMetadata("Category"));

        clone.RemoveMetadata("Category");
        Assert.False(clone.HasMetadata("Category"));
    }
}
