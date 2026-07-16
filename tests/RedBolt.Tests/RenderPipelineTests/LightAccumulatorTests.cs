namespace RedBolt.Tests;

public sealed class LightAccumulatorTests
{
    [Fact]
    public void DirectionalLightDataHasConstantDirectionAndDistance()
    {
        var light = new LightDataForAccumulator
        {
            Direction = new Vector3f(0.0f, 1.0f, 0.0f),
            LightType = LightType.Directional
        };

        var result = light.GetDirectionToLight(new Vector3f(10.0f, 20.0f, 30.0f));

        Assert.Equal(light.Direction, result.Direction);
        Assert.Equal(1.0f, result.Attenuation);
    }

    [Fact]
    public void PointLightDataUsesLinearDistanceAttenuation()
    {
        var light = new LightDataForAccumulator
        {
            Position = new Vector3f(10.0f, 0.0f, 0.0f),
            LightType = LightType.Point,
            Range = 20.0f
        };

        var result = light.GetDirectionToLight(Vector3f.Zero);

        Assert.Equal(Vector3f.Right, result.Direction);
        Assert.Equal(0.5f, result.Attenuation);
    }

    [Fact]
    public void AccumulatorPartitionsAndSortsPixelAndVertexLights()
    {
        var lightData = new LightDataForAccumulator[3];
        var context = new LightAccumulatorContext(lightData, maxVertexLights: 2, maxPixelLights: 1);
        var geometry = new CustomGeometry();
        var accumulator = new LightAccumulator();

        accumulator.AccumulateLight(context, geometry, LightImportance.Auto, 2, 2.0f);
        accumulator.AccumulateLight(context, geometry, LightImportance.Auto, 0, 0.0f);
        accumulator.AccumulateLight(context, geometry, LightImportance.Auto, 1, 1.0f);
        accumulator.Cook();

        var pixelLights = accumulator.GetPixelLights();
        Assert.Equal(1, pixelLights.Length);
        Assert.Equal(0u, pixelLights[0].LightIndex);
        var vertexLights = accumulator.GetVertexLights();
        Assert.Equal(1u, vertexLights[0]);
        Assert.Equal(2u, vertexLights[1]);
        Assert.Equal(uint.MaxValue, vertexLights[2]);
        Assert.Equal(uint.MaxValue, vertexLights[3]);
        Assert.NotEqual(0u, accumulator.GetVertexLightsHash());
    }
}
