using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class FactoryTests
{
    [Fact]
    public void Static_factory_method_returns_wrapper()
    {
        var box = FactoryBox.Create(10);

        Assert.False(box.IsNull);
        Assert.False(box.OwnsHandle);
        Assert.Equal(10, box.GetValue());
    }

    [Fact]
    public void Static_primitive_method_returns_value()
    {
        Assert.Equal(42, FactoryBox.Add(19, 23));
    }

    [Fact]
    public void Free_function_can_accept_wrapper_parameter()
    {
        var box = FactoryBox.Create(10);

        Assert.Equal(10, E2EBindings.ReadFactoryBox(box));
    }

    [Fact]
    public void Static_method_can_roundtrip_wrapper_parameter()
    {
        var box = FactoryBox.Create(10);
        var copy = FactoryBox.Identity(box);

        Assert.False(copy.IsNull);
        Assert.Equal(10, copy.GetValue());
    }

    [Fact]
    public void Static_method_can_roundtrip_null_wrapper_parameter()
    {
        var copy = FactoryBox.Identity(null!);

        Assert.True(copy.IsNull);
    }

    [Fact]
    public void Borrowed_wrapper_can_be_disposed_without_owning_native_lifetime()
    {
        var box = FactoryBox.Create(10);

        box.Dispose();

        Assert.True(box.IsNull);
    }
}
