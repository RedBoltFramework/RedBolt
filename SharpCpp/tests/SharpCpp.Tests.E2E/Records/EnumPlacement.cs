using System;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class EnumPlacementTests
{
    [Fact]
    public void Constructor_can_accept_enum_values()
    {
        var box = new EnumBox(PlacementStatus.Busy, PlacementPermissions.ReadWrite);

        Assert.Equal(PlacementStatus.Busy, box.GetStatus());
        Assert.Equal(PlacementPermissions.ReadWrite, box.GetPermissions());
    }

    [Fact]
    public void Static_factory_can_accept_enum_values()
    {
        var box = EnumBox.Create(PlacementStatus.Error, PlacementPermissions.Execute);

        Assert.Equal(PlacementStatus.Error, box.GetStatus());
        Assert.Equal(PlacementPermissions.Execute, box.GetPermissions());
    }

    [Fact]
    public void Instance_methods_can_accept_and_return_plain_enums()
    {
        var box = new EnumBox(PlacementStatus.Ready, PlacementPermissions.None);

        box.SetStatus(PlacementStatus.Busy);

        Assert.Equal(PlacementStatus.Busy, box.GetStatus());
    }

    [Fact]
    public void Instance_methods_can_accept_and_return_flags_enums()
    {
        var box = new EnumBox(PlacementStatus.Ready, PlacementPermissions.None);

        box.SetPermissions(PlacementPermissions.ReadWrite);

        Assert.Equal(PlacementPermissions.ReadWrite, box.GetPermissions());
        Assert.True(typeof(PlacementPermissions).IsDefined(typeof(FlagsAttribute), inherit: false));
    }

    [Fact]
    public void Static_methods_can_accept_and_return_flags_enums()
    {
        var combined = EnumBox.CombinePermissions(PlacementPermissions.Read, PlacementPermissions.Write);

        Assert.Equal(PlacementPermissions.ReadWrite, combined);
        Assert.Equal(PlacementPermissions.Read | PlacementPermissions.Write, combined);
    }
}
