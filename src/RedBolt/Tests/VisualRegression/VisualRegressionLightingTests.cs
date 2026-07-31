namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionLightingTests
{
    [VisualFact]
    public void Light_type_gallery_renders_directional_point_and_spot_lights()
    {
        using var fixture = new VisualRegressionFixture(384, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));

        var masks = new[] { 1u, 2u, 4u };
        var positions = new[]
        {
            new Vector3f(-3.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 0.0f, 0.0f),
            new Vector3f(3.0f, 0.0f, 0.0f)
        };
        for (var i = 0; i < positions.Length; ++i)
        {
            var model = VisualRegressionSceneFactory.AddModel(
                scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml", positions[i], 1.15f);
            model.SetLightMask(masks[i]);
        }

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            new Vector3f(-3.0f, 0.0f, 0.0f), new Color(1.0f, 0.25f, 0.2f), masks[0], brightness: 1.6f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Point, new Vector3f(0.0f, 2.5f, -3.0f),
            Vector3f.Zero, new Color(0.2f, 0.6f, 1.0f), masks[1], range: 5.0f, brightness: 3.0f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Spot, new Vector3f(3.0f, 3.5f, -3.5f),
            new Vector3f(3.0f, 0.0f, 0.0f), new Color(0.3f, 1.0f, 0.3f), masks[2], range: 7.0f,
            brightness: 3.0f);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 2.5f, -11.0f), new Vector3f(0.0f, 0.0f, 0.0f), 384.0f / 192.0f,
            fov: 35.0f);

        fixture.AssertMatches(scene, camera, "lighting/light-types.png");
    }

    [VisualFact]
    public void Directional_light_casts_a_stable_shadow()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.18f, 0.18f, 0.18f));

        var floor = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Plane.mdl", "Materials/StoneTiled.xml",
            new Vector3f(0.0f, -1.0f, 1.0f), 5.0f);
        floor.SetCastShadows(false);
        var box = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.2f);
        box.SetCastShadows(true);

        var light = VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-4.0f, 6.0f, -4.0f),
            new Vector3f(0.0f, 0.0f, 1.0f), Color.White, castShadows: true, brightness: 1.5f);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f, 0.01f));
        light.SetShadowCascade(new CascadeParameters(8.0f, 20.0f, 40.0f, 80.0f, 0.8f));
        light.SetShadowResolution(1.0f);
        light.SetShadowFadeDistance(80.0f);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(4.5f, 3.5f, -7.0f), new Vector3f(0.0f, -0.5f, 1.0f), 256.0f / 192.0f,
            fov: 40.0f);

        fixture.AssertMatches(scene, camera, "lighting/directional-shadow.png");
    }

    [VisualFact]
    public void Zone_fog_and_ambient_brightness_change_with_depth()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(
            new Color(0.55f, 0.6f, 0.7f), 1.0f, new Color(0.12f, 0.2f, 0.35f), 2.5f, 10.0f);

        for (var i = 0; i < 5; ++i)
        {
            var model = VisualRegressionSceneFactory.AddModel(
                scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml",
                new Vector3f((i & 1) == 0 ? -1.25f : 1.25f, 0.0f, i * 2.0f), 0.8f);
            model.SetCastShadows(false);
        }

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -5.5f), new Vector3f(0.0f, 0.0f, 3.5f), 256.0f / 192.0f,
            fov: 40.0f);

        fixture.AssertMatches(scene, camera, "lighting/zone-fog-depth.png");
    }
}
