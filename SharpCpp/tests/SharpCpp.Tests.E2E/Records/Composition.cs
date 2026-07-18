using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class CompositionTests
{
    [Fact]
    public void Free_function_can_accept_and_return_record_wrapper()
    {
        var payload = PayloadRecord.Create(10);
        var result = E2EBindings.EchoPayload(payload);

        Assert.False(result.OwnsHandle);
        Assert.Equal(payload.Handle, result.Handle);
        Assert.Equal(10, result.GetValue());
    }

    [Fact]
    public void Free_function_can_roundtrip_null_record_wrapper()
    {
        var result = E2EBindings.EchoPayload(null!);

        Assert.True(result.IsNull);
    }

    [Fact]
    public void Static_method_can_accept_and_return_record_wrapper()
    {
        var payload = PayloadRecord.Create(10);
        var result = OwnerRecord.Identity(payload);

        Assert.False(result.OwnsHandle);
        Assert.Equal(payload.Handle, result.Handle);
        Assert.Equal(10, result.GetValue());
    }

    [Fact]
    public void Constructor_can_accept_record_wrapper()
    {
        var payload = PayloadRecord.Create(10);
        var owner = new OwnerRecord(payload);

        Assert.Equal(payload.Handle, owner.GetPayload().Handle);
        Assert.Equal(10, owner.GetPayload().GetValue());
    }

    [Fact]
    public void Static_factory_can_accept_record_wrapper()
    {
        var payload = PayloadRecord.Create(10);
        var owner = OwnerRecord.Create(payload);

        Assert.False(owner.OwnsHandle);
        Assert.Equal(payload.Handle, owner.GetPayload().Handle);
        Assert.Equal(10, owner.GetPayload().GetValue());
    }

    [Fact]
    public void Instance_method_can_accept_and_return_record_wrapper()
    {
        var first = PayloadRecord.Create(10);
        var second = PayloadRecord.Create(20);
        var owner = new OwnerRecord(first);

        var previous = owner.ReplacePayload(second);

        Assert.False(previous.OwnsHandle);
        Assert.Equal(first.Handle, previous.Handle);
        Assert.Equal(10, previous.GetValue());

        Assert.False(owner.GetPayload().OwnsHandle);
        Assert.Equal(second.Handle, owner.GetPayload().Handle);
        Assert.Equal(20, owner.GetPayload().GetValue());
    }

    [Fact]
    public void Instance_method_can_accept_null_record_wrapper()
    {
        var first = PayloadRecord.Create(10);
        var owner = new OwnerRecord(first);

        var previous = owner.ReplacePayload(null!);

        Assert.Equal(first.Handle, previous.Handle);
        Assert.Equal(10, previous.GetValue());
        Assert.True(owner.GetPayload().IsNull);
    }
}
