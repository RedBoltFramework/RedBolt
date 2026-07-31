namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionPipelineTests
{
    [VisualFact]
    public void Default_render_path_and_fullscreen_pass_toggles_change_independent_images()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = CreatePostProcessScene(fixture, out var pipeline, out var camera);

        var baseline = fixture.Capture(scene, camera);

        pipeline.SetRenderPassEnabled("Postprocess: Vignette", true);
        pipeline.UpdateRenderPathParameter(new StringHash("Vignette: Intensity"), new Variant(0.85f));
        pipeline.UpdateRenderPathParameter(new StringHash("Vignette: Radius"), new Variant(0.45f));
        var vignette = fixture.Capture(scene, camera);

        pipeline.SetRenderPassEnabled("Postprocess: Chromatic Aberration", true);
        pipeline.UpdateRenderPathParameter(
            new StringHash("Chromatic Aberration: Amount"), new Variant(0.8f));
        var chromatic = fixture.Capture(scene, camera);

        pipeline.UpdateRenderPathParameter(
            new StringHash("Adjust Color: ColorFilter"),
            new Variant(new Vector4f(0.8f, 1.0f, 1.15f, 1.0f)));
        var adjusted = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(baseline, "pipeline/default-path.png"),
            new VisualRegressionImageCapture(vignette, "pipeline/vignette.png"),
            new VisualRegressionImageCapture(chromatic, "pipeline/chromatic-aberration.png"),
            new VisualRegressionImageCapture(adjusted, "pipeline/adjust-color.png"));
    }

    [VisualFact]
    public void Forward_and_deferred_pbr_paths_render_the_same_controlled_scene()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = CreatePostProcessScene(fixture, out var pipeline, out var camera);

        var settings = pipeline.Settings;
        var sceneProcessor = settings.SceneProcessor;
        sceneProcessor.LightingMode = DirectLightingMode.Forward;
        settings.SceneProcessor = sceneProcessor;
        pipeline.SetSettings(settings);
        var forward = fixture.Capture(scene, camera);

        sceneProcessor.LightingMode = DirectLightingMode.DeferredPBR;
        settings.SceneProcessor = sceneProcessor;
        pipeline.SetSettings(settings);
        var deferred = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(forward, "pipeline/lighting-forward.png"),
            new VisualRegressionImageCapture(deferred, "pipeline/lighting-deferred-pbr.png"));
    }

    [VisualFact]
    public void Linear_and_gamma_ldr_output_color_spaces_are_explicit()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = CreatePostProcessScene(fixture, out var pipeline, out var camera);

        SetOutputColorSpace(pipeline, RenderPipelineColorSpace.GammaLDR);
        var gamma = fixture.Capture(scene, camera);
        SetOutputColorSpace(pipeline, RenderPipelineColorSpace.LinearLDR);
        var linear = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(gamma, "pipeline/output-gamma-ldr.png"),
            new VisualRegressionImageCapture(linear, "pipeline/output-linear-ldr.png"));
    }

    [VisualFact]
    public void Deferred_hdr_pipeline_renders_tone_mapping_bloom_and_ambient_occlusion()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = CreatePostProcessScene(fixture, out var pipeline, out var camera);

        var settings = pipeline.Settings;
        var sceneProcessor = settings.SceneProcessor;
        sceneProcessor.LightingMode = DirectLightingMode.DeferredPBR;
        sceneProcessor.EnableShadows = true;
        settings.SceneProcessor = sceneProcessor;
        var renderBuffers = settings.RenderBufferManager;
        renderBuffers.ColorSpace = RenderPipelineColorSpace.LinearHDR;
        renderBuffers.InheritMultiSampleLevel = false;
        settings.RenderBufferManager = renderBuffers;
        pipeline.SetSettings(settings);

        pipeline.SetRenderPassEnabled("Postprocess: Bloom", true);
        pipeline.SetRenderPassEnabled("Postprocess: SSAO", true);
        pipeline.UpdateRenderPathParameter(new StringHash("Tone Mapping: Mode"),
            new Variant((int)ToneMappingPass.Mode.ReinhardWhite));
        pipeline.UpdateRenderPathParameter(new StringHash("Bloom: Min Brightness"), new Variant(0.15f));
        pipeline.UpdateRenderPathParameter(new StringHash("Bloom: Max Brightness"), new Variant(0.9f));
        pipeline.UpdateRenderPathParameter(new StringHash("Bloom: Base Intensity"), new Variant(1.4f));
        pipeline.UpdateRenderPathParameter(new StringHash("SSAO: Strength"), new Variant(1.1f));
        pipeline.UpdateRenderPathParameter(new StringHash("SSAO: Near Radius"), new Variant(0.2f));
        pipeline.UpdateRenderPathParameter(new StringHash("SSAO: Far Radius"), new Variant(1.5f));

        var hdr = fixture.Capture(scene, camera);

        pipeline.UpdateRenderPathParameter(new StringHash("Tone Mapping: Mode"),
            new Variant((int)ToneMappingPass.Mode.Uncharted2));
        var alternateToneMapping = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(hdr, "pipeline/deferred-hdr-bloom-ssao.png"),
            new VisualRegressionImageCapture(alternateToneMapping, "pipeline/tone-mapping-uncharted2.png"));
    }

    [VisualFact]
    public void Auto_exposure_adapts_over_fixed_render_frames()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = CreatePostProcessScene(fixture, out var pipeline, out var camera);

        var settings = pipeline.Settings;
        var renderBuffers = settings.RenderBufferManager;
        renderBuffers.ColorSpace = RenderPipelineColorSpace.LinearHDR;
        renderBuffers.InheritMultiSampleLevel = false;
        settings.RenderBufferManager = renderBuffers;
        pipeline.SetSettings(settings);
        pipeline.UpdateRenderPathParameter(new StringHash("Exposure: Automatic"), new Variant(true));
        pipeline.UpdateRenderPathParameter(new StringHash("Exposure: Min"), new Variant(0.5f));
        pipeline.UpdateRenderPathParameter(new StringHash("Exposure: Max"), new Variant(2.0f));
        pipeline.UpdateRenderPathParameter(new StringHash("Exposure: Adapt Rate"), new Variant(0.35f));

        var frames = fixture.CaptureFrames(scene, camera, frameCount: 3, warmupFrames: 2);
        fixture.AssertMatches(
            new VisualRegressionImageCapture(frames[0], "pipeline/auto-exposure-frame-0.png"),
            new VisualRegressionImageCapture(frames[1], "pipeline/auto-exposure-frame-1.png"),
            new VisualRegressionImageCapture(frames[2], "pipeline/auto-exposure-frame-2.png"));
    }

    [VisualFact]
    public void Pipeline_outline_pass_composites_outline_group_into_final_output()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = CreatePostProcessScene(fixture, out var pipeline, out var camera);
        var selected = scene.GetChild("Box", true)!.GetComponent<StaticModel>()!;
        var outline = scene.CreateComponent<OutlineGroup>();
        outline.SetColor(new Color(0.1f, 1.0f, 0.2f));
        Assert.True(outline.AddDrawable(selected));
        pipeline.SetRenderPassEnabled("Postprocess: Draw Outlines", true);

        fixture.AssertMatches(fixture.Capture(scene, camera), "pipeline/outline-pass.png");
    }

    private static Scene CreatePostProcessScene(
        VisualRegressionFixture fixture, out RenderPipeline pipeline, out Camera camera)
    {
        var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.025f, 0.025f, 0.025f));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml",
            new Vector3f(-0.8f, 0.0f, 0.0f), scale: 1.25f,
            rotation: new Quaternion(18.0f, Vector3f.Up));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/Stone.xml",
            new Vector3f(1.0f, -0.15f, 0.2f), scale: 0.8f,
            rotation: new Quaternion(-20.0f, Vector3f.Up));
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f), Vector3f.Zero,
            Color.White, brightness: 2.2f, castShadows: true);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Point, new Vector3f(1.5f, 1.5f, -2.0f), Vector3f.Zero,
            new Color(1.0f, 0.4f, 0.15f), range: 6.0f, brightness: 1.5f);
        camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(4.4f, 3.0f, -7.0f), Vector3f.Zero, 256.0f / 192.0f,
            fov: 38.0f);
        pipeline = scene.CreateComponent<RenderPipeline>();
        return scene;
    }

    private static void SetOutputColorSpace(RenderPipeline pipeline, RenderPipelineColorSpace colorSpace)
    {
        var settings = pipeline.Settings;
        var renderBuffers = settings.RenderBufferManager;
        renderBuffers.ColorSpace = colorSpace;
        renderBuffers.InheritMultiSampleLevel = false;
        settings.RenderBufferManager = renderBuffers;
        pipeline.SetSettings(settings);
    }
}
