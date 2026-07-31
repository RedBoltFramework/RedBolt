namespace RedBolt.Tests;

using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RenderAPI;
using RedBolt.Scene;

public sealed class VisualRegressionTests
{
    [Fact]
    public void Fuzzy_comparison_accepts_small_pixel_variation()
    {
        var expected = VisualRegressionImage.Solid(4, 3, 32, 64, 96);
        var actualPixels = (byte[])expected.Pixels.Clone();
        actualPixels[0] += 3;
        actualPixels[1] -= 2;
        var actual = VisualRegressionImage.FromRgba(4, 3, actualPixels);

        var comparison = actual.Compare(expected);

        Assert.True(comparison.IsMatch, comparison.ToString());
        Assert.Equal(0.0, comparison.DifferingPixelRatio);
        Assert.InRange(comparison.AverageChannelError, 0.0, 2.0);
    }

    [Fact]
    public void Fuzzy_comparison_rejects_large_pixel_region()
    {
        var expected = VisualRegressionImage.Solid(8, 8, 0, 0, 0);
        var actualPixels = (byte[])expected.Pixels.Clone();
        for (var i = 0; i < actualPixels.Length / 2; i += 4)
            actualPixels[i] = byte.MaxValue;
        var actual = VisualRegressionImage.FromRgba(8, 8, actualPixels);

        var comparison = actual.Compare(expected);

        Assert.False(comparison.IsMatch, comparison.ToString());
        Assert.True(comparison.DifferingPixelRatio > 0.4);
    }

    [VisualFact]
    public void Static_scenes_capture_low_resolution_reference()
    {
        using var fixture = new GraphicsEngineFixture();
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.VisualRegression",
            RenderBackend = RenderBackend.OpenGL,
            FullScreen = false,
            Borderless = false,
            MultiSample = 1,
            VSync = false,
            WindowHidden = true,
            WindowMaximize = false,
            WindowWidth = 96,
            WindowHeight = 64
        };

        RbfxTestData.AddResourceDirs(fixture.Resources);
        try
        {
            fixture.InitializeGraphics(parameters);
            using var redScene = CreateStaticScene("Red", Color.Red, 96.0f / 64.0f, out var redCamera);
            using var greenScene = CreateStaticScene("Green", Color.Green, 96.0f / 64.0f, out var greenCamera);
            using var blueScene = CreateStaticScene("Blue", Color.Blue, 96.0f / 64.0f, out var blueCamera);

            fixture.Renderer.SetNumViewports(3);
            using var redViewport = new Viewport(redScene, redCamera, new IntRect(0, 0, 32, 64));
            using var greenViewport = new Viewport(greenScene, greenCamera, new IntRect(32, 0, 64, 64));
            using var blueViewport = new Viewport(blueScene, blueCamera, new IntRect(64, 0, 96, 64));
            fixture.Renderer.SetViewport(0, redViewport);
            fixture.Renderer.SetViewport(1, greenViewport);
            fixture.Renderer.SetViewport(2, blueViewport);

            Assert.True(fixture.Graphics.BeginFrame());
            try
            {
                fixture.Renderer.Update(0.0f);
                fixture.Renderer.Render();
                fixture.Graphics.ResetRenderTargets();
            }
            finally
            {
                fixture.Graphics.EndFrame();
            }

            using var screenshot = new Image();
            Assert.True(fixture.Graphics.TakeScreenShot(screenshot));
            var actual = VisualRegressionImage.FromImage(screenshot).Resize(48, 32);
            var referenceFile = VisualRegressionReferences.GetPath("static-scenes.ppm");
            if (VisualRegressionReferences.UpdateReferences)
            {
                actual.SavePpm(referenceFile);
                return;
            }

            var expected = VisualRegressionImage.Load(referenceFile);
            var comparison = actual.Compare(expected);
            if (!comparison.IsMatch)
            {
                var artifactDirectory = VisualRegressionReferences.GetArtifactDirectory();
                actual.SavePng(Path.Combine(artifactDirectory, "static-scenes.actual.png"));
                if (actual.Width == expected.Width && actual.Height == expected.Height)
                    actual.CreateDifference(expected).SavePng(
                        Path.Combine(artifactDirectory, "static-scenes.diff.png"));
            }

            Assert.True(comparison.IsMatch,
                $"Static scene screenshot did not match '{referenceFile}': {comparison}. "
                + $"Artifacts: {VisualRegressionReferences.GetArtifactDirectory()}");
        }
        finally
        {
            fixture.Renderer.SetNumViewports(0);
            RbfxTestData.RemoveResourceDirs(fixture.Resources);
        }
    }

    private static Scene CreateStaticScene(string name, Color clearColor, float aspectRatio, out Camera camera)
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var zone = scene.CreateChild($"{name}Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(clearColor);
        zone.SetFogStart(0.0f);
        zone.SetFogEnd(1000.0f);

        var cameraNode = scene.CreateChild($"{name}Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -5.0f));
        camera = cameraNode.CreateComponent<Camera>();
        camera.SetAspectRatio(aspectRatio);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(100.0f);
        return scene;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    private sealed class VisualFactAttribute : Xunit.FactAttribute
    {
        public VisualFactAttribute() => Skip = VisualRegressionReferences.SkipReason;
    }

}

internal static class VisualRegressionReferences
{
    public static bool RunVisualTests => IsEnabled("REDBOLT_RUN_VISUAL_TESTS");

    public static bool UpdateReferences => IsEnabled("REDBOLT_UPDATE_VISUAL_REFERENCES");

    public static string? SkipReason
    {
        get
        {
            if (!RunVisualTests)
                return "Set REDBOLT_RUN_VISUAL_TESTS=1 to run GPU visual regression tests.";

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                return "GPU visual regression tests require an X11 DISPLAY or WAYLAND_DISPLAY.";
            }

            return null;
        }
    }

    public static string GetPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        var root = Environment.GetEnvironmentVariable("REDBOLT_VISUAL_REFERENCE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(AppContext.BaseDirectory, "VisualRegression", "References");

        var path = Path.Combine(root, fileName);
        if (UpdateReferences || File.Exists(path))
            return path;

        throw new FileNotFoundException(
            $"Visual regression reference '{fileName}' was not found. "
            + "Set REDBOLT_VISUAL_REFERENCE_ROOT or rebuild the test project.", path);
    }

    public static string GetArtifactDirectory()
    {
        var root = Environment.GetEnvironmentVariable("REDBOLT_VISUAL_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Path.GetTempPath(), "RedBoltVisualRegression");
        Directory.CreateDirectory(root);
        return root;
    }

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
