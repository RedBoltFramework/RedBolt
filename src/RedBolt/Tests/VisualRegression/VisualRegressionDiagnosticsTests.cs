namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionDiagnosticsTests
{
    [VisualFact]
    public void Outline_group_renders_a_stable_selected_drawable_outline()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        FirstCubeTestData.AddResourceDirs(fixture.Resources);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var left = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml",
            new Vector3f(-1.15f, 0.0f, 0.0f), 1.1f, new Quaternion(18.0f, Vector3f.Up));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml",
            new Vector3f(1.15f, 0.0f, 0.0f), 1.1f, new Quaternion(-18.0f, Vector3f.Up));

        var outline = scene.CreateComponent<OutlineGroup>();
        outline.SetColor(new Color(1.0f, 0.2f, 0.05f));
        Assert.True(outline.AddDrawable(left));

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.3f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.6f, -6.0f), Vector3f.Zero, 256.0f / 192.0f,
            fov: 38.0f);

        fixture.AssertMatches(scene, camera, "diagnostics/outline-group.png");
    }

    [VisualFact]
    public void Debug_renderer_renders_depth_tested_lines_triangles_and_bounds()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var debug = scene.CreateComponent<DebugRenderer>();
        debug.SetLineAntiAlias(true);
        debug.AddLine(new Vector3f(-2.0f, -1.0f, 0.0f), new Vector3f(2.0f, -1.0f, 0.0f), Color.Red);
        debug.AddLine(new Vector3f(-2.0f, 1.0f, 0.0f), new Vector3f(2.0f, 1.0f, 0.0f), Color.Green, false);
        debug.AddTriangle(
            new Vector3f(-0.75f, -0.65f, 0.0f), new Vector3f(0.0f, 0.65f, 0.0f),
            new Vector3f(0.75f, -0.65f, 0.0f), Color.Blue, false);
        debug.AddBoundingBox(new BoundingBox(new Vector3f(-1.5f, -0.5f, -0.1f),
            new Vector3f(1.5f, 0.5f, 0.1f)), Color.Yellow, false);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 3.2f);

        fixture.AssertMatches(scene, camera, "diagnostics/debug-geometry.png");
    }

    [VisualFact]
    public void Octree_queries_render_their_selected_bounds_and_query_shapes()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var positions = new[]
        {
            new Vector3f(-2.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 0.0f, 0.0f),
            new Vector3f(2.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 0.0f, 1.8f)
        };
        foreach (var position in positions)
        {
            var model = VisualRegressionSceneFactory.AddModel(
                scene, fixture.Resources, "Models/Box.mdl", "Materials/Constant/UnlitWhite.xml",
                position, 0.65f);
            model.SetViewMask(1u);
        }

        var debug = scene.CreateComponent<DebugRenderer>();
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -7.0f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 3.6f);
        fixture.Capture(scene, camera);

        var octree = scene.GetComponent<Octree>()!;
        var point = new Vector3f(-2.0f, 0.0f, 0.0f);
        var pointResults = new List<Drawable>();
        using (var query = new PointOctreeQuery(pointResults, point, DrawableFlags.Geometry, 1u))
            octree.GetDrawables(query);
        DrawBounds(debug, pointResults, Color.Red);
        debug.AddSphere(new Sphere(point, 0.12f), Color.Red, false);

        var sphere = new Sphere(Vector3f.Zero, 1.0f);
        var sphereResults = new List<Drawable>();
        using (var query = new SphereOctreeQuery(sphereResults, sphere, DrawableFlags.Geometry, 1u))
            octree.GetDrawables(query);
        DrawBounds(debug, sphereResults, Color.Green);
        debug.AddSphere(sphere, Color.Green, false);

        var box = new BoundingBox(new Vector3f(1.35f, -0.75f, -0.75f),
            new Vector3f(2.65f, 0.75f, 0.75f));
        var boxResults = new List<Drawable>();
        using (var query = new BoxOctreeQuery(boxResults, box, DrawableFlags.Geometry, 1u))
            octree.GetDrawables(query);
        DrawBounds(debug, boxResults, Color.Yellow);
        debug.AddBoundingBox(box, Color.Yellow, false);

        var frustumResults = new List<Drawable>();
        using (var query = new FrustumOctreeQuery(frustumResults, camera.GetFrustum(), DrawableFlags.Geometry, 1u))
            octree.GetDrawables(query);
        DrawBounds(debug, frustumResults, Color.Cyan);
        debug.AddFrustum(camera.GetFrustum(), Color.Cyan, false);

        var ray = new Ray(new Vector3f(0.0f, 0.0f, -6.0f), Vector3f.Forward);
        using (var query = new RayOctreeQuery(ray, RayQueryLevel.RayAabb, 20.0f, DrawableFlags.Geometry, 1u))
        {
            octree.Raycast(query);
            foreach (var result in query.Result)
            {
                if (result.Drawable is { } drawable)
                    debug.AddBoundingBox(drawable.GetWorldBoundingBox(), Color.Magenta, false);
            }
        }
        debug.AddLine(ray.Origin, ray.Origin + ray.Direction * 8.0f, Color.Magenta, false);

        var allResults = new List<Drawable>();
        using (var query = new AllContentOctreeQuery(allResults, DrawableFlags.Geometry, 1u))
            octree.GetDrawables(query);
        Assert.Equal(positions.Length, allResults.Count);

        fixture.AssertMatches(scene, camera, "diagnostics/octree-query-shapes.png");
    }

    private static void DrawBounds(DebugRenderer debug, IEnumerable<Drawable> drawables, Color color)
    {
        foreach (var drawable in drawables)
            debug.AddBoundingBox(drawable.GetWorldBoundingBox(), color, false);
    }
}
