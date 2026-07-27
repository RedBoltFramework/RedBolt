using RedBolt;
using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class AreaAllocatorTests
{
    [Fact]
    public void Constructors_and_reset_set_dimensions_and_mode()
    {
        var allocator = new AreaAllocator(4, 6, false);

        Assert.Equal(4, allocator.Width);
        Assert.Equal(6, allocator.Height);
        Assert.False(allocator.FastMode);

        allocator.Reset(2, 3, 8, 9, true);

        Assert.Equal(2, allocator.Width);
        Assert.Equal(3, allocator.Height);
        Assert.True(allocator.FastMode);
    }

    [Fact]
    public void Clone_copies_allocator_state_independently()
    {
        var allocator = new AreaAllocator(4, 4, false);
        var x = 0;
        var y = 0;
        Assert.True(allocator.Allocate(2, 2, ref x, ref y));

        var clone = Assert.IsType<AreaAllocator>(((ICloneable)allocator).Clone());

        Assert.True(clone.Allocate(2, 2, ref x, ref y));
        Assert.Equal(2, x);
        Assert.Equal(0, y);
        Assert.True(allocator.Allocate(2, 2, ref x, ref y));
        Assert.Equal(2, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void Fast_allocator_returns_non_overlapping_coordinates()
    {
        var allocator = new AreaAllocator(8, 8);
        var x = -1;
        var y = -1;

        Assert.True(allocator.Allocate(3, 2, ref x, ref y));
        Assert.Equal(0, x);
        Assert.Equal(0, y);
        Assert.True(allocator.Allocate(2, 2, ref x, ref y));
        Assert.Equal(3, x);
        Assert.Equal(0, y);
        Assert.False(allocator.Allocate(6, 7, ref x, ref y));
    }

    [Fact]
    public void Allocator_grows_alternating_dimensions_until_maximum()
    {
        var allocator = new AreaAllocator(2, 2, 4, 4);
        var x = 0;
        var y = 0;

        Assert.True(allocator.Allocate(3, 2, ref x, ref y));
        Assert.Equal(4, allocator.Width);
        Assert.Equal(2, allocator.Height);
        Assert.Equal(0, x);
        Assert.Equal(0, y);

        Assert.True(allocator.Allocate(4, 2, ref x, ref y));
        Assert.Equal(4, allocator.Width);
        Assert.Equal(4, allocator.Height);
        Assert.Equal(0, x);
        Assert.Equal(2, y);
    }

    [Fact]
    public void Precise_allocator_splits_and_reuses_remaining_rectangles()
    {
        var allocator = new AreaAllocator(4, 4, false);
        var x = 0;
        var y = 0;

        Assert.True(allocator.Allocate(2, 2, ref x, ref y));
        Assert.Equal(0, x);
        Assert.Equal(0, y);
        Assert.True(allocator.Allocate(2, 2, ref x, ref y));
        Assert.Equal(2, x);
        Assert.Equal(0, y);
        Assert.True(allocator.Allocate(2, 2, ref x, ref y));
        Assert.Equal(0, x);
        Assert.Equal(2, y);
        Assert.True(allocator.Allocate(2, 2, ref x, ref y));
        Assert.Equal(2, x);
        Assert.Equal(2, y);
        Assert.False(allocator.Allocate(1, 1, ref x, ref y));
    }
}
