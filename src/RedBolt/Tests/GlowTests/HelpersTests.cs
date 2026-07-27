using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class HelpersTests
{
    [Fact]
    public void ParallelFor_processes_each_element_once()
    {
        var hits = new int[17];
        var callbackCount = 0;

        Helpers.ParallelFor(hits.Length, 4, (fromIndex, toIndex) =>
        {
            Interlocked.Increment(ref callbackCount);
            for (var i = fromIndex; i < toIndex; ++i)
                Interlocked.Increment(ref hits[i]);
        });

        Assert.Equal(4, callbackCount);
        Assert.All(hits, hit => Assert.Equal(1, hit));
    }

    [Fact]
    public void Material_helpers_match_material_state()
    {
        var material = new Material();
        material.SetShaderParameter("MatDiffColor", new Vector4f(0.25f, 0.5f, 0.75f, 1.0f));
        material.SetShaderParameter("UOffset", new Vector4f(1.0f, 2.0f, 3.0f, 4.0f));
        material.SetShaderParameter("VOffset", new Vector4f(5.0f, 6.0f, 7.0f, 8.0f));

        Assert.True(Helpers.IsMaterialOpaque(material));
        Assert.Equal(new Color(0.25f, 0.5f, 0.75f, 1.0f), Helpers.GetMaterialDiffuseColor(material));

        material.SetShaderParameter("MatDiffColor", new Vector4f(1.0f, 1.0f, 1.0f, 0.5f));
        Assert.False(Helpers.IsMaterialOpaque(material));

        material.SetShaderParameter("MatDiffColor", Vector4f.One);
        material.SetPixelShaderDefines("ALPHAMASK");
        Assert.False(Helpers.IsMaterialOpaque(material));
    }

    [Fact]
    public void Lightmap_helpers_support_static_models_and_terrain()
    {
        var scene = new SceneType();
        var staticModel = scene.CreateChild("StaticModel").CreateComponent<StaticModel>();
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        var scaleOffset = new Vector4f(0.5f, 0.25f, 0.125f, 0.0625f);

        Helpers.SetLightmapIndex(staticModel, 3);
        Helpers.SetLightmapScaleOffset(staticModel, scaleOffset);
        Assert.Equal(3, Helpers.GetLightmapIndex(staticModel));
        Assert.Equal(scaleOffset, Helpers.GetLightmapScaleOffset(staticModel));

        Helpers.SetLightmapIndex(terrain, 4);
        Helpers.SetLightmapScaleOffset(terrain, scaleOffset);
        Assert.Equal(4, Helpers.GetLightmapIndex(terrain));
        Assert.Equal(scaleOffset, Helpers.GetLightmapScaleOffset(terrain));
    }
}
