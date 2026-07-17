using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class TerrainTests
{
    [Fact]
    public void SetHeightMap_creates_patch_geometry_and_height_samples()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Terrain");
        var terrain = node.CreateComponent<Terrain>();

        // power-of-two + 1, one 32-quad patch
        var image = new Image("HeightMap");
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
    public void UpdatePatchLod_selects_base_draw_range()
    {
        var scene = new Scene();
        var terrain = scene.CreateChild("Terrain").CreateComponent<Terrain>();
        var image = new Image("HeightMap");
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
}
