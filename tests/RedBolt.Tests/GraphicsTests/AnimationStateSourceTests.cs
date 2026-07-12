using RedBolt;

namespace RedBolt.Tests;

public sealed class AnimationStateSourceTests
{
    [Fact]
    public void Animation_state_source_starts_with_empty_states_and_throttling_enabled()
    {
        var source = new TestAnimationStateSource();

        Assert.Empty(source.GetAnimationStates());
        Assert.True(source.IsAnimationThrottlingAllowed());

        source.MarkAnimationStateTracksDirty();

        Assert.True(source.MarkedDirty);
    }

    private sealed class TestAnimationStateSource : AnimationStateSource
    {
        public bool MarkedDirty { get; private set; }

        public override void MarkAnimationStateTracksDirty()
        {
            MarkedDirty = true;
        }
    }
}
