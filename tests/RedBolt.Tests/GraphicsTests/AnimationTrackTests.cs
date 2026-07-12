using RedBolt;

namespace RedBolt.Tests;

public sealed class AnimationTrackTests
{
    [Fact]
    public void Animation_track_samples_enabled_channels()
    {
        var track = new AnimationTrack
        {
            ChannelMask = AnimationChannel.Position | AnimationChannel.Rotation | AnimationChannel.Scale
        };
        track.AddKeyFrame(new AnimationKeyFrame(0.0f, Vector3f.Zero, Quaternion.Identity, Vector3f.One));
        track.AddKeyFrame(new AnimationKeyFrame(
            1.0f, new Vector3f(10.0f, 0.0f, 0.0f), Quaternion.FromAngleAxis(90.0f, Vector3f.Up), new Vector3f(2.0f, 2.0f, 2.0f)));

        uint frameIndex = 0;
        var transform = Transform.IDENTITY;
        track.Sample(0.5f, 1.0f, false, ref frameIndex, ref transform);

        Assert.Equal(5.0f, transform.Position.X);
        Assert.True(transform.Rotation.ApproximatelyEquivalent(Quaternion.FromAngleAxis(45.0f, Vector3f.Up), 0.001f));
        Assert.Equal(new Vector3f(1.5f, 1.5f, 1.5f), transform.Scale);
        Assert.False(track.IsLooped());
    }

    [Fact]
    public void Animation_track_merges_component_tracks()
    {
        var track = new AnimationTrack();
        var positions = new[]
        {
            (Time: 0.0f, Value: Vector3f.Zero),
            (Time: 1.0f, Value: new Vector3f(10.0f, 0.0f, 0.0f))
        };
        var rotations = new[]
        {
            (Time: 0.5f, Value: Quaternion.FromAngleAxis(45.0f, Vector3f.Up))
        };

        track.CreateMerged(AnimationChannel.Position | AnimationChannel.Rotation, positions, rotations, [], 0.001f);

        Assert.Equal(3, track.KeyFrames.Count);
        Assert.Equal(0.0f, track.KeyFrames[0].Time);
        Assert.Equal(0.5f, track.KeyFrames[1].Time);
        Assert.Equal(5.0f, track.KeyFrames[1].Position.X);
        Assert.InRange(track.KeyFrames[1].Rotation.EulerAngles.Y, 44.99f, 45.01f);
        Assert.Equal(1.0f, track.KeyFrames[2].Time);
    }

    [Fact]
    public void Variant_animation_track_reports_loop_state()
    {
        var track = new VariantAnimationTrack();
        Assert.True(track.IsLooped());

        track.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 1.0f });
        track.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 1.0f });
        Assert.True(track.IsLooped());

        track.KeyFrames[1] = new VariantCurvePoint { Time = 1.0f, Value = 2.0f };
        Assert.False(track.IsLooped());
    }
}
