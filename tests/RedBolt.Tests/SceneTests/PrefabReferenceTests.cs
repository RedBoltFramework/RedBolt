namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabReferenceTests
{
    [Fact]
    public void Prefab_reference_matches_native_extensible_noncopyable_class_and_destructor_lifetime()
    {
        Assert.False(typeof(PrefabReference).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(PrefabReference)));
        Assert.Single(typeof(PrefabReference).GetConstructors());
        Assert.NotNull(typeof(PrefabReference).GetConstructor(Type.EmptyTypes));
        Assert.Equal(typeof(PrefabReference),
            typeof(PrefabReference).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
        Assert.Equal(typeof(int), typeof(PrefabReference)
            .GetField("_numInstanceComponents", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(int), typeof(PrefabReference)
            .GetField("_numInstanceChildren", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!.FieldType);

        using var scene = new Scene();
        var host = scene.CreateChild("Host");
        using var resource = new PrefabResource();
        var prefab = resource.GetMutableNodePrefab();
        prefab.Children.Add(new NodePrefab());

        var reference = host.CreateComponent<PrefabReference>();
        Assert.Equal(new StringHash(nameof(PrefabResource)), reference.GetPrefabAttr().Type);
        Assert.Equal(AttributeScopeHint.Node,
            Assert.Single(reference.Attributes, attribute => attribute.Name == "Prefab").ScopeHint);
        Assert.Equal(AttributeScopeHint.Node,
            Assert.Single(reference.Attributes, attribute => attribute.Name == "Path").ScopeHint);
        reference.SetPrefab(resource);
        Assert.Single(host.Children);

        reference.Dispose();
        Assert.Empty(host.Children);

        typeof(Resource).GetMethod("NotifyReloadFinished", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)!.Invoke(resource, null);
        Assert.Empty(host.Children);
    }

    [Fact]
    public void Prefab_reference_creates_and_removes_temporary_instance()
    {
        using var scene = new Scene();
        var host = scene.CreateChild("Host");
        using var resource = new PrefabResource();
        resource.SetName("Tree.prefab");
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
        using var scene = new Scene();
        var host = scene.CreateChild("Host");
        using var resource = new PrefabResource();
        resource.SetName("Tree.prefab");
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
