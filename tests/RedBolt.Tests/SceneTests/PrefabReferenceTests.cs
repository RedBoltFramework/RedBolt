namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabReferenceTests
{
    [Fact]
    public void Prefab_reference_creates_and_removes_temporary_instance()
    {
        var scene = new Scene();
        var host = scene.CreateChild("Host");
        var resource = new PrefabResource("Tree.prefab");
        var prefab = resource.GetMutableNodePrefab();
        var child = new NodePrefab();
        child.Node.Attributes.Add(CreateAttribute("Name", new Variant("Generated")));
        prefab.Children.Add(child);

        var reference = host.CreateComponent<PrefabReference>();
        reference.SetPrefab(resource);

        Assert.Same(resource, reference.GetPrefab());
        Assert.Single(host.Children);
        Assert.True(host.Children[0].IsTemporary);

        reference.SetEnabled(false);
        Assert.Empty(host.Children);

        reference.SetEnabled(true);
        Assert.Single(host.Children);
        Assert.True(host.Children[0].IsTemporary);
    }

    [Fact]
    public void Inline_conservative_persists_prefab_instance_and_removes_reference()
    {
        var scene = new Scene();
        var host = scene.CreateChild("Host");
        var resource = new PrefabResource("Tree.prefab");
        var prefab = resource.GetMutableNodePrefab();
        prefab.Children.Add(new NodePrefab());

        var reference = host.CreateComponent<PrefabReference>();
        reference.SetPrefab(resource);
        reference.InlineConservative();

        Assert.Null(host.GetComponent<PrefabReference>());
        Assert.Single(host.Children);
        Assert.False(host.Children[0].IsTemporary);
    }

    private static AttributePrefab CreateAttribute(string name, Variant value)
    {
        var result = new AttributePrefab(name);
        result.SetValue(ref value);
        return result;
    }
}
