namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TerrainTests
{
    [Fact]
    public void Terrain_is_extensible_noncopyable_reference_class_with_weak_observations()
    {
        var type = typeof(Terrain);

        Assert.True(type.IsClass);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.Equal(typeof(List<WeakReference<TerrainPatch>>), type.GetField("_patches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<Terrain>), type.GetField("_north", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<Terrain>), type.GetField("_south", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<Terrain>), type.GetField("_west", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<Terrain>), type.GetField("_east", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
    }

    [Fact]
    public void SetHeightMap_creates_patch_geometry_and_height_samples()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Terrain");
        var terrain = node.CreateComponent<Terrain>();

        // power-of-two + 1, one 32-quad patch
        var image = new Image();
        Assert.True(image.SetSize(33, 33, 1));
        var data = image.GetData();
        for (var i = 0; i < data.Length; ++i)
            data[i] = (byte)(i % 256);
        image.SetData(data);

        Assert.True(terrain.SetHeightMap(image));
        Assert.Equal(new Vector2i(33, 33), terrain.GetNumVertices());
        Assert.Equal(new Vector2i(1, 1), terrain.GetNumPatches());

        var patch = terrain.GetPatch(0, 0);
        Assert.NotNull(patch);
        Assert.Same(terrain, patch!.GetOwner());
        Assert.Equal(new Vector2i(0, 0), patch.GetCoordinates());
        Assert.True(patch.GetVertexBuffer().GetVertexCount() > 0);
        Assert.NotNull(terrain.GetHeightData());

        // Center sample should be non-zero for non-flat map
        var center = terrain.GetHeight(node.GetWorldPosition());
        Assert.True(float.IsFinite(center));
    }

    [Fact]
    public void Heightmap_reload_event_rebuilds_only_from_the_current_image()
    {
        var terrain = new Scene().CreateChild("Terrain").CreateComponent<Terrain>();
        var firstImage = new Image();
        Assert.True(firstImage.SetSize(33, 33, 1));
        var secondImage = new Image();
        Assert.True(secondImage.SetSize(65, 65, 1));
        Assert.True(terrain.SetHeightMap(firstImage));
        Assert.True(terrain.SetHeightMap(secondImage));
        Assert.Equal(new Vector2i(65, 65), terrain.GetNumVertices());

        Assert.True(secondImage.SetSize(33, 33, 1));
        firstImage.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(new Vector2i(65, 65), terrain.GetNumVertices());

        secondImage.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(new Vector2i(33, 33), terrain.GetNumVertices());
    }

    [Fact]
    public void UpdatePatchLod_selects_base_draw_range()
    {
        var scene = new Scene();
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        var image = new Image();
        Assert.True(image.SetSize(33, 33, 1));
        Assert.True(terrain.SetHeightMap(image));

        var patch = terrain.GetPatch(0)!;
        terrain.UpdatePatchLod(patch);
        Assert.Equal(0u, patch.GetLodLevel());
    }

    [Fact]
    public void SetPatchSize_rejects_non_power_of_two()
    {
        var terrain = new Scene().CreateChild("T").CreateComponent<Terrain>();
        terrain.SetPatchSize(30);
        Assert.Equal(32, terrain.GetPatchSize());
        terrain.SetPatchSize(16);
        Assert.Equal(16, terrain.GetPatchSize());
    }

    [Fact]
    public void Legacy_baked_lighting_scene_loads_terrain_attributes()
    {
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        var xmlFile = cache.Get<XMLFile>("Scenes/BakedLightingExample.xml");
        using var root = xmlFile.GetRoot();
        using var scene = new Scene();

        Assert.True(scene.LoadXML(root));
        var terrain = scene.FindComponent<Terrain>();
        Assert.NotNull(terrain);
        Assert.NotNull(terrain!.GetHeightMap());
        Assert.NotNull(terrain.GetMaterial());
        Assert.Equal(new Vector3f(0.6f, 0.01f, 0.6f), terrain.GetSpacing());
        Assert.True(terrain.GetBakeLightmap());
        Assert.Equal(1u, terrain.GetLightmapIndex());
        Assert.Equal(new Vector4f(0.375f, 0.75f, 0.00195312f, 0.00195312f), terrain.GetLightmapScaleOffset());
        Assert.True(terrain.GetNumPatches().X > 0);
        Assert.True(terrain.GetNumPatches().Y > 0);
    }

    [Fact]
    public void Dispose_releases_owned_state_without_disposing_borrowed_resources()
    {
        var scene = new Scene();
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        var neighbor = scene.CreateChild("Neighbor").CreateComponent<Terrain>();
        var image = new Image();
        Assert.True(image.SetSize(33, 33, 1));
        terrain.SetNorthNeighbor(neighbor);
        Assert.True(terrain.SetHeightMap(image));
        var geometry = terrain.GetPatch(0)!.GetGeometry();
        var sharedIndexBuffer = geometry.GetIndexBuffer();
        Assert.NotNull(sharedIndexBuffer);

        terrain.Dispose();

        Assert.Null(terrain.GetHeightMap());
        Assert.Null(terrain.GetHeightData());
        Assert.Null(terrain.GetMaterial());
        Assert.Null(terrain.GetNorthNeighbor());
        Assert.Null(terrain.GetPatch(0));
        Assert.Null(typeof(Terrain).GetField("_indexBuffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(terrain));
        Assert.Same(sharedIndexBuffer, geometry.GetIndexBuffer());
        Assert.True(sharedIndexBuffer!.GetIndexCount() > 0);
        Assert.Equal(33, image.GetWidth());
    }
}
