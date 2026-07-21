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
    public void Class_types_match_cpp()
    {
        Assert.True(typeof(StaticModelGeometryData).IsValueType);
        Assert.False(typeof(StaticModel).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(StaticModel)));
        Assert.True(typeof(StaticModel).GetMethod(nameof(StaticModel.SetMaterial), [typeof(Material)])!.IsVirtual);
        Assert.True(typeof(StaticModel).GetMethod(nameof(StaticModel.SetMaterial), [typeof(uint), typeof(Material)])!.IsVirtual);
        Assert.True(typeof(StaticModel).GetMethod(nameof(StaticModel.GetMaterial), Type.EmptyTypes)!.IsVirtual);
        Assert.True(typeof(StaticModel).GetMethod(nameof(StaticModel.GetMaterial), [typeof(uint)])!.IsVirtual);
    }

    [Fact]
    public void RegisterObject_registers_factory()
    {
        StaticModel.RegisterObject(Context.Instance);

        using var reflected = Assert.IsType<StaticModel>(
            Context.Instance.CreateObject(ObjectReflection<StaticModel>.TypeId));
    }

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
    public void Model_reload_event_reapplies_only_the_current_model()
    {
        var firstModel = new Model("First");
        var secondModel = new Model("Second");
        secondModel.SetBoundingBox(new BoundingBox(
            new Vector3f(-2.0f, -2.0f, -2.0f), new Vector3f(2.0f, 2.0f, 2.0f)));
        var staticModel = new Scene().CreateChild("StaticModel").CreateComponent<StaticModel>();
        staticModel.SetModel(firstModel);
        staticModel.SetModel(secondModel);

        firstModel.SetBoundingBox(new BoundingBox(
            new Vector3f(-3.0f, -3.0f, -3.0f), new Vector3f(3.0f, 3.0f, 3.0f)));
        firstModel.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(secondModel.GetBoundingBox(), staticModel.GetBoundingBox());

        secondModel.SetBoundingBox(new BoundingBox(
            new Vector3f(-4.0f, -4.0f, -4.0f), new Vector3f(4.0f, 4.0f, 4.0f)));
        secondModel.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(secondModel.GetBoundingBox(), staticModel.GetBoundingBox());
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
        Assert.Equal(new StringHash("Model"), modelAttribute.Type);
        Assert.Equal(model.Name, modelAttribute.Name);
        Assert.Equal(material.Name, materialsAttribute.Names[0]);

        cube.SetModelAttr(modelAttribute);
        cube.SetMaterialsAttr(materialsAttribute);
        Assert.Same(model, cube.GetModel());
        Assert.Same(material, cube.GetMaterial());
    }

    [Fact]
    public void Materials_attribute_reuses_cached_storage()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);
        cube.SetMaterial(material);

        var first = cube.GetMaterialsAttr();
        var second = cube.GetMaterialsAttr();

        Assert.Same(first.Names, second.Names);
        Assert.Equal(material.Name, second.Names[0]);
    }

    [Fact]
    public void Dispose_releases_owned_state_without_disposing_borrowed_model()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);
        _ = cube.GetMaterialsAttr();

        cube.Dispose();

        Assert.Null(cube.GetModel());
        Assert.Equal(0u, cube.GetNumGeometries());
        Assert.Empty(cube.GetMaterialsAttr().Names);
        Assert.Equal(1u, model.GetNumGeometries());
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
        Assert.True(MathDefs.Equals(cube.GetDrawDistance(), -1.0f));
        Assert.True(MathDefs.Equals(cube.GetShadowDistance(), 50.0f));
        Assert.True(MathDefs.Equals(cube.GetLodBias(), MathDefs.Epsilon));
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
        Assert.True(MathDefs.Equals(cube.GetScaleInLightmap(), 2.0f));
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
    public void DrawOcclusion_submits_indexed_geometry_and_respects_material()
    {
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(CreateOcclusionModel());
        var buffer = new OcclusionBuffer();
        Assert.True(buffer.SetSize(16, 16, false));
        buffer.SetMaxTriangles(0);

        Assert.False(cube.DrawOcclusion(buffer));

        var material = Resources.Get<Material>("Materials/FirstCube").Clone("NonOccluder");
        material.SetOcclusion(false);
        cube.SetMaterial(material);
        buffer.Reset();
        Assert.True(cube.DrawOcclusion(buffer));
        Assert.Equal(0u, buffer.GetNumTriangles());
    }

    private static Model CreateOcclusionModel()
    {
        var geometry = new Geometry();
        geometry.SetRawVertexData(new byte[36],
            [new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position)]);
        geometry.SetRawIndexData(new byte[6], sizeof(ushort));
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        Assert.True(indexBuffer.SetSize(3));
        indexBuffer.SetUnpackedData([0u, 1u, 2u]);
        geometry.SetIndexBuffer(indexBuffer);
        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 3, false));
        var model = new Model("Occlusion");
        model.SetNumGeometries(1);
        Assert.True(model.SetGeometry(0, 0, geometry));
        return model;
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
    public void New_model_geometry_has_initial_lod_level()
    {
        var model = new Model(string.Empty);
        var geometry = new Geometry();

        model.SetNumGeometries(1);

        Assert.Equal(1u, model.GetNumGeometryLodLevels(0));
        Assert.True(model.SetGeometry(0, 0, geometry));
        Assert.Same(geometry, model.GetGeometry(0));
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

        cube.Node.SetTransform(new Vector3f(10.0f, 0.0f, 0.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 2.0f, 2.0f));
        Assert.Equal(Intersection.Inside, cube.GetWorldBoundingBox().IsInside(new Vector3f(10.5f, 0.0f, 0.0f)));
    }

    [Fact]
    public void Custom_ray_query_uses_supplied_world_transform()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var cube = new Scene().CreateChild("Cube").CreateComponent<StaticModel>();
        cube.SetModel(model);
        var transform = Matrix3x4.FromTranslation(new Vector3f(5.0f, 0.0f, 0.0f));
        var worldBoundingBox = model.GetBoundingBox().Transformed(transform);
        using var query = new RayOctreeQuery(new Ray(new Vector3f(5.0f, 0.0f, -5.0f), Vector3f.Forward),
            RayQueryLevel.RayObb);

        cube.ProcessCustomRayQuery(query, worldBoundingBox, transform, query.Result);

        var result = Assert.Single(query.Result);
        Assert.Same(cube, result.Drawable);
        Assert.Equal(uint.MaxValue, result.SubObject);
        Assert.True(result.Distance < query.MaxDistance);
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
