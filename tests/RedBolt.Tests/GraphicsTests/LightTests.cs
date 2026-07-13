using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LightTests
{
    [Fact]
    public void Default_type_and_mode_work()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        Assert.Equal(DrawableFlags.Light, light.GetDrawableFlags());
        Assert.Equal(LightType.Point, light.GetLightType());
        Assert.Equal(LightImportance.Auto, light.GetLightImportance());
        Assert.Equal(LightMode.Realtime, light.GetLightMode());
    }

    [Fact]
    public void Effective_mask_and_range_clamp()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetRange(-1.0f);
        light.SetFov(200.0f);

        // Default is Realtime, so effective mask equals light mask.
        Assert.NotEqual(0u, light.GetLightMaskEffective());
        Assert.False(light.GetPerVertex());
        Assert.True(MathDefs.ApproximatelyEquivalent(light.GetRange(), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(light.GetFov(), MathDefs.MaxFov));
    }

    [Fact]
    public void Baked_mode_has_zero_effective_mask()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetLightMode(LightMode.Baked);
        Assert.Equal(0u, light.GetLightMaskEffective());
    }

    [Fact]
    public void Effective_color_and_specular_work()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetColor(new Color(0.5f, 0.25f, 0.125f));
        light.SetBrightness(2.0f);
        light.SetSpecularIntensity(-1.0f);

        Assert.True(light.GetEffectiveColor().ApproximatelyEquivalent(new Color(1.0f, 0.5f, 0.25f, 1.0f)));
        Assert.True(MathDefs.ApproximatelyEquivalent(light.GetEffectiveSpecularIntensity(), 0.0f));
    }

    [Fact]
    public void Shadow_bias_validation_works()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetShadowBias(new BiasParameters(2.0f, 20.0f, -1.0f));
        Assert.Equal(new BiasParameters(1.0f, 16.0f, 0.0f), light.GetShadowBias());
    }

    [Fact]
    public void Shadow_cascade_validation_works()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetShadowCascade(new CascadeParameters(-1.0f, 5.0f, 10.0f, 0.0f, -1.0f, 2.0f));
        Assert.Equal(2, light.GetNumShadowSplits());
    }

    [Fact]
    public void Shadow_scalar_validation_works()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetShadowIntensity(2.0f);
        light.SetShadowResolution(0.0f);
        light.SetShadowNearFarRatio(1.0f);
        light.SetShadowMaxExtrusion(-1.0f);

        Assert.True(MathDefs.ApproximatelyEquivalent(light.GetShadowIntensity(), 1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(light.GetShadowResolution(), 0.125f));
        Assert.True(MathDefs.ApproximatelyEquivalent(light.GetShadowNearFarRatio(), 0.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(light.GetShadowMaxExtrusion(), 0.0f));
    }

    [Fact]
    public void Realtime_mask_works()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetLightMode(LightMode.Realtime);
        light.SetRange(100.0f);
        Assert.Equal(light.GetLightMask(), light.GetLightMaskEffective());
    }

    [Fact]
    public void Directional_bounds_are_infinite()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        // Directional light has zero distance and infinite bounding box.
        // The default bounding box for directional light contains the origin.
        Assert.NotEqual(Intersection.Outside, light.GetWorldBoundingBox().IsInside(Vector3f.Zero));
    }

    [Fact]
    public void Point_bounds_follow_world_position_and_range()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();
        light.SetRange(4.0f);
        light.Node.SetPosition(new Vector3f(5.0f, 6.0f, 7.0f));

        Assert.Equal(
            new BoundingBox(new Vector3f(1.0f, 2.0f, 3.0f), new Vector3f(9.0f, 10.0f, 11.0f)),
            light.GetWorldBoundingBox());
    }

    [Fact]
    public void Intensity_sort_value_matches_light_polarity()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();

        light.SetIntensitySortValue(10.0f);
        Assert.True(light.GetSortValue() > 0.0f);

        light.SetColor(new Color(-1.0f, -1.0f, -1.0f));
        light.SetIntensitySortValue(10.0f);
        Assert.True(light.GetSortValue() < 0.0f);
    }

    [Fact]
    public void Texture_attributes_roundtrip_empty_resources()
    {
        var light = new Scene().CreateChild("Light").CreateComponent<Light>();

        light.SetRampTextureAttr(ResourceRef.Empty);
        light.SetShapeTextureAttr(ResourceRef.Empty);

        Assert.Null(light.GetRampTexture());
        Assert.Null(light.GetShapeTexture());
        Assert.Equal(StringHash.Calculate("Texture2D"), light.GetRampTextureAttr().Type);
        Assert.Equal(StringHash.Calculate("Texture"), light.GetShapeTextureAttr().Type);
    }
}
