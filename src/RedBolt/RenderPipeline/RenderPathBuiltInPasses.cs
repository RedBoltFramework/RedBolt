using System.Linq.Expressions;
using System.Reflection;

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
    private readonly Dictionary<string, object?> shaderParameters = new(StringComparer.Ordinal);
    private Parameters currentParameters = new();
    private CachedBuffers? cachedBuffers;
    private bool isAdaptedLuminanceInitialized;

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

    public override void Update(SharedRenderPassState sharedState)
    {
        base.Update(sharedState);

        if (currentParameters.AutoExposure)
            EnsureCachedBuffers(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        if (sharedState.RenderBufferManager is not { } renderBufferManager || sharedState.Graphics is not { } graphics)
            return;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        if (currentParameters.AutoExposure)
        {
            var buffers = EnsureCachedBuffers(sharedState);
            if (buffers is null)
                return;

            EvaluateDownsampledColorBuffer(renderBufferManager, graphics, buffers);
            EvaluateLuminance(renderBufferManager, graphics, buffers);
            EvaluateAdaptedLuminance(renderBufferManager, graphics, buffers);
        }

        shaderParameters.Clear();
        StructShaderParameters.Write(new ApplyExposureParameters
        {
            MinMaxExposure = new Vector2(currentParameters.MinExposure, currentParameters.MaxExposure),
            AutoExposureMiddleGrey = 0.6f
        }, shaderParameters);

        renderBufferManager.SetOutputRenderTargets(graphics);
        graphics.DrawFullscreenQuad(
            "Apply exposure",
            "v2/P_AutoExposure",
            currentParameters.AutoExposure ? "EXPOSURE AUTOEXPOSURE" : "EXPOSURE",
            BlendMode.Replace,
            bilinearColorSampler: true,
            renderBufferManager.GetSecondaryColorTexture(),
            secondaryTexture: currentParameters.AutoExposure ? cachedBuffers?.AdaptedLuminance.Texture : null,
            depthTexture: null,
            shaderParameters,
            renderBufferManager.GetInvOutputSize(),
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());
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

    private sealed class CachedBuffers
    {
        public required TextureRenderBuffer Color128 { get; init; }
        public required TextureRenderBuffer Lum64 { get; init; }
        public required TextureRenderBuffer Lum16 { get; init; }
        public required TextureRenderBuffer Lum4 { get; init; }
        public required TextureRenderBuffer Lum1 { get; init; }
        public required TextureRenderBuffer AdaptedLuminance { get; init; }
        public required TextureRenderBuffer PreviousAdaptedLuminance { get; init; }
    }

    private struct InputInvSizeParameters
    {
        [ShaderParameterName("InputInvSize")]
        public Vector2 InputInvSize;
    }

    private struct AdaptRateParameters
    {
        [ShaderParameterName("AdaptRate")]
        public float AdaptRate;
    }

    private struct ApplyExposureParameters
    {
        [ShaderParameterName("MinMaxExposure")]
        public Vector2 MinMaxExposure;

        [ShaderParameterName("AutoExposureMiddleGrey")]
        public float AutoExposureMiddleGrey;
    }

    private CachedBuffers? EnsureCachedBuffers(SharedRenderPassState sharedState)
    {
        if (sharedState.RenderBufferManager is not { } manager)
            return null;

        cachedBuffers ??= new CachedBuffers
        {
            Color128 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RGBA16F, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2(128, 128)),
            Lum64 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RG16F, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2(64, 64)),
            Lum16 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RG16F, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2(16, 16)),
            Lum4 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RG16F, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2(4, 4)),
            Lum1 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RG16F, 1, RenderBufferFlag.FixedTextureSize), new Vector2(1, 1)),
            AdaptedLuminance = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RG16F, 1, RenderBufferFlag.FixedTextureSize | RenderBufferFlag.Persistent), new Vector2(1, 1)),
            PreviousAdaptedLuminance = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RG16F, 1, RenderBufferFlag.FixedTextureSize), new Vector2(1, 1))
        };
        return cachedBuffers;
    }

    private void EvaluateDownsampledColorBuffer(RenderBufferManager renderBufferManager, Graphics graphics, CachedBuffers buffers)
    {
        var viewportTexture = renderBufferManager.GetSecondaryColorTexture();
        if (viewportTexture is null)
            return;

        renderBufferManager.SetRenderTargets(graphics, buffers.Color128);
        graphics.DrawCopyTexture(viewportTexture, bilinearFiltering: true);
    }

    private void EvaluateLuminance(RenderBufferManager renderBufferManager, Graphics graphics, CachedBuffers buffers)
    {
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Color128, buffers.Lum64, "LUMINANCE64", new Vector2(1.0f / 128.0f, 1.0f / 128.0f));
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Lum64, buffers.Lum16, "LUMINANCE16", new Vector2(1.0f / 64.0f, 1.0f / 64.0f));
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Lum16, buffers.Lum4, "LUMINANCE4", new Vector2(1.0f / 16.0f, 1.0f / 16.0f));
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Lum4, buffers.Lum1, "LUMINANCE1", new Vector2(1.0f / 4.0f, 1.0f / 4.0f));
    }

    private void EvaluateAdaptedLuminance(RenderBufferManager renderBufferManager, Graphics graphics, CachedBuffers buffers)
    {
        renderBufferManager.SetRenderTargets(graphics, buffers.PreviousAdaptedLuminance);
        var previousSource = isAdaptedLuminanceInitialized ? buffers.AdaptedLuminance.Texture : buffers.Lum1.Texture;
        graphics.DrawCopyTexture(previousSource, bilinearFiltering: false);

        shaderParameters.Clear();
        StructShaderParameters.Write(new AdaptRateParameters
        {
            AdaptRate = currentParameters.AdaptRate
        }, shaderParameters);

        renderBufferManager.SetRenderTargets(graphics, buffers.AdaptedLuminance);
        graphics.DrawFullscreenQuad(
            "Adapt luminosity",
            "v2/P_AutoExposure",
            "ADAPTLUMINANCE",
            BlendMode.Replace,
            bilinearColorSampler: true,
            buffers.PreviousAdaptedLuminance.Texture,
            secondaryTexture: buffers.Lum1.Texture,
            depthTexture: null,
            shaderParameters,
            new Vector2(1.0f, 1.0f),
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());

        isAdaptedLuminanceInitialized = true;
    }

    private void DrawLuminanceStep(RenderBufferManager renderBufferManager, Graphics graphics, TextureRenderBuffer source,
        TextureRenderBuffer target, string shaderDefines, Vector2 inputInvSize)
    {
        shaderParameters.Clear();
        StructShaderParameters.Write(new InputInvSizeParameters
        {
            InputInvSize = inputInvSize
        }, shaderParameters);

        renderBufferManager.SetRenderTargets(graphics, target);
        graphics.DrawFullscreenQuad(
            "Downsample luminosity buffer",
            "v2/P_AutoExposure",
            shaderDefines,
            BlendMode.Replace,
            bilinearColorSampler: true,
            source.Texture,
            secondaryTexture: null,
            depthTexture: null,
            shaderParameters,
            renderBufferManager.GetInvOutputSize(),
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());
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
    private static readonly Vector3 LinearLuminanceWeights = new(0.2126f, 0.7152f, 0.0722f);
    private static readonly Vector3 GammaLuminanceWeights = new(0.299f, 0.587f, 0.114f);
    private readonly Dictionary<string, object?> shaderParameters = new(StringComparer.Ordinal);
    private readonly List<TextureCache> textures = [];
    private readonly List<float> intensityMultipliers = [];
    private Parameters currentParameters = new();
    private bool isHdr;

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

        if (currentParameters.NumIterations != updated.NumIterations)
            InvalidateTextureCache();

        currentParameters = updated;
        SetInternallyEnabled(true);
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        base.Update(sharedState);

        var nextHdr = sharedState.RenderBufferManager?.IsHDR() ?? false;
        if (isHdr != nextHdr)
        {
            isHdr = nextHdr;
            InvalidateTextureCache();
        }

        RestoreTextureCache(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        if (sharedState.RenderBufferManager is not { } renderBufferManager || sharedState.Graphics is not { } graphics)
            return;

        RestoreTextureCache(sharedState);
        if (textures.Count == 0)
            return;

        var luminanceWeights = renderBufferManager.IsLinearColorSpace() ? LinearLuminanceWeights : GammaLuminanceWeights;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        var numIterations = GatherBrightRegions(renderBufferManager, graphics, textures[0].Final, luminanceWeights);
        for (var i = 0u; i < numIterations; ++i)
        {
            if (i > 0)
                CopyTexture(renderBufferManager, graphics, textures[(int)i - 1].Final, textures[(int)i].Final);

            BlurTexture(renderBufferManager, graphics, textures[(int)i].Final, textures[(int)i].Temporary, luminanceWeights);
        }

        intensityMultipliers.Clear();
        var totalIntensity = 0.0f;
        for (var i = 0u; i < numIterations; ++i)
        {
            var intensity = MathF.Pow(currentParameters.IterationFactor, i);
            intensityMultipliers.Add(intensity);
            totalIntensity += intensity;
        }

        if (totalIntensity <= MathDefs.Epsilon)
            totalIntensity = 1.0f;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);
        renderBufferManager.SetOutputRenderTargets(graphics);
        for (var i = 0; i < intensityMultipliers.Count; ++i)
        {
            var normalizedIntensity = intensityMultipliers[i] * currentParameters.Intensity / totalIntensity;
            ApplyBloom(renderBufferManager, graphics, textures[i].Final, normalizedIntensity);
        }
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

    private sealed class TextureCache
    {
        public required TextureRenderBuffer Final { get; init; }
        public required TextureRenderBuffer Temporary { get; init; }
    }

    private struct BloomParameters
    {
        [ShaderParameterName("LuminanceWeights")]
        public Vector3 LuminanceWeights;

        [ShaderParameterName("Intensity")]
        public float Intensity;

        [ShaderParameterName("InputInvSize")]
        public Vector2 InputInvSize;

        [ShaderParameterName("Threshold")]
        public Vector2 Threshold;
    }

    private void InvalidateTextureCache()
    {
        textures.Clear();
    }

    private void RestoreTextureCache(SharedRenderPassState sharedState)
    {
        if (textures.Count > 0 || sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

        for (var i = 0u; i < currentParameters.NumIterations; ++i)
        {
            var format = isHdr ? TextureFormat.RGBA16F : TextureFormat.RGBA8;
            var bufferParams = new RenderBufferParams(format, 1, RenderBufferFlag.BilinearFiltering);
            var sizeMultiplier = Vector2.One / (1u << (int)i);
            textures.Add(new TextureCache
            {
                Final = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier),
                Temporary = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier)
            });
        }
    }

    private uint GatherBrightRegions(RenderBufferManager renderBufferManager, Graphics graphics, TextureRenderBuffer destination, Vector3 luminanceWeights)
    {
        var viewportTexture = renderBufferManager.GetSecondaryColorTexture();
        if (viewportTexture is null)
            return 0;

        var minDimension = Math.Min(viewportTexture.Width, viewportTexture.Height);
        if (minDimension <= 0)
            return 0;

        shaderParameters.Clear();
        StructShaderParameters.Write(CreateBloomParameters(
            luminanceWeights,
            intensity: 0.0f,
            new Vector2(1.0f / viewportTexture.Width, 1.0f / viewportTexture.Height)),
            shaderParameters);

        renderBufferManager.SetRenderTargets(graphics, destination);
        graphics.DrawFullscreenQuad(
            "Gather bright regions",
            "v2/P_Bloom",
            "BRIGHT",
            BlendMode.Replace,
            bilinearColorSampler: true,
            viewportTexture,
            secondaryTexture: null,
            depthTexture: null,
            shaderParameters,
            renderBufferManager.GetInvOutputSize(),
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());

        var numIterations = 0u;
        while (minDimension > 1 && numIterations < currentParameters.NumIterations)
        {
            minDimension >>= 1;
            ++numIterations;
        }

        return Math.Clamp(numIterations, 1u, currentParameters.NumIterations);
    }

    private void BlurTexture(RenderBufferManager renderBufferManager, Graphics graphics, TextureRenderBuffer final,
        TextureRenderBuffer temporary, Vector3 luminanceWeights)
    {
        var texture = final.Texture;
        var invInputSize = new Vector2(
            texture.Width > 0 ? 1.0f / texture.Width : 0.0f,
            texture.Height > 0 ? 1.0f / texture.Height : 0.0f);

        shaderParameters.Clear();
        StructShaderParameters.Write(CreateBloomParameters(luminanceWeights, intensity: 0.0f, invInputSize), shaderParameters);

        renderBufferManager.SetRenderTargets(graphics, temporary);
        graphics.DrawFullscreenQuad(
            "Blur vertically",
            "v2/P_Bloom",
            "BLURH",
            BlendMode.Replace,
            bilinearColorSampler: true,
            texture,
            secondaryTexture: null,
            depthTexture: null,
            shaderParameters,
            invInputSize,
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());

        renderBufferManager.SetRenderTargets(graphics, final);
        graphics.DrawFullscreenQuad(
            "Blur horizontally",
            "v2/P_Bloom",
            "BLURV",
            BlendMode.Replace,
            bilinearColorSampler: true,
            temporary.Texture,
            secondaryTexture: null,
            depthTexture: null,
            shaderParameters,
            invInputSize,
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());
    }

    private void CopyTexture(RenderBufferManager renderBufferManager, Graphics graphics, TextureRenderBuffer source, TextureRenderBuffer destination)
    {
        renderBufferManager.SetRenderTargets(graphics, destination);
        renderBufferManager.DrawTexture(graphics, "Downscale bloom", source.Texture);
    }

    private void ApplyBloom(RenderBufferManager renderBufferManager, Graphics graphics, TextureRenderBuffer bloom, float intensity)
    {
        shaderParameters.Clear();
        StructShaderParameters.Write(CreateBloomParameters(Vector3.Zero, intensity, renderBufferManager.GetInvOutputSize()), shaderParameters);
        graphics.DrawFullscreenQuad(
            "Apply bloom",
            "v2/P_Bloom",
            "COMBINE",
            BlendMode.Add,
            bilinearColorSampler: true,
            bloom.Texture,
            secondaryTexture: null,
            depthTexture: null,
            shaderParameters,
            renderBufferManager.GetInvOutputSize(),
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());
    }

    private BloomParameters CreateBloomParameters(Vector3 luminanceWeights, float intensity, Vector2 inputInvSize)
    {
        var thresholdGap = Math.Max(0.01f, currentParameters.MaxBrightness - currentParameters.MinBrightness);
        return new BloomParameters
        {
            LuminanceWeights = luminanceWeights,
            Intensity = intensity,
            InputInvSize = inputInvSize,
            Threshold = new Vector2(currentParameters.MinBrightness, 1.0f / thresholdGap)
        };
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
    private static readonly Matrix4 FlipMatrix = new(
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, -1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f);
    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters = new Dictionary<string, object?>(0, StringComparer.Ordinal);
    private readonly Dictionary<string, object?> shaderParameters = new(StringComparer.Ordinal);
    private RenderBuffer? normalBuffer;
    private TextureCache? textures;
    private Parameters currentParameters = new();

    public AmbientOcclusionPass(string? passName = null)
        : base(passName)
    {
        SetComment(DefaultComment);
        SetTraits(new RenderPassTraits(NeedReadWriteColorBuffer: false, NeedBilinearColorSampler: false, NeedReadableDepth: true));
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

        if (currentParameters.Downscale != updated.Downscale)
            InvalidateTextureCache();

        currentParameters = updated;
        SetInternallyEnabled(true);
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        base.Update(sharedState);
        RestoreTextureCache(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        ConnectToRenderBuffer(ref normalBuffer, SharedRenderPassState.NormalBufferId, sharedState, required: false);

        if (sharedState.RenderBufferManager is not { } renderBufferManager
            || sharedState.Graphics is not { } graphics
            || sharedState.RenderCamera is not { } camera
            || currentParameters.Strength <= 0.0f)
        {
            return;
        }

        RestoreTextureCache(sharedState);
        if (textures?.CurrentTarget is null || textures.PreviousTarget is null)
            return;

        var depthTexture = renderBufferManager.GetDepthStencilTexture();
        if (depthTexture is null)
            return;

        var clipToTextureSpace = Matrix4.Identity;
        clipToTextureSpace.Scale = new Vector3(0.5f, 0.5f, 1.0f);
        clipToTextureSpace.Translation = new Vector3(0.5f, 0.5f, 0.0f);

        var viewToTextureSpace = clipToTextureSpace * camera.GetGPUProjection();
        var textureToViewSpace = viewToTextureSpace.Inverted;

        EvaluateAO(renderBufferManager, graphics, camera, depthTexture, viewToTextureSpace, textureToViewSpace);
        BlurTexture(renderBufferManager, graphics, depthTexture, textureToViewSpace);
        Blit(renderBufferManager, graphics);
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

    private sealed class TextureCache
    {
        public Texture2D? Noise { get; init; }

        public required TextureRenderBuffer CurrentTarget { get; set; }

        public required TextureRenderBuffer PreviousTarget { get; set; }
    }

    private struct AmbientOcclusionParameters
    {
        [ShaderParameterName("InputInvSize")]
        public Vector2 InputInvSize;

        [ShaderParameterName("BlurStep")]
        public Vector2 BlurStep;

        [ShaderParameterName("Strength")]
        public float Strength;

        [ShaderParameterName("Exponent")]
        public float Exponent;

        [ShaderParameterName("RadiusInfo")]
        public Vector4 RadiusInfo;

        [ShaderParameterName("FadeDistance")]
        public Vector2 FadeDistance;

        [ShaderParameterName("ViewToTexture")]
        public Matrix4 ViewToTexture;

        [ShaderParameterName("TextureToView")]
        public Matrix4 TextureToView;

        [ShaderParameterName("WorldToView")]
        public Matrix4 WorldToView;

        [ShaderParameterName("BlurZThreshold")]
        public float BlurZThreshold;

        [ShaderParameterName("BlurNormalInvThreshold")]
        public float BlurNormalInvThreshold;
    }

    private void InvalidateTextureCache()
    {
        textures = null;
    }

    private void RestoreTextureCache(SharedRenderPassState sharedState)
    {
        if (textures is not null || sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

        var resourceCache = Context.TryGetInstance()?.GetSubsystem<ResourceCache>();
        var sizeMultiplier = Vector2.One / (1u << (int)currentParameters.Downscale);
        var bufferParams = new RenderBufferParams(TextureFormat.RGBA8, 1, RenderBufferFlag.BilinearFiltering);
        textures = new TextureCache
        {
            Noise = resourceCache?.GetResourceOrNull<Texture2D>("Textures/SSAONoise.png"),
            CurrentTarget = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier),
            PreviousTarget = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier)
        };
    }

    private void EvaluateAO(RenderBufferManager renderBufferManager, Graphics graphics, Camera camera, Texture depthTexture,
        Matrix4 viewToTextureSpace, Matrix4 textureToViewSpace)
    {
        if (textures?.CurrentTarget is null)
            return;

        var inputInvSize = renderBufferManager.GetInvOutputSize();
        var worldToViewSpace = camera.GetView().Matrix4;
        var worldToViewSpaceCorrected = camera.GetFlipVertical() ? FlipMatrix * worldToViewSpace : worldToViewSpace;

        var radiusSlope = (currentParameters.RadiusFar - currentParameters.RadiusNear)
            / Math.Max(1.0f, currentParameters.DistanceFar - currentParameters.DistanceNear);
        var radiusInfo = new Vector4(
            radiusSlope,
            currentParameters.RadiusNear - radiusSlope * currentParameters.DistanceNear,
            currentParameters.RadiusNear,
            currentParameters.RadiusFar);

        shaderParameters.Clear();
        StructShaderParameters.Write(new AmbientOcclusionParameters
        {
            InputInvSize = inputInvSize,
            BlurStep = inputInvSize,
            Strength = currentParameters.Strength,
            Exponent = currentParameters.Exponent,
            RadiusInfo = radiusInfo,
            FadeDistance = new Vector2(currentParameters.FadeDistanceBegin, currentParameters.FadeDistanceEnd),
            ViewToTexture = viewToTextureSpace,
            TextureToView = textureToViewSpace,
            WorldToView = worldToViewSpaceCorrected
        }, shaderParameters);

        renderBufferManager.SetRenderTargets(graphics, textures.CurrentTarget);
        graphics.DrawFullscreenQuad(
            "Apply SSAO",
            "v2/P_SSAO",
            normalBuffer is not null ? "EVALUATE_OCCLUSION DEFERRED" : "EVALUATE_OCCLUSION",
            BlendMode.Replace,
            bilinearColorSampler: true,
            textures.Noise,
            secondaryTexture: normalBuffer?.GetTexture(),
            depthTexture: depthTexture,
            shaderParameters,
            inputInvSize,
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());

        (textures.CurrentTarget, textures.PreviousTarget) = (textures.PreviousTarget, textures.CurrentTarget);
    }

    private void BlurTexture(RenderBufferManager renderBufferManager, Graphics graphics, Texture depthTexture, Matrix4 textureToViewSpace)
    {
        if (textures?.CurrentTarget is null || textures.PreviousTarget is null)
            return;

        var textureSize = new IntVector2(textures.CurrentTarget.Texture.Width, textures.CurrentTarget.Texture.Height);
        var blurStep = new Vector2(
            textureSize.X > 0 ? 1.0f / textureSize.X : 0.0f,
            textureSize.Y > 0 ? 1.0f / textureSize.Y : 0.0f);

        var shaderDefines = normalBuffer is not null ? "BLUR DEFERRED" : "BLUR";
        BlurAxis(renderBufferManager, graphics, depthTexture, textureToViewSpace, new Vector2(blurStep.X, 0.0f), shaderDefines, "SSAO Blur Horizontally");
        BlurAxis(renderBufferManager, graphics, depthTexture, textureToViewSpace, new Vector2(0.0f, blurStep.Y), shaderDefines, "SSAO Blur Vertically");
    }

    private void BlurAxis(RenderBufferManager renderBufferManager, Graphics graphics, Texture depthTexture,
        Matrix4 textureToViewSpace, Vector2 blurStep, string shaderDefines, string debugComment)
    {
        if (textures?.CurrentTarget is null || textures.PreviousTarget is null)
            return;

        shaderParameters.Clear();
        StructShaderParameters.Write(new AmbientOcclusionParameters
        {
            BlurStep = blurStep,
            BlurZThreshold = currentParameters.BlurDepthThreshold,
            BlurNormalInvThreshold = 1.0f - currentParameters.BlurNormalThreshold,
            TextureToView = textureToViewSpace
        }, shaderParameters);

        renderBufferManager.SetRenderTargets(graphics, textures.CurrentTarget);
        graphics.DrawFullscreenQuad(
            debugComment,
            "v2/P_SSAO",
            shaderDefines,
            BlendMode.Replace,
            bilinearColorSampler: true,
            textures.PreviousTarget.Texture,
            secondaryTexture: normalBuffer?.GetTexture(),
            depthTexture: depthTexture,
            shaderParameters,
            blurStep,
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());

        (textures.CurrentTarget, textures.PreviousTarget) = (textures.PreviousTarget, textures.CurrentTarget);
    }

    private void Blit(RenderBufferManager renderBufferManager, Graphics graphics)
    {
        if (textures?.PreviousTarget is null)
            return;

        renderBufferManager.SetOutputRenderTargets(graphics);
        graphics.DrawFullscreenQuad(
            "SSAO Combine",
            "v2/P_SSAO",
            "COMBINE",
            BlendMode.Alpha,
            bilinearColorSampler: true,
            textures.PreviousTarget.Texture,
            secondaryTexture: null,
            depthTexture: null,
            EmptyParameters,
            renderBufferManager.GetInvOutputSize(),
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());
    }
}

/// <summary>
/// Post-processing pass that renders outline from the color buffer.
/// </summary>
public sealed class OutlineRenderPass : RenderPass
{
    private const string DefaultComment = "Draw outline of the contents of the color buffer";
    public static readonly StringHash ColorBufferId = "outline";
    private readonly Dictionary<string, object?> shaderParameters = new(StringComparer.Ordinal);
    private RenderBuffer? colorBuffer;

    public OutlineRenderPass(string? passName = null)
        : base(passName)
    {
        SetComment(DefaultComment);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        ConnectToRenderBuffer(ref colorBuffer, ColorBufferId, sharedState, required: false);

        if (colorBuffer?.GetTexture() is not Texture texture
            || sharedState.RenderBufferManager is not { } renderBufferManager
            || sharedState.Graphics is not { } graphics)
        {
            return;
        }

        shaderParameters.Clear();
        shaderParameters["InputInvSize"] = new Vector2(
            texture.Width > 0 ? 1.0f / texture.Width : 0.0f,
            texture.Height > 0 ? 1.0f / texture.Height : 0.0f);

        renderBufferManager.SetOutputRenderTargets(graphics);
        graphics.DrawFullscreenQuad(
            "Apply outline",
            "v2/P_Outline",
            renderBufferManager.IsLinearColorSpace() ? "URHO3D_GAMMA_CORRECTION" : string.Empty,
            BlendMode.Alpha,
            bilinearColorSampler: true,
            texture,
            secondaryTexture: null,
            depthTexture: null,
            shaderParameters,
            renderBufferManager.GetInvOutputSize(),
            renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale());
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

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class ShaderParameterNameAttribute : Attribute
{
    public ShaderParameterNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

internal static class StructShaderParameters
{
    public static void Write<T>(in T value, IDictionary<string, object?> target)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(target);
        StructShaderParameterCache<T>.Write(value, target);
    }

    private static class StructShaderParameterCache<T>
        where T : struct
    {
        private static readonly Entry[] Entries = CreateEntries();

        public static void Write(in T value, IDictionary<string, object?> target)
        {
            foreach (var entry in Entries)
                target[entry.Name] = entry.Getter(value);
        }

        private static Entry[] CreateEntries()
        {
            var accessors = new List<Entry>();
            var parameter = Expression.Parameter(typeof(T), "value");

            foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var name = field.GetCustomAttribute<ShaderParameterNameAttribute>()?.Name ?? field.Name;
                var access = Expression.Field(parameter, field);
                var convert = Expression.Convert(access, typeof(object));
                var getter = Expression.Lambda<Func<T, object?>>(convert, parameter).Compile();
                accessors.Add(new Entry(name, getter));
            }

            foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetMethod is null || property.GetIndexParameters().Length > 0)
                    continue;

                var name = property.GetCustomAttribute<ShaderParameterNameAttribute>()?.Name ?? property.Name;
                var access = Expression.Property(parameter, property);
                var convert = Expression.Convert(access, typeof(object));
                var getter = Expression.Lambda<Func<T, object?>>(convert, parameter).Compile();
                accessors.Add(new Entry(name, getter));
            }

            return accessors.ToArray();
        }

        private readonly record struct Entry(string Name, Func<T, object?> Getter);
    }
}
