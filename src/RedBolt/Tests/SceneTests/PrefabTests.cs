using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.Scene;

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
    public void Attribute_prefab_value_is_a_borrowed_view()
    {
        using var source = new CString(new string('a', 32));
        using var replacement = new CString(new string('b', 32));
        using var sourceValue = new Variant(source);
        using var replacementValue = new Variant(replacement);
        var attribute = new AttributePrefab("Name");
        attribute.SetValue(sourceValue);
        ref readonly var value = ref attribute.Value;

        Assert.Equal(new string('a', 32), value.GetCString().ConvertToString());
        Assert.NotSame(source, value.GetCString());

        attribute.SetValue(replacementValue);

        Assert.Equal(new string('b', 32), value.GetCString().ConvertToString());
        Assert.NotSame(replacement, value.GetCString());

        attribute.Dispose();

        Assert.Equal(VariantType.None, value.Type);
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

        using var attribute = animatable.GetAttribute("Object Animation");
        var value = Assert.IsType<ResourceRef>(attribute.GetObject());

        Assert.Equal(ObjectReflection<ObjectAnimation>.TypeId, value.Type);
        Assert.Empty(value.Name);
    }
}
