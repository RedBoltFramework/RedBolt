using System;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Enum;

public sealed class EnumTests
{
    [Fact]
    public void StatusCode_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(StatusCode.Busy, E2EBindings.RoundtripStatusCode(StatusCode.Busy));

        var inValue = StatusCode.Ready;
        Assert.Equal(StatusCode.Ready, E2EBindings.EchoInStatusCode(in inValue));

        var refValue = StatusCode.Ready;
        E2EBindings.CycleRefStatusCode(ref refValue);
        Assert.Equal(StatusCode.Busy, refValue);

        E2EBindings.WriteOutStatusCode(out var outValue);
        Assert.Equal(StatusCode.Error, outValue);
    }

    [Fact]
    public void Permissions_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(Permissions.ReadWrite, E2EBindings.RoundtripPermissions(Permissions.ReadWrite));

        var inValue = Permissions.Read;
        Assert.Equal(Permissions.Read, E2EBindings.EchoInPermissions(in inValue));

        var refValue = Permissions.Read;
        E2EBindings.UpgradeRefPermissions(ref refValue);
        Assert.Equal(Permissions.ReadWrite, refValue);

        E2EBindings.WriteOutPermissions(out var outValue);
        Assert.Equal(Permissions.Execute, outValue);
    }

    [Fact]
    public void Permissions_is_flags_enum()
    {
        Assert.True(typeof(Permissions).IsDefined(typeof(FlagsAttribute), inherit: false));

        var combined = E2EBindings.CombinePermissions(Permissions.Read, Permissions.Write);
        Assert.Equal(Permissions.ReadWrite, combined);
        Assert.Equal(Permissions.Read | Permissions.Write, combined);
    }
}
