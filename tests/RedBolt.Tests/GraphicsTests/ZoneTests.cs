using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ZoneTests
{
    [Fact]
    public void Default_render_state_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        Assert.Equal(DrawableFlags.Zone, zone.GetDrawableFlags());
        Assert.Equal(Zone.DefaultAmbientColor, zone.GetAmbientColor());
        Assert.Equal(Zone.DefaultFogColor, zone.GetFogColor());
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogStart(), Zone.DefaultFogStart));
    }

    [Fact]
    public void BoundingBox_and_ambient_roundtrip()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(new Vector3(-1.0f, -2.0f, -3.0f), new Vector3(1.0f, 2.0f, 3.0f)));
        zone.SetAmbientColor(new Color(0.2f, 0.3f, 0.4f));

        Assert.True(zone.GetBoundingBox().Equals(
            new BoundingBox(new Vector3(-1.0f, -2.0f, -3.0f), new Vector3(1.0f, 2.0f, 3.0f))));
        Assert.Equal(new Color(0.2f, 0.3f, 0.4f), zone.GetAmbientColor());
    }

    [Fact]
    public void Fog_helpers_clamp_and_roundtrip()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetFogColor(new Color(0.5f, 0.6f, 0.7f));
        zone.SetFogStart(-1.0f);
        zone.SetFogEnd(-1.0f);
        zone.SetFogHeight(4.0f);
        zone.SetFogHeightScale(5.0f);
        zone.SetPriority(7);

        Assert.Equal(new Color(0.5f, 0.6f, 0.7f), zone.GetFogColor());
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogStart(), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogEnd(), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogHeight(), 4.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogHeightScale(), 5.0f));
        Assert.Equal(7, zone.GetPriority());
    }

    [Fact]
    public void Mode_flags_work()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetHeightFog(true);
        zone.SetOverride(true);
        zone.SetAmbientGradient(true);
        Assert.True(zone.GetHeightFog());
        Assert.True(zone.GetOverride());
        Assert.True(zone.GetAmbientGradient());
    }

    [Fact]
    public void Ambient_gradient_fallback_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetAmbientColor(new Color(0.2f, 0.3f, 0.4f));
        zone.SetAmbientBrightness(2.0f);
        Assert.True(zone.GetAmbientStartColor().ApproximatelyEquivalent(zone.GetAmbientColor()));
        Assert.True(zone.GetAmbientEndColor().ApproximatelyEquivalent(zone.GetAmbientColor()));
        Assert.True(zone.GetAmbientLighting().ApproximatelyEquivalent(new Vector3(0.4f, 0.6f, 0.8f)));
    }

    [Fact]
    public void Oriented_containment_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(new Vector3(-1.0f, -2.0f, -3.0f), new Vector3(1.0f, 2.0f, 3.0f)));
        zone.Node.SetTransform(new Vector3(5.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3(2.0f, 1.0f, 1.0f));
        // World box: (3,-2,-3) to (7,2,3). Point (6,0,0) is inside.
        Assert.True(zone.IsInside(new Vector3(6.0f, 0.0f, 0.0f)));
    }

    [Fact]
    public void Inverse_world_transform_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.Node.SetTransform(new Vector3(5.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3(2.0f, 1.0f, 1.0f));
        var result = zone.GetInverseWorldTransform() * new Vector3(6.0f, 0.0f, 0.0f);
        Assert.True(result.ApproximatelyEquivalent(new Vector3(0.5f, 0.0f, 0.0f)));
    }
}
