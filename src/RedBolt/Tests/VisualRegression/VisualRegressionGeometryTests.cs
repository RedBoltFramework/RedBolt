namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionGeometryTests
{
    [VisualFact]
    public void Custom_geometry_renders_topologies_and_vertex_attributes()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("CustomGeometry");
        var geometry = node.CreateComponent<CustomGeometry>();
        geometry.SetNumGeometries(3);

        geometry.BeginGeometry(0, PrimitiveType.TriangleList);
        DefineColoredVertex(geometry, new Vector3f(-2.8f, -0.8f, 0.0f), Color.Red);
        DefineColoredVertex(geometry, new Vector3f(-1.9f, 1.0f, 0.0f), Color.Blue);
        DefineColoredVertex(geometry, new Vector3f(-1.0f, -0.8f, 0.0f), Color.Green);

        geometry.DefineGeometry(1, PrimitiveType.LineList, 4, false, true, false, false);
        geometry.GetVertex(1, 0).Position = new Vector3f(-0.2f, -0.8f, 0.0f);
        geometry.GetVertex(1, 1).Position = new Vector3f(1.1f, 0.8f, 0.0f);
        geometry.GetVertex(1, 2).Position = new Vector3f(-0.2f, 0.8f, 0.0f);
        geometry.GetVertex(1, 3).Position = new Vector3f(1.1f, -0.8f, 0.0f);
        geometry.GetVertex(1, 0).Color = Color.Yellow.ToUInt();
        geometry.GetVertex(1, 1).Color = Color.Yellow.ToUInt();
        geometry.GetVertex(1, 2).Color = Color.Cyan.ToUInt();
        geometry.GetVertex(1, 3).Color = Color.Cyan.ToUInt();

        geometry.DefineGeometry(2, PrimitiveType.PointList, 5, false, true, false, false);
        for (var i = 0; i < 5; ++i)
        {
            geometry.GetVertex(2, (uint)i).Position = new Vector3f(2.2f + i * 0.35f, -0.7f + (i & 1) * 1.3f, 0.0f);
            geometry.GetVertex(2, (uint)i).Color = Color.Magenta.ToUInt();
        }

        geometry.SetMaterial(0, fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        geometry.SetMaterial(1, fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        geometry.SetMaterial(2, fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        geometry.Commit();

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -7.0f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 4.8f);

        fixture.AssertMatches(scene, camera, "geometry/custom-topologies.png");
    }

    [VisualFact]
    public void Dynamic_custom_geometry_update_changes_the_captured_frame()
    {
        using var fixture = new VisualRegressionFixture(128, 96);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("DynamicGeometry");
        var geometry = node.CreateComponent<CustomGeometry>();
        geometry.SetDynamic(true);
        geometry.BeginGeometry(0, PrimitiveType.TriangleList);
        DefineColoredVertex(geometry, new Vector3f(-1.2f, -0.8f, 0.0f), Color.Red);
        DefineColoredVertex(geometry, new Vector3f(0.0f, 1.0f, 0.0f), Color.Blue);
        DefineColoredVertex(geometry, new Vector3f(1.2f, -0.8f, 0.0f), Color.Green);
        geometry.SetMaterial(fixture.Resources.Get<Material>("Materials/VColUnlit.xml"));
        geometry.Commit();

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -6.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.2f);
        var initial = fixture.Capture(scene, camera);

        geometry.GetVertex(0, 0).Position = new Vector3f(-0.2f, -0.8f, 0.0f);
        geometry.GetVertex(0, 1).Position = new Vector3f(-0.8f, 1.0f, 0.0f);
        geometry.GetVertex(0, 2).Position = new Vector3f(1.2f, 0.0f, 0.0f);
        geometry.Commit();
        var updated = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(initial, "geometry/dynamic-update-before.png"),
            new VisualRegressionImageCapture(updated, "geometry/dynamic-update-after.png"));
    }

    private static void DefineColoredVertex(CustomGeometry geometry, Vector3f position, Color color)
    {
        geometry.DefineVertex(position);
        geometry.DefineColor(color);
    }
}
