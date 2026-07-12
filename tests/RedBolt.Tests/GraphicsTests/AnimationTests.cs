using RedBolt;

namespace RedBolt.Tests;

public sealed class AnimationTests
{
    [Fact]
    public void Animation_tracks_and_triggers_follow_resource_state_rules()
    {
        var animation = new Animation("animation");
        animation.SetAnimationName("walk");
        animation.SetLength(-1.0f);
        Assert.Equal(0.0f, animation.Length);
        Assert.Equal(new StringHash("walk"), animation.AnimationNameHash);

        animation.SetLength(2.0f);
        var track = animation.CreateTrack("root");
        Assert.Same(track, animation.CreateTrack("root"));
        Assert.Equal(1u, animation.NumTracks);

        animation.AddTrigger(1.5f, false, 1);
        animation.AddTrigger(0.5f, true, 2);
        Assert.Equal(1.0f, animation.Triggers[0].Time);
        Assert.Equal(1.5f, animation.Triggers[1].Time);
        Assert.Equal(2, animation.Triggers[0].Data.GetInt());

        animation.SetNumTriggers(3);
        Assert.Equal(3u, animation.NumTriggers);
        animation.RemoveTrigger(2);
        Assert.Equal(2u, animation.NumTriggers);
        Assert.True(animation.RemoveTrack("root"));
        Assert.False(animation.RemoveTrack("root"));
    }

    [Fact]
    public void Animation_loads_xml_tracks_variants_triggers_and_metadata()
    {
        var xml = new XMLFile("walk.xml");
        Assert.True(xml.FromString("""
            <animation name="walk" length="2">
              <transform name="root" position="true" rotation="true" positionweight="0.25">
                <keyframe time="1" position="1 0 0" rotation="0 0 0 1" />
                <keyframe time="0" position="0 0 0" rotation="0 0 0 1" />
              </transform>
              <variant name="speed" type="Float" interpolation="linear">
                <keyframe time="1" value="2" />
                <keyframe time="0" value="1" />
              </variant>
              <trigger normalizedtime="0.5" type="String" value="footstep" />
              <metadata name="source" type="String" value="test" />
            </animation>
            """));

        var animation = new Animation();
        Assert.True(animation.LoadXML(xml.Root));
        Assert.Equal("walk", animation.AnimationName);
        Assert.Equal(2.0f, animation.Length);

        var track = animation.GetTrack("root");
        Assert.NotNull(track);
        Assert.Equal(AnimationChannel.Position | AnimationChannel.Rotation, track.ChannelMask);
        Assert.Equal(0.25f, track.PositionWeight);
        Assert.Equal(0.0f, track.KeyFrames[0].Time);
        Assert.Equal(1.0f, track.KeyFrames[1].Time);

        var variantTrack = animation.GetVariantTrack("speed");
        Assert.NotNull(variantTrack);
        Assert.Equal(VariantType.Float, variantTrack.GetType());
        Assert.Equal(1.0f, variantTrack.KeyFrames[0].Value.GetFloat());
        Assert.Equal(2.0f, variantTrack.KeyFrames[1].Value.GetFloat());

        Assert.Equal(1u, animation.NumTriggers);
        Assert.Equal(1.0f, animation.Triggers[0].Time);
        Assert.Equal("footstep", animation.Triggers[0].Data.GetString());
        Assert.Equal("test", animation.GetMetadata("source").GetString());
    }

    [Fact]
    public void Animation_binary_save_and_load_round_trips_tracks()
    {
        var previousContext = Context.TryGetInstance();
        Context.SetInstance(new Context());

        try
        {
            var animation = new Animation();
            animation.SetAnimationName("walk");
            animation.SetLength(1.0f);
            var track = animation.CreateTrack("root");
            track.ChannelMask = AnimationChannel.Position | AnimationChannel.Scale;
            track.KeyFrames.Add(new AnimationKeyFrame(0.0f, Vector3f.Zero, Quaternion.Identity, Vector3f.One));
            track.KeyFrames.Add(new AnimationKeyFrame(1.0f, new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3f(2.0f, 2.0f, 2.0f)));

            var variantTrack = animation.CreateVariantTrack("speed");
            variantTrack.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 1.0f });
            variantTrack.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 2.0f });
            variantTrack.Commit();

            using var stream = new MemoryStream();
            Assert.True(animation.Save(stream));
            stream.Position = 0;

            var loaded = new Animation();
            Assert.True(loaded.BeginLoad(stream));
            Assert.Equal("walk", loaded.AnimationName);
            Assert.Equal(1.0f, loaded.Length);
            Assert.Equal(1u, loaded.NumTracks);
            Assert.Equal(1u, loaded.NumVariantTracks);
            var loadedTrack = loaded.GetTrack("root");
            var loadedVariantTrack = loaded.GetVariantTrack("speed");
            Assert.NotNull(loadedTrack);
            Assert.NotNull(loadedVariantTrack);
            Assert.Equal(2.0f, loadedTrack!.KeyFrames[1].Position.X);
            Assert.Equal(2.0f, loadedVariantTrack!.KeyFrames[1].Value.GetFloat());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
