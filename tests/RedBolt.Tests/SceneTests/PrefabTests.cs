namespace RedBolt.Tests;

public sealed class PrefabTests
{
    [Fact]
    public void Prefab_values_compare_structurally_and_clone_independently()
    {
        var left = new NodePrefab();
        left.Node.SetType("Node");
        left.Node.Attributes.Add(new AttributePrefab("Name") { Value = new Variant("Root") });
        left.Children.Add(new NodePrefab());

        var clone = Assert.IsType<NodePrefab>(left.Clone());

        Assert.Equal(left, clone);
        Assert.NotSame(left, clone);
        Assert.NotSame(left.Node, clone.Node);
        Assert.NotSame(left.Node.Attributes[0], clone.Node.Attributes[0]);
    }

    [Fact]
    public void Empty_prefab_scope_hint_defaults_to_attribute()
    {
        Assert.Equal(AttributeScopeHint.Attribute, new SerializablePrefab().GetEffectiveScopeHint());
        Assert.Equal(AttributeScopeHint.Attribute, new NodePrefab().GetEffectiveScopeHint());
    }

    [Fact]
    public void Animatable_registers_object_animation_resource_attribute()
    {
        var animatable = new Animatable();

        var value = Assert.IsType<ResourceRef>(animatable.GetAttribute("Object Animation"));

        Assert.Equal(ObjectReflection<ObjectAnimation>.TypeId, value.Type);
        Assert.Empty(value.Name);
    }
}
