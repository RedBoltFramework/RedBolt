using System.Linq;
using System.Reflection;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class FieldTests
{
    [Fact]
    public void Public_fields_are_exposed_as_properties()
    {
        var payload = E2EBindings.CreateFieldPayload(10);
        var replacement = E2EBindings.CreateFieldPayload(30);
        var box = E2EBindings.CreateFieldBox(2, true, FieldMode.Active, payload, 99);

        Assert.Equal(2, box.Count);
        Assert.True(box.Enabled);
        Assert.Equal(FieldMode.Active, box.Mode);
        Assert.False(box.Payload.OwnsHandle);
        Assert.Equal(10, box.Payload.Value);
        Assert.Equal(new[] { 0, 0 }, box.PayloadSamples.Select(static sample => sample.Value).ToArray());
        Assert.Equal(new[] { 0, 0, 0 }, box.Samples.ToArray());
        Assert.Equal(99, box.FixedValue);

        box.Count = 7;
        box.Enabled = false;
        box.Mode = FieldMode.Suspended;
        box.Payload = replacement;
        using var payloadSamples = new global::SharpCpp.NativeArray<FieldPayload>(new[] { payload, replacement });
        using var samples = new global::SharpCpp.NativeArray<int>(new[] { 3, 5, 7 });
        box.PayloadSamples = payloadSamples;
        box.Samples = samples;

        Assert.Equal(7, box.Count);
        Assert.False(box.Enabled);
        Assert.Equal(FieldMode.Suspended, box.Mode);
        Assert.False(box.Payload.OwnsHandle);
        Assert.Equal(30, box.Payload.Value);
        Assert.Equal(new[] { 10, 30 }, box.PayloadSamples.Select(static sample => sample.Value).ToArray());
        Assert.Equal(new[] { 3, 5, 7 }, box.Samples.ToArray());
        Assert.Equal(99, box.FixedValue);
    }

    [Fact]
    public void Native_fixed_array_fields_validate_setter_lengths()
    {
        var payload = E2EBindings.CreateFieldPayload(10);
        var box = E2EBindings.CreateFieldBox(2, true, FieldMode.Active, payload, 99);
        using var wrongLength = (global::SharpCpp.NativeArray<int>)Activator.CreateInstance(
            typeof(global::SharpCpp.NativeArray<int>),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [IntPtr.Zero, 2, false],
            culture: null)!;

        var exception = Assert.Throws<ArgumentException>(() => box.Samples = wrongLength);

        Assert.Contains("Expected array length 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Handle_backed_native_fixed_array_fields_validate_setter_lengths()
    {
        var payload = E2EBindings.CreateFieldPayload(10);
        var box = E2EBindings.CreateFieldBox(2, true, FieldMode.Active, payload, 99);
        using var wrongLength = (global::SharpCpp.NativeArray<FieldPayload>)Activator.CreateInstance(
            typeof(global::SharpCpp.NativeArray<FieldPayload>),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [IntPtr.Zero, 1, false],
            culture: null)!;

        var exception = Assert.Throws<ArgumentException>(() => box.PayloadSamples = wrongLength);

        Assert.Contains("Expected array length 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Readonly_fields_are_exposed_as_get_only_properties()
    {
        var fixedValue = typeof(FieldBox).GetProperty(nameof(FieldBox.FixedValue))!;

        Assert.True(fixedValue.CanRead);
        Assert.False(fixedValue.CanWrite);
    }

    [Fact]
    public void Handle_backed_properties_can_store_null_wrapper_values()
    {
        var payload = E2EBindings.CreateFieldPayload(10);
        var box = E2EBindings.CreateFieldBox(2, true, FieldMode.Active, payload, 99);

        box.Payload = null!;

        Assert.True(box.Payload.IsNull);
    }
}
