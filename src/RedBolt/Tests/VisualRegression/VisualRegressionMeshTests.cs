namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionMeshTests
{
    [VisualFact]
    public void Model_geometry_lod_selects_high_and_low_detail_at_distance()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.65f, 0.65f, 0.65f));
        var model = fixture.Resources.Get<Model>("Models/Mushroom.mdl");
        Assert.True(model.GetNumGeometryLodLevels(0) >= 2u);
        var material = fixture.Resources.Get<Material>("Materials/Mushroom.xml");
        var near = AddModel(scene, model, material,
            new Vector3f(-0.9f, 0.0f, 0.0f), 1.5f);
        var far = AddModel(scene, model, material,
            new Vector3f(1.0f, 0.0f, 2.5f), 1.5f);
        Assert.True(model.GetNumGeometryLodLevels(0) >= 2u);
        Assert.NotNull(near.GetLodGeometry(0, 0));
        Assert.NotNull(near.GetLodGeometry(0, 1));
        Assert.NotNull(far.GetLodGeometry(0, 0));
        Assert.NotNull(far.GetLodGeometry(0, 1));
        Assert.NotNull(near.GetBatches()[0].Geometry);
        Assert.NotNull(far.GetBatches()[0].Geometry);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.5f, -3.5f), new Vector3f(0.0f, 1.0f, 0.8f),
            256.0f / 128.0f, fov: 45.0f);

        fixture.AssertMatches(scene, camera, "mesh/model-lod.png");
    }

    [VisualFact]
    public void Model_lod_distance_and_bias_change_repeated_instances()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.65f, 0.65f, 0.65f));
        var model = fixture.Resources.Get<Model>("Models/Mushroom.mdl");
        var material = fixture.Resources.Get<Material>("Materials/Mushroom.xml");
        var normal = AddModel(scene, model, material, new Vector3f(-2.0f, 0.0f, 2.0f), 1.0f);
        var lowBias = AddModel(scene, model, material, new Vector3f(0.0f, 0.0f, 2.0f), 1.0f);
        lowBias.SetLodBias(0.45f);
        var highBias = AddModel(scene, model, material, new Vector3f(2.0f, 0.0f, 2.0f), 1.0f);
        highBias.SetLodBias(2.0f);
        _ = normal;
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -5.0f), new Vector3f(0.0f, 0.0f, 2.0f),
            256.0f / 128.0f, fov: 38.0f);

        fixture.AssertMatches(scene, camera, "mesh/model-lod-bias.png");
    }

    [VisualFact]
    public void Static_model_renders_multiple_geometries_with_independent_materials()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var mesh = CreateMultiGeometryModel();
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("MultiGeometry");
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(mesh.Model);
        model.SetMaterial(0, fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        model.SetMaterial(1, fixture.Resources.Get<Material>("Materials/FirstCube"));
        Assert.Equal(2u, model.GetNumGeometries());
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -6.0f), Vector3f.Zero, 256.0f / 128.0f,
            orthographic: true, orthoSize: 4.5f);

        fixture.AssertMatches(scene, camera, "mesh/multiple-geometries-materials.png");
    }

    [VisualFact]
    public void Static_model_masks_isolate_view_light_shadow_zone_and_occlusion_paths()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var secondaryZone = scene.CreateChild("SecondaryZone").CreateComponent<Zone>();
        secondaryZone.SetBoundingBox(new BoundingBox(new Vector3f(0.0f, -10.0f, -10.0f),
            new Vector3f(10.0f, 10.0f, 10.0f)));
        secondaryZone.SetAmbientColor(new Color(0.05f, 0.18f, 0.35f));
        secondaryZone.SetZoneMask(2u);
        var left = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml",
            new Vector3f(-1.2f, 0.0f, 0.0f), 0.9f);
        left.SetViewMask(1u);
        left.SetLightMask(1u);
        left.SetShadowMask(1u);
        left.SetZoneMask(1u);
        left.SetOccluder(true);
        left.SetOccludee(true);
        var right = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml",
            new Vector3f(1.2f, 0.0f, 0.0f), 0.9f);
        right.SetViewMask(2u);
        right.SetLightMask(2u);
        right.SetShadowMask(2u);
        right.SetZoneMask(2u);
        right.SetOccluder(false);
        right.SetOccludee(false);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Point, new Vector3f(-2.0f, 2.5f, -3.0f), Vector3f.Zero,
            Color.White, lightMask: 1u, range: 6.0f, brightness: 3.0f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Point, new Vector3f(2.0f, 2.5f, -3.0f), Vector3f.Zero,
            Color.White, lightMask: 2u, range: 6.0f, brightness: 3.0f);

        var normalCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -5.0f), Vector3f.Zero, 256.0f / 128.0f);
        var leftCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -5.0f), Vector3f.Zero, 256.0f / 128.0f);
        leftCamera.SetViewMask(1u);
        var rightCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -5.0f), Vector3f.Zero, 256.0f / 128.0f);
        rightCamera.SetViewMask(2u);

        fixture.AssertMatches(
            new VisualRegressionCapture(scene, normalCamera, "mesh/masks-normal.png"),
            new VisualRegressionCapture(scene, leftCamera, "mesh/masks-left.png"),
            new VisualRegressionCapture(scene, rightCamera, "mesh/masks-right.png"));
    }

    [VisualFact]
    public void Mesh_winding_and_material_culling_render_the_expected_faces()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var mesh = CreateWindingModel();
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("Winding");
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(mesh.Model);
        var clockwise = fixture.Resources.Get<Material>("Materials/FirstCube").Clone("Visual.CullClockwise");
        clockwise.SetCullMode(CullMode.Clockwise);
        var counterClockwise = fixture.Resources.Get<Material>("Materials/FirstCube").Clone("Visual.CullCounterClockwise");
        counterClockwise.SetCullMode(CullMode.CounterClockwise);
        var none = fixture.Resources.Get<Material>("Materials/FirstCube").Clone("Visual.CullNone");
        none.SetCullMode(CullMode.None);
        model.SetMaterial(0, clockwise);
        model.SetMaterial(1, counterClockwise);
        model.SetMaterial(2, none);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -6.0f), Vector3f.Zero, 256.0f / 128.0f,
            orthographic: true, orthoSize: 4.0f);

        fixture.AssertMatches(scene, camera, "mesh/winding-culling.png");
    }

    [VisualFact]
    public void Sixteen_and_thirty_two_bit_indices_render_identical_geometry()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var mesh = CreateIndexWidthModel();
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("IndexWidths");
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(mesh.Model);
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -6.0f), Vector3f.Zero, 256.0f / 128.0f,
            orthographic: true, orthoSize: 4.0f);

        fixture.AssertMatches(scene, camera, "mesh/index-widths.png");
    }

    [VisualFact]
    public void Geometry_draw_ranges_select_the_expected_index_and_lod_ranges()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var mesh = CreateDrawRangeModel();
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("DrawRanges");
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(mesh.Model);
        model.SetMaterial(0, fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        model.SetMaterial(1, fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -6.0f), Vector3f.Zero, 256.0f / 128.0f,
            orthographic: true, orthoSize: 4.0f);

        fixture.AssertMatches(scene, camera, "mesh/draw-ranges.png");
    }

    private static StaticModel AddModel(Scene scene, Model model, Material material, Vector3f position, float scale)
    {
        var node = scene.CreateChild("CustomModel");
        node.SetPosition(position);
        node.SetScale(scale);
        var staticModel = node.CreateComponent<StaticModel>();
        staticModel.SetModel(model);
        staticModel.SetMaterial(material);
        return staticModel;
    }

    private static MeshAsset CreateMultiGeometryModel()
    {
        var left = CreateGeometry(
            [new Vector3f(-1.8f, -1.0f, 0.0f), new Vector3f(-0.2f, 1.0f, 0.0f),
                new Vector3f(0.2f, -1.0f, 0.0f)], [0u, 1u, 2u], false, Color.Red);
        var right = CreateGeometry(
            [new Vector3f(-0.2f, -1.0f, 0.0f), new Vector3f(1.8f, 1.0f, 0.0f),
                new Vector3f(1.4f, -1.0f, 0.0f)], [0u, 1u, 2u], false, Color.Cyan);
        var model = CreateModel([left, right], [], new BoundingBox(-2.0f, 2.0f));
        return new MeshAsset(model, left, right);
    }

    private static MeshAsset CreateWindingModel()
    {
        var clockwise = CreateGeometry(
            [new Vector3f(-1.8f, -1.0f, 0.0f), new Vector3f(-1.0f, 1.0f, 0.0f),
                new Vector3f(-0.2f, -1.0f, 0.0f)], [0u, 1u, 2u], false, Color.Red);
        var counterClockwise = CreateGeometry(
            [new Vector3f(0.2f, -1.0f, 0.0f), new Vector3f(1.0f, 1.0f, 0.0f),
                new Vector3f(1.8f, -1.0f, 0.0f)], [0u, 2u, 1u], false, Color.Green);
        var doubleSided = CreateGeometry(
            [new Vector3f(-0.8f, -0.8f, 0.0f), new Vector3f(0.0f, 0.8f, 0.0f),
                new Vector3f(0.8f, -0.8f, 0.0f)], [0u, 2u, 1u], false, Color.Blue);
        var model = CreateModel([clockwise, counterClockwise, doubleSided], [], new BoundingBox(-2.0f, 2.0f));
        return new MeshAsset(model, clockwise, counterClockwise, doubleSided);
    }

    private static MeshAsset CreateIndexWidthModel()
    {
        var small = CreateGeometry(
            [new Vector3f(-1.8f, -1.0f, 0.0f), new Vector3f(-1.8f, 1.0f, 0.0f),
                new Vector3f(-0.2f, 1.0f, 0.0f), new Vector3f(-0.2f, -1.0f, 0.0f)],
            [0u, 1u, 2u, 2u, 3u, 0u], false, Color.Red);
        var large = CreateGeometry(
            [new Vector3f(0.2f, -1.0f, 0.0f), new Vector3f(0.2f, 1.0f, 0.0f),
                new Vector3f(1.8f, 1.0f, 0.0f), new Vector3f(1.8f, -1.0f, 0.0f)],
            [0u, 1u, 2u, 2u, 3u, 0u], true, Color.Blue);
        var model = CreateModel([small, large], [], new BoundingBox(-2.0f, 2.0f));
        return new MeshAsset(model, small, large);
    }

    private static MeshAsset CreateDrawRangeModel()
    {
        var vertices = new[]
        {
            new Vector3f(-1.8f, -1.0f, 0.0f), new Vector3f(-1.8f, 1.0f, 0.0f),
            new Vector3f(-0.2f, 1.0f, 0.0f), new Vector3f(-0.2f, -1.0f, 0.0f),
            new Vector3f(0.2f, -1.0f, 0.0f), new Vector3f(0.2f, 1.0f, 0.0f),
            new Vector3f(1.8f, 1.0f, 0.0f), new Vector3f(1.8f, -1.0f, 0.0f)
        };
        var indices = new uint[] { 0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4 };
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize((uint)vertices.Length, VertexMask.Position | VertexMask.Color);
        vertexBuffer.SetUnpackedData(PackVertices(vertices, Color.White));
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        indexBuffer.SetSize((uint)indices.Length, false);
        indexBuffer.SetUnpackedData(indices);
        var first = new Geometry();
        first.SetVertexBuffer(0, vertexBuffer);
        first.SetIndexBuffer(indexBuffer);
        first.SetDrawRange(PrimitiveType.TriangleList, 0, 6);
        var second = new Geometry();
        second.SetVertexBuffer(0, vertexBuffer);
        second.SetIndexBuffer(indexBuffer);
        second.SetDrawRange(PrimitiveType.TriangleList, 6, 6);
        var model = new Model { Name = "Visual.DrawRanges" };
        model.SetVertexBuffers([vertexBuffer]);
        model.SetIndexBuffers([indexBuffer]);
        model.SetNumGeometries(2);
        model.SetGeometry(0, 0, first);
        model.SetGeometry(1, 0, second);
        model.SetGeometryCenter(0, new Vector3f(-1.0f, 0.0f, 0.0f));
        model.SetGeometryCenter(1, new Vector3f(1.0f, 0.0f, 0.0f));
        model.SetBoundingBox(new BoundingBox(-2.0f, 2.0f));
        return new MeshAsset(model, vertexBuffer, indexBuffer, first, second);
    }

    private static Model CreateModel(
        IReadOnlyList<MeshPart> lod0,
        IReadOnlyList<MeshPart> lod1,
        BoundingBox boundingBox)
    {
        var model = new Model { Name = "Visual.CustomModel" };
        var parts = lod0.Count;
        model.SetNumGeometries((uint)parts);
        var vertexBuffers = lod0.Concat(lod1).Select(part => part.VertexBuffer).Distinct().ToArray();
        var indexBuffers = lod0.Concat(lod1).Select(part => part.IndexBuffer).Distinct().ToArray();
        model.SetVertexBuffers(vertexBuffers);
        model.SetIndexBuffers(indexBuffers);
        for (var i = 0; i < lod0.Count; ++i)
        {
            model.SetGeometry((uint)i, 0, lod0[i].Geometry);
            model.SetGeometryCenter((uint)i, Vector3f.Zero);
            if (i < lod1.Count)
            {
                model.SetNumGeometryLodLevels((uint)i, 2);
                model.SetGeometry((uint)i, 1, lod1[i].Geometry);
            }
        }
        model.SetBoundingBox(boundingBox);
        return model;
    }

    private static MeshPart CreateGeometry(
        Vector3f[] positions, uint[] indices, bool largeIndices, Color color)
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize((uint)positions.Length, VertexMask.Position | VertexMask.Color);
        vertexBuffer.SetUnpackedData(PackVertices(positions, color));
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        indexBuffer.SetSize((uint)indices.Length, largeIndices);
        indexBuffer.SetUnpackedData(indices);
        var geometry = new Geometry();
        geometry.SetVertexBuffer(0, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, (uint)indices.Length);
        return new MeshPart(vertexBuffer, indexBuffer, geometry);
    }

    private static Vector4f[] PackVertices(IReadOnlyList<Vector3f> positions, Color color)
    {
        var result = new Vector4f[positions.Count * 2];
        var colorVector = new Vector4f(color.R, color.G, color.B, color.A);
        for (var i = 0; i < positions.Count; ++i)
        {
            result[i * 2] = new Vector4f(positions[i], 1.0f);
            result[i * 2 + 1] = colorVector;
        }
        return result;
    }

    private sealed record MeshPart(VertexBuffer VertexBuffer, IndexBuffer IndexBuffer, Geometry Geometry) : IDisposable
    {
        public void Dispose()
        {
            Geometry.Dispose();
            IndexBuffer.Dispose();
            VertexBuffer.Dispose();
        }
    }

    private sealed class MeshAsset : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> _parts;

        public MeshAsset(Model model, params IDisposable[] parts)
        {
            Model = model;
            _parts = parts;
        }

        public Model Model { get; }

        public void Dispose()
        {
            Model.Dispose();
            for (var i = _parts.Count - 1; i >= 0; --i)
                _parts[i].Dispose();
        }
    }
}
