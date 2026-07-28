namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Scene;

public partial class AttributeTests
{
    [Fact]
    public void Serializable_routes_attribute_access_through_attribute_accessor()
    {
        var serializable = new AccessorSerializable();

        using var initialValue = serializable.GetAttribute("Value");
        Assert.Equal(123, initialValue.GetObject());

        Assert.True(serializable.SetAttribute("Value", 456));

        Assert.Equal(456, serializable.StoredValue);
        using var changedValue = serializable.GetAttribute("Value");
        Assert.Equal(456, changedValue.GetObject());
    }

    [Fact]
    public void AttributeInfo_matches_rbfx_enum_metadata_and_copy_contract()
    {
        var defaultValue = new Variant(new CString("default"));
        var metadataValue = new Variant(new CString("metadata"));
        using var attribute = new AttributeInfo("Value", typeof(CString), defaultValue, enumNames: ["Zero", "One"]);
        attribute.SetMetadata("Metadata", metadataValue);
        attribute.ScopeHint = AttributeScopeHint.Scene;

        Assert.Equal("One", attribute.ConvertEnumToString(1));
        Assert.Equal(0, attribute.ConvertEnumToUInt("zero"));
        Assert.Equal(-1, attribute.ConvertEnumToUInt("missing"));
        Assert.Equal("metadata", attribute.GetMetadata<CString>("Metadata").ToString());
        Assert.Equal(0, attribute.GetMetadata<int>("missing"));

        ref readonly var borrowedMetadata = ref attribute.GetMetadata("Metadata");
        using var replacementText = new CString("replacement");
        using var replacementMetadata = new Variant(replacementText);
        attribute.SetMetadata("Metadata", replacementMetadata);
        Assert.Equal("replacement", borrowedMetadata.GetCString().ToString());

        using var copy = Assert.IsType<AttributeInfo>(((ICloneable)attribute).Clone());
        Assert.Equal(attribute.Name, copy.Name);
        Assert.Equal(attribute.DefaultValue, copy.DefaultValue);
        Assert.Equal(attribute.ScopeHint, copy.ScopeHint);
        Assert.Equal("replacement", copy.GetMetadata<CString>("Metadata").ToString());

        defaultValue.Dispose();
        metadataValue.Dispose();
        Assert.Equal("default", attribute.DefaultValue.GetCString().ToString());
        Assert.Equal("replacement", attribute.GetMetadata<CString>("Metadata").ToString());
        Assert.Equal("default", copy.DefaultValue.GetCString().ToString());
        Assert.Equal("replacement", copy.GetMetadata<CString>("Metadata").ToString());
    }

    [Fact]
    public void Attribute_class_kinds_match_cpp_value_and_pointer_types()
    {
        using var attribute = new AttributeInfo();

        Assert.False(typeof(AttributeInfo).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(AttributeInfo)));
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(AttributeInfo)));
        Assert.True(typeof(AttributeHandle).IsValueType);

        using var handleSource = new AttributeInfo("Value", typeof(int), new Variant(0));
        using var reflection = new ObjectReflection(typeof(AccessorSerializable));
        var handle = reflection.AddAttribute(handleSource);
        var handleCopy = handle;
        handleCopy.SetScopeHint(AttributeScopeHint.Scene);
        Assert.Equal(AttributeScopeHint.Scene, reflection.GetAttribute("Value").ScopeHint);
    }

    [Fact]
    public void Default_attribute_handle_matches_rbfx_empty_handle_contract()
    {
        default(AttributeHandle).SetMetadata("Metadata", new Variant("value")).SetScopeHint(AttributeScopeHint.Scene);
    }

    [Fact]
    public void ObjectReflection_destroys_removed_attribute_copies()
    {
        using var firstValue = new CString("first");
        using var secondValue = new CString("second");
        using var firstVariant = new Variant(firstValue);
        using var secondVariant = new Variant(secondValue);
        using var firstSource = new AttributeInfo("First", typeof(CString), firstVariant);
        using var secondSource = new AttributeInfo("Second", typeof(CString), secondVariant);
        var reflection = new ObjectReflection(typeof(AccessorSerializable));

        reflection.AddAttribute(firstSource);
        reflection.AddAttribute(secondSource);
        var firstCopy = reflection.GetAttribute("First");
        var secondCopy = reflection.GetAttribute("Second");

        reflection.RemoveAttribute("First");
        reflection.RemoveAllAttributes();

        Assert.True(firstCopy.DefaultValue.IsEmpty);
        Assert.True(secondCopy.DefaultValue.IsEmpty);
        Assert.Equal("first", firstSource.DefaultValue.GetCString().ToString());
        Assert.Equal("second", secondSource.DefaultValue.GetCString().ToString());
    }

    [Reflected]
    private sealed partial class AccessorSerializable : Serializable
    {
        public AccessorSerializable()
        {
        }

        [Serialized("Value", AccessorType = typeof(ValueAccessor))]
        public int StoredValue { get; set; } = 123;

        private sealed class ValueAccessor : AttributeAccessor
        {
            public override void Get(Serializable ptr, ref Variant dest)
            {
                dest = new Variant(((AccessorSerializable)ptr).StoredValue);
            }

            public override void Set(Serializable ptr, Variant src)
            {
                ((AccessorSerializable)ptr).StoredValue = src.GetInt();
            }
        }
    }
}
