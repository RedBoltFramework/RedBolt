using RedBolt;

namespace RedBolt.Tests;

public sealed class IndexAllocatorTests
{
    [Fact]
    public void Allocates_non_zero_indices_and_reuses_released_indices()
    {
        var allocator = new IndexAllocator();

        Assert.Equal(1u, allocator.GetNextFreeIndex());
        Assert.Equal(1u, allocator.Allocate());
        Assert.Equal(2u, allocator.Allocate());
        Assert.Equal(3u, allocator.GetNextFreeIndex());
        Assert.Equal(3u, allocator.GetSize());

        allocator.Release(1);

        Assert.Equal(2u, allocator.GetSize());
        Assert.Equal(1u, allocator.Allocate());
    }

    [Fact]
    public void Shrink_and_clear_reset_unused_tail_indices()
    {
        var allocator = new IndexAllocator();
        allocator.Allocate();
        allocator.Allocate();
        allocator.Allocate();
        allocator.Release(1);
        allocator.Release(2);
        allocator.Release(3);

        allocator.Shrink();

        Assert.Equal(1u, allocator.GetNextFreeIndex());
        Assert.Equal(1u, allocator.GetSize());

        allocator.Clear();

        Assert.Equal(1u, allocator.GetNextFreeIndex());
        Assert.Equal(1u, allocator.GetSize());
    }

    [Fact]
    public void IDFamily_assigns_one_id_per_instance_and_releases_it()
    {
        var first = new IDFamily<IndexAllocatorTests>();
        var second = new IDFamily<IndexAllocatorTests>();

        Assert.NotEqual(0u, first.GetObjectID());
        Assert.NotEqual(first.GetObjectID(), second.GetObjectID());
        Assert.Equal(3u, IDFamily<IndexAllocatorTests>.GetNextFreeObjectID());

        first.ReleaseObjectID();
        first.ReleaseObjectID();

        Assert.Equal(0u, first.GetObjectID());
        Assert.Equal(3u, IDFamily<IndexAllocatorTests>.GetNextFreeObjectID());

        second.ReleaseObjectID();

        Assert.Equal(2u, IDFamily<IndexAllocatorTests>.GetNextFreeObjectID());
    }
}
