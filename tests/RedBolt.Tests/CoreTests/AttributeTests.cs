namespace RedBolt.Tests;

public class AttributeTests
{
    [Fact]
    public void Serializable_routes_attribute_access_through_attribute_accessor()
    {
        var serializable = new AccessorSerializable();

        Assert.Equal(123, serializable.GetAttribute("Value"));

        Assert.True(serializable.SetAttribute("Value", 456));

        Assert.Equal(456, serializable.StoredValue);
        Assert.Equal(456, serializable.GetAttribute("Value"));
    }

    [Fact]
    public void AttributeInfo_matches_rbfx_enum_metadata_and_copy_contract()
    {
        var attribute = new AttributeInfo("Value", typeof(int), 42, enumNames: ["Zero", "One"]);
        attribute.SetMetadata("Metadata", "value");
        attribute.ScopeHint = AttributeScopeHint.Scene;

        Assert.Equal("One", attribute.ConvertEnumToString(1));
        Assert.Equal(0u, attribute.ConvertEnumToUInt("zero"));
        Assert.Equal(uint.MaxValue, attribute.ConvertEnumToUInt("missing"));
        Assert.Equal("value", attribute.GetMetadata<string>("Metadata"));
        Assert.Equal(0, attribute.GetMetadata<int>("missing"));

        var copy = Assert.IsType<AttributeInfo>(((ICloneable)attribute).Clone());
        Assert.Equal(attribute.Name, copy.Name);
        Assert.Equal(attribute.DefaultValue, copy.DefaultValue);
        Assert.Equal(attribute.ScopeHint, copy.ScopeHint);
        Assert.Equal("value", copy.GetMetadata<string>("Metadata"));
    }

    [Fact]
    public void Default_attribute_handle_matches_rbfx_empty_handle_contract()
    {
        default(AttributeHandle).SetMetadata("Metadata", "value").SetScopeHint(AttributeScopeHint.Scene);
    }

    private sealed class AccessorSerializable : Serializable
    {
        public AccessorSerializable()
        {
            RegisterAttribute("Value", typeof(int), new ValueAccessor(), defaultValue: 123);
        }

        public int StoredValue { get; set; } = 123;

        private sealed class ValueAccessor : AttributeAccessor
        {
            public override object Get(Serializable ptr)
            {
                return ((AccessorSerializable)ptr).StoredValue;
            }

            public override void Set(Serializable ptr, object src)
            {
                ((AccessorSerializable)ptr).StoredValue = src is int value ? value : 0;
            }
        }
    }
}
