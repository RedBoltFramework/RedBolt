namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionTerrainTextTests
{
    [VisualFact]
    public void Terrain_heightmap_spacing_smoothing_and_lod_render()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.2f, 0.2f, 0.2f));
        using var heightMap = CreateHeightMap(65, 65);
        var terrainNode = scene.CreateChild("Terrain");
        var terrain = terrainNode.CreateComponent<Terrain>();
        terrain.SetPatchSize(32);
        terrain.SetSpacing(new Vector3f(0.18f, 0.01f, 0.18f));
        terrain.SetMaxLodLevels(3);
        terrain.SetSmoothing(true);
        terrain.SetMaterial(fixture.Resources.Get<Material>("Materials/StoneTiled"));
        terrain.SetCastShadows(true);
        terrain.SetOccluder(false);
        terrain.SetOccludee(false);
        terrain.SetHeightMap(heightMap);

        var patch = Assert.IsType<TerrainPatch>(terrain.GetPatch(0, 0));
        Assert.Equal(new Vector2i(2, 2), terrain.GetNumPatches());
        Assert.False(patch.GetGeometry().IsHeadless());
        Assert.NotEqual(0u, patch.GetGeometry().GetIndexCount());
        Assert.NotEqual(0u, patch.GetGeometry().GetVertexCount());
        Assert.True(patch.GetWorldBoundingBox().Defined);
        Assert.True(patch.IsInOctree());
        Assert.NotNull(patch.GetBatches()[0].Material);

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-4.0f, 6.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.5f, castShadows: true);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(8.0f, 7.0f, -10.0f), Vector3f.Zero, 256.0f / 192.0f,
            fov: 42.0f);

        var actual = fixture.Capture(scene, camera);
        Assert.True(camera.GetFrustum().IsInside(patch.GetWorldBoundingBox()) != Intersection.Outside,
            $"Terrain bounds: {patch.GetWorldBoundingBox()}, camera: {camera.Node.GetWorldPosition()}");
        var candidates = new List<Drawable>();
        using (var query = new FrustumOctreeQuery(candidates, camera.GetFrustum(), DrawableFlags.Geometry))
            scene.GetComponent<Octree>()!.GetDrawables(query);
        Assert.Contains(patch, candidates);
        fixture.AssertMatches(actual, "terrain/heightmap-lod.png");
    }

    [VisualFact]
    public void Text3d_bitmap_sdf_alignment_wrap_and_corner_colors_render()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);

        var bitmapNode = scene.CreateChild("BitmapText");
        bitmapNode.SetPosition(new Vector3f(-1.8f, 1.0f, 0.0f));
        bitmapNode.SetScale(1.2f);
        var bitmap = bitmapNode.CreateComponent<Text3D>();
        bitmap.SetFont(fixture.Resources.Get<Font>("Fonts/Anonymous Pro.ttf"), 42.0f);
        bitmap.SetText("Bitmap\nwrap");
        bitmap.SetWidth(190);
        bitmap.SetWordwrap(true);
        bitmap.SetAlignment(HorizontalAlignment.Left, VerticalAlignment.Top);
        bitmap.SetColor(Corner.TopLeft, Color.Red);
        bitmap.SetColor(Corner.TopRight, Color.Yellow);
        bitmap.SetColor(Corner.BottomLeft, Color.Cyan);
        bitmap.SetColor(Corner.BottomRight, Color.White);

        var sdfNode = scene.CreateChild("SdfText");
        sdfNode.SetPosition(new Vector3f(-1.8f, -0.25f, 0.0f));
        sdfNode.SetScale(1.2f);
        var sdf = sdfNode.CreateComponent<Text3D>();
        sdf.SetFont(fixture.Resources.Get<Font>("Fonts/Anonymous Pro.sdf"), 36.0f);
        sdf.SetText("SDF 0123 ΩЖ");
        sdf.SetAlignment(HorizontalAlignment.Left, VerticalAlignment.Center);
        sdf.SetOpacity(0.9f);

        var overlayNode = scene.CreateChild("OverlayText");
        overlayNode.SetPosition(new Vector3f(-1.8f, -1.2f, 0.2f));
        var overlay = overlayNode.CreateComponent<Text3D>();
        overlay.SetFont(fixture.Resources.Get<Font>("Fonts/Anonymous Pro.ttf"), 26.0f);
        overlay.SetText("Overlay depth-off");
        overlay.SetDepthTest(false);
        overlay.SetColor(new Color(0.5f, 0.8f, 1.0f));

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 3.5f);

        fixture.AssertMatches(scene, camera, "text/text3d-gallery.png");
    }

    [VisualFact]
    public void Text3d_harfbuzz_shaping_handles_combining_marks_and_ligatures()
    {
        using var fixture = new VisualRegressionFixture(256, 128);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var textNode = scene.CreateChild("ShapedText");
        textNode.SetPosition(new Vector3f(-0.35f, 0.0f, 0.0f));
        var text = textNode.CreateComponent<Text3D>();
        text.SetFont(fixture.Resources.Get<Font>("Fonts/NotoSans-Condensed.ttf"), 24.0f);
        text.SetWidth(500);
        text.SetAlignment(HorizontalAlignment.Left, VerticalAlignment.Center);
        text.SetText("Cafe\u0301  A\u0308  fi fl  ΩЖ");

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 256.0f / 128.0f,
            orthographic: true, orthoSize: 2.5f);
        var latin = fixture.Capture(scene, camera);

        text.SetText("A\u0301  e\u0308  O\u0302  fi fl");
        var alternate = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(latin, "text/harfbuzz-combining.png"),
            new VisualRegressionImageCapture(alternate, "text/harfbuzz-combining-alternate.png"));
    }

    [VisualFact]
    public void Terrain_smoothing_patch_size_and_lod_variants_render_at_native_resolution()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var unsmoothed = CaptureTerrain(fixture, smoothing: false, patchSize: 16, maxLodLevels: 1,
            cameraPosition: new Vector3f(5.5f, 4.0f, -7.0f), materialName: "Materials/Terrain.xml");
        var smoothed = CaptureTerrain(fixture, smoothing: true, patchSize: 16, maxLodLevels: 1,
            cameraPosition: new Vector3f(5.5f, 4.0f, -7.0f), materialName: "Materials/Terrain.xml");
        var largePatches = CaptureTerrain(fixture, smoothing: true, patchSize: 32, maxLodLevels: 1,
            cameraPosition: new Vector3f(5.5f, 4.0f, -7.0f), materialName: "Materials/Terrain.xml");
        var lodFar = CaptureTerrain(fixture, smoothing: true, patchSize: 16, maxLodLevels: 4,
            cameraPosition: new Vector3f(18.0f, 10.0f, -24.0f), materialName: "Materials/Terrain.xml");

        fixture.AssertMatches(
            new VisualRegressionImageCapture(unsmoothed, "terrain/smoothing-off.png"),
            new VisualRegressionImageCapture(smoothed, "terrain/smoothing-on.png"),
            new VisualRegressionImageCapture(largePatches, "terrain/patch-size-32.png"),
            new VisualRegressionImageCapture(lodFar, "terrain/lod-far.png"));
    }

    [VisualFact]
    public void Terrain_neighbor_stitching_material_blend_and_lighting_masks_render()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var independent = CaptureTerrainPair(fixture, stitched: false, materialName: "Materials/Terrain.xml",
            lightMask: uint.MaxValue, shadowMask: uint.MaxValue);
        var stitched = CaptureTerrainPair(fixture, stitched: true, materialName: "Materials/Terrain.xml",
            lightMask: uint.MaxValue, shadowMask: uint.MaxValue);
        var pbr = CaptureTerrainPair(fixture, stitched: true, materialName: "Materials/Showcase/PBRTerrain.xml",
            lightMask: 1u, shadowMask: 1u);
        var masked = CaptureTerrainPair(fixture, stitched: true, materialName: "Materials/Terrain.xml",
            lightMask: 2u, shadowMask: 2u);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(independent, "terrain/neighbors-independent.png"),
            new VisualRegressionImageCapture(stitched, "terrain/neighbors-stitched.png"),
            new VisualRegressionImageCapture(pbr, "terrain/material-pbr.png"),
            new VisualRegressionImageCapture(masked, "terrain/light-shadow-mask.png"));
    }

    [VisualFact]
    public void Terrain_occlusion_distances_and_debug_geometry_change_the_capture()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var normal = CaptureTerrainVisibility(fixture, drawDistance: 100.0f, shadowDistance: 100.0f,
            occluder: false, occludee: true, debug: false);
        var shortDistance = CaptureTerrainVisibility(fixture, drawDistance: 2.0f, shadowDistance: 2.0f,
            occluder: true, occludee: true, debug: false);
        var debug = CaptureTerrainVisibility(fixture, drawDistance: 100.0f, shadowDistance: 100.0f,
            occluder: false, occludee: true, debug: true);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(normal, "terrain/visibility-normal.png"),
            new VisualRegressionImageCapture(shortDistance, "terrain/visibility-distance.png"),
            new VisualRegressionImageCapture(debug, "terrain/debug-geometry.png"));
    }

    [VisualFact]
    public void Octree_visibility_updates_when_objects_cross_the_camera_frustum()
    {
        using var fixture = new VisualRegressionFixture();
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var left = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Constant/GlowingRed.xml",
            new Vector3f(-1.2f, 0.0f, 0.0f), 0.8f);
        var right = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Constant/GlowingBlue.xml",
            new Vector3f(1.2f, 0.0f, 0.0f), 0.8f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.5f);
        var both = fixture.Capture(scene, camera);
        camera.Node.SetPosition(new Vector3f(-1.2f, 0.0f, -5.0f));
        camera.Node.LookAt(left.Node.GetWorldPosition(), Vector3f.Up);
        var leftOnly = fixture.Capture(scene, camera);
        camera.Node.SetPosition(new Vector3f(1.2f, 0.0f, -5.0f));
        camera.Node.LookAt(right.Node.GetWorldPosition(), Vector3f.Up);
        var rightOnly = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(both, "octree/frustum-both.png"),
            new VisualRegressionImageCapture(leftOnly, "octree/frustum-left.png"),
            new VisualRegressionImageCapture(rightOnly, "octree/frustum-right.png"));
    }

    private static VisualRegressionImage CaptureTerrain(
        VisualRegressionFixture fixture, bool smoothing, int patchSize, uint maxLodLevels,
        Vector3f cameraPosition, string materialName)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.1f, 0.12f));
        using var heightMap = CreateHeightMap(65, 65);
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        terrain.SetPatchSize(patchSize);
        terrain.SetSpacing(new Vector3f(0.18f, 0.01f, 0.18f));
        terrain.SetMaxLodLevels(maxLodLevels);
        terrain.SetSmoothing(smoothing);
        terrain.SetMaterial(fixture.Resources.Get<Material>(materialName));
        terrain.SetHeightMap(heightMap);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, cameraPosition, new Vector3f(5.5f, 0.0f, 5.5f), 256.0f / 192.0f, fov: 42.0f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-4.0f, 6.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.4f, castShadows: false);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureTerrainPair(
        VisualRegressionFixture fixture, bool stitched, string materialName,
        uint lightMask, uint shadowMask)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.1f, 0.12f));
        using var firstMap = CreateHeightMap(33, 33);
        using var secondMap = CreateHeightMap(33, 33);
        var first = scene.CreateChild("TerrainWest").CreateComponent<Terrain>();
        var secondNode = scene.CreateChild("TerrainEast");
        secondNode.SetPosition(new Vector3f(5.76f, 0.0f, 0.0f));
        var second = secondNode.CreateComponent<Terrain>();
        foreach (var terrain in new[] { first, second })
        {
            terrain.SetPatchSize(16);
            terrain.SetSpacing(new Vector3f(0.18f, 0.01f, 0.18f));
            terrain.SetMaxLodLevels(3);
            terrain.SetSmoothing(true);
            terrain.SetMaterial(fixture.Resources.Get<Material>(materialName));
            terrain.SetLightMask(lightMask);
            terrain.SetShadowMask(shadowMask);
        }
        first.SetHeightMap(firstMap);
        second.SetHeightMap(secondMap);
        if (stitched)
        {
            first.SetEastNeighbor(second);
            second.SetWestNeighbor(first);
        }
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-4.0f, 7.0f, -4.0f),
            Vector3f.Zero, Color.White, lightMask: 1u, brightness: 1.4f, castShadows: true);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(5.5f, 5.5f, -8.0f), new Vector3f(5.5f, 0.0f, 2.8f),
            256.0f / 192.0f, fov: 42.0f);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureTerrainVisibility(
        VisualRegressionFixture fixture, float drawDistance, float shadowDistance,
        bool occluder, bool occludee, bool debug)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.1f, 0.12f));
        using var heightMap = CreateHeightMap(65, 65);
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        terrain.SetPatchSize(16);
        terrain.SetSpacing(new Vector3f(0.18f, 0.01f, 0.18f));
        terrain.SetMaxLodLevels(3);
        terrain.SetMaterial(fixture.Resources.Get<Material>("Materials/Terrain.xml"));
        terrain.SetHeightMap(heightMap);
        terrain.SetDrawDistance(drawDistance);
        terrain.SetShadowDistance(shadowDistance);
        terrain.SetOccluder(occluder);
        terrain.SetOccludee(occludee);
        terrain.SetEnableDebug(debug);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-4.0f, 6.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.4f, castShadows: true);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(5.5f, 5.5f, -8.0f), new Vector3f(5.5f, 0.0f, 5.5f),
            256.0f / 192.0f, fov: 42.0f);
        return fixture.Capture(scene, camera);
    }

    private static Image CreateHeightMap(int width, int height)
    {
        var image = new Image();
        Assert.True(image.SetSize(width, height, 1));
        var data = image.GetData();
        for (var z = 0; z < height; ++z)
        {
            for (var x = 0; x < width; ++x)
            {
                var dx = (x - (width - 1) * 0.5f) / (width * 0.5f);
                var dz = (z - (height - 1) * 0.5f) / (height * 0.5f);
                var radial = MathF.Max(0.0f, 1.0f - MathF.Sqrt(dx * dx + dz * dz));
                var ripple = 0.5f + 0.5f * MathF.Sin(x * 0.34f) * MathF.Cos(z * 0.27f);
                data[z * width + x] = (byte)global::System.Math.Clamp(
                    (int)(35.0f + 180.0f * radial * ripple), 0, 255);
            }
        }
        image.SetData(data);
        return image;
    }
}
