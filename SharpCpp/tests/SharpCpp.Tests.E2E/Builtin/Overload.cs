using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Builtin;

public sealed class OverloadTests
{
    [Fact]
    public void Free_function_overloads_dispatch_by_signature()
    {
        Assert.Equal(3, E2EBindings.OverloadValue(3));
        Assert.Equal(7, E2EBindings.OverloadValue(3, 4));
    }
}
