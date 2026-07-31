namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionProbeTests
{
    [VisualFact]
    public void Reflection_probe_baked_priority_selects_the_nearest_custom_cubemap()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateProbeScene(fixture, out var model, out var camera);
        using var redCube = CreateFaceColorCube(new Color(1.0f, 0.08f, 0.03f));
        using var blueCube = CreateFaceColorCube(new Color(0.03f, 0.15f, 1.0f));
        var firstProbe = AddProbe(scene, redCube, new Vector3f(-1.4f, 0.0f, 0.0f), 0);
        var secondProbe = AddProbe(scene, blueCube, new Vector3f(1.4f, 0.0f, 0.0f), 1);
        firstProbe.SetBoundingBox(new BoundingBox(new Vector3f(-3.0f, -2.0f, -3.0f),
            new Vector3f(0.0f, 2.0f, 3.0f)));
        secondProbe.SetBoundingBox(new BoundingBox(new Vector3f(0.0f, -2.0f, -3.0f),
            new Vector3f(3.0f, 2.0f, 3.0f)));
        model.SetReflectionMode(ReflectionMode.NearestProbe);
        model.Node.SetPosition(new Vector3f(-1.3f, 0.0f, 0.0f));
        var left = fixture.Capture(scene, camera);
        model.Node.SetPosition(new Vector3f(1.3f, 0.0f, 0.0f));
        var right = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(left, "environment/probe-baked-left.png"),
            new VisualRegressionImageCapture(right, "environment/probe-baked-right.png"));
    }

    [VisualFact]
    public void Reflection_probe_mixed_and_zone_blending_render_stable_environment_variants()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateProbeScene(fixture, out var model, out var camera);
        using var cube = CreateFaceColorCube(new Color(0.1f, 0.8f, 0.3f));
        var probe = AddProbe(scene, cube, Vector3f.Zero, 1);
        probe.SetProbeType(ReflectionProbeType.Mixed);
        probe.SetTextureSize(32);
        probe.SetBoundingBox(new BoundingBox(new Vector3f(-3.0f, -2.0f, -3.0f),
            new Vector3f(3.0f, 2.0f, 3.0f)));
        model.SetReflectionMode(ReflectionMode.BlendProbesAndZone);

        var mixed = fixture.Capture(scene, camera);
        model.SetReflectionMode(ReflectionMode.Zone);
        var zoneOnly = fixture.Capture(scene, camera);
        model.SetReflectionMode(ReflectionMode.BlendProbes);
        var probeOnly = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(mixed, "environment/probe-mixed-zone.png"),
            new VisualRegressionImageCapture(zoneOnly, "environment/probe-zone-only.png"),
            new VisualRegressionImageCapture(probeOnly, "environment/probe-mixed-only.png"));
    }

    [VisualFact]
    public void Reflection_probe_dynamic_and_sliced_updates_converge_after_fixed_frames()
    {
        using var fixture = new VisualRegressionFixture(192, 144);
        using var scene = CreateProbeScene(fixture, out var model, out var camera);
        var probe = scene.CreateChild("DynamicProbe").CreateComponent<ReflectionProbe>();
        probe.SetProbeType(ReflectionProbeType.Dynamic);
        probe.SetTextureSize(32);
        probe.SetRealtimeUpdate(false);
        probe.SetSlicedUpdate(false);
        probe.SetBoundingBox(new BoundingBox(new Vector3f(-3.0f, -2.0f, -3.0f),
            new Vector3f(3.0f, 2.0f, 3.0f)));
        model.SetReflectionMode(ReflectionMode.NearestProbe);
        probe.QueueRender();
        var first = fixture.Capture(scene, camera);
        var stable = fixture.Capture(scene, camera);

        probe.SetSlicedUpdate(true);
        probe.QueueRender();
        var slicedFrames = fixture.CaptureFrames(scene, camera, 3, warmupFrames: 0);
        model.Node.SetPosition(new Vector3f(0.35f, 0.0f, 0.0f));
        var moved = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(first, "environment/probe-dynamic-first.png"),
            new VisualRegressionImageCapture(stable, "environment/probe-dynamic-stable.png"),
            new VisualRegressionImageCapture(slicedFrames[0], "environment/probe-sliced-frame-0.png"),
            new VisualRegressionImageCapture(slicedFrames[1], "environment/probe-sliced-frame-1.png"),
            new VisualRegressionImageCapture(slicedFrames[2], "environment/probe-sliced-frame-2.png"),
            new VisualRegressionImageCapture(moved, "environment/probe-dynamic-moved.png"));
    }

    [VisualFact]
    public void Reflection_probe_box_projection_masks_and_clip_settings_change_the_capture()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateProbeScene(fixture, out var model, out var camera);
        using var cube = CreateFaceColorCube(new Color(0.8f, 0.2f, 0.8f));
        var probe = AddProbe(scene, cube, Vector3f.Zero, 1);
        probe.SetBoundingBox(new BoundingBox(new Vector3f(-2.0f, -2.0f, -3.0f),
            new Vector3f(2.0f, 2.0f, 3.0f)));
        model.SetReflectionMode(ReflectionMode.NearestProbe);

        var normal = fixture.Capture(scene, camera);
        probe.SetBoxProjectionUsed(true);
        probe.SetProjectionBox(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f),
            new Vector3f(1.0f, 1.0f, 1.0f)));
        var boxProjected = fixture.Capture(scene, camera);
        probe.SetViewMask(0u);
        var masked = fixture.Capture(scene, camera);
        probe.SetViewMask(uint.MaxValue);
        probe.SetNearClip(1.0f);
        probe.SetFarClip(8.0f);
        probe.SetTextureSize(16);
        var clipped = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(normal, "environment/probe-no-box-projection.png"),
            new VisualRegressionImageCapture(boxProjected, "environment/probe-box-projection.png"),
            new VisualRegressionImageCapture(masked, "environment/probe-view-masked.png"),
            new VisualRegressionImageCapture(clipped, "environment/probe-clips-small-texture.png"));
    }

    [VisualFact]
    public void Light_probe_group_and_global_illumination_use_tracked_baked_data()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.03f, 0.03f, 0.03f));
        var groupNode = scene.CreateChild("LightProbes");
        groupNode.SetScale(new Vector3f(4.0f, 2.0f, 4.0f));
        var group = groupNode.CreateComponent<LightProbeGroup>();
        group.SetAutoPlacementEnabled(true);
        group.SetAutoPlacementStep(2.0f);
        group.SetLightMask(1u);
        group.SetZoneMask(uint.MaxValue);
        var globalIllumination = scene.CreateComponent<GlobalIllumination>();
        globalIllumination.SetFileRef(new ResourceRef(
            new StringHash(nameof(BinaryFile)),
            "Scenes/BakedLightingExample.xml.d/GI.bin"));

        var model = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/PBR/Leather.xml",
            Vector3f.Zero, 1.5f);
        model.SetGlobalIlluminationType(GlobalIlluminationType.BlendLightProbes);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            Vector3f.Zero, Color.White, lightMask: 1u, brightness: 1.3f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.6f, 2.4f, -6.0f), Vector3f.Zero, 256.0f / 192.0f, fov: 40.0f);

        var probeData = fixture.Capture(scene, camera);
        group.SetAutoPlacementEnabled(false);
        group.SetLightProbes([
            new LightProbe { Position = new Vector3f(-1.5f, 0.0f, 0.0f) },
            new LightProbe { Position = new Vector3f(1.5f, 0.0f, 0.0f) }
        ]);
        globalIllumination.SetOffset(new Vector3f(0.25f, 0.0f, -0.25f));
        globalIllumination.SetEmissionBrightness(0.5f);
        var explicitProbes = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(probeData, "environment/light-probe-auto.png"),
            new VisualRegressionImageCapture(explicitProbes, "environment/light-probe-explicit-offset.png"));
    }

    [VisualFact]
    public void Baked_scene_gallery_renders_lightmaps_and_baked_environment_resources()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var sceneResource = fixture.Resources.Get<SceneResource>("Scenes/BakedLightingExample.xml");
        var scene = sceneResource.Scene;
        var cameraNode = scene.CreateChild("VisualCamera");
        cameraNode.SetPosition(new Vector3f(6.0f, 4.0f, -9.0f));
        cameraNode.LookAt(Vector3f.Zero, Vector3f.Up);
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetAspectRatio(256.0f / 192.0f);
        camera.SetFov(40.0f);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(100.0f);

        var baked = fixture.Capture(scene, camera);
        var global = scene.GetComponent<GlobalIllumination>();
        if (global is not null)
        {
            global.SetOffset(new Vector3f(0.1f, 0.0f, 0.0f));
            global.SetEmissionBrightness(1.2f);
        }
        var offset = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(baked, "environment/baked-scene.png"),
            new VisualRegressionImageCapture(offset, "environment/baked-scene-offset-emission.png"));
    }

    private static Scene CreateProbeScene(
        VisualRegressionFixture fixture, out StaticModel model, out Camera camera)
    {
        var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.06f, 0.06f, 0.06f));
        scene.CreateComponent<ReflectionProbeManager>();
        var zone = scene.GetChild("Zone")!.GetComponent<Zone>()!;
        zone.SetZoneTexture(fixture.Resources.Get<TextureCube>("Textures/Skybox.xml"));
        zone.SetBackgroundBrightness(0.35f);
        var node = scene.CreateChild("ReflectiveModel");
        node.SetScale(1.4f);
        model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Sphere.mdl"));
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/PBR/Lead.xml"));
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.4f);
        camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.6f, 2.3f, -6.0f), Vector3f.Zero, 256.0f / 192.0f, fov: 40.0f);
        return scene;
    }

    private static ReflectionProbe AddProbe(Scene scene, TextureCube cube, Vector3f position, int priority)
    {
        var node = scene.CreateChild($"Probe{priority}");
        node.SetPosition(position);
        var probe = node.CreateComponent<ReflectionProbe>();
        probe.SetProbeType(ReflectionProbeType.CustomTexture);
        probe.SetTexture(cube);
        probe.SetPriority(priority);
        return probe;
    }

    private static TextureCube CreateFaceColorCube(Color color)
    {
        var cube = new TextureCube { Name = "VisualRegression.ProbeCube" };
        for (var faceIndex = 0; faceIndex < (int)CubeMapFace.MaxCubeMapFaces; ++faceIndex)
        {
            using var image = new Image();
            Assert.True(image.SetSize(16, 16, 4));
            var data = new byte[16 * 16 * 4];
            var faceColor = faceIndex switch
            {
                0 => color,
                1 => new Color(color.B * 0.8f, color.R * 0.8f, color.G * 0.8f),
                2 => new Color(color.G, color.B, color.R),
                3 => new Color(color.R * 0.6f, color.G * 0.6f, color.B * 0.6f),
                4 => new Color(color.B, color.R, color.G),
                _ => new Color(color.G * 0.6f, color.B * 0.6f, color.R * 0.6f)
            };
            for (var i = 0; i < data.Length; i += 4)
            {
                data[i] = (byte)global::System.Math.Clamp(faceColor.R * 255.0f, 0.0f, 255.0f);
                data[i + 1] = (byte)global::System.Math.Clamp(faceColor.G * 255.0f, 0.0f, 255.0f);
                data[i + 2] = (byte)global::System.Math.Clamp(faceColor.B * 255.0f, 0.0f, 255.0f);
                data[i + 3] = 255;
            }
            image.SetData(data);
            Assert.True(cube.SetData((CubeMapFace)faceIndex, image));
        }
        return cube;
    }
}
