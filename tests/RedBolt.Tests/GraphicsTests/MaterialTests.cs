using RedBolt;
using System.Reflection;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class MaterialTests
{
    private readonly HeadlessEngineFixture _fixture;

    public MaterialTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Xml_texture_resource_type_matches_definition()
    {
        var material = Resources.Get<Material>("Materials/DefaultSkybox.xml");
        Assert.IsType<TextureCube>(material.GetTexture("Albedo"));
    }

    [Fact]
    public void Render_state_helpers_roundtrip()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var originalHash = material.GetPipelineStateHash();
        material.SetCullMode(CullMode.Clockwise);
        material.SetShadowCullMode(CullMode.None);
        material.SetFillMode(FillMode.Wireframe);

        Assert.Equal(CullMode.Clockwise, material.GetCullMode());
        Assert.Equal(CullMode.None, material.GetShadowCullMode());
        Assert.Equal(FillMode.Wireframe, material.GetFillMode());
        Assert.NotEqual(originalHash, material.GetPipelineStateHash());
        Assert.Equal(material.GetPipelineStateHash(), material.GetPipelineStateHash());
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
    public void Material_auxiliary_scene_specular_and_shader_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetShaderParameter("MatSpecColor", new Vector4f(1.0f, 0.0f, 0.0f, 1.0f));
        Assert.True(material.GetSpecular());

        material.SetShaderParameter("MatSpecColor", Vector4f.Zero);
        Assert.False(material.GetSpecular());

        material.MarkForAuxView(17u);
        Assert.Equal(17u, material.GetAuxViewFrameNumber());

        var scene = new Scene();
        material.SetScene(scene);
        Assert.Same(scene, material.GetScene());
        material.SetScene(null);
        Assert.Null(material.GetScene());

        material.ReleaseShaders();
    }

    [Fact]
    public void Shader_parameter_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetShaderParameter("CustomValue", Material.ParseShaderParameterValue("1 2 3"), isCustom: true);

        var value = material.GetShaderParameter("CustomValue");
        Assert.IsType<Vector3f>(value);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), (Vector3f)value);
        Assert.True(material.ShaderParameters["CustomValue"].IsCustom);
    }

    [Fact]
    public void Shader_parameter_animation_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var animation = new ValueAnimation { Name = "Animations/MaterialParameter" };
        animation.ValueType = VariantType.Float;
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 1.0f);

        material.SetShaderParameterAnimation("Roughness", animation, WrapMode.Once, 2.0f);

        Assert.Same(animation, material.GetShaderParameterAnimation("Roughness"));
        Assert.Equal(WrapMode.Once, material.GetShaderParameterAnimationWrapMode("Roughness"));
        Assert.Equal(2.0f, material.GetShaderParameterAnimationSpeed("Roughness"));

        var update = new CoreEvents.UpdateEvent { TimeStep = 0.25f };
        Context.Instance.SendEvent(ref update);
        Assert.Equal(0.5f, material.GetShaderParameter("Roughness"));

        material.SetShaderParameterAnimation("Roughness", null);
        Assert.Null(material.GetShaderParameterAnimation("Roughness"));
    }

    [Fact]
    public void Uv_transform_sets_shader_parameters()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetUVTransform(new Vector2f(0.25f, 0.5f), 90.0f, new Vector2f(2.0f, 4.0f));

        Assert.True(Assert.IsType<Vector4f>(material.GetShaderParameter("UOffset"))
            .ApproximatelyEquivalent(new Vector4f(0.0f, -4.0f, 2.75f, 0.0f)));
        Assert.True(Assert.IsType<Vector4f>(material.GetShaderParameter("VOffset"))
            .ApproximatelyEquivalent(new Vector4f(2.0f, 0.0f, 0.0f, 0.0f)));
    }

    [Fact]
    public void Texture_slot_helpers_work()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var albedo = new Texture2D { Name = "Textures/Albedo" };
        albedo.SetSize(2, 2, TextureFormat.TexFormatRgba8Unorm);
        var reflection = new TextureCube { Name = "Textures/Reflection" };
        reflection.SetSize(2, TextureFormat.TexFormatRgba8Unorm);

        material.SetTexture("Albedo", albedo);
        material.SetTexture("Reflection1", reflection);

        Assert.Same(albedo, material.GetTexture("Albedo"));
        Assert.Same(reflection, material.GetTexture(new StringHash("Reflection1")));
        Assert.Equal(2, material.GetTextures().Count);

        var clone = material.Clone("Materials/TextureClone");
        Assert.Same(albedo, clone.GetTexture("Albedo"));

        material.SetTexture("Albedo", null);
        Assert.Null(material.GetTexture("Albedo"));

        material.SetTexture("diffuse", albedo);
        Assert.Same(albedo, material.GetTexture("diffuse"));
        Assert.Null(material.GetTexture("Albedo"));
    }

    [Fact]
    public void Texture_reload_event_invalidates_only_the_current_texture()
    {
        var material = new Material { Name = "Materials/Reload" };
        var firstTexture = new Texture2D { Name = "Textures/First" };
        var secondTexture = new Texture2D { Name = "Textures/Second" };
        material.SetTexture("Albedo", firstTexture);
        material.SetTexture("Albedo", secondTexture);
        material.GetPipelineStateHash();

        var tracker = typeof(Material).GetField("_pipelineStateTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(material)!;
        var hashField = typeof(PipelineStateTracker).GetField("_pipelineStateHash", BindingFlags.Instance | BindingFlags.NonPublic)!;

        firstTexture.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.NotEqual(0u, (uint)hashField.GetValue(tracker)!);

        secondTexture.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(0u, (uint)hashField.GetValue(tracker)!);
    }

    [Fact]
    public void Technique_entry_management_works()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var t0 = new Technique { Name = "Techniques/Smoke" };
        t0.CreatePass("alpha");
        var t1 = new Technique { Name = "Techniques/Xml" };
        t1.CreatePass("base");

        material.SetNumTechniques(2);
        material.SetTechnique(0, t0, MaterialQuality.Low, 0.0f);
        material.SetTechnique(1, t1, MaterialQuality.High, 25.0f);

        Assert.Equal(2u, material.GetNumTechniques());
        Assert.Equal(0.0f, material.GetTechniqueEntry(0).LodDistance);
        Assert.Equal(MaterialQuality.Low, material.GetTechniqueEntry(0).QualityLevel);
        Assert.NotNull(material.GetTechnique(0)?.GetPass("alpha"));
        Assert.NotNull(material.GetTechnique(1)?.GetPass("base"));

        material.SetTechniques(material.GetTechniques());
        Assert.Equal(25.0f, material.GetTechniqueEntry(0).LodDistance);

        var originalVariant = new TechniqueEntry(t0, MaterialQuality.Low, 1.0f) { Original = t0 };
        var changedOriginal = originalVariant;
        changedOriginal.Original = t1;
        Assert.Equal(originalVariant, changedOriginal);
    }

    [Fact]
    public void Shader_define_application_works()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var technique = new Technique { Name = "Techniques/Smoke" };
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
    public void Shader_parameter_values_are_independent()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var source = new byte[] { 1, 2, 3 };
        material.SetShaderParameter("Buffer", source);
        source[0] = 9;

        var stored = Assert.IsType<byte[]>(material.GetShaderParameter("Buffer"));
        Assert.Equal(new byte[] { 1, 2, 3 }, stored);
        stored[1] = 9;
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(material.GetShaderParameter("Buffer")));
    }

    [Fact]
    public void Shader_parameter_variant_is_a_borrowed_view()
    {
        using var source = new CString("value");
        var parameter = new MaterialShaderParameter("Value", source);
        ref readonly var borrowedValue = ref parameter.VariantValue;

        parameter.Dispose();

        Assert.True(borrowedValue.IsEmpty);
    }

    [Fact]
    public void Clone_uses_requested_name_and_independent_parameter_values()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        material.SetShaderParameter("Buffer", new byte[] { 1, 2, 3 });
        var clone = material.Clone();

        Assert.Equal(string.Empty, clone.Name);
        var cloneValue = Assert.IsType<byte[]>(clone.GetShaderParameter("Buffer"));
        cloneValue[0] = 9;
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(material.GetShaderParameter("Buffer")));
    }
}
