namespace RedBolt.Tests;

using System;

using RedBolt.Glow;
using RedBolt.Math;

public sealed class BakedSceneChunkTests
{
    [Fact]
    public void Light_probe_collection_for_baking_clones_base_and_baking_data()
    {
        var collection = new LightProbeCollectionForBaking();
        collection.WorldPositions.Add(Vector3f.One);
        collection.Offsets.Add(2);
        collection.Counts.Add(3);
        collection.Names.Add("Probe");
        collection.LightMasks.Add(4);
        collection.BackgroundIds.Add(5);

        var clone = Assert.IsType<LightProbeCollectionForBaking>(((ICloneable)collection).Clone());
        collection.WorldPositions[0] = Vector3f.Zero;
        collection.Offsets[0] = 0;
        collection.Counts[0] = 0;
        collection.Names[0] = string.Empty;
        collection.LightMasks[0] = 0;
        collection.BackgroundIds[0] = 0;

        Assert.Equal(Vector3f.One, clone.WorldPositions[0]);
        Assert.Equal(2u, clone.Offsets[0]);
        Assert.Equal(3u, clone.Counts[0]);
        Assert.Equal("Probe", clone.Names[0]);
        Assert.Equal(4u, clone.LightMasks[0]);
        Assert.Equal(5u, clone.BackgroundIds[0]);
    }
}
