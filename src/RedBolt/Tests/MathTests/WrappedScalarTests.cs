using RedBolt;

namespace RedBolt.Tests;

public sealed class WrappedScalarTests
{
    [Fact]
    public void Wrapped_scalars_are_value_types_with_independent_copies()
    {
        Assert.True(typeof(WrappedScalar<float>).IsValueType);
        Assert.True(typeof(WrappedScalarRange<float>).IsValueType);

        var value = new WrappedScalar<float>(1.0f, 0.0f, 2.5f);
        var copy = value;
        var range = copy.UpdateWrapped(0.5f);
        var rangeCopy = range;

        Assert.Equal(1.0f, value.Value());
        Assert.Equal(1.5f, copy.Value());
        Assert.Equal(range, rangeCopy);
    }

    [Fact]
    public void Constructor_orders_boundaries_and_clamps_value()
    {
        var value = new WrappedScalar<float>(20.0f, 10.0f, 0.0f);

        Assert.Equal(10.0f, value.Value());
        Assert.Equal(0.0f, value.Min());
        Assert.Equal(10.0f, value.Max());

        value.Set(-1.0f);
        Assert.Equal(0.0f, value.Value());
    }

    [Fact]
    public void Wrapped_update_returns_directional_range()
    {
        var value = new WrappedScalar<float>(8.0f, 0.0f, 10.0f);
        var range = value.UpdateWrapped(5.0f);

        Assert.Equal(3.0f, value.Value());
        Assert.Equal(8.0f, range.Begin());
        Assert.Equal(3.0f, range.End());
        Assert.True(range.ContainsInclusive(9.0f));
        Assert.False(range.ContainsInclusive(5.0f));
        Assert.True(range.ContainsExcludingBegin(9.0f));
        Assert.True(range.ContainsExcludingEnd(8.0f));
    }

    [Fact]
    public void Clamped_update_can_return_the_out_of_bounds_range()
    {
        var value = new WrappedScalar<float>(5.0f, 0.0f, 10.0f);
        var range = value.UpdateClamped(10.0f, true);

        Assert.Equal(10.0f, value.Value());
        Assert.Equal(5.0f, range.Begin());
        Assert.Equal(15.0f, range.End());

        var clampedRange = value.UpdateClamped(-20.0f);
        Assert.Equal(0.0f, value.Value());
        Assert.Equal(10.0f, clampedRange.Begin());
        Assert.Equal(0.0f, clampedRange.End());
    }

    [Fact]
    public void Multiple_wraps_and_boundary_clamping_match()
    {
        var value = new WrappedScalar<float>(2.0f, 0.0f, 10.0f);
        var range = value.UpdateWrapped(25.0f);

        Assert.Equal(7.0f, value.Value());
        Assert.True(range.ContainsInclusive(0.0f));
        Assert.True(range.ContainsInclusive(10.0f));

        var restricted = value.MinMaxClamped(5.0f, 8.0f);
        Assert.Equal(5.0f, restricted.Min());
        Assert.Equal(8.0f, restricted.Max());
        Assert.Equal(7.0f, restricted.Value());
    }
}
