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
