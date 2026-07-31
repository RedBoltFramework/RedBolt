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

    [Fact]
    public void Visual_reference_shot_path_uses_reference_directory_and_base_name()
    {
        var referenceFile = Path.Combine("references", "mushroom-ambient.png");

        var shotFile = VisualRegressionReferences.GetShotPath(referenceFile);

        Assert.Equal(Path.Combine("references", "mushroom-ambient_shot.png"), shotFile);
    }

    [VisualFact]
    public void Mushroom_model_renders_with_zone_ambient_lighting()
    {
        using var fixture = new VisualRegressionFixture();
        using var scene = CreateAmbientModelScene(fixture.Resources, 128.0f / 96.0f, out var camera);

        fixture.AssertMatches(scene, camera, "mushroom-ambient.png");
    }

    private static Scene CreateAmbientModelScene(ResourceCache resources, float aspectRatio, out Camera camera)
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var zone = scene.CreateChild("AmbientZone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.7f, 0.7f, 0.7f));
        zone.SetAmbientBrightness(1.0f);
        zone.SetFogColor(Color.Black);
        zone.SetFogStart(1000.0f);
        zone.SetFogEnd(2000.0f);

        var modelNode = scene.CreateChild("Mushroom");
        modelNode.SetPosition(new Vector3f(0.0f, 0.0f, 0.0f));
        modelNode.SetScale(1.5f);
        var model = modelNode.CreateComponent<StaticModel>();
        model.SetModel(resources.Get<Model>("Models/Mushroom.mdl"));
        model.SetMaterial(resources.Get<Material>("Materials/Mushroom.xml"));

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 1.5f, -3.5f));
        cameraNode.LookAt(new Vector3f(0.0f, 1.0f, 0.0f), Vector3f.Up);
        camera = cameraNode.CreateComponent<Camera>();
        camera.SetAspectRatio(aspectRatio);
        camera.SetFov(45.0f);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(100.0f);
        return scene;
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

    public static string GetShotPath(string referenceFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceFile);

        var directory = Path.GetDirectoryName(referenceFile);
        var fileName = $"{Path.GetFileNameWithoutExtension(referenceFile)}_shot.png";
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
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
