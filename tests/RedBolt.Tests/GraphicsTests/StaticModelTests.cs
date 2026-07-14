using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class StaticModelTests
{
    private readonly HeadlessEngineFixture _fixture;

    public StaticModelTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

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
    public void Resource_attributes_roundtrip()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);
        cube.SetMaterial(material);

        var modelAttribute = cube.GetModelAttr();
        var materialsAttribute = cube.GetMaterialsAttr();
        Assert.Equal(StringHash.Calculate("Model"), modelAttribute.Type);
        Assert.Equal(model.Name, modelAttribute.Name);
        Assert.Equal(material.Name, materialsAttribute.Names[0]);

        cube.SetModelAttr(modelAttribute);
        cube.SetMaterialsAttr(materialsAttribute);
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
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetDrawDistance(), -1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetShadowDistance(), 50.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetLodBias(), MathDefs.Epsilon));
        Assert.Equal(0x11u, cube.GetViewMask());
        Assert.Equal(0x22u, cube.GetLightMask());
        Assert.Equal(0x33u, cube.GetShadowMask());
        Assert.Equal(0x44u, cube.GetZoneMask());
        Assert.True(cube.GetCastShadows());
        Assert.False(cube.IsOccluder());
        Assert.False(cube.IsOccludee());

        var pipelineStateHash = cube.GetPipelineStateHash();
        cube.SetGlobalIlluminationType(GlobalIlluminationType.BlendLightProbes);
        Assert.NotEqual(pipelineStateHash, cube.GetPipelineStateHash());
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
        cube.SetLightmapScaleOffset(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f));
        Assert.True(cube.GetBakeLightmap());
        Assert.True(cube.GetBakeLightmapEffective());
        Assert.True(MathDefs.ApproximatelyEquivalent(cube.GetScaleInLightmap(), 2.0f));
        Assert.Equal(3u, cube.GetLightmapIndex());
        Assert.Equal(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f), cube.GetLightmapScaleOffset());
    }

    [Fact]
    public void Occlusion_lod_level_works()
    {
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetOcclusionLodLevel(2u);
        Assert.Equal(2u, cube.GetOcclusionLodLevel());
    }

    [Fact]
    public void Occluder_triangle_count_respects_geometry_and_material()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);
        var geometry = cube.GetLodGeometry(0u, uint.MaxValue)!;

        Assert.Equal(geometry.GetIndexCount() / 3u, cube.GetNumOccluderTriangles());

        var material = Resources.Get<Material>("Materials/FirstCube").Clone("Occluder");
        material.SetOcclusion(false);
        cube.SetMaterial(material);
        Assert.Equal(0u, cube.GetNumOccluderTriangles());
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
    public void Lod_geometry_falls_back_to_visible_geometry_when_level_is_out_of_range()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);

        Assert.Same(cube.GetLodGeometry(0u, uint.MaxValue), cube.GetLodGeometry(0u, 0u));
        Assert.Null(cube.GetLodGeometry(1u, 0u));
    }

    [Fact]
    public void Bounding_boxes_work()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);

        Assert.Equal(Intersection.Inside, model.GetBoundingBox().IsInside(Vector3f.Zero));

        cube.Node.SetTransform(new Vector3f(10.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3f(2.0f, 2.0f, 2.0f));
        Assert.Equal(Intersection.Inside, cube.GetWorldBoundingBox().IsInside(new Vector3f(10.5f, 0.0f, 0.0f)));
    }

    [Fact]
    public void IsInsideLocal_requires_geometry_inside_bounding_box()
    {
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f)));

        Assert.False(cube.IsInsideLocal(Vector3f.Zero));
    }

    [Fact]
    public void ApplyMaterialList_ignores_missing_resource()
    {
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();

        cube.ApplyMaterialList("MissingMaterials.txt");

        Assert.Empty(cube.GetMaterialsAttr().Names);
    }
}
