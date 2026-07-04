using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class StaticModelTests
{
    private readonly HeadlessEngineFixture fixture;

    public StaticModelTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Model_and_material_work()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);
        cube.SetMaterial(material);
        Assert.Same(model, cube.GetModel());
        Assert.Same(material, cube.GetMaterial());
    }

    [Fact]
    public void Drawable_distance_and_masks_work()
    {
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetDrawDistance(-1.0f);
        cube.SetShadowDistance(50.0f);
        cube.SetLodBias(0.0f);
        cube.SetViewMask(0x11u);
        cube.SetLightMask(0x22u);
        cube.SetShadowMask(0x33u);
        cube.SetZoneMask(0x44u);
        cube.SetCastShadows(true);
        cube.SetOccluder(false);
        cube.SetOccludee(false);

        Assert.Equal(DrawableFlags.Geometry, cube.GetDrawableFlags());
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetDrawDistance(), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetShadowDistance(), 50.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetLodBias(), MathDefs.Epsilon));
        Assert.Equal(0x11u, cube.GetViewMask());
        Assert.Equal(0x22u, cube.GetLightMask());
        Assert.Equal(0x33u, cube.GetShadowMask());
        Assert.Equal(0x44u, cube.GetZoneMask());
        Assert.True(cube.GetCastShadows());
        Assert.False(cube.IsOccluder());
        Assert.False(cube.IsOccludee());
    }

    [Fact]
    public void Lightmap_helpers_work()
    {
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetBakeLightmap(true);
        cube.SetScaleInLightmap(0.0f);
        Assert.False(cube.GetBakeLightmapEffective());

        cube.SetScaleInLightmap(2.0f);
        cube.SetLightmapIndex(3u);
        cube.SetLightmapScaleOffset(new Vector4(1.0f, 2.0f, 3.0f, 4.0f));
        Assert.True(cube.GetBakeLightmap());
        Assert.True(cube.GetBakeLightmapEffective());
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetScaleInLightmap(), 2.0f));
        Assert.Equal(3u, cube.GetLightmapIndex());
        Assert.Equal(new Vector4(1.0f, 2.0f, 3.0f, 4.0f), cube.GetLightmapScaleOffset());
    }

    [Fact]
    public void Occlusion_lod_level_works()
    {
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetOcclusionLodLevel(2u);
        Assert.Equal(2u, cube.GetOcclusionLodLevel());
    }

    [Fact]
    public void Model_geometry_collections_work()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        Assert.Equal(1u, model.GetNumGeometries());
        Assert.Single(model.GetGeometries());
        Assert.Single(model.GetGeometryCenters());
    }

    [Fact]
    public void Bounding_boxes_work()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);

        Assert.Equal(Intersection.Inside, model.GetBoundingBox().IsInside(Vector3.Zero));
        Assert.True(cube.IsInsideLocal(Vector3.Zero));

        cube.Node.SetTransform(new Vector3(10.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3(2.0f, 2.0f, 2.0f));
        Assert.Equal(Intersection.Inside, cube.GetWorldBoundingBox().IsInside(new Vector3(10.5f, 0.0f, 0.0f)));
        Assert.True(cube.IsInside(new Vector3(10.5f, 0.0f, 0.0f)));
    }
}
