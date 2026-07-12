using Xunit;

namespace RedBolt.Tests;

public sealed class PipelineStateTrackerTests
{
    [Fact]
    public void Pipeline_state_hash_is_cached_and_dependencies_invalidate_it()
    {
        var dependency = new TestTracker(11);
        var subscriber = new TestTracker(13);
        using var subscription = subscriber.AddDependency(dependency);

        Assert.Equal(11u, dependency.GetPipelineStateHash());
        Assert.Equal(11u, dependency.GetPipelineStateHash());
        Assert.Equal(1, dependency.RecalculationCount);

        Assert.Equal(13u, subscriber.GetPipelineStateHash());
        Assert.Equal(1, subscriber.RecalculationCount);

        dependency.MarkPipelineStateHashDirty();
        Assert.Equal(13u, subscriber.GetPipelineStateHash());
        Assert.Equal(2, subscriber.RecalculationCount);
    }

    [Fact]
    public void Pipeline_state_subscription_clone_keeps_dependency_reference()
    {
        var dependency = new TestTracker(17);
        var subscriber = new TestTracker(19);
        using var subscription = subscriber.AddDependency(dependency);
        using var clone = (PipelineStateSubscription)subscription.Clone();

        dependency.GetPipelineStateHash();
        subscriber.GetPipelineStateHash();
        var recalcCount = subscriber.RecalculationCount;
        dependency.MarkPipelineStateHashDirty();

        subscriber.GetPipelineStateHash();
        Assert.Equal(recalcCount + 1, subscriber.RecalculationCount);
    }

    private sealed class TestTracker(uint hash) : PipelineStateTracker
    {
        public int RecalculationCount { get; private set; }

        public PipelineStateSubscription AddDependency(PipelineStateTracker dependency) => CreateDependency(dependency);

        protected override uint RecalculatePipelineStateHash()
        {
            ++RecalculationCount;
            return hash;
        }
    }
}
