using RedBolt;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Tests;

public sealed class NumericRangeTests
{
    [Fact]
    public void Range_is_sequential_reference_free_value_type()
    {
        Assert.True(typeof(NumericRange<int>).IsValueType);
        Assert.Equal(LayoutKind.Sequential, typeof(NumericRange<int>).StructLayoutAttribute!.Value);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<NumericRange<int>>());
        Assert.Equal(8, Unsafe.SizeOf<NumericRange<int>>());
    }

    [Fact]
    public void Default_range_is_invalid()
    {
        var range = new FloatRange();

        Assert.False(range.IsValid());
        Assert.Equal(float.MaxValue, range.First);
        Assert.Equal(float.MinValue, range.Second);
    }

    [Fact]
    public void Valid_range_checks_intersection_and_boundaries()
    {
        var range = new NumericRange<int>(2, 5);

        Assert.True(range.IsValid());
        Assert.True(range.Intersect(new NumericRange<int>(5, 9)));
        Assert.False(range.Intersect(new NumericRange<int>(6, 9)));
        Assert.True(range.ContainsInclusive(2));
        Assert.True(range.ContainsInclusive(5));
        Assert.False(range.ContainsExclusive(2));
        Assert.True(range.ContainsExclusive(3));
        Assert.False(range.ContainsExclusive(5));
    }

    [Fact]
    public void Union_and_intersection_accumulate_and_trim_ranges()
    {
        var lhs = new NumericRange<int>(2, 5);
        var rhs = new NumericRange<int>(4, 9);

        var union = lhs | rhs;
        var intersection = lhs & rhs;

        Assert.Equal(new NumericRange<int>(2, 9), union);
        Assert.Equal(new NumericRange<int>(4, 5), intersection);
        Assert.Equal(new NumericRange<int>(2, 5), lhs);
        Assert.Equal(new NumericRange<int>(4, 9), rhs);
    }

    [Fact]
    public void Unsigned_range_alias_preserves_numeric_type()
    {
        UintRange range = new(2u, 5u);

        Assert.Equal(2u, range.First);
        Assert.Equal(5u, range.Second);
        Assert.True(range.ContainsInclusive(3u));
    }
}
