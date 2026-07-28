namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TerrainPatchTests
{
    [Fact]
    public void TerrainPatch_is_extensible_noncopyable_reference_class_with_weak_observations()
    {
        var type = typeof(TerrainPatch);

        Assert.True(type.IsClass);
        Assert.False(type.IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        Assert.Equal(typeof(WeakReference<Terrain>), type.GetField("_owner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<TerrainPatch>), type.GetField("_north", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<TerrainPatch>), type.GetField("_south", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<TerrainPatch>), type.GetField("_west", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(WeakReference<TerrainPatch>), type.GetField("_east", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType);
        Assert.Null(type.GetField("_worldTransform", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void Defaults_match_cpp_constructor()
    {
        var patch = new Scene().CreateChild("Patch").CreateComponent<TerrainPatch>();
        Assert.Equal(DrawableFlags.Geometry, patch.GetDrawableFlags());
        Assert.Equal(0u, patch.GetLodLevel());
        Assert.Equal(Vector2i.Zero, patch.GetCoordinates());
        Assert.Null(patch.GetOwner());
        Assert.Null(patch.GetNorthPatch());
        Assert.NotNull(patch.GetGeometry());
        Assert.NotNull(patch.GetMaxLodGeometry());
        Assert.NotNull(patch.GetOcclusionGeometry());
        Assert.NotNull(patch.GetVertexBuffer());
        Assert.Equal(GeometryType.StaticNoInstancing, patch.GetBatches()[0].GeometryType);
        Assert.Equal(UpdateGeometryType.MainThread, patch.GetUpdateGeometryType());
    }

    [Fact]
    public void Neighbors_coordinates_and_material_roundtrip()
    {
        var scene = new Scene();
        var patch = scene.CreateChild("Patch").CreateComponent<TerrainPatch>();
        var north = scene.CreateChild("N").CreateComponent<TerrainPatch>();
        var south = scene.CreateChild("S").CreateComponent<TerrainPatch>();
        var west = scene.CreateChild("W").CreateComponent<TerrainPatch>();
        var east = scene.CreateChild("E").CreateComponent<TerrainPatch>();
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        var material = new Material { Name = string.Empty };

        patch.SetOwner(terrain);
        patch.SetNeighbors(north, south, west, east);
        patch.SetCoordinates(new Vector2i(3, 4));
        patch.SetMaterial(material);
        patch.SetBoundingBox(new BoundingBox(Vector3f.Zero, Vector3f.One));
        patch.ResetLod();

        Assert.Same(terrain, patch.GetOwner());
        Assert.Same(north, patch.GetNorthPatch());
        Assert.Same(south, patch.GetSouthPatch());
        Assert.Same(west, patch.GetWestPatch());
        Assert.Same(east, patch.GetEastPatch());
        Assert.Equal(new Vector2i(3, 4), patch.GetCoordinates());
        Assert.Same(material, patch.GetBatches()[0].Material);
        Assert.Equal(0u, patch.GetLodLevel());
    }

    [Fact]
    public void Lightmap_helpers_update_batch()
    {
        var patch = new Scene().CreateChild("Patch").CreateComponent<TerrainPatch>();
        patch.SetBakeLightmap(true);
        patch.SetLightmapIndex(2u);
        patch.SetLightmapScaleOffset(new Vector4f(1, 2, 3, 4));

        Assert.True(patch.GetBakeLightmap());
        Assert.Equal(2u, patch.GetLightmapIndex());
        Assert.Equal(new Vector4f(1, 2, 3, 4), patch.GetLightmapScaleOffset());
        Assert.Equal(2u, patch.GetBatches()[0].LightmapIndex);
        Assert.Equal(new Vector4f(1, 2, 3, 4), patch.GetBatches()[0].LightmapScaleOffset);

        patch.SetBakeLightmap(false);
        Assert.Equal(0u, patch.GetBatches()[0].LightmapIndex);
        Assert.Null(patch.GetBatches()[0].LightmapScaleOffset);
    }

    [Fact]
    public void GetLodGeometry_returns_max_for_level_zero()
    {
        var patch = new Scene().CreateChild("Patch").CreateComponent<TerrainPatch>();
        Assert.Same(patch.GetMaxLodGeometry(), patch.GetLodGeometry(0, 0));
        Assert.Same(patch.GetGeometry(), patch.GetLodGeometry(0, 1));
    }

    [Fact]
    public void Dispose_releases_derived_state_before_drawable_state()
    {
        var scene = new Scene();
        var patch = scene.CreateChild("Patch").CreateComponent<TerrainPatch>();
        var owner = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        var north = scene.CreateChild("North").CreateComponent<TerrainPatch>();
        patch.SetOwner(owner);
        patch.SetNeighbors(north, null, null, null);
        patch.GetLodErrors().Add(1.0f);
        var geometry = patch.GetGeometry();
        var vertexBuffer = patch.GetVertexBuffer();

        patch.Dispose();

        Assert.Null(patch.GetOwner());
        Assert.Null(patch.GetNorthPatch());
        Assert.Empty(patch.GetLodErrors());
        Assert.Empty(patch.GetBatches());
        Assert.NotNull(geometry);
        Assert.NotNull(vertexBuffer);
        Assert.Null(typeof(TerrainPatch).GetField("_geometry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(patch));
        Assert.Null(typeof(TerrainPatch).GetField("_vertexBuffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(patch));
    }
}
