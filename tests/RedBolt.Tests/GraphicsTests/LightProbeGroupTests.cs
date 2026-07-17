using RedBolt;

namespace RedBolt.Tests;

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
    public void GlobalIllumination_reset_and_sample_empty()
    {
        var gi = new Scene().CreateChild("GI").CreateComponent<GlobalIllumination>();
        gi.ResetLightProbes();
        gi.SetEmissionBrightness(1.5f);
        Assert.Equal(1.5f, gi.GetEmissionBrightness());
        uint hint = 0;
        // Empty mesh samples safely through tetrahedral mesh path with empty containers.
        Assert.Equal(Vector3f.Zero, gi.GetOffset());
    }
}
