namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabResourceTests
{
    [Fact]
    public void Mutable_node_prefab_and_slices_follow_scene_layout()
    {
        var resource = new PrefabResource("Assets/Tree.prefab");
        var scenePrefab = resource.GetMutableScenePrefab();
        scenePrefab.Node.Attributes.Add(new AttributePrefab { Name = "Name", Value = new Variant("Scene") });

        var rootPrefab = resource.GetMutableNodePrefab();
        rootPrefab.Node.Attributes.Add(new AttributePrefab { Name = "Name", Value = new Variant("Root") });
        var childPrefab = new NodePrefab();
        childPrefab.Node.Attributes.Add(new AttributePrefab { Name = "Name", Value = new Variant("Branch") });
        rootPrefab.Children.Add(childPrefab);

        Assert.Same(scenePrefab, resource.GetScenePrefab());
        Assert.Same(rootPrefab, resource.GetNodePrefab());
        Assert.Same(childPrefab, resource.GetNodePrefabSlice("Branch"));
        Assert.Same(childPrefab, resource.GetNodePrefabSlice("#0"));
        Assert.Equal("Root", rootPrefab.GetNodeName());
        Assert.False(rootPrefab.IsEmpty());
    }

    [Fact]
    public void Prefab_resource_roundtrips_through_simple_resource_archive()
    {
        var resource = new PrefabResource("Tree.prefab");
        var rootPrefab = resource.GetMutableNodePrefab();
        rootPrefab.Node.Attributes.Add(new AttributePrefab { Name = "Name", Value = new Variant("Root") });
        rootPrefab.Node.Attributes.Add(new AttributePrefab { Name = "Next Node ID", Value = new Variant(12u) });

        using var stream = new MemoryStream();
        Assert.True(resource.Save(stream));

        stream.Position = 0;
        var loaded = new PrefabResource();
        Assert.True(loaded.Load(stream));
        Assert.Equal("Root", loaded.GetNodePrefab().GetNodeName());
    }

    [Fact]
    public void Normalize_ids_removes_scene_id_attributes()
    {
        var resource = new PrefabResource();
        var scenePrefab = resource.GetMutableScenePrefab();
        scenePrefab.Node.Attributes.Add(new AttributePrefab { Name = "Next Node ID", Value = new Variant(3u) });
        scenePrefab.Node.Attributes.Add(new AttributePrefab { Name = "Next Component ID", Value = new Variant(4u) });
        scenePrefab.Node.Attributes.Add(new AttributePrefab { Name = "Name", Value = new Variant("Scene") });

        resource.NormalizeIds();

        Assert.Single(scenePrefab.Node.Attributes);
        Assert.Equal("Name", scenePrefab.Node.Attributes[0].Name);
    }
}
