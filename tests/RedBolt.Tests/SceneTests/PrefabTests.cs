namespace RedBolt.Tests;

public sealed class PrefabTests
{
    [Fact]
    public void Prefab_types_preserve_native_extensible_class_kind_and_identifier_types()
    {
        Assert.False(typeof(AttributePrefab).IsSealed);
        Assert.False(typeof(SerializablePrefab).IsSealed);
        Assert.False(typeof(NodePrefab).IsSealed);
        Assert.False(typeof(PrefabNormalizer).IsSealed);
        Assert.Equal(typeof(AttributeId), typeof(AttributePrefab).GetProperty(nameof(AttributePrefab.Id))!.PropertyType);
        Assert.Equal(typeof(SerializableId), typeof(SerializablePrefab).GetProperty(nameof(SerializablePrefab.Id))!.PropertyType);
        Assert.Equal(StringHash.Empty, new AttributePrefab().NameHash);
        Assert.Equal(StringHash.Empty, new SerializablePrefab().TypeNameHash);
    }

    [Fact]
    public void Prefab_values_compare_structurally_and_clone_independently()
    {
        var left = new NodePrefab();
        left.Node.SetType("Node");
        var value = new Variant("Root");
        var attribute = new AttributePrefab("Name");
        attribute.SetValue(ref value);
        left.Node.Attributes.Add(attribute);
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
    public void Prefab_dispose_destroys_owned_nested_values()
    {
        var prefab = new NodePrefab();
        var value = new Variant("Root");
        var attribute = new AttributePrefab("Name");
        attribute.SetValue(ref value);
        prefab.Node.Attributes.Add(attribute);
        prefab.Components.Add(new SerializablePrefab());
        prefab.Children.Add(new NodePrefab());

        prefab.Dispose();

        Assert.True(prefab.IsEmpty());
        Assert.Equal(VariantType.None, attribute.Value.Type);
    }

    [Fact]
    public void Animatable_registers_object_animation_resource_attribute()
    {
        var animatable = new TestAnimatable();

        var value = Assert.IsType<ResourceRef>(animatable.GetAttribute("Object Animation"));

        Assert.Equal(ObjectReflection<ObjectAnimation>.TypeId, value.Type);
        Assert.Empty(value.Name);
    }
}
