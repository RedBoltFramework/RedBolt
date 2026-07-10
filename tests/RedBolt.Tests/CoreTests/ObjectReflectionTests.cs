using RedBolt;

namespace RedBolt.Tests;

public sealed class ObjectReflectionTests
{
    private class BaseObject : Object
    {
    }

    private sealed class DerivedObject : BaseObject
    {
    }

    private sealed class FactoryObject : Object
    {
    }

    [Fact]
    public void Managed_type_metadata_uses_the_csharp_type_system()
    {
        Assert.Equal("RedBolt.StaticModel", typeof(StaticModel).GetClassName());
        Assert.Equal(StringHash.Calculate("RedBolt.StaticModel"), new StringHash(typeof(StaticModel).GetClassName()).Value);
        Assert.Equal(typeof(StaticModel), ObjectReflection<StaticModel>.Type);
        Assert.Equal(typeof(StaticModel).GetClassName(), ObjectReflection<StaticModel>.ClassName);
        Assert.Equal(new StringHash(typeof(StaticModel).GetClassName()), ObjectReflection<StaticModel>.TypeId);
    }

    [Fact]
    public void Formatted_type_name_is_csharp_style()
    {
        Assert.Equal("Dictionary<String, List<Int32>>",
            typeof(Dictionary<string, List<int>>).GetFormattedTypeName());
    }

    [Fact]
    public void Object_type_checks_use_csharp_assignability()
    {
        var value = new DerivedObject();

        Assert.True(value.IsInstanceOf(typeof(Object)));
        Assert.True(value.IsInstanceOf(typeof(BaseObject)));
        Assert.True(value.IsInstanceOf<DerivedObject>());
        Assert.True(value.IsInstanceOf<BaseObject>());
        Assert.False(value.IsInstanceOf<FactoryObject>());
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
    public void Registry_reports_removal_and_forwards_generic_attribute_updates()
    {
        var registry = new ObjectReflectionRegistry();
        var reflection = registry.AddReflection<FactoryObject>();
        reflection.AddAttribute(new AttributeInfo("Value", typeof(int), 1));
        ObjectReflection? removedReflection = null;
        registry.ReflectionRemoved += reflection => removedReflection = reflection;

        registry.UpdateAttributeDefaultValue<FactoryObject>("Value", new Variant(2));
        Assert.Equal(2, reflection.GetAttribute("Value").DefaultValue);
        Assert.Throws<InvalidOperationException>(() => registry.AddReflection<FactoryObject>());
        registry.RemoveAttribute<FactoryObject>("Value");
        registry.RemoveReflection<FactoryObject>();

        Assert.Null(reflection.GetAttribute("Value"));
        Assert.Same(reflection, removedReflection);
        Assert.NotSame(reflection, registry.AddReflection<FactoryObject>());
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

    [Fact]
    public void Context_forwards_reflection_type_names_and_attributes()
    {
        var context = new Context();
        var reflection = context.AddReflection<FactoryObject>();
        reflection.AddAttribute(new AttributeInfo("Value", typeof(int), 42));

        Assert.Equal(typeof(FactoryObject).GetClassName(), context.GetTypeName(ObjectReflection<FactoryObject>.TypeId));
        Assert.Equal(42, context.GetAttribute<FactoryObject>("Value").DefaultValue);
        Assert.Null(context.GetAttribute<FactoryObject>("Missing"));
        Assert.Equal(string.Empty, context.GetTypeName("Missing"));
    }
}
