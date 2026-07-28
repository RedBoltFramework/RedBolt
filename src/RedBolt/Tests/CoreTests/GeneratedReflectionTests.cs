namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Scene;

[Reflected("Tests")]
public partial class GeneratedReflectionObject : Serializable
{
    private static readonly string[] NamedFieldNames = ["Zero", "One"];

    [Serialized("Named Field", EnumNamesSource = nameof(NamedFieldNames))]
    private int _namedField = 1;

    [Serialized("Field")]
    private int _field = 42;

    [Serialized("Property")]
    public string Property { get; set; } = "initial";

    private int _backedProperty = 23;

    private int _custom = GetCustomDefault();

    private int _setterTarget = GetSetterTargetDefault();

    public int Field => _field;

    [Serialized("Backed Property")]
    public int BackedProperty
    {
        get => _backedProperty;
        set => _backedProperty = value;
    }

    public int Custom => _custom;

    public int SetterTarget => _setterTarget;

    private static int GetCustomDefault() => 17;

    private static int GetSetterTargetDefault() => 29;

    [Serialized("Custom", DefaultSource = nameof(GetCustomDefault), Setter = nameof(SetCustom))]
    private int GetCustom() => _custom;

    private void SetCustom(int value) => _custom = value;

    private int GetSetterTarget() => _setterTarget;

    [Serialized("Setter Target", DefaultSource = nameof(GetSetterTargetDefault), Getter = nameof(GetSetterTarget))]
    private void SetSetterTarget(int value) => _setterTarget = value;
}

[Reflected("Tests")]
public partial class GeneratedReflectionDerived : GeneratedReflectionObject
{
    [Serialized("Derived")]
    public bool Derived { get; set; } = true;
}

[Reflected("Tests")]
public partial class GeneratedReflectionIntermediate : GeneratedReflectionObject
{
    [Serialized("Intermediate")]
    public int Intermediate { get; set; }
}

[Reflected(
    "Tests",
    InheritAttributesFrom = typeof(GeneratedReflectionObject),
    InheritAttributesOrder = 1)]
public partial class GeneratedExplicitInheritance : GeneratedReflectionIntermediate
{
    [Serialized("Before")]
    public int Before { get; set; }

    [Serialized("After")]
    public int After { get; set; }
}

[Reflected("Tests")]
[RemoveSerialized("Field")]
public partial class GeneratedReflectionRemoved : GeneratedReflectionObject
{
}

public class ManualReflectionBase : Serializable
{
}

[Reflected("Tests")]
public partial class GeneratedReflectionFromManualBase : ManualReflectionBase
{
}

[Reflected("Tests", Factory = false)]
public partial class GeneratedReflectionWithoutFactory : Serializable
{
}

[Reflected("Tests")]
public partial class GeneratedReflectionNamespaceUsing : Serializable
{
    [Serialized("Imported Enum")]
    public AutoRemoveMode ImportedEnum { get; set; } = AutoRemoveMode.Disabled;
}

public sealed partial class GeneratedReflectionTests
{
    [Reflected("Tests")]
    private partial class GeneratedNestedBase : Serializable
    {
        [Serialized("Nested Value")]
        public int NestedValue { get; set; } = 9;
    }

    [Reflected("Tests")]
    private sealed partial class GeneratedNestedDerived : GeneratedNestedBase
    {
    }

    [Fact]
    public void Generated_reflection_registers_user_assembly_and_initializer_defaults()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var reflection = context.GetReflection<GeneratedReflectionObject>();
            var instance = Assert.IsType<GeneratedReflectionObject>(reflection.CreateObject());
            var withoutFactory = context.GetReflection<GeneratedReflectionWithoutFactory>();

            Assert.Equal("Tests", reflection.Category);
            Assert.False(withoutFactory.HasObjectFactory());
            Assert.Null(withoutFactory.CreateObject());
            Assert.Equal(1, reflection.GetAttribute("Named Field").Accessor!.GetObject(instance));
            Assert.Equal(42, instance.Field);
            Assert.Equal("initial", instance.Property);
            Assert.Equal(23, instance.BackedProperty);
            Assert.Equal(17, instance.Custom);
            Assert.Equal(29, instance.SetterTarget);
            Assert.Equal(1, reflection.GetAttribute("Named Field").DefaultValue.GetInt());
            Assert.Equal(["Zero", "One"], reflection.GetAttribute("Named Field").EnumNames);
            Assert.Equal(42, reflection.GetAttribute("Field").DefaultValue.GetInt());
            Assert.Equal("initial", reflection.GetAttribute("Property").DefaultValue.GetString());
            Assert.Equal(23, reflection.GetAttribute("Backed Property").DefaultValue.GetInt());
            Assert.Equal(17, reflection.GetAttribute("Custom").DefaultValue.GetInt());
            Assert.Equal(29, reflection.GetAttribute("Setter Target").DefaultValue.GetInt());

            reflection.GetAttribute("Custom").Accessor!.SetObject(instance, 31);
            reflection.GetAttribute("Setter Target").Accessor!.SetObject(instance, 37);
            Assert.Equal(31, reflection.GetAttribute("Custom").Accessor!.GetObject(instance));
            Assert.Equal(37, reflection.GetAttribute("Setter Target").Accessor!.GetObject(instance));

            context.RegisterAssembly(typeof(GeneratedReflectionObject).Assembly);
            Assert.Equal(6u, reflection.NumAttributes);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Generated_reflection_registers_private_nested_inheritance_in_order()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var reflection = context.GetReflection<GeneratedNestedDerived>();
            using var instance = Assert.IsType<GeneratedNestedDerived>(reflection.CreateObject());

            Assert.Equal(9, reflection.GetAttribute("Nested Value").DefaultValue.GetInt());
            Assert.Equal(9, instance.NestedValue);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Generated_reflection_preserves_namespace_local_using_directives()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var reflection = context.GetReflection<GeneratedReflectionNamespaceUsing>();
            using var instance = Assert.IsType<GeneratedReflectionNamespaceUsing>(reflection.CreateObject());

            Assert.Equal(AutoRemoveMode.Disabled, instance.ImportedEnum);
            Assert.Equal(
                AutoRemoveMode.Disabled,
                reflection.GetAttribute("Imported Enum").Accessor!.GetObject(instance));
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Generated_reflection_uses_explicit_inheritance_source_and_member_declaration_order()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var reflection = context.GetReflection<GeneratedExplicitInheritance>();

            Assert.Equal(
                ["Before", "Named Field", "Field", "Property", "Backed Property", "Custom", "Setter Target", "After"],
                reflection.Attributes.Select(static attribute => attribute.Name));
            Assert.DoesNotContain(reflection.Attributes, static attribute => attribute.Name == "Intermediate");
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Generated_reflection_inherits_and_removes_serialized_attributes()
    {
        var previousContext = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);

        try
        {
            var derived = context.GetReflection<GeneratedReflectionDerived>();
            Assert.NotNull(derived.GetAttribute("Field"));
            Assert.NotNull(derived.GetAttribute("Property"));
            Assert.NotNull(derived.GetAttribute("Custom"));
            Assert.NotNull(derived.GetAttribute("Derived"));

            var removed = context.GetReflection<GeneratedReflectionRemoved>();
            Assert.Null(removed.GetAttribute("Field"));
            Assert.NotNull(removed.GetAttribute("Property"));
            Assert.NotNull(removed.GetAttribute("Custom"));

            var manualBase = context.AddReflection<ManualReflectionBase>();
            using var manualAttribute = new AttributeInfo("Manual", typeof(int), 23);
            manualBase.AddAttribute(manualAttribute);
            context.RegisterAssembly(typeof(GeneratedReflectionFromManualBase).Assembly);

            var fromManualBase = context.GetReflection<GeneratedReflectionFromManualBase>();
            Assert.Equal(23, fromManualBase.GetAttribute("Manual").DefaultValue.GetInt());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
