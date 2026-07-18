using SharpCpp.Support;
using SharpCpp.Tests.E2E.Generated;

using System.Reflection;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class ProtectedTests
{
    private sealed class ManagedProtectedCounter : ProtectedCounter
    {
        public ManagedProtectedCounter(int seed)
            : base(seed)
        {
        }

        public int CallbackCount { get; private set; }

        public int CallIncrementFromManaged(int amount)
        {
            return Increment(amount);
        }

        public int ReadFactorFromManaged()
        {
            return Factor;
        }

        public void WriteFactorFromManaged(int value)
        {
            Factor = value;
        }

        protected override int Scale(int value)
        {
            CallbackCount++;
            return GetSeed() + (Factor * 10) + value;
        }
    }

    [Fact]
    public void Director_records_expose_protected_members_as_non_public_surface()
    {
        Assert.Null(typeof(ProtectedCounter).GetConstructor([typeof(int)]));

        var constructor = typeof(ProtectedCounter).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(int)],
            modifiers: null);
        Assert.NotNull(constructor);
        Assert.True(constructor!.IsFamily);

        Assert.Null(typeof(ProtectedCounter).GetMethod("Increment", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(ProtectedCounter).GetMethod("Scale", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(ProtectedCounter).GetProperty("Factor", BindingFlags.Instance | BindingFlags.Public));

        var increment = typeof(ProtectedCounter).GetMethod("Increment", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(increment);
        Assert.True(increment!.IsFamily);
        Assert.False(increment.IsVirtual);

        var scale = typeof(ProtectedCounter).GetMethod("Scale", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(scale);
        Assert.True(scale!.IsFamily);
        Assert.True(scale.IsVirtual);

        var factor = typeof(ProtectedCounter).GetProperty("Factor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(factor);
        Assert.True(factor!.GetMethod!.IsFamily);
        Assert.True(factor.SetMethod!.IsFamily);
    }

    [Fact]
    public void Managed_subclasses_can_use_protected_methods_fields_and_virtuals()
    {
        using var counter = new ManagedProtectedCounter(4);

        Assert.Equal(2, counter.ReadFactorFromManaged());
        Assert.Equal(7, counter.CallIncrementFromManaged(3));
        Assert.Equal(7, counter.GetSeed());

        counter.WriteFactorFromManaged(5);

        Assert.Equal(5, counter.ReadFactorFromManaged());
        Assert.Equal(58, counter.CallScale(1));
        Assert.Equal(1, counter.CallbackCount);
    }

    [Fact]
    public void Managed_protected_director_instances_are_reused_by_polymorphic_create_helper()
    {
        using var counter = new ManagedProtectedCounter(4);
        var handle = NativeObject.GetHandle(counter);

        Assert.Same(counter, ProtectedCounter.CreateManagedWrapper(handle, ownsHandle: false));
    }

    [Fact]
    public void Non_director_records_do_not_expose_protected_surface()
    {
        Assert.NotNull(typeof(PlainProtectedCounter).GetMethod(nameof(PlainProtectedCounter.VisibleValue), BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(typeof(PlainProtectedCounter).GetMethod("HiddenValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Null(typeof(PlainProtectedCounter).GetProperty("Factor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

        var counter = new PlainProtectedCounter();
        Assert.Equal(21, counter.VisibleValue());
    }
}
