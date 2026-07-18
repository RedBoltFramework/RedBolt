using SharpCpp.Tests.E2E.Generated;
using SharpCpp.Support;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class InheritanceTests
{
    [Fact]
    public void Derived_wrapper_inherits_from_base_wrapper()
    {
        var derived = new DerivedCounter(10, 2);

        Assert.IsAssignableFrom<BaseCounter>(derived);
        Assert.Equal(2, derived.GetKind());
        Assert.Equal(10, derived.GetValue());
        Assert.Equal(2, derived.GetBonus());
        Assert.Equal(12, derived.AddAll());
    }

    [Fact]
    public void Derived_wrapper_can_be_used_through_base_view()
    {
        BaseCounter counter = new DerivedCounter(10, 2);

        Assert.Equal(2, counter.GetKind());
        Assert.Equal(10, counter.GetValue());
    }

    [Fact]
    public void Derived_wrapper_created_by_constructor_owns_handle_and_disposes_through_base_layer()
    {
        var counter = new DerivedCounter(10, 2);

        Assert.True(counter.OwnsHandle);

        counter.Dispose();

        Assert.True(counter.IsNull);
    }

    [Fact]
    public void Free_function_can_accept_derived_wrapper_as_base_parameter()
    {
        var counter = new DerivedCounter(10, 2);

        Assert.Equal(10, E2EBindings.ReadBaseCounter(counter));
        Assert.Equal(2, E2EBindings.ReadBaseCounterKind(counter));
    }

    [Fact]
    public void Static_method_can_roundtrip_base_typed_wrapper()
    {
        var counter = new DerivedCounter(10, 2);
        var result = DerivedCounter.IdentityBase(counter);

        Assert.IsType<DerivedCounter>(result);
        Assert.Equal(10, result.GetValue());
    }

    [Fact]
    public void Instance_method_can_roundtrip_base_typed_wrapper()
    {
        var owner = new DerivedCounter(10, 2);
        var argument = new DerivedCounter(20, 3);
        var result = owner.EchoBase(argument);

        Assert.IsType<DerivedCounter>(result);
        Assert.Equal(20, result.GetValue());
    }

    [Fact]
    public void Base_typed_fields_can_store_derived_wrappers()
    {
        var derived = new DerivedCounter(10, 2);
        var holder = E2EBindings.CreateInheritanceHolder(derived);

        Assert.IsType<DerivedCounter>(holder.BaseView);
        Assert.Equal(10, holder.BaseView.GetValue());

        holder.BaseView = new DerivedCounter(20, 3);

        Assert.IsType<DerivedCounter>(holder.BaseView);
        Assert.Equal(20, holder.BaseView.GetValue());
    }

    [Fact]
    public void Base_typed_free_function_return_uses_derived_wrapper_and_cache_identity()
    {
        var first = E2EBindings.GetSharedDerivedAsBase();
        var second = E2EBindings.GetSharedDerivedAsBase();

        Assert.IsType<DerivedCounter>(first);
        Assert.Same(first, second);
        Assert.Equal(30, first.GetValue());
        Assert.Equal(4, ((DerivedCounter)first).GetBonus());
    }

    [Fact]
    public void Derived_constructor_registers_only_the_derived_type_cache()
    {
        using var counter = new DerivedCounter(10, 2);
        var handle = NativeObject.GetHandle(counter);

        Assert.False(BaseCounter.s_instanceCache.TryGetExisting(handle, ownsHandle: false, out _));
        Assert.True(DerivedCounter.s_instanceCache.TryGetExisting(handle, ownsHandle: false, out var cached));
        Assert.Same(counter, cached);
    }

    [Fact]
    public void Polymorphic_create_resolves_native_type_before_cache_lookup()
    {
        using var counter = new DerivedCounter(10, 2);
        var handle = NativeObject.GetHandle(counter);
        using var baseView = new BaseCounter(handle, ownsHandle: false);
        NativeObjectRegistry.RegisterInstance<BaseCounter>(baseView);

        var resolved = BaseCounter.CreateManagedWrapper(handle, ownsHandle: false);

        Assert.Same(counter, resolved);
        Assert.IsType<DerivedCounter>(resolved);
    }
}
