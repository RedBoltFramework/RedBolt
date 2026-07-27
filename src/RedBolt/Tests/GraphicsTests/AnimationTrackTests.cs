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
        track.AddKeyFrame(new AnimationKeyFrame(0.0f, Vector3f.Zero, Quaternion.IDENTITY, Vector3f.One));
        track.AddKeyFrame(new AnimationKeyFrame(
            1.0f, new Vector3f(10.0f, 0.0f, 0.0f), new Quaternion(90.0f, Vector3f.Up), new Vector3f(2.0f, 2.0f, 2.0f)));

        uint frameIndex = 0;
        var transform = Transform.Identity;
        track.Sample(0.5f, 1.0f, false, ref frameIndex, ref transform);

        Assert.Equal(5.0f, transform.Position.X);
        Assert.True(transform.Rotation.ApproximatelyEquivalent(new Quaternion(45.0f, Vector3f.Up), 0.001f));
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
            (Time: 0.5f, Value: new Quaternion(45.0f, Vector3f.Up))
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

    [Fact]
    public void Animation_track_clone_is_independent()
    {
        var track = new AnimationTrack
        {
            Name = "Bone",
            NameHash = new StringHash("Bone"),
            ChannelMask = AnimationChannel.Position,
            PositionWeight = 0.25f,
            RotationWeight = 0.5f,
            ScaleWeight = 0.75f
        };
        track.AddKeyFrame(new AnimationKeyFrame(1.0f, Vector3f.One));

        var clone = track.Clone();
        track.Dispose();

        Assert.Equal("Bone", clone.Name);
        Assert.Equal(new StringHash("Bone"), clone.NameHash);
        Assert.Equal(AnimationChannel.Position, clone.ChannelMask);
        Assert.Equal(0.25f, clone.PositionWeight);
        Assert.Equal(0.5f, clone.RotationWeight);
        Assert.Equal(0.75f, clone.ScaleWeight);
        Assert.Single(clone.KeyFrames);
        Assert.Equal(1.0f, clone.KeyFrames[0].Time);
    }

    [Fact]
    public void Variant_animation_track_clone_is_independent()
    {
        var track = new VariantAnimationTrack
        {
            Name = "Attribute",
            NameHash = new StringHash("Attribute"),
            Interpolation = KeyFrameInterpolation.TangentSpline,
            SplineTension = 0.25f,
            Type = VariantType.String,
            Weight = 0.5f
        };
        track.KeyFrames.Add(new VariantCurvePoint { Time = 1.0f, Value = "Value" });
        track.InTangents.Add("In");
        track.OutTangents.Add("Out");

        var clone = track.Clone();
        track.Dispose();

        Assert.Equal("Attribute", clone.Name);
        Assert.Equal(new StringHash("Attribute"), clone.NameHash);
        Assert.Equal(KeyFrameInterpolation.TangentSpline, clone.Interpolation);
        Assert.Equal(0.25f, clone.SplineTension);
        Assert.Equal(VariantType.String, clone.Type);
        Assert.Equal(0.5f, clone.Weight);
        Assert.Equal("Value", clone.KeyFrames[0].Value.GetString());
        Assert.Equal("In", clone.InTangents[0].GetString());
        Assert.Equal("Out", clone.OutTangents[0].GetString());
    }
}
