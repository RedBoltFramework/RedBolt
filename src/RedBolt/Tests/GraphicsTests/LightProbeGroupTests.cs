namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LightProbeGroupTests
{
    [Fact]
    public void Auto_placement_fills_volume_with_probes()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Probes");
        node.SetScale(2.0f);
        var group = node.CreateComponent<LightProbeGroup>();
        group.SetAutoPlacementStep(1.0f);
        group.SetAutoPlacementEnabled(true);

        Assert.True(group.GetLightProbes().Count > 0);
        Assert.True(group.GetLocalBoundingBox().Defined);
    }

    [Fact]
    public void CollectLightProbes_from_scene_gathers_enabled_groups()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Probes");
        var group = node.CreateComponent<LightProbeGroup>();
        group.SetLightProbes(
        [
            new LightProbe { Position = Vector3f.Zero },
            new LightProbe { Position = Vector3f.One }
        ]);

        var collection = new LightProbeCollection();
        var baked = new LightProbeCollectionBakedData();
        LightProbeGroup.CollectLightProbes(scene, collection, baked);
        Assert.Equal(2u, collection.GetNumProbes());
        Assert.Equal(1u, collection.GetNumGroups());
        Assert.Equal(2u, baked.Size);
    }

    [Fact]
    public void Light_probes_are_copied_and_round_trip_through_serialized_attribute()
    {
        var source = new List<LightProbe>
        {
            new() { Position = new Vector3f(1.0f, 2.0f, 3.0f) },
            new() { Position = new Vector3f(4.0f, 5.0f, 6.0f) }
        };
        var group = new LightProbeGroup();
        group.SetLightProbes(source);

        source[0] = new LightProbe { Position = Vector3f.Zero };
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), group.GetLightProbes()[0].Position);

        var restored = new LightProbeGroup();
        restored.SetSerializedLightProbes(group.GetSerializedLightProbes());

        Assert.Equal(group.GetLightProbes(), restored.GetLightProbes());

        var restoredFromNativeData = new LightProbeGroup();
        restoredFromNativeData.SetSerializedLightProbes("AgwAAIA/AAAAQAAAQEA=");
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), Assert.Single(restoredFromNativeData.GetLightProbes()).Position);
    }

    [Fact]
    public void Light_probe_collections_clone_independently()
    {
        var collection = new LightProbeCollection();
        collection.WorldPositions.Add(Vector3f.One);
        collection.Offsets.Add(0);
        collection.Counts.Add(1);
        collection.Names.Add("Probes");
        var collectionCopy = (LightProbeCollection)collection.Clone();

        var bakedData = new LightProbeCollectionBakedData();
        bakedData.SphericalHarmonics.Add(new SphericalHarmonicsDot9(Vector3f.One));
        bakedData.Ambient.Add(Vector3f.One);
        var bakedDataCopy = (LightProbeCollectionBakedData)bakedData.Clone();

        collectionCopy.Clear();
        bakedDataCopy.Clear();

        Assert.Equal(1u, collection.GetNumProbes());
        Assert.Equal(1u, bakedData.Size);
    }

    [Fact]
    public void GlobalIllumination_reset_and_sample_empty()
    {
        var gi = new Scene().CreateChild("GI").CreateComponent<GlobalIllumination>();
        gi.ResetLightProbes();
        gi.SetEmissionBrightness(1.5f);
        Assert.Equal(1.5f, gi.GetEmissionBrightness());
        Assert.True(gi.SetAttribute("Emission Brightness", 2.5f));
        Assert.Equal(2.5f, gi.GetEmissionBrightness());
        Assert.True(gi.SetAttribute("Offset", Vector3f.One));
        Assert.Equal(Vector3f.One, gi.GetOffset());
        using var dataFile = gi.GetAttribute("Data File");
        Assert.IsType<ResourceRef>(dataFile.GetObject());
        uint hint = 0;
        // Empty mesh samples safely through tetrahedral mesh path with empty containers.
        Assert.Equal(Vector3f.Zero, gi.SampleAverageAmbient(Vector3f.Zero, ref hint));
    }
}
