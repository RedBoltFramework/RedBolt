namespace RedBolt;

/// <summary>
/// Render pass that applies a shader to the entire output area.
/// </summary>
public sealed class FullScreenShaderPass : RenderPass
{
    private readonly Dictionary<string, object?> parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> currentParameters = new(StringComparer.Ordinal);
    private string shaderName = string.Empty;
    private string shaderDefines = string.Empty;
    private BlendMode blendMode = BlendMode.Replace;
    private bool needReadWriteColorOutput;
    private bool bilinearColorSampler;
    private bool disableOnDefaultParameters;
    private string parametersPrefix = string.Empty;

    public FullScreenShaderPass(string? passName = null)
        : base(passName)
    {
        RegisterAttribute(
            "Shader Name",
            typeof(string),
            self => ((FullScreenShaderPass)self).shaderName,
            (self, value) => ((FullScreenShaderPass)self).shaderName = value as string ?? string.Empty,
            string.Empty);
        RegisterAttribute(
            "Shader Defines",
            typeof(string),
            self => ((FullScreenShaderPass)self).shaderDefines,
            (self, value) => ((FullScreenShaderPass)self).shaderDefines = value as string ?? string.Empty,
            string.Empty);
        RegisterAttribute(
            "Blend Mode",
            typeof(BlendMode),
            self => ((FullScreenShaderPass)self).blendMode,
            (self, value) => ((FullScreenShaderPass)self).blendMode = ParseBlendMode(value),
            BlendMode.Replace);
        RegisterAttribute(
            "Read+Write Color Output",
            typeof(bool),
            self => ((FullScreenShaderPass)self).needReadWriteColorOutput,
            (self, value) => ((FullScreenShaderPass)self).needReadWriteColorOutput = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Bilinear Color Sampler",
            typeof(bool),
            self => ((FullScreenShaderPass)self).bilinearColorSampler,
            (self, value) => ((FullScreenShaderPass)self).bilinearColorSampler = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Disable On Default Parameters",
            typeof(bool),
            self => ((FullScreenShaderPass)self).disableOnDefaultParameters,
            (self, value) => ((FullScreenShaderPass)self).disableOnDefaultParameters = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Parameters Prefix",
            typeof(string),
            self => ((FullScreenShaderPass)self).parametersPrefix,
            (self, value) => ((FullScreenShaderPass)self).parametersPrefix = value as string ?? string.Empty,
            string.Empty);
        RegisterAttribute(
            "Parameters",
            typeof(Dictionary<string, object?>),
            self => new Dictionary<string, object?>(((FullScreenShaderPass)self).parameters, StringComparer.Ordinal),
            (self, value) => ((FullScreenShaderPass)self).SetParameters(value),
            null);
    }

    public override void CollectParameters(IDictionary<string, object?> target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (var (name, defaultValue) in parameters)
        {
            var externalName = parametersPrefix + name;
            DeclareParameter(externalName, defaultValue, target);
        }
    }

    public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> externalParameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(externalParameters);

        currentParameters.Clear();
        var shouldBeDisabled = disableOnDefaultParameters;
        foreach (var (name, defaultValue) in parameters)
        {
            var externalName = parametersPrefix + name;
            if (externalParameters.TryGetValue(externalName, out var value))
            {
                currentParameters[name] = value;
                if (shouldBeDisabled && !Equals(value, defaultValue))
                    shouldBeDisabled = false;
            }
            else
                currentParameters[name] = defaultValue;
        }

        SetTraits(new RenderPassTraits(needReadWriteColorOutput, bilinearColorSampler));
        SetInternallyEnabled(!shouldBeDisabled);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        if (sharedState.RenderBufferManager is not { } renderBufferManager || sharedState.Graphics is not { } graphics || string.IsNullOrWhiteSpace(shaderName))
            return;

        if (needReadWriteColorOutput)
            renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        renderBufferManager.SetOutputRenderTargets(graphics);
        if (needReadWriteColorOutput)
        {
            renderBufferManager.DrawFeedbackViewportQuad(
                graphics,
                shaderName,
                shaderDefines,
                blendMode,
                bilinearColorSampler,
                currentParameters,
                GetComment());
        }
        else
        {
            renderBufferManager.DrawViewportQuad(
                graphics,
                shaderName,
                shaderDefines,
                blendMode,
                bilinearColorSampler,
                currentParameters,
                GetComment());
        }
    }

    public override RenderPass Clone()
    {
        var clone = new FullScreenShaderPass(GetPassName())
        {
            shaderName = shaderName,
            shaderDefines = shaderDefines,
            blendMode = blendMode,
            needReadWriteColorOutput = needReadWriteColorOutput,
            bilinearColorSampler = bilinearColorSampler,
            disableOnDefaultParameters = disableOnDefaultParameters,
            parametersPrefix = parametersPrefix
        };
        clone.SetEnabledByDefault(IsEnabledByDefault());
        clone.SetComment(GetComment());
        foreach (var (name, value) in parameters)
            clone.parameters[name] = value;
        return clone;
    }

    private void SetParameters(object? value)
    {
        parameters.Clear();
        if (value is not IReadOnlyDictionary<string, object?> values)
            return;

        foreach (var (name, parameterValue) in values)
            parameters[name] = parameterValue;
    }

    private static BlendMode ParseBlendMode(object? value)
    {
        return value switch
        {
            BlendMode mode => mode,
            string name when Enum.TryParse<BlendMode>(name, ignoreCase: true, out var mode) => mode,
            int numeric when numeric >= 0 && numeric <= (int)BlendMode.Decal => (BlendMode)numeric,
            _ => BlendMode.Replace
        };
    }
}

/// <summary>
/// Post-processing pass that converts HDR linear color input to LDR gamma color.
/// </summary>
public sealed class ToneMappingPass : RenderPass
{
    private const string DefaultComment = "Convert color from linear HDR space to gamma LDR space";
    private const string ModeName = "Tone Mapping: Mode";
    private const string ModeMetadataName = "Tone Mapping: Mode@";
    private static readonly string[] ModeMetadata = ["None", "Reinhard", "ReinhardWhite", "Uncharted2"];
    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters = new Dictionary<string, object?>(0, StringComparer.Ordinal);
    private Mode mode;

    public ToneMappingPass(string? passName = null)
        : base(passName)
    {
        SetComment(DefaultComment);
        SetTraits(new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: false));
    }

    public override void CollectParameters(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        DeclareParameter(ModeName, (int)Mode.None, parameters);
        DeclareParameter(ModeMetadataName, ModeMetadata, parameters);
    }

    public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);

        if (settings.RenderBufferManager.ColorSpace != RenderPipelineColorSpace.LinearHDR)
        {
            SetInternallyEnabled(false);
            return;
        }

        SetInternallyEnabled(true);
        if (LoadParameter(ModeName, parameters) is int modeValue)
            mode = (Mode)Math.Clamp(modeValue, 0, (int)Mode.Count);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        if (sharedState.RenderBufferManager is not { } renderBufferManager || sharedState.Graphics is not { } graphics)
            return;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);
        renderBufferManager.SetOutputRenderTargets(graphics);
        renderBufferManager.DrawFeedbackViewportQuad(
            graphics,
            "v2/P_ToneMapping",
            GetModeDefines(mode),
            BlendMode.Replace,
            bilinearColorSampler: true,
            parameters: EmptyParameters,
            debugComment: "Apply tone mapping");
    }

    public override RenderPass Clone()
    {
        var clone = new ToneMappingPass(GetPassName())
        {
            mode = mode
        };
        clone.SetEnabledByDefault(IsEnabledByDefault());
        clone.SetComment(GetComment());
        return clone;
    }

    private enum Mode
    {
        None,
        Reinhard,
        ReinhardWhite,
        Uncharted2,
        Count
    }

    private static string GetModeDefines(Mode mode)
    {
        return mode switch
        {
            Mode.Reinhard => "REINHARD",
            Mode.ReinhardWhite => "REINHARDWHITE",
            Mode.Uncharted2 => "UNCHARTED2",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Post-processing pass that adjusts HDR scene exposure.
/// </summary>
public sealed class AutoExposurePass : RenderPass
{
    private const string DefaultComment = "Adjust exposure of the camera within specified range";
    private const string AutoExposureName = "Exposure: Automatic";
    private const string MinExposureName = "Exposure: Min";
    private const string MaxExposureName = "Exposure: Max";
    private const string AdaptRateName = "Exposure: Adapt Rate";
    private Parameters currentParameters = new();

    public AutoExposurePass(string? passName = null)
        : base(passName)
    {
        SetComment(DefaultComment);
        SetTraits(new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: false));
    }

    public override void CollectParameters(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var defaults = new Parameters();
        DeclareParameter(AutoExposureName, defaults.AutoExposure, parameters);
        DeclareParameter(MinExposureName, defaults.MinExposure, parameters);
        DeclareParameter(MaxExposureName, defaults.MaxExposure, parameters);
        DeclareParameter(AdaptRateName, defaults.AdaptRate, parameters);
    }

    public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);

        if (settings.RenderBufferManager.ColorSpace != RenderPipelineColorSpace.LinearHDR)
        {
            SetInternallyEnabled(false);
            return;
        }

        var updated = new Parameters();
        if (LoadParameter(AutoExposureName, parameters) is bool automatic)
            updated.AutoExposure = automatic;
        if (RenderPathParameterHelpers.TryGetFloat(MinExposureName, parameters, out var minExposure))
            updated.MinExposure = minExposure;
        if (RenderPathParameterHelpers.TryGetFloat(MaxExposureName, parameters, out var maxExposure))
            updated.MaxExposure = maxExposure;
        if (RenderPathParameterHelpers.TryGetFloat(AdaptRateName, parameters, out var adaptRate))
            updated.AdaptRate = adaptRate;

        currentParameters = updated;
        SetInternallyEnabled(true);
    }

    public override RenderPass Clone()
    {
        var clone = new AutoExposurePass(GetPassName())
        {
            currentParameters = currentParameters
        };
        clone.SetEnabledByDefault(IsEnabledByDefault());
        clone.SetComment(GetComment());
        return clone;
    }

    private struct Parameters
    {
        public bool AutoExposure;
        public float MinExposure = 1.0f;
        public float MaxExposure = 3.0f;
        public float AdaptRate = 0.6f;

        public Parameters()
        {
        }
    }
}

/// <summary>
/// Post-processing pass that applies bloom to the scene.
/// </summary>
public sealed class BloomPass : RenderPass
{
    private const uint MaxIterations = 16;
    private const string DefaultComment = "Create bloom around bright parts of the image";
    private const string NumIterationsName = "Bloom: Num Iterations";
    private const string MinBrightnessName = "Bloom: Min Brightness";
    private const string MaxBrightnessName = "Bloom: Max Brightness";
    private const string IntensityName = "Bloom: Base Intensity";
    private const string IterationFactorName = "Bloom: Iteration Intensity Factor";
    private Parameters currentParameters = new();

    public BloomPass(string? passName = null)
        : base(passName)
    {
        SetComment(DefaultComment);
        SetTraits(new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: true));
    }

    public override void CollectParameters(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var defaults = new Parameters();
        DeclareParameter(NumIterationsName, defaults.NumIterations, parameters);
        DeclareParameter(MinBrightnessName, defaults.MinBrightness, parameters);
        DeclareParameter(MaxBrightnessName, defaults.MaxBrightness, parameters);
        DeclareParameter(IntensityName, defaults.Intensity, parameters);
        DeclareParameter(IterationFactorName, defaults.IterationFactor, parameters);
    }

    public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);

        var updated = new Parameters();
        if (RenderPathParameterHelpers.TryGetUInt(NumIterationsName, parameters, out var numIterations))
            updated.NumIterations = Math.Clamp(numIterations, 1u, MaxIterations);
        if (RenderPathParameterHelpers.TryGetFloat(MinBrightnessName, parameters, out var minBrightness))
            updated.MinBrightness = minBrightness;
        if (RenderPathParameterHelpers.TryGetFloat(MaxBrightnessName, parameters, out var maxBrightness))
            updated.MaxBrightness = maxBrightness;
        if (RenderPathParameterHelpers.TryGetFloat(IntensityName, parameters, out var intensity))
            updated.Intensity = intensity;
        if (RenderPathParameterHelpers.TryGetFloat(IterationFactorName, parameters, out var iterationFactor))
            updated.IterationFactor = iterationFactor;

        currentParameters = updated;
        SetInternallyEnabled(true);
    }

    public override RenderPass Clone()
    {
        var clone = new BloomPass(GetPassName())
        {
            currentParameters = currentParameters
        };
        clone.SetEnabledByDefault(IsEnabledByDefault());
        clone.SetComment(GetComment());
        return clone;
    }

    private struct Parameters
    {
        public uint NumIterations = 5;
        public float MinBrightness = 0.8f;
        public float MaxBrightness = 1.0f;
        public float Intensity = 1.0f;
        public float IterationFactor = 1.0f;

        public Parameters()
        {
        }
    }
}

/// <summary>
/// Post-processing pass that renders screen-space ambient occlusion.
/// </summary>
public sealed class AmbientOcclusionPass : RenderPass
{
    private const string DefaultComment = "Screen-space ambient occlusion";
    private const string DownscaleName = "SSAO: Downscale";
    private const string StrengthName = "SSAO: Strength";
    private const string ExponentName = "SSAO: Exponent";
    private const string RadiusNearName = "SSAO: Near Radius";
    private const string DistanceNearName = "SSAO: Near Distance";
    private const string RadiusFarName = "SSAO: Far Radius";
    private const string DistanceFarName = "SSAO: Far Distance";
    private const string FadeDistanceBeginName = "SSAO: Begin Fade Distance";
    private const string FadeDistanceEndName = "SSAO: End Fade Distance";
    private const string BlurDepthThresholdName = "SSAO: Depth Threshold";
    private const string BlurNormalThresholdName = "SSAO: Normal Threshold";
    private Parameters currentParameters = new();

    public AmbientOcclusionPass(string? passName = null)
        : base(passName)
    {
        SetComment(DefaultComment);
    }

    public override void CollectParameters(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var defaults = new Parameters();
        DeclareParameter(DownscaleName, defaults.Downscale, parameters);
        DeclareParameter(StrengthName, defaults.Strength, parameters);
        DeclareParameter(ExponentName, defaults.Exponent, parameters);
        DeclareParameter(RadiusNearName, defaults.RadiusNear, parameters);
        DeclareParameter(DistanceNearName, defaults.DistanceNear, parameters);
        DeclareParameter(RadiusFarName, defaults.RadiusFar, parameters);
        DeclareParameter(DistanceFarName, defaults.DistanceFar, parameters);
        DeclareParameter(FadeDistanceBeginName, defaults.FadeDistanceBegin, parameters);
        DeclareParameter(FadeDistanceEndName, defaults.FadeDistanceEnd, parameters);
        DeclareParameter(BlurDepthThresholdName, defaults.BlurDepthThreshold, parameters);
        DeclareParameter(BlurNormalThresholdName, defaults.BlurNormalThreshold, parameters);
    }

    public override void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!settings.RenderBufferManager.ReadableDepth)
        {
            SetInternallyEnabled(false);
            return;
        }

        var updated = new Parameters();
        if (RenderPathParameterHelpers.TryGetUInt(DownscaleName, parameters, out var downscale))
            updated.Downscale = downscale;
        if (RenderPathParameterHelpers.TryGetFloat(StrengthName, parameters, out var strength))
            updated.Strength = strength;
        if (RenderPathParameterHelpers.TryGetFloat(ExponentName, parameters, out var exponent))
            updated.Exponent = exponent;
        if (RenderPathParameterHelpers.TryGetFloat(RadiusNearName, parameters, out var radiusNear))
            updated.RadiusNear = radiusNear;
        if (RenderPathParameterHelpers.TryGetFloat(DistanceNearName, parameters, out var distanceNear))
            updated.DistanceNear = distanceNear;
        if (RenderPathParameterHelpers.TryGetFloat(RadiusFarName, parameters, out var radiusFar))
            updated.RadiusFar = radiusFar;
        if (RenderPathParameterHelpers.TryGetFloat(DistanceFarName, parameters, out var distanceFar))
            updated.DistanceFar = distanceFar;
        if (RenderPathParameterHelpers.TryGetFloat(FadeDistanceBeginName, parameters, out var fadeDistanceBegin))
            updated.FadeDistanceBegin = fadeDistanceBegin;
        if (RenderPathParameterHelpers.TryGetFloat(FadeDistanceEndName, parameters, out var fadeDistanceEnd))
            updated.FadeDistanceEnd = fadeDistanceEnd;
        if (RenderPathParameterHelpers.TryGetFloat(BlurDepthThresholdName, parameters, out var blurDepthThreshold))
            updated.BlurDepthThreshold = blurDepthThreshold;
        if (RenderPathParameterHelpers.TryGetFloat(BlurNormalThresholdName, parameters, out var blurNormalThreshold))
            updated.BlurNormalThreshold = blurNormalThreshold;

        currentParameters = updated;
        SetInternallyEnabled(true);
    }

    public override RenderPass Clone()
    {
        var clone = new AmbientOcclusionPass(GetPassName())
        {
            currentParameters = currentParameters
        };
        clone.SetEnabledByDefault(IsEnabledByDefault());
        clone.SetComment(GetComment());
        return clone;
    }

    private struct Parameters
    {
        public uint Downscale;
        public float Strength = 0.7f;
        public float Exponent = 1.5f;
        public float RadiusNear = 0.05f;
        public float DistanceNear = 1.0f;
        public float RadiusFar = 1.0f;
        public float DistanceFar = 100.0f;
        public float FadeDistanceBegin = 100.0f;
        public float FadeDistanceEnd = 200.0f;
        public float BlurDepthThreshold = 0.1f;
        public float BlurNormalThreshold = 0.2f;

        public Parameters()
        {
        }
    }
}

/// <summary>
/// Post-processing pass that renders outline from the color buffer.
/// </summary>
public sealed class OutlineRenderPass : RenderPass
{
    private const string DefaultComment = "Draw outline of the contents of the color buffer";

    public OutlineRenderPass(string? passName = null)
        : base(passName)
    {
        SetComment(DefaultComment);
    }

    public override RenderPass Clone()
    {
        var clone = new OutlineRenderPass(GetPassName());
        clone.SetEnabledByDefault(IsEnabledByDefault());
        clone.SetComment(GetComment());
        return clone;
    }
}

internal static class RenderPathBuiltInPassFactory
{
    public static RenderPass? Create(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return typeName switch
        {
            nameof(AutoExposurePass) => new AutoExposurePass(),
            nameof(AmbientOcclusionPass) => new AmbientOcclusionPass(),
            nameof(BloomPass) => new BloomPass(),
            nameof(FullScreenShaderPass) => new FullScreenShaderPass(),
            nameof(OutlineRenderPass) => new OutlineRenderPass(),
            nameof(ToneMappingPass) => new ToneMappingPass(),
            _ => null
        };
    }

}

internal static class RenderPathParameterHelpers
{
    public static bool TryGetFloat(string name, IReadOnlyDictionary<string, object?> parameters, out float value)
    {
        if (Load(name, parameters) is float floatValue)
        {
            value = floatValue;
            return true;
        }

        if (Load(name, parameters) is double doubleValue)
        {
            value = (float)doubleValue;
            return true;
        }

        if (Load(name, parameters) is int intValue)
        {
            value = intValue;
            return true;
        }

        value = 0.0f;
        return false;
    }

    public static bool TryGetUInt(string name, IReadOnlyDictionary<string, object?> parameters, out uint value)
    {
        if (Load(name, parameters) is uint uintValue)
        {
            value = uintValue;
            return true;
        }

        if (Load(name, parameters) is int intValue && intValue >= 0)
        {
            value = (uint)intValue;
            return true;
        }

        value = 0u;
        return false;
    }

    private static object? Load(string name, IReadOnlyDictionary<string, object?> parameters)
    {
        return parameters.TryGetValue(name, out var value) ? value : null;
    }
}
