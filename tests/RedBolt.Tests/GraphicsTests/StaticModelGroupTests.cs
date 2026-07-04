using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class StaticModelGroupTests
{
    private readonly HeadlessEngineFixture fixture;

    public StaticModelGroupTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

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
        a.SetPosition(new Vector3(30.0f, 0.0f, 0.0f));
        var b = scene.CreateChild("B");
        b.SetPosition(new Vector3(40.0f, 0.0f, 0.0f));

        group.AddInstanceNode(a);
        group.AddInstanceNode(b);

        Assert.Equal(2u, group.GetNumInstanceNodes());
        Assert.Same(b, group.GetInstanceNode(1u));
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
    }
}
