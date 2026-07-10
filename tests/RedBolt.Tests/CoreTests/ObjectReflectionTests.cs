using RedBolt;

namespace RedBolt.Tests;

public sealed class ObjectReflectionTests
{
    private sealed class FactoryObject : Object
    {
    }

    [Fact]
    public void Managed_type_metadata_uses_the_csharp_type_system()
    {
        Assert.Equal("RedBolt.StaticModel", typeof(StaticModel).GetClassName());
        Assert.Equal(StringHash.Calculate("RedBolt.StaticModel"), new StringHash(typeof(StaticModel).GetClassName()).Value);
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
        var typeId = new StringHash(typeof(FactoryObject).GetClassName());
        Assert.IsType<FactoryObject>(registry.CreateObject(typeId));
        Assert.Equal(new[] { typeId }, registry.GetObjectCategories()["Core"]);
        Assert.Equal(2, reflection.GetAttribute("Value").DefaultValue);
        Assert.Equal(AttributeScopeHint.Node, reflection.GetEffectiveScopeHint());

        registry.RemoveReflection<FactoryObject>();
        Assert.False(registry.IsReflected<FactoryObject>());
    }

    [Fact]
    public void Object_reads_type_and_category_from_managed_reflection()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.AddFactoryReflection<FactoryObject>("Core");
            var value = new FactoryObject();

            Assert.Equal(typeof(FactoryObject).GetClassName(), value.GetTypeName());
            Assert.Equal(new StringHash(typeof(FactoryObject).GetClassName()), value.GetTypeNameHash());
            Assert.Equal("Core", value.GetCategory());
        }
        finally
        {
            Context.SetInstance(null);
        }
    }
}
