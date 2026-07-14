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
        group.SetModel(Resources.Get<Model>("Models/ProceduralBox"));
        group.SetMaterial(Resources.Get<Material>("Materials/FirstCube"));
        var a = scene.CreateChild("A");
        var b = scene.CreateChild("B");

        group.SetNodeIDsAttr([2u, a.Id, b.Id]);
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

        group.SetNodeIDsAttr([1u, first.Id]);
        group.ApplyAttributes();
        Assert.Contains(group, first.GetListeners());

        group.SetNodeIDsAttr([1u, second.Id]);
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
        Assert.True(group.GetNodeIDsAttr().SequenceEqual([1u, a.Id]));
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
        Assert.Equal(1u, group.GetNumWorldTransforms());

        var ray = new Ray(group.GetBoundingBox().Center - Vector3f.Forward * 10.0f, Vector3f.Forward);
        var results = new List<RayQueryResult>();
        group.ProcessRayQuery(new RayOctreeQuery(results, ray, RayQueryLevel.RayObb, 100.0f));
        var result = results.Single();

        Assert.Same(group, result.Drawable);
        Assert.Equal(0u, result.SubObject);
        Assert.True(result.Distance < 100.0f);
    }
}
