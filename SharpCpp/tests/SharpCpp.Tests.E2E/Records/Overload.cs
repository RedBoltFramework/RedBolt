using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class OverloadTests
{
    [Fact]
    public void Static_method_overloads_dispatch_by_signature()
    {
        Assert.Equal(3, OverloadBox.Combine(3));
        Assert.Equal(7, OverloadBox.Combine(3, 4));
    }

    [Fact]
    public void Instance_method_overloads_dispatch_by_signature()
    {
        var box = new OverloadBox(10);

        Assert.Equal(15, box.Mix(5));
        Assert.Equal(17, box.Mix(3, 4));
    }
}
