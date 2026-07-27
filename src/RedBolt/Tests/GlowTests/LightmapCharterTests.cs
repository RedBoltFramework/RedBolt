using RedBolt.Core;
using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LightmapCharterTests
{
    [Fact]
    public void Chart_region_reports_normalized_transform()
    {
        var region = new LightmapChartRegion(2, new Vector2i(4, 8), new Vector2i(16, 24), 64);

        Assert.Equal(2, region.ChartIndex);
        Assert.Equal(new IntRect(4, 8, 20, 32), region.RectTexels);
        Assert.Equal(new Vector2f(0.25f, 0.375f), region.GetScale());
        Assert.Equal(new Vector2f(0.0625f, 0.125f), region.GetOffset());
        Assert.Equal(new Vector4f(0.25f, 0.375f, 0.0625f, 0.125f), region.GetScaleOffset());
    }

    [Fact]
    public void Chart_clone_copies_allocator_and_elements()
    {
        var chart = new LightmapChart(3, 16);
        var x = 0;
        var y = 0;
        Assert.True(chart.Allocator.Allocate(4, 4, ref x, ref y));
        chart.Elements.Add(new LightmapChartElement { ObjectIndex = 1 });

        var clone = chart.Clone();
        var cloneX = 0;
        var cloneY = 0;
        var originalX = 0;
        var originalY = 0;
        Assert.True(clone.Allocator.Allocate(4, 4, ref cloneX, ref cloneY));
        Assert.True(chart.Allocator.Allocate(4, 4, ref originalX, ref originalY));
        clone.Elements.Clear();

        Assert.Equal(new Vector2i(originalX, originalY), new Vector2i(cloneX, cloneY));
        Assert.Single(chart.Elements);
    }

    [Fact]
    public void Generate_and_apply_charts_updates_scene_geometry()
    {
        var scene = new SceneType();
        scene.CreateComponent<Octree>();
        var model = new Model();
        using var lightmapSize = new Variant(new Vector2i(16, 8));
        using var lightmapDensity = new Variant(10.0f);
        model.AddMetadata(LightmapUVGenerationSettings.LightmapSizeKey, lightmapSize);
        model.AddMetadata(LightmapUVGenerationSettings.LightmapDensityKey, lightmapDensity);

        var first = scene.CreateChild("First").CreateComponent<StaticModel>();
        first.SetModel(model);
        var second = scene.CreateChild("Second").CreateComponent<StaticModel>();
        second.SetModel(model);
        Component?[] geometries = [first, second];
        var settings = new LightmapChartingSettings
        {
            LightmapSize = 32,
            Padding = 1,
            TexelDensity = 10.0f,
            MinObjectScale = 1.0f,
            DefaultChartSize = 16
        };

        var charts = LightmapCharter.GenerateLightmapCharts(geometries, settings, 5);

        var chart = Assert.Single(charts);
        Assert.Equal(5, chart.Index);
        Assert.Equal(32, chart.LightmapSize);
        Assert.Equal(2, chart.Elements.Count);
        Assert.Contains(chart.Elements, element => ReferenceEquals(element.Component, first) && element.ObjectIndex == 0);
        Assert.Contains(chart.Elements, element => ReferenceEquals(element.Component, second) && element.ObjectIndex == 1);
        Assert.All(chart.Elements, element => Assert.Equal(new Vector2i(16, 8), element.Region.RectTexels.Size));

        LightmapCharter.ApplyLightmapCharts(charts);

        foreach (var element in chart.Elements)
        {
            Assert.Equal(5, Helpers.GetLightmapIndex(element.Component!));
            Assert.Equal(element.Region.GetScaleOffset(), Helpers.GetLightmapScaleOffset(element.Component!));
        }
    }
}
