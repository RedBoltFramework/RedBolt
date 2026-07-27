using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Graphics;

/// <summary>Lightmap chart allocation settings.</summary>
public struct LightmapChartingSettings
{
    /// <summary>Size of lightmap chart.</summary>
    public uint LightmapSize = 512;
    /// <summary>Padding between individual objects on the chart.</summary>
    public uint Padding = 1;
    /// <summary>Texel density in texels per Scene unit.</summary>
    public float TexelDensity = 10.0f;
    /// <summary>Minimal scale of object lightmaps.</summary>
    /// <remarks>Values below 1 may cause lightmap bleeding due to insufficient padding.
    /// Values above 0 may cause inconsistent lightmap density if object scale is too small.</remarks>
    public float MinObjectScale = 1.0f;
    /// <summary>Default chart size for models w/o metadata. Don't rely on it.</summary>
    public uint DefaultChartSize = 16;

    public LightmapChartingSettings()
    {
    }
}

/// <summary>Lightmap geometry buffer baking settings.</summary>
public struct LightmapGeometryBakingSettings
{
    /// <summary>Baking render path.</summary>
    public string RenderPathName = "RenderPaths/LightmapGBuffer.xml";
    /// <summary>Baking materials.</summary>
    public string MaterialName = "Materials/LightmapBaker.xml";
    /// <summary>Lightmap UV channel. 2nd channel by default.</summary>
    public uint UVChannel = 1;
    /// <summary>Position bias in geometry buffer in direction of face normal. Scaled with position itself.</summary>
    public float ScaledPositionBias = 0.00002f;
    /// <summary>Constant position bias.</summary>
    public float ConstantPositionBias = 0.0001f;

    public LightmapGeometryBakingSettings()
    {
    }
}

/// <summary>Settings for geometry buffer preprocessing.</summary>
public struct GeometryBufferPreprocessSettings
{
    /// <summary>Number of tasks to spawn.</summary>
    public uint NumTasks = 1;
    /// <summary>Determines how much position is pushed from behind backface to prevent shadow bleeding.</summary>
    public float ConstPositionBackfaceBias = 0.0f;
    /// <summary>Determines how much position is pushed from behind backface to prevent shadow bleeding. Scaled with position itself.</summary>
    public float ScaledPositionBackfaceBias = 0.00002f;

    public GeometryBufferPreprocessSettings()
    {
    }
}

/// <summary>Parameters of emission light tracing.</summary>
public struct EmissionLightTracingSettings
{
    /// <summary>Number of tasks to spawn.</summary>
    public uint NumTasks = 1;

    public EmissionLightTracingSettings()
    {
    }
}

/// <summary>Parameters of direct light tracing.</summary>
public struct DirectLightTracingSettings
{
    /// <summary>Number of tasks to spawn.</summary>
    public uint NumTasks = 1;
    /// <summary>Max number of samples per element.</summary>
    public uint MaxSamples = 10;

    /// <summary>Construct default.</summary>
    public DirectLightTracingSettings()
    {
    }

    /// <summary>Construct for given max samples.</summary>
    public DirectLightTracingSettings(uint maxSamples)
        : this()
    {
        MaxSamples = maxSamples;
    }
}

/// <summary>Parameters of indirect light tracing.</summary>
public struct IndirectLightTracingSettings
{
    /// <summary>Max number of bounces.</summary>
    public const uint MaximumBounces = 8;

    /// <summary>Number of tasks to spawn.</summary>
    public uint NumTasks = 1;
    /// <summary>Max number of samples per element.</summary>
    public uint MaxSamples = 10;
    /// <summary>Max number of bounces.</summary>
    public uint MaxBounces = 2;
    /// <summary>Position bias in direction of face normal after hit. Scaled with position.</summary>
    public float ScaledPositionBounceBias = 0.00002f;
    /// <summary>Constant position bias in direction of face normal after hit.</summary>
    public float ConstPositionBounceBias = 0.0f;

    /// <summary>Construct default.</summary>
    public IndirectLightTracingSettings()
    {
    }

    /// <summary>Construct for given max samples and bounces.</summary>
    public IndirectLightTracingSettings(uint maxSamples, uint maxBounces)
        : this()
    {
        MaxSamples = maxSamples;
        MaxBounces = maxBounces;
    }
}

/// <summary>Parameters for indirect light filtering.</summary>
public struct EdgeStoppingGaussFilterParameters
{
    /// <summary>Kernel radius.</summary>
    public int KernelRadius = 2;
    /// <summary>Upscale factor for offsets.</summary>
    public int Upscale = 1;
    /// <summary>Color weight. The lesser value is, the more color details are preserved on flat surface.</summary>
    public float LuminanceSigma = 10.0f;
    /// <summary>Normal weight. The higher value is, the more color details are preserved on normal edges.</summary>
    public float NormalPower = 4.0f;
    /// <summary>Position weight. The lesser value is, the more color details are preserved on position edges.</summary>
    public float PositionSigma = 1.0f;

    public EdgeStoppingGaussFilterParameters()
    {
    }

    public EdgeStoppingGaussFilterParameters(int kernelRadius)
        : this()
    {
        KernelRadius = kernelRadius;
    }
}

/// <summary>Lightmap stitching settings.</summary>
public struct LightmapStitchingSettings
{
    /// <summary>Number of iterations.</summary>
    public uint NumIterations = 8;
    /// <summary>Blend factor.</summary>
    public float BlendFactor = 0.5f;
    /// <summary>Model used for background during stitching.</summary>
    public string StitchBackgroundModelName = "Models/Plane.mdl";
    /// <summary>Technique used for background during stitching.</summary>
    public string StitchBackgroundTechniqueName = "Techniques/DiffUnlit.xml";
    /// <summary>Technique used for seams rendering during stitching.</summary>
    public string StitchSeamsTechniqueName = "Techniques/DiffUnlitAlpha.xml";

    public LightmapStitchingSettings()
    {
    }
}

/// <summary>Light calculation properties that can be used to adjust result.</summary>
public struct LightCalculationProperties
{
    /// <summary>Emission light brightness multiplier.</summary>
    public float EmissionBrightness = 1.0f;

    public LightCalculationProperties()
    {
    }
}

/// <summary>Incremental light baker settings.</summary>
public struct IncrementalLightBakerSettings
{
    /// <summary>Size of the chunk.</summary>
    public Vector3f ChunkSize = Vector3f.One * 128.0f;
    /// <summary>Additional space around chunk to collect indirect lighting.</summary>
    public float IndirectPadding = 32.0f;
    /// <summary>Shadow casting distance for directional light.</summary>
    public float DirectionalLightShadowDistance = 128.0f;
    /// <summary>Output directory name.</summary>
    public string OutputDirectory = string.Empty;
    /// <summary>Global illumination data file.</summary>
    public string GIDataFileName = "GI.bin";
    /// <summary>Lightmap name format string. Placeholder 1: global lightmap index.</summary>
    public string LightmapNameFormat = "Textures/Lightmap-{}.png";
    /// <summary>Light probe group name format string. Placeholders 1-3 are chunk index; placeholder 4 is the group index.</summary>
    public string LightProbeGroupNameFormat = "Binary/LightProbeGroup-{}-{}-{}-{}.bin";

    public IncrementalLightBakerSettings()
    {
    }
}

/// <summary>Aggregated light baking settings.</summary>
public struct LightBakingSettings
{
    /// <summary>Charting settings.</summary>
    public LightmapChartingSettings Charting = new();
    /// <summary>Geometry baking settings.</summary>
    public LightmapGeometryBakingSettings GeometryBufferBaking = new();
    /// <summary>Geometry buffer preprocessing settings.</summary>
    public GeometryBufferPreprocessSettings GeometryBufferPreprocessing = new();

    /// <summary>Settings for emission light tracing.</summary>
    public EmissionLightTracingSettings EmissionTracing = new();
    /// <summary>Settings for direct light tracing for charts.</summary>
    public DirectLightTracingSettings DirectChartTracing = new(10);
    /// <summary>Settings for direct light tracing for light probes.</summary>
    public DirectLightTracingSettings DirectProbesTracing = new(32);
    /// <summary>Settings for indirect light tracing for charts.</summary>
    public IndirectLightTracingSettings IndirectChartTracing = new(10, 2);
    /// <summary>Settings for indirect light tracing for light probes.</summary>
    public IndirectLightTracingSettings IndirectProbesTracing = new(64, 2);

    /// <summary>Direct light filtering settings.</summary>
    public EdgeStoppingGaussFilterParameters DirectFilter = new(2);
    /// <summary>Indirect light filtering settings.</summary>
    public EdgeStoppingGaussFilterParameters IndirectFilter = new(5);

    /// <summary>Stitching settings.</summary>
    public LightmapStitchingSettings Stitching = new();
    /// <summary>Calculation properties.</summary>
    public LightCalculationProperties Properties = new();
    /// <summary>Incremental light baker settings.</summary>
    public IncrementalLightBakerSettings Incremental = new();

    public LightBakingSettings()
    {
    }
}
