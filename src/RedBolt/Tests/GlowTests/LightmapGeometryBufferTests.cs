using RedBolt.Glow;
using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class LightmapGeometryBufferTests
{
    [Fact]
    public void Geometry_mapping_defaults_to_invalid_indices()
    {
        var mapping = new GeometryIdToObjectMapping();

        Assert.Equal(int.MaxValue, mapping.ObjectIndex);
        Assert.Equal(int.MaxValue, mapping.GeometryIndex);
        Assert.Equal(int.MaxValue, mapping.LodIndex);
    }

    [Fact]
    public void Lightmap_seam_transforms_and_copies_by_value()
    {
        var seam = new LightmapSeam();
        seam.Positions[0] = new Vector2f(1.0f, 2.0f);
        seam.Positions[1] = new Vector2f(3.0f, 4.0f);
        seam.OtherPositions[0] = new Vector2f(5.0f, 6.0f);
        seam.OtherPositions[1] = new Vector2f(7.0f, 8.0f);

        var transformed = seam.Transformed(new Vector2f(2.0f, 3.0f), new Vector2f(4.0f, 5.0f));

        Assert.Equal(new Vector2f(6.0f, 11.0f), transformed.Positions[0]);
        Assert.Equal(new Vector2f(10.0f, 17.0f), transformed.Positions[1]);
        Assert.Equal(new Vector2f(14.0f, 23.0f), transformed.OtherPositions[0]);
        Assert.Equal(new Vector2f(18.0f, 29.0f), transformed.OtherPositions[1]);

        var clone = seam.Clone();
        clone.Positions[0] = Vector2f.Zero;

        Assert.Equal(new Vector2f(1.0f, 2.0f), seam.Positions[0]);
    }

    [Fact]
    public void Chart_geometry_buffer_initializes_texels_and_copies_by_value()
    {
        var seam = new LightmapSeam();
        seam.Positions[0] = Vector2f.One;
        var buffer = new LightmapChartGeometryBuffer(7, 2);
        buffer.Seams.Add(seam);

        Assert.Equal(7, buffer.Index);
        Assert.Equal(2, buffer.LightmapSize);
        Assert.Equal(4, buffer.Positions.Count);
        Assert.Equal(4, buffer.SmoothPositions.Count);
        Assert.Equal(4, buffer.SmoothNormals.Count);
        Assert.Equal(4, buffer.FaceNormals.Count);
        Assert.Equal(4, buffer.GeometryIds.Count);
        Assert.Equal(4, buffer.LightMasks.Count);
        Assert.Equal(4, buffer.BackgroundIds.Count);
        Assert.Equal(4, buffer.TexelRadiuses.Count);
        Assert.Equal(4, buffer.Albedo.Count);
        Assert.Equal(4, buffer.Emission.Count);
        Assert.Equal(new Vector2i(1, 1), buffer.IndexToLocation(3));
        Assert.True(buffer.IsValidLocation(new Vector2i(1, 1)));
        Assert.False(buffer.IsValidLocation(new Vector2i(2, 1)));
        Assert.Equal(3, buffer.LocationToIndex(new Vector2i(1, 1)));

        var clone = buffer.Clone();
        clone.Positions[0] = Vector3f.One;
        var clonedSeam = clone.Seams[0];
        clonedSeam.Positions[0] = Vector2f.Zero;

        Assert.Equal(Vector3f.Zero, buffer.Positions[0]);
        Assert.Equal(Vector2f.One, buffer.Seams[0].Positions[0]);
    }

    [Fact]
    public void Baking_scene_collections_copy_by_value()
    {
        var seam = new LightmapSeam();
        seam.Positions[0] = Vector2f.One;
        var bakingScene = new LightmapGeometryBakingScene
        {
            Index = 3,
            LightmapSize = 16,
            Seams = [seam]
        };
        var scenes = new LightmapGeometryBakingScenesArray
        {
            BakingScenes = [bakingScene],
            IdToObject = [new GeometryIdToObjectMapping()]
        };

        var clone = scenes.Clone();
        var clonedBakingScene = clone.BakingScenes[0];
        var clonedSeam = clonedBakingScene.Seams[0];
        clonedSeam.Positions[0] = Vector2f.Zero;
        clone.IdToObject.Clear();

        Assert.Equal(Vector2f.One, scenes.BakingScenes[0].Seams[0].Positions[0]);
        Assert.Single(scenes.IdToObject);
    }
}
