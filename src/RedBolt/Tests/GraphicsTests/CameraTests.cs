namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Graphics;
using FillMode = global::RedBolt.Graphics.FillMode;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CameraTests
{
    [Fact]
    public void Borrowed_dependencies_match_cpp_pointer_semantics()
    {
        var camera = new Camera();
        var zone = new Zone();

        camera.SetZone(zone);

        var graphicsReference = GetPrivateField<WeakReference<GraphicsSystem>?>(camera, "_graphics");
        var storedZone = GetPrivateField<Zone?>(camera, "_zone");
        Assert.NotNull(graphicsReference);
        Assert.True(graphicsReference.TryGetTarget(out var graphics));
        Assert.Same(Context.Instance.GetSubsystem<GraphicsSystem>(), graphics);
        Assert.Same(zone, storedZone);
        Assert.Same(zone, camera.GetZone());

        camera.SetZone(null);
        Assert.Null(camera.GetZone());
    }

    [Fact]
    public void Enabled_attribute_updates_live_property()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        Assert.True(camera.SetAttribute("Is Enabled", false));
        Assert.False(camera.Enabled);
        Assert.False(camera.IsEnabledEffective);
    }

    [Fact]
    public void Clip_and_fov_helpers_clamp_values()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetNearClip(-1.0f);
        camera.SetFarClip(200.0f);
        camera.SetFov(200.0f);

        Assert.True(MathDefs.Equals(camera.GetNearClip(), MathDefs.MinNearClip));
        Assert.True(MathDefs.Equals(camera.GetFarClip(), 200.0f));
        Assert.True(MathDefs.Equals(camera.GetFov(), 160.0f));
    }

    [Fact]
    public void Aspect_and_ortho_size_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetAspectRatio(2.0f);
        camera.SetOrthoSize(new Vector2f(40.0f, 20.0f));

        Assert.False(camera.GetAutoAspectRatio());
        Assert.True(camera.IsOrthographic());
        Assert.True(MathDefs.Equals(camera.GetAspectRatio(), 2.0f));
        Assert.True(MathDefs.Equals(camera.GetOrthoSize(), 20.0f));
    }

    [Fact]
    public void Zoom_and_lod_bias_clamp_to_epsilon()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetZoom(0.0f);
        camera.SetLodBias(0.0f);
        Assert.True(MathDefs.Equals(camera.GetZoom(), MathDefs.Epsilon));
        Assert.True(MathDefs.Equals(camera.GetLodBias(), MathDefs.Epsilon));
    }

    [Fact]
    public void View_masks_roundtrip()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetViewMask(0x0fu);
        camera.SetZoneMask(0xf0u);
        camera.SetViewOverrideFlags(ViewOverride.DisableShadows | ViewOverride.DisableOcclusion);

        Assert.Equal(0x0fu, camera.GetViewMask());
        Assert.Equal(0xf0u, camera.GetZoneMask());
        Assert.Equal(ViewOverride.DisableShadows | ViewOverride.DisableOcclusion, camera.GetViewOverrideFlags());
    }

    [Fact]
    public void Projection_and_mouse_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetProjectionOffset(new Vector2f(0.1f, 0.2f));
        camera.SetMousePosition(new Vector2f(0.5f, 0.25f));
        camera.SetOrthographic(true);

        Assert.True(camera.GetProjectionOffset().ApproximatelyEquivalent(new Vector2f(0.1f, 0.2f)));
        Assert.True(camera.HasMousePosition());
        Assert.True(camera.IsOrthographic());
    }

    [Fact]
    public void FillMode_and_distance_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetFillMode(FillMode.Point);
        Assert.Equal(FillMode.Point, camera.GetFillMode());
        Assert.True(MathDefs.Equals(camera.GetDistance(new Vector3f(0.0f, 0.0f, 5.0f)), 5.0f));
        Assert.True(camera.GetLodDistance(100.0f, 1.0f, 1.0f) > 0.0f);
    }

    [Fact]
    public void View_matrix_tracks_camera_transform()
    {
        var scene = new Scene();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -6.0f));
        var camera = cameraNode.CreateComponent<Camera>();

        var transformed = camera.GetView().Matrix4 * new Vector4f(Vector3f.Zero, 1.0f);
        Assert.True(new Vector3f(transformed.X, transformed.Y, transformed.Z)
            .ApproximatelyEquivalent(new Vector3f(0.0f, 0.0f, 6.0f)));
    }

    [Fact]
    public void View_matrix_ignores_camera_node_scale()
    {
        var scene = new Scene();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -6.0f));
        cameraNode.SetScale(4.0f);
        var camera = cameraNode.CreateComponent<Camera>();

        var transformed = camera.GetView().Matrix4 * new Vector4f(Vector3f.Zero, 1.0f);
        Assert.True(new Vector3f(transformed.X, transformed.Y, transformed.Z)
            .ApproximatelyEquivalent(new Vector3f(0.0f, 0.0f, 6.0f)));
    }

    [Fact]
    public void Perspective_projection_matches_upstream_formula()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetAspectRatio(2.0f);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(100.0f);
        camera.SetFov(45.0f);

        var projection = camera.GetProjection(ignoreFlip: true);
        var h = 1.0f / MathF.Tan(45.0f * MathDefs.DegToRad * 0.5f);
        var w = h / 2.0f;
        var q = 100.0f / (100.0f - 0.1f);
        var r = -q * 0.1f;

        Assert.True(MathDefs.Equals(projection.M00, w));
        Assert.True(MathDefs.Equals(projection.M11, h));
        Assert.True(MathDefs.Equals(projection.M22, q));
        Assert.True(MathDefs.Equals(projection.M23, r));
        Assert.True(MathDefs.Equals(projection.M32, 1.0f));
    }

    [Fact]
    public void OpenGL_gpu_projection_matches_upstream_adjustment()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetAspectRatio(2.0f);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(100.0f);
        camera.SetFov(45.0f);

        var projection = camera.GetProjection(ignoreFlip: true);
        var gpuProjection = camera.GetGPUProjection();

        Assert.True(MathDefs.Equals(gpuProjection.M00, projection.M00));
        Assert.True(MathDefs.Equals(gpuProjection.M11, projection.M11));
        Assert.True(MathDefs.Equals(gpuProjection.M20, 2.0f * projection.M20 - projection.M30));
        Assert.True(MathDefs.Equals(gpuProjection.M21, 2.0f * projection.M21 - projection.M31));
        Assert.True(MathDefs.Equals(gpuProjection.M22, 2.0f * projection.M22 - projection.M32));
        Assert.True(MathDefs.Equals(gpuProjection.M23, 2.0f * projection.M23 - projection.M33));
    }

    [Fact]
    public void Projection_flip_vertical_matches_gpu_projection_behavior()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetFlipVertical(true);

        var projection = camera.GetProjection(ignoreFlip: true);
        var flippedProjection = camera.GetProjection();

        Assert.True(MathDefs.Equals(flippedProjection.M11, -projection.M11));
        Assert.True(MathDefs.Equals(flippedProjection.M00, projection.M00));
        Assert.True(MathDefs.Equals(flippedProjection.M22, projection.M22));
    }

    [Fact]
    public void Viewport_type_and_pointer_ownership_match_cpp()
    {
        Assert.False(typeof(Viewport).IsSealed);
        Assert.True(typeof(RedObject).IsAssignableFrom(typeof(Viewport)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Viewport)));

        Assert.Equal(typeof(WeakReference<Scene>), GetPrivateFieldInfo("_scene").FieldType);
        Assert.Equal(typeof(WeakReference<Camera>), GetPrivateFieldInfo("_camera").FieldType);
        Assert.Equal(typeof(WeakReference<Camera>), GetPrivateFieldInfo("_rightEye").FieldType);
        Assert.Equal(typeof(WeakReference<Camera>), GetPrivateFieldInfo("_cullCamera").FieldType);
        Assert.Equal(typeof(WeakReference<RenderPipelineComponent>), GetPrivateFieldInfo("_renderPipeline").FieldType);
        Assert.Equal(typeof(RenderPipelineView), GetPrivateFieldInfo("_renderPipelineView").FieldType);
    }

    [Fact]
    public void Viewport_accepts_and_releases_nullable_cpp_pointer_state()
    {
        using var viewport = new Viewport(null, null);

        Assert.Null(viewport.GetScene());
        Assert.Null(viewport.GetCamera());
        Assert.Null(viewport.GetCullCamera());
        Assert.False(viewport.IsStereo());

        viewport.SetScene(null);
        viewport.SetCamera(null);
        viewport.SetCullCamera(null);
        viewport.SetEye(null, 1);

        Assert.Null(viewport.GetScene());
        Assert.Null(viewport.GetCamera());
        Assert.Null(viewport.GetCullCamera());
        Assert.False(viewport.IsStereo());
    }

    [Fact]
    public void Viewport_managed_state_helpers_work()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var childCamera = scene.CreateChild("ChildCamera").CreateComponent<Camera>();

        viewport.SetDrawDebug(false);
        viewport.SetCullCamera(childCamera);
        viewport.SetEye(childCamera, 1);

        Assert.Equal(new IntRect(0, 0, 320, 200), viewport.GetRect());
        Assert.False(viewport.GetDrawDebug());
        Assert.Same(childCamera, viewport.GetCullCamera());
        Assert.True(viewport.IsStereo());
        Assert.Same(childCamera, viewport.GetEye(1));
    }

    [Fact]
    public void Viewport_recreates_view_when_automatic_pipeline_changes()
    {
        var firstScene = new Scene();
        var firstCamera = firstScene.CreateChild("Camera").CreateComponent<Camera>();
        firstScene.CreateComponent<RenderPipelineComponent>();
        var secondScene = new Scene();
        secondScene.CreateComponent<RenderPipelineComponent>();

        var viewport = new Viewport(firstScene, firstCamera);
        viewport.AllocateView();
        var firstView = viewport.GetRenderPipelineView();
        Assert.NotNull(firstView);

        viewport.SetScene(secondScene);
        Assert.Same(firstView, GetPrivateField<RenderPipelineView>(viewport, "_renderPipelineView"));
        viewport.AllocateView();
        var secondView = viewport.GetRenderPipelineView();

        Assert.NotNull(secondView);
        Assert.NotSame(firstView, secondView);
        Assert.Same(secondScene.GetComponent<RenderPipelineComponent>(), secondView.GetRenderPipeline());
    }

    [Fact]
    public void Viewport_destroys_owned_render_pipeline_view_at_reset_boundaries()
    {
        var scene = new Scene();
        var renderPipeline = new RenderPipelineComponent();
        var firstView = new TrackingRenderPipelineView(renderPipeline);
        var viewport = new Viewport(scene, null);
        SetPrivateField(viewport, "_renderPipelineView", firstView);

        viewport.SetScene(null);

        Assert.True(firstView.IsDisposed);
        Assert.Null(GetPrivateFieldInfo("_renderPipelineView").GetValue(viewport));

        var secondView = new TrackingRenderPipelineView(renderPipeline);
        SetPrivateField(viewport, "_renderPipelineView", secondView);
        viewport.Dispose();

        Assert.True(secondView.IsDisposed);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field.GetValue(instance));
    }

    private static System.Reflection.FieldInfo GetPrivateFieldInfo(string fieldName)
    {
        var field = typeof(Viewport).GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private sealed class TrackingRenderPipelineView : RenderPipelineView
    {
        private FrameInfo _frameInfo;
        private RenderPipelineStats _stats;

        public TrackingRenderPipelineView(RenderPipelineComponent renderPipeline)
            : base(renderPipeline)
        {
        }

        public bool IsDisposed { get; private set; }

        public override bool IsLinearColorSpace => false;

        public override bool Define(RenderSurface? renderTarget, Viewport viewport, GraphicsSystem graphics) => false;

        public override void Update(GraphicsSystem graphics)
        {
        }

        public override void Render(GraphicsSystem graphics, MaterialQuality materialQuality)
        {
        }

        public override ref readonly FrameInfo GetFrameInfo() => ref _frameInfo;

        public override ref readonly RenderPipelineStats GetStats() => ref _stats;

        public override void DrawDebugGeometries(bool depthTest)
        {
        }

        public override void DrawDebugLights(bool depthTest)
        {
        }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }
    }
}
