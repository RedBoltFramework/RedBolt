using RbfxUserCode;

using Xunit;

namespace SharpCpp.Rbfx.Tests.E2E;

public sealed class SharedPtrBindingTests
{
    [Fact]
    public void Shared_ptr_return_promotes_existing_wrapper_to_owning_and_survives_owner_dispose()
    {
        using var owner = new SharedPtrOwner();

        var raw = owner.GetPayloadRaw();
        Assert.NotNull(raw);
        Assert.False(raw!.OwnsNativeHandle);

        var shared = owner.GetPayloadShared();
        Assert.Same(raw, shared);
        Assert.True(shared!.OwnsNativeHandle);
        Assert.Equal(42, shared.GetValue());

        owner.Dispose();

        shared.SetValue(17);
        Assert.Equal(17, shared.GetValue());

        shared.Dispose();
    }

    [Fact]
    public void Weak_ptr_return_locks_and_promotes_existing_wrapper_to_owning_and_survives_owner_dispose()
    {
        using var owner = new SharedPtrOwner();

        var raw = owner.GetPayloadRaw();
        Assert.NotNull(raw);
        Assert.False(raw!.OwnsNativeHandle);

        var weak = owner.GetPayloadWeak();
        Assert.Same(raw, weak);
        Assert.True(weak!.OwnsNativeHandle);
        Assert.Equal(42, weak.GetValue());

        owner.Dispose();

        weak.SetValue(29);
        Assert.Equal(29, weak.GetValue());

        weak.Dispose();
    }
}
