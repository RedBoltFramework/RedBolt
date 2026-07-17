using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TerrainPatchTests
{
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
        var material = new Material(string.Empty);

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
}
