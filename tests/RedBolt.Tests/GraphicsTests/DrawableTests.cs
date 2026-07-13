namespace RedBolt.Tests;

public sealed class DrawableTests
{
    [Fact]
    public void Drawable_exposes_batches_and_mutable_light_probe_hint()
    {
        using var drawable = new CustomGeometry();

        Assert.Empty(drawable.GetBatches());

        ref var hint = ref drawable.GetMutableLightProbeTetrahedronHint();
        Assert.Equal(uint.MaxValue, hint);

        hint = 12u;

        Assert.Equal(12u, drawable.GetMutableLightProbeTetrahedronHint());
    }

    [Fact]
    public void Drawable_tracks_view_cameras_by_frame_number()
    {
        using var drawable = new CustomGeometry();
        var camera = new Camera();
        var frame = new CommonFrameInfo
        {
            FrameNumber = 4
        };
        frame.Cameras[0] = camera;

        drawable.MarkInView(frame);

        Assert.True(drawable.IsInView(frame));
        Assert.True(drawable.IsInView(frame, anyCamera: true));

        drawable.MarkInView(5u);

        Assert.False(drawable.IsInView(frame));
    }
}
