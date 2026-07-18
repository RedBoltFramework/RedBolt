using System.Reflection;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class PartialOperatorTests
{
    [Fact]
    public void Partial_operators_fall_back_to_named_methods()
    {
        Assert.Null(typeof(PartialOperatorBox).GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static, [typeof(PartialOperatorBox), typeof(PartialOperatorBox)]));
        Assert.Null(typeof(PartialOperatorBox).GetMethod("op_LessThan", BindingFlags.Public | BindingFlags.Static, [typeof(PartialOperatorBox), typeof(PartialOperatorBox)]));
        Assert.NotNull(typeof(PartialOperatorBox).GetMethod(nameof(PartialOperatorBox.Equals), [typeof(PartialOperatorBox)]));
        Assert.NotNull(typeof(PartialOperatorBox).GetMethod(nameof(PartialOperatorBox.LessThan), [typeof(PartialOperatorBox)]));
    }

    [Fact]
    public void Partial_operator_named_methods_dispatch_through_native_code()
    {
        var left = new PartialOperatorBox(10);
        var right = new PartialOperatorBox(7);
        var same = new PartialOperatorBox(10);

        Assert.True(left.Equals(same));
        Assert.False(left.Equals(right));
        Assert.True(right.LessThan(left));
        Assert.False(left.LessThan(right));
    }
}
