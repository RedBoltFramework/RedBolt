using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class NativeInterfaceTests
{
    [Fact]
    public void Pure_virtual_method_dispatches_through_native_interface_pointer()
    {
        var counter = E2EBindings.GetNativeInterfaceCounter();

        Assert.False(counter.IsNull);
        Assert.Equal(42, counter.GetValue());
    }

    [Fact]
    public void Interface_out_parameter_materializes_wrapper()
    {
        E2EBindings.CreateNativeInterfaceCounter(out var counter);

        Assert.False(counter.IsNull);
        Assert.Equal(73, counter.GetValue());
    }

    [Fact]
    public void Interface_pointer_arrays_are_passed_as_native_pointer_arrays()
    {
        var counter = E2EBindings.GetNativeInterfaceCounter();
        var targets = new[]
        {
            E2EBindings.GetNativeInterfaceTargetA(),
            E2EBindings.GetNativeInterfaceTargetB()
        };

        Assert.Equal(25, counter.SumTargets(targets, (uint)targets.Length));
    }
}
