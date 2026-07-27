using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

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
        using var outlineGroup = new OutlineGroup();
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
    public void Outline_group_copies_only_shader_parameters_and_textures()
    {
        var referenceMaterial = Resources.Get<Material>("Materials/Mushroom.xml");
        using var outlineGroup = new OutlineGroup();
        var outlineMaterial = outlineGroup.GetOutlineMaterial(referenceMaterial);

        Assert.Single(outlineMaterial.GetTechniques());
        Assert.NotSame(referenceMaterial.GetTechnique(0), outlineMaterial.GetTechnique(0));
        Assert.Equal(referenceMaterial.GetShaderParameter("MatDiffColor"), outlineMaterial.GetShaderParameter("MatDiffColor"));
        Assert.Same(referenceMaterial.GetTexture("Albedo"), outlineMaterial.GetTexture("Albedo"));
    }

    [Fact]
    public void Outline_group_uses_pod_material_key_values()
    {
        var referenceMaterial = Resources.Get<Material>("Materials/FirstCube");
        using var equivalentMaterial = referenceMaterial.Clone();
        using var outlineGroup = new OutlineGroup();

        var outlineMaterial = outlineGroup.GetOutlineMaterial(referenceMaterial);
        equivalentMaterial.SetShaderParameter("MatDiffColor", Color.Black);
        Assert.Same(outlineMaterial, outlineGroup.GetOutlineMaterial(equivalentMaterial));

        equivalentMaterial.SetShaderParameter("Roughness", 0.123f);
        Assert.NotSame(outlineMaterial, outlineGroup.GetOutlineMaterial(equivalentMaterial));
    }

    [Fact]
    public void Outline_group_tracks_drawables()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Drawable");
        var drawable = node.CreateComponent<StaticModel>();
        var outlineGroup = scene.CreateComponent<OutlineGroup>();

        Assert.False(outlineGroup.HasDrawables());
        Assert.True(outlineGroup.AddDrawable(drawable));
        Assert.True(outlineGroup.HasDrawable(drawable));
        Assert.True(outlineGroup.ContainsDrawable(drawable));
        Assert.True(outlineGroup.RemoveDrawable(drawable));
        Assert.False(outlineGroup.HasDrawables());
    }

    [Fact]
    public void Outline_group_resolves_drawable_attributes()
    {
        var scene = new SceneType();
        var node = scene.CreateChild("Drawable");
        var drawable = node.CreateComponent<StaticModel>();
        var outlineGroup = scene.CreateChild("Outline").CreateComponent<OutlineGroup>();

        using var drawablesAttr = new VariantVector { drawable.Id };
        outlineGroup.SetDrawablesAttr(drawablesAttr);
        drawablesAttr.Dispose();
        Assert.False(outlineGroup.HasDrawables());

        outlineGroup.ApplyAttributes();

        Assert.True(outlineGroup.HasDrawable(drawable));
        var serializedDrawables = outlineGroup.GetDrawablesAttr();
        Assert.Equal(drawable.Id, Assert.Single(serializedDrawables).GetUInt());

        outlineGroup.SetDrawablesAttr(serializedDrawables);
        Assert.False(outlineGroup.HasDrawables());
        outlineGroup.ApplyAttributes();
        Assert.True(outlineGroup.HasDrawable(drawable));
    }
}
