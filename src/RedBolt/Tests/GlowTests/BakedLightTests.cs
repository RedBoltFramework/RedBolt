namespace RedBolt.Tests;

using RedBolt.Glow;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BakedLightTests
{
    [Fact]
    public void Baked_light_captures_spot_light_state()
    {
        using var scene = new Scene();
        var node = scene.CreateChild("Light");
        node.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        node.SetRotation(new Quaternion(15.0f, 30.0f, 45.0f));
        var light = node.CreateComponent<Light>();
        light.SetLightType(LightType.Spot);
        light.SetLightMode(LightMode.Baked);
        light.SetLightMask(0x12345678u);
        light.SetColor(new Color(0.25f, 0.5f, 0.75f));
        light.SetBrightness(2.0f);
        light.SetIndirectBrightness(0.75f);
        light.SetRange(12.0f);
        light.SetFov(60.0f);
        light.SetRadius(0.5f);

        var bakedLight = new BakedLight(light);

        Assert.Equal(LightType.Spot, bakedLight.LightType);
        Assert.Equal(LightMode.Baked, bakedLight.LightMode);
        Assert.Equal(0x12345678u, bakedLight.LightMask);
        Assert.Equal(light.GetEffectiveColor().GammaToLinear(), bakedLight.Color);
        Assert.Equal(0.75f, bakedLight.IndirectBrightness);
        Assert.Equal(60.0f, bakedLight.Fov);
        Assert.Equal(MathDefs.Cos(30.0f), bakedLight.Cutoff);
        Assert.Equal(12.0f, bakedLight.Distance);
        Assert.Equal(0.5f, bakedLight.Radius);
        Assert.Equal(0.0f, bakedLight.Angle);
        Assert.Equal(0.0f, bakedLight.HalfAngleTan);
        Assert.Equal(node.GetWorldPosition(), bakedLight.Position);
        Assert.Equal(node.GetWorldDirection(), bakedLight.Direction);
        Assert.Equal(node.GetWorldRotation(), bakedLight.Rotation);
    }

    [Fact]
    public void Baked_directional_light_uses_radius_as_angle()
    {
        using var scene = new Scene();
        var light = scene.CreateChild("Light").CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetRadius(4.0f);

        var bakedLight = new BakedLight(light);

        Assert.Equal(0.0f, bakedLight.Radius);
        Assert.Equal(4.0f, bakedLight.Angle);
        Assert.Equal(MathDefs.Tan(2.0f), bakedLight.HalfAngleTan);
    }
}
