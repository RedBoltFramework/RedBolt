namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionRenderSurfaceTests
{
    [VisualFact]
    public void Render_to_texture_surface_captures_the_scene_at_native_resolution()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero,
            scale: 1.5f, rotation: new Quaternion(25.0f, Vector3f.Up));
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f), Vector3f.Zero,
            Color.White, brightness: 1.4f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.2f, 2.7f, -4.4f), Vector3f.Zero, 128.0f / 96.0f,
            fov: 38.0f);

        var actual = fixture.CaptureToRenderSurface(scene, camera);
        fixture.AssertMatches(actual, "surfaces/render-to-texture.png");
    }

    [VisualFact]
    public void Render_surface_update_modes_control_when_a_surface_is_rendered()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.04f, 0.04f, 0.04f));
        var modelNode = scene.CreateChild("UpdateModeBox");
        modelNode.SetPosition(Vector3f.Zero);
        modelNode.SetScale(1.5f);
        var model = modelNode.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/FirstCube"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(2.8f, 2.2f, -4.2f), Vector3f.Zero, 128.0f / 96.0f,
            fov: 40.0f);

        using var texture = new Texture2D { Name = "VisualRegression.UpdateModes" };
        Assert.True(texture.SetSize(128, 96, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget));
        var surface = Assert.IsType<RenderSurface>(texture.GetRenderSurface());
        using var viewport = new Viewport(scene, camera, new IntRect(0, 0, 128, 96));
        surface.SetNumViewports(1);
        surface.SetViewport(0, viewport);
        fixture.Renderer.SetNumViewports(0);

        surface.SetUpdateMode(RenderSurfaceUpdateMode.ManualUpdate);
        Assert.Equal(RenderSurfaceUpdateMode.ManualUpdate, surface.GetUpdateMode());
        surface.QueueUpdate();
        Assert.True(surface.IsUpdateQueued());
        fixture.RenderFrameForVisualTest();
        Assert.False(surface.IsUpdateQueued());
        var manual = fixture.ReadTextureImage(texture);

        modelNode.SetRotation(new Quaternion(0.0f, 35.0f, 0.0f));
        surface.SetUpdateMode(RenderSurfaceUpdateMode.UpdateAlways);
        Assert.Equal(RenderSurfaceUpdateMode.UpdateAlways, surface.GetUpdateMode());
        fixture.RenderFrameForVisualTest();
        var always = fixture.ReadTextureImage(texture);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(manual, "surfaces/update-manual.png"),
            new VisualRegressionImageCapture(always, "surfaces/update-always.png"));
    }

    [VisualFact]
    public void Render_surface_linked_depth_stencil_and_resolve_render_the_same_scene()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.06f, 0.06f, 0.06f));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", Vector3f.Zero,
            scale: 1.1f, rotation: new Quaternion(22.0f, Vector3f.Up));
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -5.0f), Vector3f.Zero,
            Color.White, brightness: 1.3f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(2.8f, 2.1f, -4.4f), Vector3f.Zero, 128.0f / 96.0f,
            fov: 38.0f);

        using var color = new Texture2D { Name = "VisualRegression.LinkedColor" };
        using var depth = new Texture2D { Name = "VisualRegression.LinkedDepth" };
        Assert.True(color.SetSize(128, 96, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget));
        Assert.True(depth.SetSize(128, 96, TextureFormat.TexFormatD24UnormS8Uint,
            TextureFlag.BindDepthStencil));
        var colorSurface = Assert.IsType<RenderSurface>(color.GetRenderSurface());
        var depthSurface = Assert.IsType<RenderSurface>(depth.GetRenderSurface());
        colorSurface.SetLinkedDepthStencil(depthSurface);

        var actual = fixture.CaptureToRenderSurface(colorSurface, scene, camera);
        fixture.AssertMatches(actual, "surfaces/linked-depth.png");
    }

    [VisualFact]
    public void Render_surface_array_layers_are_captured_as_independent_native_images()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.03f, 0.03f, 0.03f));
        var modelNode = scene.CreateChild("ArrayLayerBox");
        modelNode.SetScale(1.4f);
        var model = modelNode.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/FirstCube"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(2.8f, 2.2f, -4.2f), Vector3f.Zero, 128.0f / 96.0f,
            fov: 40.0f);

        using var texture = new Texture2DArray { Name = "VisualRegression.ArrayRenderTarget" };
        Assert.True(texture.SetSize(3, 128, 96, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget));
        var captures = new List<VisualRegressionImageCapture>(3);
        for (var layer = 0; layer < 3; ++layer)
        {
            modelNode.SetPosition(new Vector3f((layer - 1) * 0.45f, 0.0f, 0.0f));
            var surface = Assert.IsType<RenderSurface>(texture.GetRenderSurface(layer));
            fixture.RenderToSurface(surface, scene, camera);
            captures.Add(new VisualRegressionImageCapture(
                ReadArrayLayer(texture, layer), $"surfaces/array-layer-{layer}.png"));
        }

        fixture.AssertMatches(captures.ToArray());
    }

    [VisualFact]
    public void Render_surface_cubemap_faces_are_captured_as_independent_native_images()
    {
        using var fixture = new VisualRegressionFixture(64, 64);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.03f, 0.03f, 0.03f));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero,
            scale: 1.0f, rotation: new Quaternion(15.0f, Vector3f.Up));
        var cameraNode = scene.CreateChild("CubemapCamera");
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetAspectRatio(1.0f);
        camera.SetFov(90.0f);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(20.0f);

        using var texture = new TextureCube { Name = "VisualRegression.CubeRenderTarget" };
        Assert.True(texture.SetSize(64, TextureFormat.TexFormatRgba8Unorm,
            TextureFlag.BindRenderTarget));
        var faceCameras = new (CubeMapFace Face, Vector3f Position, string Name)[]
        {
            (CubeMapFace.PositiveX, new Vector3f(3.0f, 0.0f, 0.0f), "positive-x"),
            (CubeMapFace.NegativeX, new Vector3f(-3.0f, 0.0f, 0.0f), "negative-x"),
            (CubeMapFace.PositiveY, new Vector3f(0.0f, 3.0f, 0.0f), "positive-y"),
            (CubeMapFace.NegativeY, new Vector3f(0.0f, -3.0f, 0.0f), "negative-y"),
            (CubeMapFace.PositiveZ, new Vector3f(0.0f, 0.0f, 3.0f), "positive-z"),
            (CubeMapFace.NegativeZ, new Vector3f(0.0f, 0.0f, -3.0f), "negative-z")
        };
        var captures = new List<VisualRegressionImageCapture>(faceCameras.Length);
        foreach (var (face, position, name) in faceCameras)
        {
            cameraNode.SetPosition(position);
            cameraNode.LookAt(Vector3f.Zero, position.Y is > 0.9f or < -0.9f
                ? Vector3f.Forward
                : Vector3f.Up);
            var surface = Assert.IsType<RenderSurface>(texture.GetRenderSurface(face));
            fixture.RenderToSurface(surface, scene, camera);
            using var image = Assert.IsType<Image>(texture.GetImage(face));
            captures.Add(new VisualRegressionImageCapture(
                VisualRegressionImage.FromImage(image), $"surfaces/cube-{name}.png"));
        }

        fixture.AssertMatches(captures.ToArray());
    }

    private static VisualRegressionImage ReadArrayLayer(Texture2DArray texture, int layer)
    {
        using var image = new Image();
        Assert.True(image.SetSize(texture.Width, texture.Height, 4));
        Assert.True(texture.GetData(layer, 0, image.GetData()));
        return VisualRegressionImage.FromImage(image);
    }
}
