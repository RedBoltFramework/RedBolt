namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionHarnessTests
{
    [VisualFact]
    public void Native_capture_dimensions_match_viewport_and_reference()
    {
        using var fixture = new VisualRegressionFixture(160, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero, 1.2f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.8f, -3.8f), Vector3f.Zero, 160.0f / 96.0f);

        var actual = fixture.Capture(scene, camera);

        Assert.Equal(160, actual.Width);
        Assert.Equal(96, actual.Height);
        fixture.AssertMatches(actual, "harness/native-capture-dimensions.png");
    }

    [VisualFact]
    public void Clear_color_and_depth_are_reset_between_scenes_and_frames()
    {
        using var fixture = new VisualRegressionFixture(256, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);

        using var firstScene = VisualRegressionSceneFactory.CreateScene(Color.Red, fogStart: 0.0f, fogEnd: 1.0f);
        VisualRegressionSceneFactory.AddModel(
            firstScene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero, 1.1f);
        var firstCamera = VisualRegressionSceneFactory.AddCamera(
            firstScene, new Vector3f(0.0f, 0.8f, -3.8f), Vector3f.Zero, 256.0f / 96.0f);

        using var secondScene = VisualRegressionSceneFactory.CreateScene(Color.Black, fogStart: 0.0f, fogEnd: 1.0f);
        var secondCamera = VisualRegressionSceneFactory.AddCamera(
            secondScene, new Vector3f(0.0f, 0.8f, -3.8f), Vector3f.Zero, 256.0f / 96.0f);

        var first = fixture.Capture(firstScene, firstCamera);
        var second = fixture.Capture(secondScene, secondCamera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(first, "harness/clear-state-first.png"),
            new VisualRegressionImageCapture(second, "harness/clear-state-second.png"));
    }

    [VisualFact]
    public void Warmup_frames_are_not_used_as_the_reference_capture()
    {
        using var fixture = new VisualRegressionFixture(256, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", Vector3f.Zero, 1.0f,
            new Quaternion(17.0f, Vector3f.Up));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.2f), new Vector3f(0.0f, 0.5f, 0.0f), 256.0f / 96.0f);

        var warmed = fixture.Capture(scene, camera);
        var postWarmup = fixture.CaptureFrames(scene, camera, 1, warmupFrames: 0)[0];
        Assert.True(warmed.Compare(postWarmup, 0, 0.0, 0.0).IsMatch,
            "The warmed capture must match the next frame exactly.");

        fixture.AssertMatches(
            new VisualRegressionImageCapture(warmed, "harness/warmup-warmed.png"),
            new VisualRegressionImageCapture(postWarmup, "harness/warmup-post-warmup.png"));
    }

    [VisualFact]
    public void Repeated_native_captures_are_deterministic()
    {
        using var fixture = new VisualRegressionFixture(256, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.0f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.2f), Vector3f.Zero, 256.0f / 96.0f);

        var frames = fixture.CaptureFrames(scene, camera, 2);

        Assert.True(frames[0].Compare(frames[1], 0, 0.0, 0.0).IsMatch,
            "Identical deterministic frames must produce identical native pixels.");
        fixture.AssertMatches(
            new VisualRegressionImageCapture(frames[0], "harness/repeat-determinism-first.png"),
            new VisualRegressionImageCapture(frames[1], "harness/repeat-determinism-second.png"));
    }

    [VisualFact]
    public void Resize_recreates_the_view_and_capture_buffers()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero, 1.2f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.8f, -3.8f), Vector3f.Zero, 128.0f / 96.0f);

        var before = fixture.Capture(scene, camera);
        fixture.Resize(96, 96);
        camera.SetAspectRatio(1.0f);
        var after = fixture.Capture(scene, camera);

        Assert.Equal(96, after.Width);
        Assert.Equal(96, after.Height);
        fixture.AssertMatches(
            new VisualRegressionImageCapture(before, "harness/resize-before.png"),
            new VisualRegressionImageCapture(after, "harness/resize-after.png"));
    }

    [VisualFact]
    public void Multisample_resolve_is_stable_at_a_supported_small_sample_count()
    {
        using var fixture = new VisualRegressionFixture(256, 96, multiSample: 4);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", Vector3f.Zero, 1.0f,
            new Quaternion(23.0f, Vector3f.Up));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.2f), new Vector3f(0.0f, 0.5f, 0.0f), 256.0f / 96.0f);

        var supported = fixture.Graphics.GetSupportedMultiSample(TextureFormat.TexFormatRgba8Unorm, 4);
        Assert.Equal(supported, fixture.Graphics.GetMultiSample());
        var frames = fixture.CaptureFrames(scene, camera, 2);
        Assert.True(frames[0].Compare(frames[1], 0, 0.0, 0.0).IsMatch);
        fixture.AssertMatches(
            new VisualRegressionImageCapture(frames[0], "harness/msaa-resolve-first.png"),
            new VisualRegressionImageCapture(frames[1], "harness/msaa-resolve-second.png"));
    }
}
