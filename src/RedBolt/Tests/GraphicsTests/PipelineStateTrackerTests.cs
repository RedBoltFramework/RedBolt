using Xunit;

using RedBolt.Graphics;
namespace RedBolt.Tests;

public sealed class PipelineStateTrackerTests
{
    [Fact]
    public void Tracker_and_subscription_preserve_cpp_class_types_and_value_subscriber_record()
    {
        Assert.False(typeof(PipelineStateTracker).IsValueType);
        Assert.False(typeof(PipelineStateSubscription).IsValueType);
        Assert.False(typeof(PipelineStateSubscription).IsSealed);

        var subscriberReference = typeof(PipelineStateTracker).GetNestedType("SubscriberReference",
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(subscriberReference);
        Assert.True(subscriberReference.IsValueType);
    }

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

    [Fact]
    public void Pipeline_state_subscription_copy_assignment_relinks_reference_counts()
    {
        var firstDependency = new TestTracker(23);
        var secondDependency = new TestTracker(29);
        var subscriber = new TestTracker(31);
        using var firstSubscription = subscriber.AddDependency(firstDependency);
        using var secondSubscription = subscriber.AddDependency(secondDependency);

        firstDependency.GetPipelineStateHash();
        secondDependency.GetPipelineStateHash();
        subscriber.GetPipelineStateHash();
        secondSubscription.CopyAssign(firstSubscription);
        subscriber.GetPipelineStateHash();
        var recalcCount = subscriber.RecalculationCount;

        secondDependency.MarkPipelineStateHashDirty();
        subscriber.GetPipelineStateHash();
        Assert.Equal(recalcCount, subscriber.RecalculationCount);

        firstDependency.MarkPipelineStateHashDirty();
        subscriber.GetPipelineStateHash();
        Assert.Equal(recalcCount + 1, subscriber.RecalculationCount);
    }

    [Fact]
    public void Pipeline_state_subscription_move_assignment_swaps_and_releases_old_reference()
    {
        var firstDependency = new TestTracker(37);
        var secondDependency = new TestTracker(41);
        var subscriber = new TestTracker(43);
        using var target = subscriber.AddDependency(firstDependency);
        var source = subscriber.AddDependency(secondDependency);

        target.MoveAssign(source);
        source.Dispose();
        firstDependency.GetPipelineStateHash();
        secondDependency.GetPipelineStateHash();
        subscriber.GetPipelineStateHash();
        var recalcCount = subscriber.RecalculationCount;

        firstDependency.MarkPipelineStateHashDirty();
        subscriber.GetPipelineStateHash();
        Assert.Equal(recalcCount, subscriber.RecalculationCount);

        secondDependency.MarkPipelineStateHashDirty();
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
