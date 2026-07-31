namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionZoneLightingTests
{
    [VisualFact]
    public void Zone_ambient_brightness_and_gradient_change_lighting_across_the_scene()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.35f, 0.4f, 0.5f));
        var zone = scene.GetChild("Zone")!.GetComponent<Zone>()!;
        zone.SetBoundingBox(new BoundingBox(new Vector3f(-8.0f, -4.0f, -8.0f),
            new Vector3f(8.0f, 4.0f, 8.0f)));
        var model = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.3f);
        model.SetCastShadows(false);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.2f, 2.2f, -5.5f), Vector3f.Zero, 256.0f / 192.0f, fov: 40.0f);

        zone.SetAmbientBrightness(0.25f);
        var dim = fixture.Capture(scene, camera);
        zone.SetAmbientBrightness(1.0f);
        var bright = fixture.Capture(scene, camera);

        var lower = scene.CreateChild("LowerAmbient").CreateComponent<Zone>();
        lower.SetBoundingBox(new BoundingBox(new Vector3f(-8.0f, -4.0f, -8.0f),
            new Vector3f(8.0f, 4.0f, 0.0f)));
        lower.SetAmbientColor(new Color(0.1f, 0.15f, 0.4f));
        lower.SetPriority(1);
        zone.SetAmbientGradient(true);
        zone.UpdateAmbientGradient();
        var gradient = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(dim, "lighting/zone-ambient-dim.png"),
            new VisualRegressionImageCapture(bright, "lighting/zone-ambient-bright.png"),
            new VisualRegressionImageCapture(gradient, "lighting/zone-ambient-gradient.png"));
    }

    [VisualFact]
    public void Zone_priority_override_and_masks_select_the_expected_environment()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var primary = scene.GetChild("Zone")!.GetComponent<Zone>()!;
        primary.SetBoundingBox(new BoundingBox(new Vector3f(-5.0f, -3.0f, -5.0f),
            new Vector3f(5.0f, 3.0f, 5.0f)));
        primary.SetAmbientColor(new Color(0.12f, 0.35f, 0.12f));
        var secondary = scene.CreateChild("PriorityZone").CreateComponent<Zone>();
        secondary.SetBoundingBox(new BoundingBox(new Vector3f(-2.5f, -3.0f, -5.0f),
            new Vector3f(2.5f, 3.0f, 5.0f)));
        secondary.SetAmbientColor(new Color(0.5f, 0.08f, 0.08f));
        secondary.SetPriority(2);
        secondary.SetOverride(true);

        var model = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Constant/MattWhite.xml",
            Vector3f.Zero, 1.4f);
        model.SetZoneMask(uint.MaxValue);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.2f, 2.2f, -5.5f), Vector3f.Zero, 256.0f / 192.0f, fov: 40.0f);

        var overrideImage = fixture.Capture(scene, camera);
        secondary.SetOverride(false);
        secondary.SetPriority(-1);
        var primaryImage = fixture.Capture(scene, camera);
        model.SetZoneMask(1u << 7);
        var maskedImage = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(overrideImage, "lighting/zone-priority-override.png"),
            new VisualRegressionImageCapture(primaryImage, "lighting/zone-priority-primary.png"),
            new VisualRegressionImageCapture(maskedImage, "lighting/zone-mask-default.png"));
    }

    [VisualFact]
    public void Zone_height_fog_changes_visibility_with_camera_and_model_height()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(
            new Color(0.3f, 0.35f, 0.4f), 1.0f, new Color(0.1f, 0.18f, 0.28f), 1.0f, 14.0f);
        var zone = scene.GetChild("Zone")!.GetComponent<Zone>()!;
        zone.SetHeightFog(true);
        zone.SetFogHeight(0.0f);
        zone.SetFogHeightScale(1.5f);
        for (var i = 0; i < 5; ++i)
        {
            var model = VisualRegressionSceneFactory.AddModel(
                scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml",
                new Vector3f((i - 2) * 1.2f, (i & 1) == 0 ? -1.2f : 1.2f, i * 1.5f), 0.65f);
            model.SetCastShadows(false);
        }
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -4.5f), new Vector3f(0.0f, 0.0f, 4.0f),
            256.0f / 192.0f, fov: 40.0f);

        var normal = fixture.Capture(scene, camera);
        zone.SetFogHeight(1.5f);
        zone.SetFogHeightScale(0.35f);
        var shifted = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(normal, "lighting/zone-height-fog.png"),
            new VisualRegressionImageCapture(shifted, "lighting/zone-height-fog-shifted.png"));
    }

    [VisualFact]
    public void Light_importance_temperature_physical_values_and_masks_affect_the_surface()
    {
        using var fixture = new VisualRegressionFixture(384, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var masks = new[] { 1u, 2u, 4u };
        for (var i = 0; i < 3; ++i)
        {
            var model = VisualRegressionSceneFactory.AddModel(
                scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml",
                new Vector3f((i - 1) * 2.2f, 0.0f, 0.0f), 1.0f);
            model.SetLightMask(masks[i]);
        }
        var auto = VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            new Vector3f(-2.2f, 0.0f, 0.0f), Color.White, masks[0], brightness: 1.5f);
        auto.SetLightImportance(LightImportance.Auto);
        var important = VisualRegressionSceneFactory.AddLight(
            scene, LightType.Point, new Vector3f(0.0f, 2.5f, -3.0f), Vector3f.Zero,
            new Color(0.2f, 0.5f, 1.0f), masks[1], range: 6.0f, brightness: 2.5f);
        important.SetLightImportance(LightImportance.Important);
        important.SetTemperature(3000.0f);
        var physical = VisualRegressionSceneFactory.AddLight(
            scene, LightType.Spot, new Vector3f(3.0f, 3.0f, -3.5f),
            new Vector3f(2.2f, 0.0f, 0.0f), Color.White, masks[2], range: 7.0f, brightness: 1.8f);
        physical.SetLightImportance(LightImportance.NotImportant);
        physical.SetUsePhysicalValues(true);
        physical.SetTemperature(6500.0f);
        physical.SetSpecularIntensity(0.4f);
        physical.SetRampTexture(fixture.Resources.Get<Texture2D>("Textures/Ramp.png"));
        physical.SetShapeTexture(fixture.Resources.Get<Texture2D>("Textures/Spot.png"));

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 2.5f, -10.0f), Vector3f.Zero, 384.0f / 192.0f, fov: 35.0f);
        var allLights = fixture.Capture(scene, camera);
        important.SetEnabled(false);
        var withoutImportant = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(allLights, "lighting/light-importance-temperature.png"),
            new VisualRegressionImageCapture(withoutImportant, "lighting/light-mask-disabled.png"));
    }

    [VisualFact]
    public void Light_modes_distinguish_realtime_mixed_and_baked_contributions()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateShadowScene(fixture, LightType.Directional, out var camera, out var light);

        light.SetLightMode(LightMode.Realtime);
        var realtime = fixture.Capture(scene, camera);
        light.SetLightMode(LightMode.Mixed);
        var mixed = fixture.Capture(scene, camera);
        light.SetLightMode(LightMode.Baked);
        var baked = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(realtime, "lighting/light-mode-realtime.png"),
            new VisualRegressionImageCapture(mixed, "lighting/light-mode-mixed.png"),
            new VisualRegressionImageCapture(baked, "lighting/light-mode-baked.png"));
    }

    [VisualFact]
    public void Point_and_spot_lights_cast_deterministic_projected_shadows()
    {
        using var pointFixture = new VisualRegressionFixture(256, 192);
        using var pointScene = CreateShadowScene(pointFixture, LightType.Point, out var pointCamera,
            out var pointLight);
        var point = pointFixture.Capture(pointScene, pointCamera);
        pointLight.SetShadowIntensity(0.55f);
        var pointSoft = pointFixture.Capture(pointScene, pointCamera);

        using var spotFixture = new VisualRegressionFixture(256, 192);
        using var spotScene = CreateShadowScene(spotFixture, LightType.Spot, out var spotCamera,
            out var spotLight);
        spotLight.SetFov(30.0f);
        spotLight.SetAspectRatio(1.5f);
        var spot = spotFixture.Capture(spotScene, spotCamera);

        pointFixture.AssertMatches(
            new VisualRegressionImageCapture(point, "lighting/point-shadow.png"),
            new VisualRegressionImageCapture(pointSoft, "lighting/point-shadow-soft.png"));
        spotFixture.AssertMatches(new VisualRegressionImageCapture(spot, "lighting/spot-shadow.png"));
    }

    [VisualFact]
    public void Shadow_bias_resolution_and_distance_change_shadow_stability()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateShadowScene(fixture, LightType.Directional, out var camera, out var light);

        light.SetShadowBias(new BiasParameters(0.0001f, 0.25f, 0.0f));
        light.SetShadowResolution(1.0f);
        light.SetShadowFadeDistance(80.0f);
        var highResolution = fixture.Capture(scene, camera);

        light.SetShadowBias(new BiasParameters(0.01f, 2.0f, 0.1f));
        light.SetShadowResolution(0.5f);
        light.SetShadowFadeDistance(12.0f);
        var biased = fixture.Capture(scene, camera);

        light.SetShadowNearFarRatio(0.2f);
        light.SetShadowMaxExtrusion(20.0f);
        var clipped = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(highResolution, "lighting/shadow-high-resolution.png"),
            new VisualRegressionImageCapture(biased, "lighting/shadow-biased-low-resolution.png"),
            new VisualRegressionImageCapture(clipped, "lighting/shadow-near-far-extrusion.png"));
    }

    private static Scene CreateShadowScene(
        VisualRegressionFixture fixture,
        LightType lightType,
        out Camera camera,
        out Light light)
    {
        var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.14f, 0.14f, 0.14f));
        var floor = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Plane.mdl", "Materials/StoneTiled.xml",
            new Vector3f(0.0f, -1.0f, 1.0f), 5.0f);
        floor.SetCastShadows(false);
        var box = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.2f);
        box.SetCastShadows(true);
        light = VisualRegressionSceneFactory.AddLight(
            scene, lightType, new Vector3f(-3.5f, 5.0f, -3.5f),
            new Vector3f(0.0f, -0.5f, 1.0f), Color.White, brightness: 1.6f,
            range: lightType == LightType.Directional ? 10.0f : 8.0f, castShadows: true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f, 0.01f));
        light.SetShadowResolution(1.0f);
        light.SetShadowFadeDistance(80.0f);
        camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(4.5f, 3.5f, -7.0f), new Vector3f(0.0f, -0.5f, 1.0f),
            256.0f / 192.0f, fov: 40.0f);
        return scene;
    }
}
