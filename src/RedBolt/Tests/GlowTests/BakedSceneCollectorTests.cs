using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BakedSceneCollectorTests
{
    private readonly HeadlessEngineFixture _fixture;

    public BakedSceneCollectorTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Default_collector_locks_queries_and_unlocks_lightmapped_scene()
    {
        var scene = new SceneType();
        var octree = scene.CreateComponent<Octree>();
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4);

        var staticModel = scene.CreateChild("Geometry").CreateComponent<StaticModel>();
        staticModel.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        staticModel.SetBakeLightmap(true);
        staticModel.Node!.SetPosition(new Vector3f(2.0f, 3.0f, 4.0f));

        var bakedLight = scene.CreateChild("BakedLight").CreateComponent<Light>();
        bakedLight.SetLightMode(LightMode.Mixed);
        var realtimeLight = scene.CreateChild("RealtimeLight").CreateComponent<Light>();
        realtimeLight.SetLightMode(LightMode.Realtime);

        var fogColor = new Color(0.25f, 0.5f, 0.75f);
        var staticZone = scene.CreateChild("StaticZone").CreateComponent<Zone>();
        staticZone.SetBoundingBox(new BoundingBox(-100.0f, 100.0f));
        staticZone.SetBackgroundStatic(true);
        staticZone.SetFogColor(fogColor);
        staticZone.SetBackgroundBrightness(0.5f);

        var dynamicZone = scene.CreateChild("DynamicZone").CreateComponent<Zone>();
        dynamicZone.SetBoundingBox(new BoundingBox(-100.0f, 100.0f));

        octree.QueueUpdate(staticModel);
        octree.QueueUpdate(bakedLight);
        octree.QueueUpdate(realtimeLight);
        octree.QueueUpdate(staticZone);
        octree.QueueUpdate(dynamicZone);
        octree.Update(new FrameInfo());

        using var collector = new DefaultBakedSceneCollector();
        collector.LockScene(scene, new Vector3f(32.0f, 32.0f, 32.0f));

        var chunk = Assert.Single(collector.GetChunks());
        Assert.Same(staticModel, Assert.Single(collector.GetUniqueGeometries(chunk)));
        Assert.Equal(staticModel.GetWorldBoundingBox(), collector.GetChunkBoundingBox(chunk));
        Assert.Contains(staticModel, collector.GetGeometriesInBoundingBox(chunk, staticModel.GetWorldBoundingBox()));
        Assert.Same(bakedLight, Assert.Single(
            collector.GetLightsInBoundingBox(chunk, new BoundingBox(-100.0f, 100.0f))));

        var copiedGeometries = collector.GetUniqueGeometries(chunk);
        copiedGeometries.Clear();
        Assert.Same(staticModel, Assert.Single(collector.GetUniqueGeometries(chunk)));

        var backgrounds = Assert.IsAssignableFrom<IReadOnlyList<BakedSceneBackground>>(collector.GetBackgrounds());
        Assert.Equal(2, backgrounds.Count);
        var blackBackground = backgrounds[0];
        Assert.Null(blackBackground.Image);
        Assert.Equal(0.0f, blackBackground.Intensity);
        Assert.Equal(fogColor, backgrounds[1].Color);
        Assert.Equal(0.5f, backgrounds[1].Intensity);
        Assert.Equal(1U, collector.GetZoneBackground(chunk, staticZone));
        Assert.Equal(0U, collector.GetZoneBackground(chunk, dynamicZone));

        collector.UnlockScene();

        Assert.Empty(collector.GetChunks());
        Assert.Same(backgrounds, collector.GetBackgrounds());
    }
}
