using System.Threading.Tasks;

using RedBolt.Core;
namespace RedBolt.Tests;

public class ThreadSafeCacheTests
{
    [Fact]
    public void ThreadSafeCache_is_invalidated_by_default_and_restore_sets_value()
    {
        var cache = new ThreadSafeCache<int>();

        Assert.True(cache.IsInvalidated());

        cache.Restore(42);

        Assert.False(cache.IsInvalidated());
        Assert.Equal(42, cache.Get());
    }

    [Fact]
    public void ThreadSafeCache_invalidate_marks_cache_dirty_again()
    {
        var cache = new ThreadSafeCache<string>();

        cache.Restore("value");
        Assert.False(cache.IsInvalidated());

        cache.Invalidate();

        Assert.True(cache.IsInvalidated());
        Assert.Equal("value", cache.Get());
    }

    [Fact]
    public async Task ThreadSafeCache_parallel_restore_keeps_first_restored_value()
    {
        var cache = new ThreadSafeCache<int>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(1, 8)
            .Select(value => Task.Run(async () =>
            {
                await gate.Task;
                cache.Restore(value);
            }))
            .ToArray();

        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.False(cache.IsInvalidated());
        Assert.InRange(cache.Get(), 1, 8);
    }
}
