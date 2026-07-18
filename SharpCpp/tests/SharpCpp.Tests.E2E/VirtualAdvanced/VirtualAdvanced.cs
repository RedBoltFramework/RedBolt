using System.Reflection;

using SharpCpp.Support;
using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.VirtualAdvanced;

public sealed class VirtualAdvancedTests
{
    private sealed class ManagedAdvVirtualCounter : AdvVirtualCounter
    {
        public ManagedAdvVirtualCounter(int seed)
            : base(seed)
        {
        }

        public int CallbackCount { get; private set; }

        public int Offset { get; private set; }

        public override int GetValue()
        {
            CallbackCount++;
            return GetSeed() + Offset;
        }

        public override int Add(int amount)
        {
            CallbackCount++;
            Offset += amount;
            return GetSeed() + Offset;
        }
    }

    private sealed class ManagedAdvAccumulator : AdvAbstractAccumulator
    {
        public ManagedAdvAccumulator(int baseValue)
            : base(baseValue)
        {
        }

        public int CallbackCount { get; private set; }

        public int Extra { get; set; }

        public override int Compute(int value)
        {
            CallbackCount++;
            return GetBaseValue() + value + Extra;
        }
    }

    [Fact]
    public void Virtual_methods_and_abstract_slots_have_expected_modifiers()
    {
        Assert.True(typeof(AdvVirtualCounter).GetMethod(nameof(AdvVirtualCounter.GetValue))!.IsVirtual);
        Assert.False(typeof(AdvVirtualCounter).GetMethod(nameof(AdvVirtualCounter.GetValue))!.IsAbstract);
        Assert.True(typeof(AdvVirtualCounter).GetMethod(nameof(AdvVirtualCounter.Add))!.IsVirtual);
        Assert.True(typeof(AdvAbstractAccumulator).IsAbstract);
        Assert.True(typeof(AdvAbstractAccumulator).GetMethod(nameof(AdvAbstractAccumulator.Compute))!.IsAbstract);
    }

    [Fact]
    public void Managed_director_instances_are_reused_by_create_helpers()
    {
        using var counter = new ManagedAdvVirtualCounter(10);
        var counterHandle = NativeObject.GetHandle(counter);

        Assert.Same(counter, AdvVirtualCounter.CreateManagedWrapper(counterHandle, ownsHandle: false));

        using var accumulator = new ManagedAdvAccumulator(7);
        var accumulatorHandle = NativeObject.GetHandle(accumulator);

        Assert.Same(accumulator, AdvAbstractAccumulator.CreateManagedWrapper(accumulatorHandle, ownsHandle: false));
    }

    [Fact]
    public void Native_calls_dispatch_to_managed_virtual_overrides()
    {
        using var counter = new ManagedAdvVirtualCounter(10);
        AdvVirtualCounter baseView = counter;

        Assert.Equal(10, counter.GetValue());
        Assert.Equal(1, counter.CallbackCount);
        Assert.Equal(10, baseView.GetValue());
        Assert.Equal(2, counter.CallbackCount);

        Assert.Equal(15, counter.Add(5));
        Assert.Equal(3, counter.CallbackCount);
        Assert.Equal(20, baseView.Add(5));
        Assert.Equal(4, counter.CallbackCount);

        Assert.Equal(20, E2EBindings.InvokeAdvVirtualCounterGetValue(counter));
        Assert.Equal(5, counter.CallbackCount);

        Assert.Equal(25, E2EBindings.InvokeAdvVirtualCounterAdd(counter, 5));
        Assert.Equal(6, counter.CallbackCount);
        Assert.Equal(15, counter.Offset);
    }

    [Fact]
    public void Native_calls_dispatch_to_managed_pure_virtual_overrides()
    {
        using var accumulator = new ManagedAdvAccumulator(7)
        {
            Extra = 5
        };
        AdvAbstractAccumulator baseView = accumulator;

        Assert.Equal(13, accumulator.Compute(1));
        Assert.Equal(1, accumulator.CallbackCount);

        Assert.Equal(14, baseView.Compute(2));
        Assert.Equal(2, accumulator.CallbackCount);

        Assert.Equal(13, E2EBindings.InvokeAdvAbstractAccumulatorCompute(accumulator, 1));
        Assert.Equal(3, accumulator.CallbackCount);
    }

    [Fact]
    public void Protected_override_of_public_base_renders_public_managed_surface()
    {
        var adjust = typeof(AdvProtectedOverrideDerived).GetMethod(nameof(AdvProtectedOverrideDerived.Adjust), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(adjust);
        Assert.True(adjust!.IsVirtual);
        Assert.True(adjust.IsPublic);

        using var value = new AdvProtectedOverrideDerived(1);
        AdvPublicVirtualBase baseView = value;

        Assert.Equal(16, value.Adjust(5));
        Assert.Equal(16, baseView.Adjust(5));
        Assert.Equal(16, E2EBindings.InvokeAdvPublicVirtualBaseAdjust(value, 5));
    }

    [Fact]
    public void Public_override_of_protected_base_renders_protected_managed_surface()
    {
        Assert.Null(typeof(AdvPublicOverrideDerived).GetMethod("Adjust", BindingFlags.Instance | BindingFlags.Public));

        var adjust = typeof(AdvPublicOverrideDerived).GetMethod("Adjust", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(adjust);
        Assert.True(adjust!.IsFamily);
        Assert.True(adjust.IsVirtual);

        using var value = new AdvPublicOverrideDerived(1);
        AdvProtectedVirtualBase baseView = value;

        Assert.Equal(36, value.InvokeAdjust(5));
        Assert.Equal(36, baseView.InvokeAdjust(5));
        Assert.Equal(36, E2EBindings.InvokeAdvProtectedVirtualBaseAdjust(value, 5));
    }

    [Fact]
    public void Final_overrides_render_as_sealed_without_redeclaration_on_leaf()
    {
        var finalMode = typeof(AdvFinalMid).GetMethod(nameof(AdvFinalMid.Mode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(finalMode);
        Assert.True(finalMode!.IsVirtual);
        Assert.True(finalMode.IsFinal);

        Assert.Null(typeof(AdvFinalLeaf).GetMethod(nameof(AdvFinalLeaf.Mode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));

        using var leaf = new AdvFinalLeaf(5);
        AdvFinalRoot rootView = leaf;
        AdvFinalMid midView = leaf;

        Assert.Equal(25, leaf.Mode());
        Assert.Equal(25, rootView.Mode());
        Assert.Equal(35, leaf.Value());
        Assert.Equal(35, midView.Value());
        Assert.Equal(25, E2EBindings.InvokeAdvFinalRootMode(leaf));
        Assert.Equal(35, E2EBindings.InvokeAdvFinalMidValue(leaf));
    }

    private sealed class ManagedAdvVirtualCounterCallingBase : AdvVirtualCounter
    {
        public ManagedAdvVirtualCounterCallingBase(int seed)
            : base(seed)
        {
        }

        public int GetValueCallbackCount { get; private set; }

        public int AddCallbackCount { get; private set; }

        public override int GetValue()
        {
            GetValueCallbackCount++;
            return 100 + base.GetValue();
        }

        public override int Add(int amount)
        {
            AddCallbackCount++;
            return 1000 + base.Add(amount);
        }
    }

    [Fact]
    public void Managed_virtual_overrides_can_delegate_to_base_implementation()
    {
        using var counter = new ManagedAdvVirtualCounterCallingBase(10);
        AdvVirtualCounter baseView = counter;

        Assert.Equal(110, counter.GetValue());
        Assert.Equal(1, counter.GetValueCallbackCount);

        Assert.Equal(110, baseView.GetValue());
        Assert.Equal(2, counter.GetValueCallbackCount);

        Assert.Equal(1015, counter.Add(5));
        Assert.Equal(1, counter.AddCallbackCount);

        Assert.Equal(1020, baseView.Add(5));
        Assert.Equal(2, counter.AddCallbackCount);

        Assert.Equal(120, E2EBindings.InvokeAdvVirtualCounterGetValue(counter));
        Assert.Equal(3, counter.GetValueCallbackCount);

        Assert.Equal(1025, E2EBindings.InvokeAdvVirtualCounterAdd(counter, 5));
        Assert.Equal(3, counter.AddCallbackCount);
    }
}
