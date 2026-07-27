using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class StaticModelGroupTests
{
    private readonly HeadlessEngineFixture _fixture;

    public StaticModelGroupTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Class_type_and_instance_storage_match_cpp()
    {
        Assert.False(typeof(StaticModelGroup).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(StaticModelGroup)));
        var instanceNodes = typeof(StaticModelGroup).GetField("_instanceNodes",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(typeof(List<WeakReference<Node>>), instanceNodes.FieldType);
        Assert.Equal(typeof(VariantVector), typeof(StaticModelGroup).GetMethod(nameof(StaticModelGroup.GetNodeIDsAttr))!.ReturnType);
    }

    [Fact]
    public void Constructor_initializes_node_ids_and_factory()
    {
        using var group = new StaticModelGroup();
        Assert.Equal(0u, Assert.Single(group.GetNodeIDsAttr()).GetUInt());

        using var reflected = Assert.IsType<StaticModelGroup>(
            Context.Instance.CreateObject(ObjectReflection<StaticModelGroup>.TypeId));
    }

    [Fact]
    public void Node_ids_attribute_returns_cached_container()
    {
        using var group = new StaticModelGroup();

        Assert.Same(group.GetNodeIDsAttr(), group.GetNodeIDsAttr());
    }

    [Fact]
    public void Dispose_removes_instance_listener_without_disposing_node()
    {
        var scene = new Scene();
        var instance = scene.CreateChild("Instance");
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        group.AddInstanceNode(instance);

        group.Dispose();

        Assert.DoesNotContain(group, instance.GetListeners());
        Assert.Equal("Instance", instance.Name);
    }

    [Fact]
    public void Instance_nodes_work()
    {
        var scene = new Scene();
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        group.SetModel(model);
        group.SetMaterial(material);
        var a = scene.CreateChild("A");
        a.SetPosition(new Vector3f(30.0f, 0.0f, 0.0f));
        var b = scene.CreateChild("B");
        b.SetPosition(new Vector3f(40.0f, 0.0f, 0.0f));

        group.AddInstanceNode(a);
        group.AddInstanceNode(b);

        Assert.Equal(2u, group.GetNumInstanceNodes());
        Assert.Same(b, group.GetInstanceNode(1u));
        Assert.Contains(group, a.GetListeners());
        Assert.Contains(group, b.GetListeners());
    }

    [Fact]
    public void NodeId_attribute_resolution_works()
    {
        var scene = new Scene();
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        group.SetModel(CreateOcclusionModel());
        group.SetMaterial(Resources.Get<Material>("Materials/FirstCube"));
        var a = scene.CreateChild("A");
        var b = scene.CreateChild("B");

        using var nodeIds = new VariantVector { 2u, a.Id, b.Id };
        group.SetNodeIDsAttr(nodeIds);
        group.ApplyAttributes();

        Assert.Equal(2u, group.GetNumInstanceNodes());
        Assert.Same(a, group.GetInstanceNode(0u));
        Assert.Same(b, group.GetInstanceNode(1u));
    }

    [Fact]
    public void Apply_attributes_removes_listeners_from_previous_instance_nodes()
    {
        var scene = new Scene();
        var first = scene.CreateChild("First");
        var second = scene.CreateChild("Second");
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();

        using var firstNodeIds = new VariantVector { 1u, first.Id };
        group.SetNodeIDsAttr(firstNodeIds);
        group.ApplyAttributes();
        Assert.Contains(group, first.GetListeners());

        using var secondNodeIds = new VariantVector { 1u, second.Id };
        group.SetNodeIDsAttr(secondNodeIds);
        group.ApplyAttributes();

        Assert.DoesNotContain(group, first.GetListeners());
        Assert.Contains(group, second.GetListeners());
    }

    [Fact]
    public void Remove_instance_node_works()
    {
        var scene = new Scene();
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        group.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        group.SetMaterial(Resources.Get<Material>("Materials/FirstCube"));
        var a = scene.CreateChild("A");
        var b = scene.CreateChild("B");
        group.AddInstanceNode(a);
        group.AddInstanceNode(b);

        group.RemoveInstanceNode(b);
        Assert.Equal(1u, group.GetNumInstanceNodes());
        var nodeIds = group.GetNodeIDsAttr();
        Assert.Equal(1u, nodeIds[0].GetUInt());
        Assert.Equal(a.Id, nodeIds[1].GetUInt());
        Assert.DoesNotContain(group, b.GetListeners());
    }

    [Fact]
    public void Ray_query_uses_requested_geometry_detail()
    {
        var scene = new Scene();
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        group.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        group.SetMaterial(Resources.Get<Material>("Materials/FirstCube"));
        group.SetBoundingBox(new BoundingBox(-1.0f, 1.0f));
        var instance = scene.CreateChild("Instance");
        group.AddInstanceNode(instance);
        _ = group.GetWorldBoundingBox();

        var ray = new Ray(group.GetBoundingBox().Center - Vector3f.Forward * 10.0f, Vector3f.Forward);
        var results = new List<RayQueryResult>();
        using var query = new RayOctreeQuery(results, ray, RayQueryLevel.RayObb, 100.0f);
        group.ProcessRayQuery(query, results);
        var result = results.Single();

        Assert.Same(group, result.Drawable);
        Assert.Equal(0u, result.SubObject);
        Assert.True(result.Distance < 100.0f);
    }

    [Fact]
    public void Occluder_triangle_count_scales_with_enabled_instances_and_material()
    {
        var scene = new Scene();
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        var model = Resources.Get<Model>("Models/ProceduralBox");
        group.SetModel(model);
        group.SetMaterial(Resources.Get<Material>("Materials/FirstCube"));
        group.AddInstanceNode(scene.CreateChild("A"));
        group.AddInstanceNode(scene.CreateChild("B"));

        var geometry = group.GetLodGeometry(0u, uint.MaxValue)!;
        Assert.Equal(2u * geometry.GetIndexCount() / 3u, group.GetNumOccluderTriangles());

        var material = Resources.Get<Material>("Materials/FirstCube").Clone("GroupOccluder");
        material.SetOcclusion(false);
        group.SetMaterial(material);
        Assert.Equal(0u, group.GetNumOccluderTriangles());
    }

    [Fact]
    public void DrawOcclusion_submits_each_enabled_instance()
    {
        var scene = new Scene();
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        group.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        group.AddInstanceNode(scene.CreateChild("A"));
        group.AddInstanceNode(scene.CreateChild("B"));
        var buffer = new OcclusionBuffer();
        Assert.True(buffer.SetSize(16, 16, false));
        buffer.SetMaxTriangles(uint.MaxValue);

        Assert.True(group.DrawOcclusion(buffer));

        var geometry = group.GetLodGeometry(0u, uint.MaxValue)!;
        Assert.Equal(2u * geometry.GetIndexCount() / 3u, buffer.GetNumTriangles());
    }

    private static Model CreateOcclusionModel()
    {
        var geometry = new Geometry();
        geometry.SetRawVertexData(new byte[36],
            [new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position)]);
        geometry.SetRawIndexData(new byte[6], sizeof(ushort));
        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 3, false));
        var model = new Model { Name = "Occlusion" };
        model.SetNumGeometries(1);
        Assert.True(model.SetGeometry(0, 0, geometry));
        return model;
    }

    [Fact]
    public void Batch_update_refreshes_group_distance_and_lod_distance()
    {
        var scene = new Scene();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        var group = scene.CreateChild("Group").CreateComponent<StaticModelGroup>();
        group.SetBoundingBox(new BoundingBox(-1.0f, 1.0f));
        group.AddInstanceNode(scene.CreateChild("Instance"));

        group.UpdateBatches(new FrameInfo { Camera = camera });

        Assert.True(group.GetDistance() > 0.0f);
        Assert.True(group.GetLodDistance() > 0.0f);
    }
}
