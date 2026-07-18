using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class ConstructorTests
{
    [Fact]
    public void Public_constructor_creates_wrapper_and_initializes_state()
    {
        var box = new ConstructorBox(10);

        Assert.False(box.IsNull);
        Assert.True(box.OwnsHandle);
        Assert.Equal(10, box.GetValue());
    }

    [Fact]
    public void Constructor_created_wrapper_can_be_disposed()
    {
        var box = new ConstructorBox(10);

        box.Dispose();

        Assert.True(box.IsNull);
    }

    [Fact]
    public void Overloaded_constructor_creates_wrapper_and_initializes_state()
    {
        var box = new ConstructorBox(10, 2);

        Assert.False(box.IsNull);
        Assert.True(box.OwnsHandle);
        Assert.Equal(12, box.GetValue());
    }
}
