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

    [Fact]
    public void Drawable_renderer_wide_view_queries_use_renderer_frame_number()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var renderer = new Renderer();
            context.RegisterSubsystem(renderer);
            using var drawable = new CustomGeometry();
            var camera = new Camera();
            renderer.GetFrameInfo().FrameNumber = 4;
            drawable.MarkInView(new CommonFrameInfo { FrameNumber = 4, Cameras = { [0] = camera } });

            Assert.True(drawable.IsInView());
            Assert.True(drawable.IsInView(camera));
            Assert.True(drawable.IsInView(null));

            renderer.GetFrameInfo().FrameNumber = 5;
            Assert.False(drawable.IsInView());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
