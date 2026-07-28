using RedBolt.Core;
using RedBolt.Scene;

namespace RedBolt.Tests;

[Reflected("Tests")]
[Serialized(
    "Named Field",
    typeof(int),
    DefaultSource = nameof(_namedField),
    EnumNamesSource = nameof(NamedFieldNames),
    Getter = nameof(_namedField),
    Setter = nameof(_namedField))]
public partial class GeneratedReflectionObject : Serializable
{
    private static readonly string[] NamedFieldNames = ["Zero", "One"];

    private int _namedField = 1;

    [Serialized("Field")]
    private int _field = 42;

    [Serialized("Property")]
    public string Property { get; set; } = "initial";

    [Serialized(
        "Custom",
        typeof(int),
        DefaultSource = nameof(GetCustomDefault),
        Getter = nameof(GetCustom),
        Setter = nameof(SetCustom))]
    private int _custom = GetCustomDefault();

    public int Field => _field;

    public int Custom => _custom;

    private static int GetCustomDefault() => 17;

    private int GetCustom() => _custom;

    private void SetCustom(int value) => _custom = value;
}

[Reflected("Tests")]
public partial class GeneratedReflectionDerived : GeneratedReflectionObject
{
    [Serialized("Derived")]
    public bool Derived { get; set; } = true;
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
            Assert.Equal(17, instance.Custom);
            Assert.Equal(1, reflection.GetAttribute("Named Field").DefaultValue.GetInt());
            Assert.Equal(["Zero", "One"], reflection.GetAttribute("Named Field").EnumNames);
            Assert.Equal(42, reflection.GetAttribute("Field").DefaultValue.GetInt());
            Assert.Equal("initial", reflection.GetAttribute("Property").DefaultValue.GetString());
            Assert.Equal(17, reflection.GetAttribute("Custom").DefaultValue.GetInt());

            context.RegisterAssembly(typeof(GeneratedReflectionObject).Assembly);
            Assert.Equal(4u, reflection.NumAttributes);
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
