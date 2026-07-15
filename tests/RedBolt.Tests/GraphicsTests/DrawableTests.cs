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
    public void Drawable_update_batches_calculates_distance_and_lod_distance()
    {
        var scene = new Scene();
        var drawable = scene.CreateChild("Drawable").CreateComponent<CustomGeometry>();
        drawable.Node.SetPosition(new Vector3f(5.0f, 0.0f, 0.0f));
        var camera = new Camera();
        var frame = new CommonFrameInfo { Camera = camera };

        drawable.UpdateBatches(frame);

        Assert.Equal(camera.GetDistance(drawable.GetWorldBoundingBox().Center), drawable.GetDistance());
        Assert.Equal(
            camera.GetLodDistance(drawable.GetDistance(), drawable.GetWorldBoundingBox().Size.X / 3.0f +
                drawable.GetWorldBoundingBox().Size.Y / 3.0f + drawable.GetWorldBoundingBox().Size.Z / 3.0f,
                drawable.GetLodBias()),
            drawable.GetLodDistance());
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

    [Fact]
    public void Drawable_exposes_cpp_geometry_update_defaults_and_lod_fallback()
    {
        using var drawable = new CustomGeometry();

        Assert.Equal(UpdateGeometryType.None, drawable.GetUpdateGeometryType());
        Assert.Equal(0u, drawable.GetNumOccluderTriangles());
        Assert.NotNull(drawable.GetLodGeometry(0u, 0u));
        Assert.Null(drawable.GetLodGeometry(1u, 0u));
    }
}
