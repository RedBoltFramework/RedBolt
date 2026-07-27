using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

public sealed class DrawableTests
{
    public DrawableTests()
    {
        TestTypeRegistration.RegisterFactory<CustomGeometry>();
        TestTypeRegistration.RegisterFactory<UpdateProbeDrawable>();
        TestTypeRegistration.RegisterFactory<Octree>();
    }

    private sealed class UpdateProbeDrawable : Drawable
    {
        public int NumUpdates { get; private set; }
        public int NumBoundingBoxUpdates { get; private set; }

        public override void Update(FrameInfo frame)
        {
            ++NumUpdates;
        }

        public void ResetBoundingBoxUpdates()
        {
            NumBoundingBoxUpdates = 0;
            _worldBoundingBoxDirty = true;
        }

        protected override void OnWorldBoundingBoxUpdate()
        {
            ++NumBoundingBoxUpdates;
        }
    }

    [Fact]
    public void Drawable_exposes_batches_and_mutable_light_probe_hint()
    {
        using var drawable = new CustomGeometry();

        Assert.Single(drawable.GetBatches());

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
        var frame = new FrameInfo
        {
            FrameNumber = 4,
            AdditionalCameras = (camera, null)
        };

        drawable.MarkInView(frame);

        Assert.True(drawable.IsInView(frame));
        Assert.True(drawable.IsInView(frame, anyCamera: true));

        drawable.MarkInView(5u);

        Assert.False(drawable.IsInView(frame));
    }

    [Fact]
    public void Drawable_update_batches_calculates_distance_and_lod_distance()
    {
        var scene = new SceneType();
        var drawable = scene.CreateChild("Drawable").CreateComponent<CustomGeometry>();
        drawable.Node.SetPosition(new Vector3f(5.0f, 0.0f, 0.0f));
        var camera = new Camera();
        var frame = new FrameInfo { Camera = camera };

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
            var frameNumber = renderer.GetFrameInfo().FrameNumber;
            drawable.MarkInView(new FrameInfo { FrameNumber = frameNumber, AdditionalCameras = (camera, null) });

            Assert.True(drawable.IsInView());
            Assert.True(drawable.IsInView(camera));
            Assert.True(drawable.IsInView(null));

            drawable.MarkInView(frameNumber + 1);
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

    [Fact]
    public void Drawable_properties_use_cpp_setter_behavior()
    {
        using var drawable = new CustomGeometry();
        var originalHash = drawable.GetPipelineStateHash();

        drawable.LodBias = 0.0f;
        drawable.GlobalIlluminationType = GlobalIlluminationType.UseLightMap;
        drawable.ZoneMask = 1u;

        Assert.Equal(MathDefs.Epsilon, drawable.GetLodBias());
        Assert.NotEqual(originalHash, drawable.GetPipelineStateHash());
        Assert.True(drawable.IsZoneDirty());
    }

    [Fact]
    public void Drawable_dispose_cancels_queued_octree_update()
    {
        var octree = new SceneType().CreateComponent<Octree>();
        var drawable = new UpdateProbeDrawable();
        octree.AddManualDrawable(drawable);
        Assert.True(drawable.IsInOctree());
        drawable.ResetBoundingBoxUpdates();
        octree.QueueUpdate(drawable);

        drawable.Dispose();
        octree.Update(new FrameInfo());

        Assert.Equal(0, drawable.NumUpdates);
        Assert.Equal(0, drawable.NumBoundingBoxUpdates);
        Assert.False(drawable.IsInOctree());
        Assert.DoesNotContain(drawable, octree.GetAllDrawables());
    }
}
