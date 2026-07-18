using SharpCpp.Support;

using System.Reflection;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class DirectorTests
{
    private sealed class PassthroughCallbackCounter : CallbackCounter
    {
        public PassthroughCallbackCounter(int seed)
            : base(seed)
        {
        }
    }

    private sealed class ManagedCallbackCounter : CallbackCounter
    {
        public ManagedCallbackCounter(int seed)
            : base(seed)
        {
        }

        public override int GetValue()
        {
            return 21;
        }
    }

    [Fact]
    public void Virtual_methods_are_exposed_with_expected_modifiers()
    {
        var getValue = typeof(CallbackCounter).GetMethod(nameof(CallbackCounter.GetValue), BindingFlags.Instance | BindingFlags.Public)!;

        Assert.True(getValue.IsVirtual);
        Assert.False(getValue.IsAbstract);
    }

    [Fact]
    public void Managed_director_instances_are_reused_by_polymorphic_create_helpers()
    {
        using var counter = new ManagedCallbackCounter(10);
        var handle = NativeObject.GetHandle(counter);

        Assert.Same(counter, CallbackCounter.CreateManagedWrapper(handle, ownsHandle: false));
    }

    [Fact]
    public void Managed_subclasses_without_overrides_fall_back_to_native_virtual_implementation()
    {
        using var counter = new PassthroughCallbackCounter(10);

        Assert.Equal(20, counter.CallVirtualTwice());
    }

    [Fact]
    public void Native_virtual_dispatch_can_call_managed_override_through_director()
    {
        var counter = new ManagedCallbackCounter(10);

        Assert.Equal(42, counter.CallVirtualTwice());
    }
}
