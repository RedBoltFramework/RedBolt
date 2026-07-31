namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionCameraTests
{
    [VisualFact]
    public void Camera_near_far_and_side_planes_clip_geometry_at_the_expected_limits()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);

        AddClippingModel(scene, fixture.Resources, "Models/Box.mdl", new Vector3f(-2.2f, 0.0f, -4.7f), 1.2f);
        AddClippingModel(scene, fixture.Resources, "Models/Sphere.mdl", new Vector3f(0.0f, 0.0f, -3.0f), 0.8f);
        AddClippingModel(scene, fixture.Resources, "Models/Cone.mdl", new Vector3f(2.2f, 0.0f, -1.3f), 1.2f);
        AddClippingModel(scene, fixture.Resources, "Models/TeaPot.mdl", new Vector3f(0.0f, 0.0f, -7.0f), 0.8f);
        AddClippingModel(scene, fixture.Resources, "Models/Cylinder.mdl", new Vector3f(4.0f, 0.0f, -3.0f), 0.8f);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -6.0f), Vector3f.Zero, 256.0f / 128.0f, fov: 42.0f);
        camera.SetNearClip(1.5f);
        camera.SetFarClip(5.0f);

        fixture.AssertMatches(scene, camera, "camera/near-far-clipping.png");
    }

    [VisualFact]
    public void Camera_flip_projection_offset_and_zoom_preserve_asymmetric_geometry()
    {
        using var fixture = new VisualRegressionFixture();
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", Vector3f.Zero, 1.0f,
            new Quaternion(31.0f, Vector3f.Up));

        var baseCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.2f), new Vector3f(0.0f, 0.55f, 0.0f), 128.0f / 96.0f);
        var flippedCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.2f), new Vector3f(0.0f, 0.55f, 0.0f), 128.0f / 96.0f);
        flippedCamera.SetFlipVertical(true);
        var offsetCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.2f), new Vector3f(0.0f, 0.55f, 0.0f), 128.0f / 96.0f);
        offsetCamera.SetProjectionOffset(new Vector2f(0.18f, -0.12f));
        var zoomCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.2f), new Vector3f(0.0f, 0.55f, 0.0f), 128.0f / 96.0f);
        zoomCamera.SetZoom(1.45f);

        fixture.AssertMatches(
            new VisualRegressionCapture(scene, baseCamera, "camera/projection-base.png"),
            new VisualRegressionCapture(scene, flippedCamera, "camera/projection-flipped.png"),
            new VisualRegressionCapture(scene, offsetCamera, "camera/projection-offset.png"),
            new VisualRegressionCapture(scene, zoomCamera, "camera/projection-zoom.png"));
    }

    [VisualFact]
    public void Camera_clip_and_reflection_planes_change_the_visible_scene()
    {
        using var fixture = new VisualRegressionFixture();
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        AddClippingModel(scene, fixture.Resources, "Models/Box.mdl", new Vector3f(-0.9f, 0.0f, 0.0f), 0.85f);
        AddClippingModel(scene, fixture.Resources, "Models/TeaPot.mdl", new Vector3f(0.9f, 0.6f, 0.0f), 0.85f);

        var baseCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.5f), Vector3f.Zero, 128.0f / 96.0f);
        var clipCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.5f), Vector3f.Zero, 128.0f / 96.0f);
        clipCamera.SetUseClipping(true);
        clipCamera.SetClipPlane(new Plane(Vector3f.Right, Vector3f.Zero));
        var reflectionCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.5f), Vector3f.Zero, 128.0f / 96.0f);
        reflectionCamera.SetUseReflection(true);
        reflectionCamera.SetReflectionPlane(new Plane(Vector3f.Up, Vector3f.Zero));

        fixture.AssertMatches(
            new VisualRegressionCapture(scene, baseCamera, "camera/clip-reflection-base.png"),
            new VisualRegressionCapture(scene, clipCamera, "camera/clip-plane.png"),
            new VisualRegressionCapture(scene, reflectionCamera, "camera/reflection-plane.png"));
    }

    [VisualFact]
    public void Camera_view_override_flags_control_material_quality_shadows_and_occlusion()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.12f, 0.12f, 0.12f));
        var floor = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Plane.mdl", "Materials/StoneTiled.xml",
            new Vector3f(0.0f, -1.0f, 1.0f), 4.0f);
        floor.SetOccluder(true);
        var box = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.1f);
        box.SetCastShadows(true);
        box.SetOccludee(true);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f), Vector3f.Zero,
            Color.White, brightness: 1.5f, castShadows: true);

        var normal = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.5f, 2.8f, -6.0f), Vector3f.Zero, 256.0f / 128.0f);
        var lowQuality = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.5f, 2.8f, -6.0f), Vector3f.Zero, 256.0f / 128.0f);
        lowQuality.SetViewOverrideFlags(ViewOverride.LowMaterialQuality);
        var noShadows = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.5f, 2.8f, -6.0f), Vector3f.Zero, 256.0f / 128.0f);
        noShadows.SetViewOverrideFlags(ViewOverride.DisableShadows);
        var noOcclusion = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.5f, 2.8f, -6.0f), Vector3f.Zero, 256.0f / 128.0f);
        noOcclusion.SetViewOverrideFlags(ViewOverride.DisableOcclusion);

        fixture.AssertMatches(
            new VisualRegressionCapture(scene, normal, "camera/overrides-normal.png"),
            new VisualRegressionCapture(scene, lowQuality, "camera/overrides-low-quality.png"),
            new VisualRegressionCapture(scene, noShadows, "camera/overrides-no-shadows.png"),
            new VisualRegressionCapture(scene, noOcclusion, "camera/overrides-no-occlusion.png"));
    }

    [VisualFact]
    public void Multiple_viewports_render_independent_scenes_without_state_leakage()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var redScene = VisualRegressionSceneFactory.CreateScene(Color.Red, fogStart: 0.0f, fogEnd: 1.0f);
        using var blueScene = VisualRegressionSceneFactory.CreateScene(Color.Blue, fogStart: 0.0f, fogEnd: 1.0f);
        VisualRegressionSceneFactory.AddModel(
            redScene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero, 1.0f);
        VisualRegressionSceneFactory.AddModel(
            blueScene, fixture.Resources, "Models/Sphere.mdl", "Materials/FirstCube", Vector3f.Zero, 1.0f);
        var redCamera = VisualRegressionSceneFactory.AddCamera(
            redScene, new Vector3f(0.0f, 0.8f, -3.8f), Vector3f.Zero, 128.0f / 96.0f);
        var blueCamera = VisualRegressionSceneFactory.AddCamera(
            blueScene, new Vector3f(0.0f, 0.8f, -3.8f), Vector3f.Zero, 128.0f / 96.0f);

        fixture.AssertMatches(
            new VisualRegressionCapture(redScene, redCamera, "camera/multiple-viewport-red.png"),
            new VisualRegressionCapture(blueScene, blueCamera, "camera/multiple-viewport-blue.png"));
    }

    [VisualFact]
    public void Separate_culling_camera_covers_the_render_camera_frustum()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", new Vector3f(-1.2f, 0.0f, 0.0f), 0.9f);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", new Vector3f(1.2f, 0.5f, 1.0f), 0.9f);
        var renderCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.1f, -5.0f), Vector3f.Zero, 256.0f / 128.0f, fov: 35.0f);
        var cullCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -5.0f), new Vector3f(0.0f, 0.3f, 0.8f),
            256.0f / 128.0f, fov: 65.0f);

        fixture.AssertMatches("camera/separate-cull-camera.png",
            new VisualRegressionView(scene, renderCamera, new IntRect(0, 0, 256, 128),
                CullCamera: cullCamera));
    }

    [VisualFact]
    public void Stereo_viewport_renders_left_and_right_eye_cameras()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", Vector3f.Zero, 1.0f,
            new Quaternion(21.0f, Vector3f.Up));
        var left = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(-0.08f, 1.0f, -4.2f), Vector3f.Zero, 256.0f / 128.0f);
        var right = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.08f, 1.0f, -4.2f), Vector3f.Zero, 256.0f / 128.0f);

        fixture.AssertMatches("camera/stereo-eyes.png",
            new VisualRegressionView(scene, left, new IntRect(0, 0, 256, 128), RightEye: right));
    }

    [VisualFact]
    public void Camera_operator_reframes_tracked_nodes_at_fixed_steps()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var first = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", new Vector3f(-1.5f, 0.0f, 0.0f), 0.8f);
        var second = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", new Vector3f(1.5f, 0.6f, 0.8f), 0.8f);
        var cameraNode = scene.CreateChild("OperatorCamera");
        cameraNode.SetPosition(new Vector3f(0.0f, 1.0f, -4.5f));
        cameraNode.LookAt(Vector3f.Zero, Vector3f.Up);
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetAspectRatio(256.0f / 128.0f);
        camera.SetFov(40.0f);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(100.0f);
        var cameraOperator = cameraNode.CreateComponent<CameraOperator>();
        cameraOperator.TrackNode(first.Node);

        var before = fixture.Capture(scene, camera);
        cameraOperator.TrackNode(second.Node);
        cameraOperator.SetUniformPadding(0.25f);
        cameraOperator.MoveCamera();
        var after = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(before, "camera/operator-before.png"),
            new VisualRegressionImageCapture(after, "camera/operator-after.png"));
    }

    [VisualFact]
    public void Camera_fill_modes_render_solid_wireframe_and_points()
    {
        using var fixture = new VisualRegressionFixture(384, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", Vector3f.Zero, 1.1f,
            new Quaternion(24.0f, Vector3f.Up));

        var views = new VisualRegressionView[3];
        var fillModes = new[] { FillMode.Solid, FillMode.Wireframe, FillMode.Point };
        for (var i = 0; i < fillModes.Length; ++i)
        {
            var camera = VisualRegressionSceneFactory.AddCamera(
                scene, new Vector3f(0.0f, 1.1f, -4.2f), new Vector3f(0.0f, 0.7f, 0.0f), 128.0f / 128.0f,
                fov: 35.0f);
            camera.SetFillMode(fillModes[i]);
            views[i] = new VisualRegressionView(scene, camera, new IntRect(i * 128, 0, 128, 128));
        }

        fixture.AssertMatches("camera/fill-modes.png", views);
    }

    [VisualFact]
    public void Camera_view_masks_isolate_selected_drawables()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);

        var left = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube",
            new Vector3f(-1.3f, 0.0f, 0.0f), 0.9f);
        left.SetViewMask(1u);
        var right = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube",
            new Vector3f(1.3f, 0.0f, 0.0f), 0.9f);
        right.SetViewMask(2u);

        var firstCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.8f, -4.2f), Vector3f.Zero, 128.0f / 128.0f);
        firstCamera.SetViewMask(1u);
        var secondCamera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.8f, -4.2f), Vector3f.Zero, 128.0f / 128.0f);
        secondCamera.SetViewMask(2u);

        fixture.AssertMatches("camera/view-masks.png",
            new VisualRegressionView(scene, firstCamera, new IntRect(0, 0, 128, 128)),
            new VisualRegressionView(scene, secondCamera, new IntRect(128, 0, 128, 128)));
    }

    [VisualFact]
    public void Viewport_rectangle_preserves_a_clear_border()
    {
        using var fixture = new VisualRegressionFixture();
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero, 1.2f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.8f, -3.8f), Vector3f.Zero, 96.0f / 80.0f);

        fixture.AssertMatches("camera/viewport-border.png",
            new VisualRegressionView(scene, camera, new IntRect(16, 8, 96, 80)));
    }

    private static void AddClippingModel(
        Scene scene, ResourceCache resources, string modelName, Vector3f position, float scale)
    {
        VisualRegressionSceneFactory.AddModel(
            scene, resources, modelName, "Materials/FirstCube", position, scale,
            new Quaternion(position.X * 13.0f + position.Z * 7.0f, Vector3f.Up));
    }
}
