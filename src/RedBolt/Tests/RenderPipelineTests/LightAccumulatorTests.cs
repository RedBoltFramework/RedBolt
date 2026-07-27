using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;

namespace RedBolt.Tests;

public sealed class LightAccumulatorTests
{
    [Fact]
    public void ContextUsesCppDefaults()
    {
        var context = new LightAccumulatorContext();

        Assert.Equal(4, context.MaxVertexLights);
        Assert.Equal(1, context.MaxPixelLights);
        Assert.Null(context.Lights);
    }

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
        var context = new LightAccumulatorContext
        {
            MaxVertexLights = 2,
            MaxPixelLights = 1,
            Lights = lightData
        };
        var geometry = new CustomGeometry();
        var accumulator = new LightAccumulator();

        accumulator.AccumulateLight(context, geometry, LightImportance.Auto, 2, 2.0f);
        accumulator.AccumulateLight(context, geometry, LightImportance.Auto, 0, 0.0f);
        accumulator.AccumulateLight(context, geometry, LightImportance.Auto, 1, 1.0f);
        accumulator.Cook();

        var pixelLights = accumulator.PixelLights;
        Assert.Equal(1, pixelLights.Length);
        Assert.Equal(0, pixelLights[0].LightIndex);
        var vertexLights = accumulator.VertexLights;
        Assert.Equal(1, vertexLights[0]);
        Assert.Equal(2, vertexLights[1]);
        Assert.Equal(int.MaxValue, vertexLights[2]);
        Assert.Equal(int.MaxValue, vertexLights[3]);
        Assert.NotEqual(0u, accumulator.VertexLightsHash);
    }

    [Fact]
    public void OverflowLightIsAccumulatedIntoSphericalHarmonics()
    {
        var lightData = new[]
        {
            new LightDataForAccumulator
            {
                Color = Color.White,
                Direction = Vector3f.Right,
                LightType = LightType.Directional,
                InverseSpotCutoff = 1.0f
            },
            new LightDataForAccumulator
            {
                Color = Color.White,
                Direction = Vector3f.Right,
                LightType = LightType.Directional,
                InverseSpotCutoff = 1.0f
            }
        };
        var context = new LightAccumulatorContext
        {
            MaxVertexLights = 0,
            MaxPixelLights = 1,
            Lights = lightData
        };
        var accumulator = new LightAccumulator();

        accumulator.AccumulateLight(context, new CustomGeometry(), LightImportance.Auto, 0, 0.0f);
        accumulator.AccumulateLight(context, new CustomGeometry(), LightImportance.Auto, 1, 1.0f);

        Assert.NotEqual(Vector3f.Zero, accumulator.SphericalHarmonics.EvaluateAverage());
        Assert.Equal(1, accumulator.PixelLights.Length);
        Assert.Equal(0, accumulator.PixelLights[0].LightIndex);
    }

    [Fact]
    public void ResetClearsIndexedLightsButPreservesAccumulatedLighting()
    {
        var context = new LightAccumulatorContext
        {
            Lights = new LightDataForAccumulator[1]
        };
        var accumulator = new LightAccumulator
        {
            SphericalHarmonics = new SphericalHarmonicsDot9(Vector3f.One)
        };
        accumulator.AccumulateLight(context, new CustomGeometry(), LightImportance.Auto, 0, 0.0f);
        accumulator.Cook();

        accumulator.ResetLights();
        accumulator.Cook();

        Assert.Equal(0, accumulator.PixelLights.Length);
        Assert.Equal(int.MaxValue, accumulator.VertexLights[0]);
        Assert.NotEqual(0u, accumulator.VertexLightsHash);
        Assert.Equal(Vector3f.One, accumulator.SphericalHarmonics.EvaluateAverage());
    }

    [Fact]
    public void AccumulatorIsPodValueWithIndependentLightStorage()
    {
        Assert.True(typeof(LightDataForAccumulator).IsValueType);
        Assert.True(typeof(LightAccumulatorContext).IsValueType);
        Assert.True(typeof(LightAccumulator).IsValueType);
        Assert.Equal(Color.White, new LightDataForAccumulator().Color);

        var context = new LightAccumulatorContext
        {
            Lights = new LightDataForAccumulator[1]
        };
        var original = new LightAccumulator();
        original.AccumulateLight(context, new CustomGeometry(), LightImportance.Auto, 0, 0.0f);

        var probe = new System.Runtime.CompilerServices.StrongBox<ReflectionProbeData>();
        original.ReflectionProbes[0] = probe;

        var copy = original;
        copy.ResetLights();

        Assert.Equal(1, original.PixelLights.Length);
        Assert.Equal(0, copy.PixelLights.Length);
        Assert.Same(probe, copy.ReflectionProbes[0]);
    }
}
