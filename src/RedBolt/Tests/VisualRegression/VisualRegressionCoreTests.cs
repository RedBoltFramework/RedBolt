namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionCoreTests
{
    [VisualFact]
    public void Box_model_renders_with_unlit_material()
    {
        using var fixture = new VisualRegressionFixture();
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/FirstCube", Vector3f.Zero, 1.4f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.1f, -3.4f), new Vector3f(0.0f, 0.0f, 0.0f), 128.0f / 96.0f);

        fixture.AssertMatches(scene, camera, "core/box-unlit.png");
    }

    [VisualFact]
    public void Primitive_model_gallery_renders_core_shapes()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);

        var models = new[]
        {
            "Models/Box.mdl",
            "Models/Plane.mdl",
            "Models/Sphere.mdl",
            "Models/Cone.mdl",
            "Models/Cylinder.mdl",
            "Models/Dome.mdl",
            "Models/Pyramid.mdl",
            "Models/TeaPot.mdl",
            "Models/Torus.mdl"
        };
        for (var i = 0; i < models.Length; ++i)
        {
            var x = (i - (models.Length - 1) * 0.5f) * 2.0f;
            var isPlane = models[i].EndsWith("Plane.mdl", StringComparison.Ordinal);
            var y = isPlane ? -0.55f : 0.0f;
            VisualRegressionSceneFactory.AddModel(
                scene, fixture.Resources, models[i], "Materials/FirstCube", new Vector3f(x, y, 0.0f),
                isPlane ? 0.8f : 0.65f,
                new Quaternion(i * 17.0f, Vector3f.Up));
        }

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 3.0f, -14.0f), new Vector3f(0.0f, 0.0f, 0.0f), 256.0f / 192.0f,
            fov: 35.0f);

        fixture.AssertMatches(scene, camera, "core/primitive-models.png");
    }

    [VisualFact]
    public void Static_model_group_renders_reused_instance_transforms()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.04f, 0.04f, 0.04f));
        var groupNode = scene.CreateChild("StaticModelGroup");
        var group = groupNode.CreateComponent<StaticModelGroup>();
        group.SetModel(fixture.Resources.Get<Model>("Models/TeaPot.mdl"));
        group.SetMaterial(fixture.Resources.Get<Material>("Materials/FirstCube"));

        for (var i = 0; i < 5; ++i)
        {
            var instanceNode = scene.CreateChild($"Instance_{i}");
            instanceNode.SetPosition(new Vector3f((i - 2.0f) * 1.7f, (i & 1) == 0 ? 0.0f : 0.25f, 0.0f));
            instanceNode.SetRotation(new Quaternion(i * 19.0f, Vector3f.Up));
            instanceNode.SetScale(0.65f + (i & 1) * 0.08f);
            group.AddInstanceNode(instanceNode);
        }
        Assert.Equal(5u, group.GetNumInstanceNodes());

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-4.0f, 5.0f, -5.0f),
            Vector3f.Zero, Color.White, brightness: 1.2f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 2.2f, -9.5f), Vector3f.Zero, 256.0f / 128.0f,
            fov: 35.0f);

        fixture.AssertMatches(scene, camera, "core/static-model-group.png");
    }

    [VisualFact]
    public void Perspective_and_orthographic_projections_render_side_by_side()
    {
        using var fixture = new VisualRegressionFixture(256, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/TeaPot.mdl", "Materials/FirstCube", Vector3f.Zero, 1.0f,
            new Quaternion(24.0f, Vector3f.Up));

        var perspective = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.2f, -4.2f), new Vector3f(0.0f, 0.7f, 0.0f), 128.0f / 96.0f,
            fov: 35.0f);
        var orthographic = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.2f, -4.2f), new Vector3f(0.0f, 0.7f, 0.0f), 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.2f);

        fixture.AssertMatches("camera/projection-gallery.png",
            new VisualRegressionView(scene, perspective, new IntRect(0, 0, 128, 96)),
            new VisualRegressionView(scene, orthographic, new IntRect(128, 0, 128, 96)));
    }
}
