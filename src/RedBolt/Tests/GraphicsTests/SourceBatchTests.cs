using RedBolt;

using RedBolt.Graphics;
using RedBolt.Math;
namespace RedBolt.Tests;

public sealed class SourceBatchTests
{
    [Fact]
    public void Defaults_match_cpp_source_batch()
    {
        var batch = new SourceBatch();

        Assert.Equal(0.0f, batch.Distance);
        Assert.Null(batch.Geometry);
        Assert.Null(batch.Material);
        Assert.Same(batch.WorldTransform, new SourceBatch().WorldTransform);
        Assert.Equal(Matrix3x4.Identity, batch.WorldTransform[0]);
        Assert.Equal(1u, batch.NumWorldTransforms);
        Assert.Null(batch.InstancingData);
        Assert.Equal(GeometryType.Static, batch.GeometryType);
        Assert.Null(batch.LightmapScaleOffset);
        Assert.Equal(0u, batch.LightmapIndex);
    }

    [Fact]
    public void Equality_matches_cpp_compared_fields()
    {
        var first = new SourceBatch();
        var second = new SourceBatch();

        Assert.Equal(first, second);
        Assert.False(first != second);

        second.LightmapIndex = 1;
        second.LightmapScaleOffset = new Vector4f(1.0f, 2.0f, 3.0f, 4.0f);
        Assert.Equal(first, second);

        second.Distance = 1.0f;
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Clone_preserves_shallow_copy_semantics()
    {
        var batch = new SourceBatch
        {
            Distance = 3.0f,
            WorldTransform = [Matrix3x4.Identity, Matrix3x4.FromTranslation(Vector3f.One)],
            NumWorldTransforms = 2,
            GeometryType = GeometryType.Skinned
        };

        var clone = (SourceBatch)batch.Clone();

        Assert.Equal(batch, clone);
        Assert.Same(batch.WorldTransform, clone.WorldTransform);
    }
}
