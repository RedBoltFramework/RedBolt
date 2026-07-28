namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;

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

    [Fact]
    public void Animation_state_source_exposes_live_shared_states_and_releases_them_on_destruction()
    {
        var source = new TestAnimationStateSource();
        using var state = new AnimationState();
        var states = source.GetAnimationStates();

        source.AddAnimationState(state);

        Assert.Single(states);
        Assert.Same(state, states[0]);

        source.Dispose();

        Assert.Empty(states);
    }

    [Fact]
    public void Animation_state_preserves_lifecycle_and_track_dirty_state()
    {
        var animation = new Animation();
        var state = new AnimationState();
        var track = new AnimationTrack();

        state.Initialize(animation, "Root", AnimationBlendMode.Additive);
        state.Update(true, 1.5f, 0.75f);

        Assert.Same(animation, state.GetAnimation());
        Assert.Equal("Root", state.GetStartBone());
        Assert.True(state.IsLooped());
        Assert.True(state.IsEnabled());
        Assert.Equal(0.0f, state.GetTime());
        Assert.Equal(0.75f, state.GetWeight());
        Assert.Equal(AnimationBlendMode.Additive, state.GetBlendMode());
        Assert.True(state.AreTracksDirty());

        state.AddModelTrack(new ModelAnimationStateTrack { Track = track });
        state.OnTracksReady();
        Assert.False(state.AreTracksDirty());

        state.MarkTracksDirty();
        state.ClearAllTracks();
        Assert.True(state.AreTracksDirty());
    }

    private sealed class TestAnimationStateSource : AnimationStateSource
    {
        public bool MarkedDirty { get; private set; }

        public void AddAnimationState(AnimationState state)
        {
            _animationStates.Add(state);
        }

        public override void MarkAnimationStateTracksDirty()
        {
            MarkedDirty = true;
        }
    }
}
