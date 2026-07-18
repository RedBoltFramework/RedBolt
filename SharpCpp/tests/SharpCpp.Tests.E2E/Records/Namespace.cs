using E2e.Records;
using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class NamespaceTests
{
    [Fact]
    public void Namespaced_record_constructs_and_calls_methods()
    {
        using var counter = new NamespacedCounter(42);

        Assert.Equal(42, counter.GetValue());
    }

    [Fact]
    public void Namespaced_record_static_method_roundtrips_handle()
    {
        var original = new NamespacedCounter(7);

        var result = NamespacedCounter.Identity(original);

        Assert.Equal(7, result.GetValue());
    }

    [Fact]
    public void Namespaced_record_disposes_cleanly()
    {
        var counter = new NamespacedCounter(10);

        counter.Dispose();

        Assert.True(counter.IsNull);
    }
}