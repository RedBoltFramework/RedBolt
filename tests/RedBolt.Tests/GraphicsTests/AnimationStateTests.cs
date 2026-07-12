namespace RedBolt.Tests;

public sealed class AnimationStateTests
{
    [Fact]
    public void Animation_state_blends_node_tracks()
    {
        var animation = new Animation("NodeAnimation");
        animation.SetLength(1.0f);
        var track = animation.CreateTrack("Node");
        track.ChannelMask = AnimationChannel.Position;
        track.AddKeyFrame(new AnimationKeyFrame(0.0f, Vector3f.Zero));
        track.AddKeyFrame(new AnimationKeyFrame(1.0f, new Vector3f(10.0f, 0.0f, 0.0f)));

        var scene = new Scene();
        var node = scene.CreateChild("Node");

        var firstState = new AnimationState();
        firstState.Initialize(animation, string.Empty, AnimationBlendMode.Lerp);
        firstState.Update(false, 0.5f, 1.0f);
        firstState.AddNodeTrack(new NodeAnimationStateTrack { Track = track, Node = node });

        var secondState = new AnimationState();
        secondState.Initialize(animation, string.Empty, AnimationBlendMode.Lerp);
        secondState.Update(false, 1.0f, 0.5f);
        secondState.AddNodeTrack(new NodeAnimationStateTrack { Track = track, Node = node });

        var output = new Dictionary<Node, NodeAnimationOutput>();
        firstState.CalculateNodeTracks(output);
        secondState.CalculateNodeTracks(output);

        Assert.Equal(7.5f, output[node].LocalToParent.Position.X);
        Assert.Equal(AnimationChannel.Position, output[node].Dirty);
    }

    [Fact]
    public void Animation_state_applies_additive_node_track_delta()
    {
        var animation = new Animation("AdditiveAnimation");
        animation.SetLength(1.0f);
        var track = animation.CreateTrack("Node");
        track.ChannelMask = AnimationChannel.Position;
        track.AddKeyFrame(new AnimationKeyFrame(0.0f, new Vector3f(2.0f, 0.0f, 0.0f)));
        track.AddKeyFrame(new AnimationKeyFrame(1.0f, new Vector3f(6.0f, 0.0f, 0.0f)));

        var scene = new Scene();
        var node = scene.CreateChild("Node");
        var state = new AnimationState();
        state.Initialize(animation, string.Empty, AnimationBlendMode.Additive);
        state.Update(false, 1.0f, 0.5f);
        state.AddNodeTrack(new NodeAnimationStateTrack { Track = track, Node = node });

        var output = new Dictionary<Node, NodeAnimationOutput>
        {
            [node] = new NodeAnimationOutput
            {
                Dirty = AnimationChannel.Position,
                LocalToParent = Transform.Identity
            }
        };
        state.CalculateNodeTracks(output);

        Assert.Equal(2.0f, output[node].LocalToParent.Position.X);
        Assert.Equal(AnimationChannel.Position, output[node].Dirty);
    }

    [Fact]
    public void Animation_state_blends_attribute_tracks()
    {
        var animation = new Animation("AttributeAnimation");
        animation.SetLength(1.0f);
        var track = animation.CreateVariantTrack("Value");
        track.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = new Variant(2.0f) });
        track.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = new Variant(6.0f) });

        var attribute = new AnimatedAttributeReference { AttributeIndex = 1 };
        var state = new AnimationState();
        state.Initialize(animation, string.Empty, AnimationBlendMode.Lerp);
        state.Update(false, 0.5f, 1.0f);
        state.AddAttributeTrack(new AttributeAnimationStateTrack { Track = track, Attribute = attribute });

        var output = new Dictionary<AnimatedAttributeReference, Variant>();
        state.CalculateAttributeTracks(output);

        Assert.Equal(4.0f, output[attribute].GetFloat());
    }
}
