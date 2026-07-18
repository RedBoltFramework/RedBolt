using SharpCpp.Model.Ast;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class BindingTypeTests
{
    [Fact]
    public void HasUnresolvedManagedNamespaceSeparators_accepts_global_prefix_in_nested_generic_arguments()
    {
        var argument = BindingType.CreateHandle("Urho3D::StringHash*", "global::Urho3D.Math.StringHash");
        var vector = BindingType.CreateHandle(
            "ea::vector<Urho3D::StringHash>*",
            "Ea.Vector<{0}>",
            genericArguments: [argument]);

        Assert.False(vector.HasUnresolvedManagedNamespaceSeparators);
    }

    [Fact]
    public void HasUnresolvedManagedNamespaceSeparators_detects_unmapped_cpp_namespace_in_nested_generic_arguments()
    {
        var argument = BindingType.CreateHandle("Urho3D::StringHash*", "Urho3D::StringHash");
        var vector = BindingType.CreateHandle(
            "ea::vector<Urho3D::StringHash>*",
            "Ea.Vector<{0}>",
            genericArguments: [argument]);

        Assert.True(vector.HasUnresolvedManagedNamespaceSeparators);
    }
}
