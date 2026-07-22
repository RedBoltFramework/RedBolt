using System.Runtime.InteropServices;

namespace RedBolt.Tests;

public sealed class AnimationStateTests
{
    [Fact]
    public void Animation_state_blends_node_tracks()
    {
        var animation = new Animation { Name = "NodeAnimation" };
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
    public void Animation_state_calculates_model_tracks_by_bone_index()
    {
        var animation = new Animation();
        animation.SetLength(1.0f);
        var track = new AnimationTrack();
        track.ChannelMask = AnimationChannel.Position;
        track.AddKeyFrame(new AnimationKeyFrame(0.0f, new Vector3f(0.0f, 0.0f, 0.0f)));
        track.AddKeyFrame(new AnimationKeyFrame(1.0f, new Vector3f(10.0f, 0.0f, 0.0f)));

        var animatedBone = new Bone { Animated = true };
        var disabledBone = new Bone { Animated = false };
        var state = new AnimationState();
        state.Initialize(animation, string.Empty, AnimationBlendMode.Lerp);
        state.Update(false, 1.0f, 1.0f);
        state.AddModelTrack(new ModelAnimationStateTrack
        {
            Track = track,
            Bone = animatedBone,
            BoneIndex = 1
        });
        state.AddModelTrack(new ModelAnimationStateTrack
        {
            Track = track,
            Bone = disabledBone,
            BoneIndex = 0
        });

        var output = new List<ModelAnimationOutput>
        {
            new() { LocalToParent = Transform.Identity },
            new() { LocalToParent = Transform.Identity }
        };

        state.CalculateModelTracks(output);

        Assert.Equal(0.0f, output[0].LocalToParent.Position.X);
        Assert.Equal(10.0f, output[1].LocalToParent.Position.X);
        Assert.Equal(AnimationChannel.Position, output[1].Dirty);
    }

    [Fact]
    public void Animation_state_applies_additive_node_track_delta()
    {
        var animation = new Animation { Name = "AdditiveAnimation" };
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
        var animation = new Animation { Name = "AttributeAnimation" };
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

    [Fact]
    public void Animation_state_tracks_static_property_and_animation_revision_changes()
    {
        using var animation = new Animation();
        using var state = new AnimationState();

        state.Initialize(animation, "Root", AnimationBlendMode.Lerp);
        state.OnTracksReady();
        Assert.False(state.AreTracksDirty());

        state.Initialize(animation, "Spine", AnimationBlendMode.Lerp);
        Assert.True(state.AreTracksDirty());

        state.OnTracksReady();
        state.Initialize(animation, "Spine", AnimationBlendMode.Additive);
        Assert.True(state.AreTracksDirty());

        state.OnTracksReady();
        state.ClearAllTracks();
        Assert.False(state.AreTracksDirty());

        animation.CreateTrack("Node");
        Assert.True(state.AreTracksDirty());
    }

    [Fact]
    public void Animation_state_replaces_owned_attribute_variant_with_an_independent_copy()
    {
        using var animation = new Animation();
        animation.SetLength(1.0f);
        var track = animation.CreateVariantTrack("Value");
        using var keyFrameValue = new CString("sample");
        track.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = new Variant(keyFrameValue) });
        track.Commit();

        var attribute = new AnimatedAttributeReference { AttributeIndex = 1 };
        using var state = new AnimationState();
        state.Initialize(animation, string.Empty, AnimationBlendMode.Lerp);
        state.Update(false, 0.0f, 1.0f);
        state.AddAttributeTrack(new AttributeAnimationStateTrack { Track = track, Attribute = attribute });

        using var oldValue = new CString("old");
        var outputValue = new Variant(oldValue);
        var output = new Dictionary<AnimatedAttributeReference, Variant> { [attribute] = outputValue.Clone() };
        outputValue.Dispose();
        ref var storedOutput = ref CollectionsMarshal.GetValueRefOrNullRef(output, attribute);
        var disposedOutput = storedOutput.GetCString();

        state.CalculateAttributeTracks(output);

        Assert.Equal(0, disposedOutput.ByteLength);
        Assert.Equal("sample", storedOutput.GetCString().ConvertToString());
        Assert.NotSame(track.KeyFrames[0].Value.GetCString(), storedOutput.GetCString());

        storedOutput.Dispose();
    }

    [Fact]
    public void Animated_attribute_reference_sets_animated_model_morph_weight()
    {
        var model = new Model { Name = "MorphAnimation" };
        var skeleton = new Skeleton();
        skeleton.SetNumBones(1);
        skeleton.GetModifiableBones()[0].Name = "Root";
        skeleton.GetModifiableBones()[0].NameHash = new StringHash("Root");
        skeleton.GetModifiableBones()[0].ParentIndex = 0;
        model.SetSkeleton(skeleton);
        model.SetMorphs([new ModelMorph { Name = "Smile", NameHash = new StringHash("Smile") }]);
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, false);

        var attribute = new AnimatedAttributeReference
        {
            Serializable = animatedModel,
            AttributeType = AnimatedAttributeType.AnimatedModelMorphs,
            SubAttributeKey = 0
        };
        var value = new Variant(0.75f);
        attribute.SetValue(value);

        Assert.Equal(0.75f, animatedModel.GetMorphWeight("Smile"));
    }
}
