using RedBolt;

namespace RedBolt.Tests;

public sealed class ObjectReflectionTests
{
    private sealed class FactoryObject : Object
    {
    }

    [Fact]
    public void Provides_fallback_metadata()
    {
        Assert.Equal("RedBolt.StaticModel", ObjectReflection<StaticModel>.ClassName);
        Assert.Equal(StringHash.Calculate("RedBolt.StaticModel"), ObjectReflection<StaticModel>.TypeId.Value);
    }

    [Fact]
    public void Formatted_type_name_is_csharp_style()
    {
        Assert.Equal("Dictionary<String, List<Int32>>",
            typeof(Dictionary<string, List<int>>).GetFormattedTypeName());
    }

    [Fact]
    public void Registry_manages_factories_categories_and_attributes()
    {
        var registry = new ObjectReflectionRegistry();
        var reflection = registry.AddFactoryReflection<FactoryObject>("Core");
        var attribute = new AttributeInfo("Value", typeof(int), 1);

        reflection.AddAttribute(attribute).SetScopeHint(AttributeScopeHint.Node);
        reflection.UpdateAttributeDefaultValue("Value", new Variant(2));

        Assert.True(registry.IsReflected<FactoryObject>());
        Assert.Same(reflection, registry.GetReflection<FactoryObject>());
        Assert.IsType<FactoryObject>(registry.CreateObject(ObjectReflection<FactoryObject>.TypeId));
        Assert.Equal(new[] { ObjectReflection<FactoryObject>.TypeId }, registry.GetObjectCategories()["Core"]);
        Assert.Equal(2, reflection.GetAttribute("Value").DefaultValue);
        Assert.Equal(AttributeScopeHint.Node, reflection.GetEffectiveScopeHint());

        registry.RemoveReflection<FactoryObject>();
        Assert.False(registry.IsReflected<FactoryObject>());
    }
}
