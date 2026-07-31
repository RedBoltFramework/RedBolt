namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionEnvironmentTests
{
    [VisualFact]
    public void Skybox_cubemap_and_zone_environment_render_together()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var zone = scene.GetChild("Zone")!.GetComponent<Zone>()!;
        zone.SetBackgroundBrightness(0.7f);
        zone.SetZoneTexture(fixture.Resources.Get<TextureCube>("Textures/Skybox.xml"));

        var skyNode = scene.CreateChild("Skybox");
        skyNode.SetRotation(new Quaternion(18.0f, 32.0f, 0.0f));
        var skybox = skyNode.CreateComponent<Skybox>();
        skybox.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        skybox.SetMaterial(fixture.Resources.Get<Material>("Materials/Skybox.xml"));

        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/PBR/Lead.xml",
            new Vector3f(-1.35f, 0.0f, 0.0f), 1.0f);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/PBR/Leather.xml",
            new Vector3f(1.35f, 0.0f, 0.0f), 1.0f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.4f);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.4f, -6.0f), new Vector3f(0.0f, 0.0f, 0.0f), 256.0f / 192.0f,
            fov: 40.0f);

        fixture.AssertMatches(scene, camera, "environment/skybox-zone-cubemap.png");
    }

    [VisualFact]
    public void Skybox_zone_background_brightness_scales_hdr_environment()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var zone = scene.GetChild("Zone")!.GetComponent<Zone>()!;
        zone.SetZoneTexture(fixture.Resources.Get<TextureCube>("Textures/Skybox.xml"));

        var skyNode = scene.CreateChild("Skybox");
        skyNode.SetRotation(new Quaternion(18.0f, 32.0f, 0.0f));
        var skybox = skyNode.CreateComponent<Skybox>();
        skybox.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        skybox.SetMaterial(fixture.Resources.Get<Material>("Materials/Skybox.xml"));

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.4f, -6.0f), Vector3f.Zero, 256.0f / 192.0f,
            fov: 40.0f);

        zone.SetBackgroundBrightness(0.25f);
        var low = fixture.Capture(scene, camera);
        zone.SetBackgroundBrightness(0.75f);
        var normal = fixture.Capture(scene, camera);
        zone.SetBackgroundBrightness(1.5f);
        var high = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(low, "environment/skybox-background-low.png"),
            new VisualRegressionImageCapture(normal, "environment/skybox-background-normal.png"),
            new VisualRegressionImageCapture(high, "environment/skybox-background-high.png"));
    }

    [VisualFact]
    public void Cubemap_renderer_captures_six_faces_with_fixed_clips_and_view_mask()
    {
        using var fixture = new VisualRegressionFixture(64, 64);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        AddColoredBox(fixture, scene, new Vector3f(2.0f, 0.0f, 0.0f), Color.Red, 1u);
        AddColoredBox(fixture, scene, new Vector3f(-2.0f, 0.0f, 0.0f), Color.Green, 1u);
        AddColoredBox(fixture, scene, new Vector3f(0.0f, 2.0f, 0.0f), Color.Blue, 1u);
        AddColoredBox(fixture, scene, new Vector3f(0.0f, -2.0f, 0.0f), Color.Yellow, 1u);
        AddColoredBox(fixture, scene, new Vector3f(0.0f, 0.0f, 2.0f), Color.Cyan, 1u);
        AddColoredBox(fixture, scene, new Vector3f(0.0f, 0.0f, -2.0f), Color.Magenta, 1u);

        var settings = new CubemapRenderingSettings
        {
            TextureSize = 64,
            NearClip = 0.1f,
            FarClip = 8.0f,
            ViewMask = 1u
        };
        using var texture = new TextureCube { Name = "VisualRegression.CubemapRenderer" };
        CubemapRenderer.DefineTexture(texture, settings);
        using var renderer = new CubemapRenderer(scene);
        fixture.Renderer.SetNumViewports(0);
        var result = renderer.Update(new CubemapUpdateParameters
        {
            Settings = settings,
            Position = Vector3f.Zero,
            OverrideFinalTexture = texture
        });
        Assert.True(result.IsComplete);

        fixture.RenderFrameForVisualTest();

        var captures = new List<VisualRegressionImageCapture>(6);
        foreach (var (face, name) in new[]
        {
            (CubeMapFace.PositiveX, "positive-x"),
            (CubeMapFace.NegativeX, "negative-x"),
            (CubeMapFace.PositiveY, "positive-y"),
            (CubeMapFace.NegativeY, "negative-y"),
            (CubeMapFace.PositiveZ, "positive-z"),
            (CubeMapFace.NegativeZ, "negative-z")
        })
        {
            using var image = Assert.IsType<Image>(texture.GetImage(face));
            captures.Add(new VisualRegressionImageCapture(
                VisualRegressionImage.FromImage(image), $"environment/cubemap-renderer-{name}.png"));
        }

        fixture.AssertMatches(captures.ToArray());
    }

    [VisualFact]
    public void Pbr_material_gallery_renders_dielectric_and_metal_surfaces()
    {
        using var fixture = new VisualRegressionFixture(384, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.12f, 0.12f, 0.12f));

        var materials = new[]
        {
            "Materials/PBR/Lead.xml",
            "Materials/PBR/Leather.xml",
            "Materials/PBR/Mud.xml",
            "Materials/PBR/Sand.xml"
        };
        for (var i = 0; i < materials.Length; ++i)
        {
            VisualRegressionSceneFactory.AddModel(
                scene, fixture.Resources, "Models/Sphere.mdl", materials[i],
                new Vector3f((i - 1.5f) * 2.1f, 0.0f, 0.0f), 0.9f);
        }

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-4.0f, 5.0f, -5.0f),
            Vector3f.Zero, Color.White, brightness: 1.6f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Point, new Vector3f(0.0f, 2.0f, -3.0f),
            Vector3f.Zero, new Color(0.5f, 0.7f, 1.0f), range: 8.0f, brightness: 2.0f);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 2.4f, -10.0f), new Vector3f(0.0f, 0.0f, 0.0f), 384.0f / 192.0f,
            fov: 35.0f);

        fixture.AssertMatches(scene, camera, "materials/pbr-gallery.png");
    }

    private static void AddColoredBox(
        VisualRegressionFixture fixture, Scene scene, Vector3f position, Color color, uint viewMask)
    {
        var node = scene.CreateChild($"Box-{position}");
        node.SetPosition(position);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        var material = fixture.Resources.Get<Material>("Materials/Constant/UnlitWhite.xml")
            .Clone($"VisualRegression.Cubemap.{position}");
        material.SetShaderParameter("MatDiffColor", color);
        model.SetMaterial(material);
        model.SetViewMask(viewMask);
    }
}
