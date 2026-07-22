namespace RedBolt;

/// <summary>
/// Render pass that applies a shader to the entire output area.
/// </summary>
public sealed class FullScreenShaderPass : RenderPass
{
    private readonly StringVariantMap _parameters = new();
    private readonly List<StringHash> _shaderParameterSources = [];
    private readonly List<Variant> _shaderParameterDefaults = [];
    private readonly List<ShaderParameterDesc> _shaderParameters = [];
    private readonly List<ShaderResourceDesc> _shaderResources = [];
    private StaticPipelineStateId _pipelineStateId;
    private string _debugComment = string.Empty;
    private string _shaderName = string.Empty;
    private string _shaderDefines = string.Empty;
    private BlendMode _blendMode = BlendMode.Replace;
    private bool _needReadWriteColorOutput;
    private bool _bilinearColorSampler;
    private bool _disableOnDefaultParameters;
    private string _parametersPrefix = string.Empty;

    public FullScreenShaderPass()
    {
        RegisterAttribute(
            "Shader Name",
            typeof(string),
            self => ((FullScreenShaderPass)self)._shaderName,
            (self, value) => ((FullScreenShaderPass)self).SetShaderName(value as string ?? string.Empty),
            string.Empty);
        RegisterAttribute(
            "Shader Defines",
            typeof(string),
            self => ((FullScreenShaderPass)self)._shaderDefines,
            (self, value) => ((FullScreenShaderPass)self).SetShaderDefines(value as string ?? string.Empty),
            string.Empty);
        RegisterAttribute(
            "Blend Mode",
            typeof(BlendMode),
            self => ((FullScreenShaderPass)self)._blendMode,
            (self, value) => ((FullScreenShaderPass)self).SetBlendMode(ParseBlendMode(value)),
            BlendMode.Replace);
        RegisterAttribute(
            "Read+Write Color Output",
            typeof(bool),
            self => ((FullScreenShaderPass)self)._needReadWriteColorOutput,
            (self, value) => ((FullScreenShaderPass)self).SetNeedReadWriteColorOutput(value is bool enabled && enabled),
            false);
        RegisterAttribute(
            "Bilinear Color Sampler",
            typeof(bool),
            self => ((FullScreenShaderPass)self)._bilinearColorSampler,
            (self, value) => ((FullScreenShaderPass)self)._bilinearColorSampler = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Disable On Default Parameters",
            typeof(bool),
            self => ((FullScreenShaderPass)self)._disableOnDefaultParameters,
            (self, value) => ((FullScreenShaderPass)self)._disableOnDefaultParameters = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Parameters Prefix",
            typeof(string),
            self => ((FullScreenShaderPass)self)._parametersPrefix,
            (self, value) => ((FullScreenShaderPass)self).SetParametersPrefix(value as string ?? string.Empty),
            string.Empty);
        RegisterAttribute(
            "Parameters",
            typeof(StringVariantMap),
            self => ((FullScreenShaderPass)self)._parameters,
            (self, value) => ((FullScreenShaderPass)self).SetParameters(value),
            null);
    }

    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<FullScreenShaderPass>(ObjectCategory.Category_RenderPass);
    }

    public override void CollectParameters(StringVariantMap target)
    {
        foreach (var (name, defaultValue) in _parameters)
            DeclareParameter(_parametersPrefix + name, defaultValue, target);
    }

    public override void InitializeView(RenderPipelineView view)
    {
        ClearShaderParameterStorage();

        foreach (var (name, defaultValue) in _parameters)
        {
            _shaderParameterSources.Add(new StringHash(_parametersPrefix + name));
            _shaderParameterDefaults.Add(defaultValue.Clone());
            _shaderParameters.Add(new ShaderParameterDesc
            {
                Name = new StringHash(name),
                Value = defaultValue.Clone()
            });
        }
        _pipelineStateId = StaticPipelineStateId.Invalid;
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
        var shouldBeDisabled = _disableOnDefaultParameters;
        for (var i = 0; i < _shaderParameters.Count; ++i)
        {
            foreach (var (name, value) in parameters)
            {
                if (StringHash.Calculate(name) != _shaderParameterSources[i].Value)
                    continue;

                var oldParameter = _shaderParameters[i];
                _shaderParameters[i] = new ShaderParameterDesc
                {
                    Name = oldParameter.Name,
                    Value = value.Clone()
                };
                oldParameter.Dispose();
                if (shouldBeDisabled && value != _shaderParameterDefaults[i])
                    shouldBeDisabled = false;
                break;
            }
        }

        _traits = new RenderPassTraits(_needReadWriteColorOutput, _bilinearColorSampler);
        _isEnabledInternally = !shouldBeDisabled;
    }

    public override void Dispose()
    {
        ClearShaderParameterStorage();
        _parameters.Dispose();
        base.Dispose();
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        if (sharedState.RenderBufferManager is not { } renderBufferManager || string.IsNullOrEmpty(_shaderName))
            return;

        if (_pipelineStateId == StaticPipelineStateId.Invalid)
        {
            _debugComment = $"Apply shader \"{_shaderName}\"({_shaderDefines})";
            IReadOnlyList<NamedSamplerStateDesc> samplers = _needReadWriteColorOutput
                ? [("Albedo", SamplerStateDesc.Bilinear())]
                : Array.Empty<NamedSamplerStateDesc>();
            _pipelineStateId = renderBufferManager.CreateQuadPipelineState(
                _blendMode, _shaderName, _shaderDefines, samplers);
        }

        if (_needReadWriteColorOutput)
            renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        renderBufferManager.SetOutputRenderTargets();
        if (_needReadWriteColorOutput)
        {
            renderBufferManager.DrawFeedbackViewportQuad(
                _debugComment, _pipelineStateId, _shaderResources, _shaderParameters);
        }
        else
        {
            renderBufferManager.DrawViewportQuad(
                _debugComment, _pipelineStateId, _shaderResources, _shaderParameters);
        }
    }

    private void SetParameters(object? value)
    {
        _parameters.Clear();
        if (value is not IReadOnlyDictionary<string, Variant> values)
            return;

        foreach (var (name, parameterValue) in values)
            _parameters[name] = parameterValue;
    }

    private void SetShaderName(string value)
    {
        _shaderName = value;
        InvalidateCache();
    }

    private void SetShaderDefines(string value)
    {
        _shaderDefines = value;
        InvalidateCache();
    }

    private void SetBlendMode(BlendMode value)
    {
        _blendMode = value;
        InvalidateCache();
    }

    private void SetNeedReadWriteColorOutput(bool value)
    {
        _needReadWriteColorOutput = value;
        InvalidateCache();
    }

    private void InvalidateCache()
    {
        _pipelineStateId = StaticPipelineStateId.Invalid;
        _debugComment = string.Empty;
    }

    private void SetParametersPrefix(string prefix)
    {
        _parametersPrefix = prefix;
    }

    private void ClearShaderParameterStorage()
    {
        for (var i = _shaderParameters.Count - 1; i >= 0; --i)
        {
            var parameter = _shaderParameters[i];
            parameter.Dispose();
        }
        _shaderParameters.Clear();

        var defaults = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_shaderParameterDefaults);
        for (var i = defaults.Length - 1; i >= 0; --i)
            defaults[i].Dispose();
        _shaderParameterDefaults.Clear();

        _shaderParameterSources.Clear();
        _shaderResources.Clear();
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
    private static readonly StringVector ModeMetadata = ["None", "Reinhard", "ReinhardWhite", "Uncharted2"];
    private static readonly NamedSamplerStateDesc[] Samplers =
        [("Albedo", SamplerStateDesc.Bilinear())];
    private static readonly ShaderResourceDesc[] EmptyResources = [];
    private static readonly ShaderParameterDesc[] EmptyParameters = [];
    private StaticPipelineStateId? _pipelineState;
    private Mode _mode;

    public ToneMappingPass()
    {
        SetComment(DefaultComment);
        _traits = new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: false);
    }

    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<ToneMappingPass>(ObjectCategory.Category_RenderPass);
    }

    public override void CollectParameters(StringVariantMap parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        DeclareParameter(ModeName, (int)Mode.None, parameters);
        using var modeMetadata = new Variant(ModeMetadata);
        DeclareParameter(ModeMetadataName, modeMetadata, parameters);
    }

    public override void InitializeView(RenderPipelineView view)
    {
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);

        if (settings.RenderBufferManager.ColorSpace != RenderPipelineColorSpace.LinearHDR)
        {
            _isEnabledInternally = false;
            InvalidateCache();
            return;
        }

        _isEnabledInternally = true;
        ref readonly var value = ref LoadParameter(ModeName, parameters);
        if (value.Type == VariantType.Int)
        {
            var newMode = (Mode)Math.Clamp(value.GetInt(), 0, (int)Mode.Count);
            if (_mode != newMode)
            {
                _mode = newMode;
                InvalidateCache();
            }
        }
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        if (sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

        RestoreCache(sharedState);
        renderBufferManager.SwapColorBuffers(synchronizeContents: false);
        renderBufferManager.SetOutputRenderTargets();
        renderBufferManager.DrawFeedbackViewportQuad(
            "Apply tone mapping",
            _pipelineState!.Value,
            EmptyResources,
            EmptyParameters);
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
            Mode.Reinhard => "REINHARD ",
            Mode.ReinhardWhite => "REINHARDWHITE ",
            Mode.Uncharted2 => "UNCHARTED2 ",
            _ => string.Empty
        };
    }

    private void InvalidateCache()
    {
        _pipelineState = null;
    }

    private void RestoreCache(SharedRenderPassState sharedState)
    {
        if (_pipelineState.HasValue || sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

        _pipelineState = renderBufferManager.CreateQuadPipelineState(
            BlendMode.Replace,
            "v2/P_ToneMapping",
            GetModeDefines(_mode),
            Samplers);
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
    private Parameters _currentParameters = new();
    private CachedBuffers? _cachedBuffers;
    private bool _isAdaptedLuminanceInitialized;

    public AutoExposurePass()
    {
        SetComment(DefaultComment);
        _traits = new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: false);
    }

    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<AutoExposurePass>(ObjectCategory.Category_RenderPass);
    }

    public override void CollectParameters(StringVariantMap parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var defaults = new Parameters();
        DeclareParameter(AutoExposureName, defaults.AutoExposure, parameters);
        DeclareParameter(MinExposureName, defaults.MinExposure, parameters);
        DeclareParameter(MaxExposureName, defaults.MaxExposure, parameters);
        DeclareParameter(AdaptRateName, defaults.AdaptRate, parameters);
    }

    public override void InitializeView(RenderPipelineView view)
    {
        var renderDevice = GetSubsystem<RenderDevice>()!;
        SubscribeToEvent<GraphicsEvents.DeviceResetEvent>(renderDevice,
            (ref GraphicsEvents.DeviceResetEvent _) => _isAdaptedLuminanceInitialized = false);
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);

        if (settings.RenderBufferManager.ColorSpace != RenderPipelineColorSpace.LinearHDR)
        {
            _isEnabledInternally = false;
            return;
        }

        var updated = new Parameters();
        ref readonly var autoExposure = ref LoadParameter(AutoExposureName, parameters);
        if (autoExposure.Type == VariantType.Bool)
            updated.AutoExposure = autoExposure.GetBool();
        ref readonly var minExposure = ref LoadParameter(MinExposureName, parameters);
        if (minExposure.Type == VariantType.Float)
            updated.MinExposure = minExposure.GetFloat();
        ref readonly var maxExposure = ref LoadParameter(MaxExposureName, parameters);
        if (maxExposure.Type == VariantType.Float)
            updated.MaxExposure = maxExposure.GetFloat();
        ref readonly var adaptRate = ref LoadParameter(AdaptRateName, parameters);
        if (adaptRate.Type == VariantType.Float)
            updated.AdaptRate = adaptRate.GetFloat();

        if (_currentParameters.AutoExposure != updated.AutoExposure)
            _cachedBuffers = null;

        _isEnabledInternally = true;
        _currentParameters = updated;
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        base.Update(sharedState);

        if (_currentParameters.AutoExposure)
            EnsureCachedBuffers(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        if (sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

        var graphics = GetSubsystem<Graphics>()!;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        if (_currentParameters.AutoExposure)
        {
            var buffers = EnsureCachedBuffers(sharedState);
            if (buffers is null)
                return;

            EvaluateDownsampledColorBuffer(renderBufferManager, graphics, buffers);
            EvaluateLuminance(renderBufferManager, graphics, buffers);
            EvaluateAdaptedLuminance(renderBufferManager, graphics, buffers);
        }

        var applyExposureParameters = new ApplyExposureParameters
        {
            MinMaxExposure = new Vector2f(_currentParameters.MinExposure, _currentParameters.MaxExposure),
            AutoExposureMiddleGrey = 0.6f
        };

        renderBufferManager.SetOutputRenderTargets();
        graphics.DrawFullscreenQuad(
            "Apply exposure",
            "P_AutoExposure",
            _currentParameters.AutoExposure ? "EXPOSURE AUTOEXPOSURE" : "EXPOSURE",
            BlendMode.Replace,
            bilinearColorSampler: true,
            renderBufferManager.SecondaryColorTexture,
            secondaryTexture: _currentParameters.AutoExposure ? _cachedBuffers?.AdaptedLuminance.GetTexture() : null,
            depthTexture: null,
            applyExposureParameters,
            renderBufferManager.InvOutputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);
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
        public required RenderBuffer Color128 { get; init; }
        public required RenderBuffer Lum64 { get; init; }
        public required RenderBuffer Lum16 { get; init; }
        public required RenderBuffer Lum4 { get; init; }
        public required RenderBuffer Lum1 { get; init; }
        public required RenderBuffer AdaptedLuminance { get; init; }
        public required RenderBuffer PreviousAdaptedLuminance { get; init; }
    }

    private struct InputInvSizeParameters
    {
        [ShaderParameterName("InputInvSize")]
        public Vector2f InputInvSize;
    }

    private struct AdaptRateParameters
    {
        [ShaderParameterName("AdaptRate")]
        public float AdaptRate;
    }

    private struct ApplyExposureParameters
    {
        [ShaderParameterName("MinMaxExposure")]
        public Vector2f MinMaxExposure;

        [ShaderParameterName("AutoExposureMiddleGrey")]
        public float AutoExposureMiddleGrey;
    }

    private CachedBuffers? EnsureCachedBuffers(SharedRenderPassState sharedState)
    {
        if (sharedState.RenderBufferManager is not { } manager)
            return null;

        _cachedBuffers ??= new CachedBuffers
        {
            Color128 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRgba16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(128, 128)),
            Lum64 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(64, 64)),
            Lum16 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(16, 16)),
            Lum4 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(4, 4)),
            Lum1 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.FixedTextureSize), new Vector2f(1, 1)),
            AdaptedLuminance = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.FixedTextureSize | RenderBufferFlag.Persistent), new Vector2f(1, 1)),
            PreviousAdaptedLuminance = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.FixedTextureSize), new Vector2f(1, 1))
        };
        return _cachedBuffers;
    }

    private void EvaluateDownsampledColorBuffer(RenderBufferManager renderBufferManager, Graphics graphics, CachedBuffers buffers)
    {
        var viewportTexture = renderBufferManager.SecondaryColorTexture;
        if (viewportTexture is null)
            return;

        renderBufferManager.SetRenderTargets(null, buffers.Color128);
        renderBufferManager.DrawTexture("Downsample color buffer", viewportTexture);
    }

    private void EvaluateLuminance(RenderBufferManager renderBufferManager, Graphics graphics, CachedBuffers buffers)
    {
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Color128, buffers.Lum64, "LUMINANCE64", new Vector2f(1.0f / 128.0f, 1.0f / 128.0f));
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Lum64, buffers.Lum16, "LUMINANCE16", new Vector2f(1.0f / 64.0f, 1.0f / 64.0f));
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Lum16, buffers.Lum4, "LUMINANCE4", new Vector2f(1.0f / 16.0f, 1.0f / 16.0f));
        DrawLuminanceStep(renderBufferManager, graphics, buffers.Lum4, buffers.Lum1, "LUMINANCE1", new Vector2f(1.0f / 4.0f, 1.0f / 4.0f));
    }

    private void EvaluateAdaptedLuminance(RenderBufferManager renderBufferManager, Graphics graphics, CachedBuffers buffers)
    {
        renderBufferManager.SetRenderTargets(null, buffers.PreviousAdaptedLuminance);
        var previousSource = _isAdaptedLuminanceInitialized
            ? buffers.AdaptedLuminance.GetTexture()
            : buffers.Lum1.GetTexture();
        renderBufferManager.DrawTexture("Store previous luminance", previousSource!);

        var adaptRateParameters = new AdaptRateParameters
        {
            AdaptRate = _currentParameters.AdaptRate
        };

        renderBufferManager.SetRenderTargets(null, buffers.AdaptedLuminance);
        graphics.DrawFullscreenQuad(
            "Adapt luminosity",
            "P_AutoExposure",
            "ADAPTLUMINANCE",
            BlendMode.Replace,
            bilinearColorSampler: true,
            buffers.PreviousAdaptedLuminance.GetTexture(),
            secondaryTexture: buffers.Lum1.GetTexture(),
            depthTexture: null,
            adaptRateParameters,
            new Vector2f(1.0f, 1.0f),
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);

        _isAdaptedLuminanceInitialized = true;
    }

    private void DrawLuminanceStep(RenderBufferManager renderBufferManager, Graphics graphics, RenderBuffer source,
        RenderBuffer target, string shaderDefines, Vector2f inputInvSize)
    {
        var inputInvSizeParameters = new InputInvSizeParameters
        {
            InputInvSize = inputInvSize
        };

        renderBufferManager.SetRenderTargets(null, target);
        graphics.DrawFullscreenQuad(
            "Downsample luminosity buffer",
            "P_AutoExposure",
            shaderDefines,
            BlendMode.Replace,
            bilinearColorSampler: true,
            source.GetTexture(),
            secondaryTexture: null,
            depthTexture: null,
            inputInvSizeParameters,
            renderBufferManager.InvOutputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);
    }
}

/// <summary>
/// Post-processing pass that applies bloom to the scene.
/// </summary>
public sealed class BloomPass : RenderPass
{
    private const int MaxIterations = 16;
    private const string DefaultComment = "Create bloom around bright parts of the image";
    private const string NumIterationsName = "Bloom: Num Iterations";
    private const string MinBrightnessName = "Bloom: Min Brightness";
    private const string MaxBrightnessName = "Bloom: Max Brightness";
    private const string IntensityName = "Bloom: Base Intensity";
    private const string IterationFactorName = "Bloom: Iteration Intensity Factor";
    private static readonly Vector3f LinearLuminanceWeights = new(0.2126f, 0.7152f, 0.0722f);
    private static readonly Vector3f GammaLuminanceWeights = new(0.299f, 0.587f, 0.114f);
    private readonly List<TextureCache> _textures = [];
    private readonly List<float> _intensityMultipliers = [];
    private Parameters _currentParameters = new();
    private bool _isHdr;

    public BloomPass()
    {
        SetComment(DefaultComment);
        _traits = new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: true);
    }

    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<BloomPass>(ObjectCategory.Category_RenderPass);
    }

    public override void CollectParameters(StringVariantMap parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var defaults = new Parameters();
        DeclareParameter(NumIterationsName, defaults.NumIterations, parameters);
        DeclareParameter(MinBrightnessName, defaults.MinBrightness, parameters);
        DeclareParameter(MaxBrightnessName, defaults.MaxBrightness, parameters);
        DeclareParameter(IntensityName, defaults.Intensity, parameters);
        DeclareParameter(IterationFactorName, defaults.IterationFactor, parameters);
    }

    public override void InitializeView(RenderPipelineView view)
    {
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);

        var updated = new Parameters();
        ref readonly var numIterations = ref LoadParameter(NumIterationsName, parameters);
        if (numIterations.Type == VariantType.Int)
            updated.NumIterations = Math.Clamp(numIterations.GetInt(), 1, MaxIterations);
        ref readonly var minBrightness = ref LoadParameter(MinBrightnessName, parameters);
        if (minBrightness.Type == VariantType.Float)
            updated.MinBrightness = minBrightness.GetFloat();
        ref readonly var maxBrightness = ref LoadParameter(MaxBrightnessName, parameters);
        if (maxBrightness.Type == VariantType.Float)
            updated.MaxBrightness = maxBrightness.GetFloat();
        ref readonly var intensity = ref LoadParameter(IntensityName, parameters);
        if (intensity.Type == VariantType.Float)
            updated.Intensity = intensity.GetFloat();
        ref readonly var iterationFactor = ref LoadParameter(IterationFactorName, parameters);
        if (iterationFactor.Type == VariantType.Float)
            updated.IterationFactor = iterationFactor.GetFloat();

        if (_currentParameters.NumIterations != updated.NumIterations)
            InvalidateTextureCache();

        _currentParameters = updated;
        _isEnabledInternally = true;
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        base.Update(sharedState);

        var nextHdr = sharedState.RenderBufferManager?.IsHDR ?? false;
        if (_isHdr != nextHdr)
        {
            _isHdr = nextHdr;
            InvalidateTextureCache();
        }

        RestoreTextureCache(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        if (sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

        var graphics = GetSubsystem<Graphics>()!;

        RestoreTextureCache(sharedState);
        if (_textures.Count == 0)
            return;

        var luminanceWeights = renderBufferManager.IsLinearColorSpace ? LinearLuminanceWeights : GammaLuminanceWeights;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        var numIterations = GatherBrightRegions(renderBufferManager, graphics, _textures[0].Final, luminanceWeights);
        for (var i = 0; i < numIterations; ++i)
        {
            if (i > 0)
                CopyTexture(renderBufferManager, graphics, _textures[i - 1].Final, _textures[i].Final);

            BlurTexture(renderBufferManager, graphics, _textures[i].Final, _textures[i].Temporary, luminanceWeights);
        }

        _intensityMultipliers.Clear();
        var totalIntensity = 0.0f;
        for (var i = 0; i < numIterations; ++i)
        {
            var intensity = MathF.Pow(_currentParameters.IterationFactor, i);
            _intensityMultipliers.Add(intensity);
            totalIntensity += intensity;
        }

        if (totalIntensity <= MathDefs.Epsilon)
            totalIntensity = 1.0f;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);
        renderBufferManager.SetOutputRenderTargets();
        for (var i = 0; i < _intensityMultipliers.Count; ++i)
        {
            var normalizedIntensity = _intensityMultipliers[i] * _currentParameters.Intensity / totalIntensity;
            ApplyBloom(renderBufferManager, graphics, _textures[i].Final, normalizedIntensity);
        }
    }

    private struct Parameters
    {
        public int NumIterations = 5;
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
        public required RenderBuffer Final { get; init; }
        public required RenderBuffer Temporary { get; init; }
    }

    private struct BloomParameters
    {
        [ShaderParameterName("LuminanceWeights")]
        public Vector3f LuminanceWeights;

        [ShaderParameterName("Intensity")]
        public float Intensity;

        [ShaderParameterName("InputInvSize")]
        public Vector2f InputInvSize;

        [ShaderParameterName("Threshold")]
        public Vector2f Threshold;
    }

    private void InvalidateTextureCache()
    {
        _textures.Clear();
    }

    private void RestoreTextureCache(SharedRenderPassState sharedState)
    {
        if (_textures.Count > 0 || sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

        for (var i = 0; i < _currentParameters.NumIterations; ++i)
        {
            var format = _isHdr ? TextureFormat.TexFormatRgba16Float : TextureFormat.TexFormatRgba8Unorm;
            var bufferParams = new RenderBufferParams(format, 1, RenderBufferFlag.BilinearFiltering);
            var sizeMultiplier = Vector2f.One / (1 << i);
            _textures.Add(new TextureCache
            {
                Final = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier),
                Temporary = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier)
            });
        }
    }

    private int GatherBrightRegions(RenderBufferManager renderBufferManager, Graphics graphics, RenderBuffer destination, Vector3f luminanceWeights)
    {
        var viewportTexture = renderBufferManager.SecondaryColorTexture;
        if (viewportTexture is null)
            return 0;

        var viewportTextureSize = viewportTexture.GetParams().Size;
        var minDimension = Math.Min(viewportTextureSize.X, viewportTextureSize.Y);
        if (minDimension <= 0)
            return 0;

        var bloomParameters = CreateBloomParameters(
            luminanceWeights,
            intensity: 0.0f,
            new Vector2f(1.0f / viewportTextureSize.X, 1.0f / viewportTextureSize.Y));

        renderBufferManager.SetRenderTargets(null, destination);
        graphics.DrawFullscreenQuad(
            "Gather bright regions",
            "P_Bloom",
            "BRIGHT",
            BlendMode.Replace,
            bilinearColorSampler: true,
            viewportTexture,
            secondaryTexture: null,
            depthTexture: null,
            bloomParameters,
            renderBufferManager.InvOutputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);

        var numIterations = 0;
        while (minDimension > 1 && numIterations < _currentParameters.NumIterations)
        {
            minDimension >>= 1;
            ++numIterations;
        }

        return Math.Clamp(numIterations, 1, _currentParameters.NumIterations);
    }

    private void BlurTexture(RenderBufferManager renderBufferManager, Graphics graphics, RenderBuffer final,
        RenderBuffer temporary, Vector3f luminanceWeights)
    {
        var texture = final.GetTexture()!;
        var textureSize = texture.GetParams().Size;
        var invInputSize = new Vector2f(
            textureSize.X > 0 ? 1.0f / textureSize.X : 0.0f,
            textureSize.Y > 0 ? 1.0f / textureSize.Y : 0.0f);

        var bloomParameters = CreateBloomParameters(luminanceWeights, intensity: 0.0f, invInputSize);

        renderBufferManager.SetRenderTargets(null, temporary);
        graphics.DrawFullscreenQuad(
            "Blur vertically",
            "P_Bloom",
            "BLURH",
            BlendMode.Replace,
            bilinearColorSampler: true,
            texture,
            secondaryTexture: null,
            depthTexture: null,
            bloomParameters,
            invInputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);

        renderBufferManager.SetRenderTargets(null, final);
        graphics.DrawFullscreenQuad(
            "Blur horizontally",
            "P_Bloom",
            "BLURV",
            BlendMode.Replace,
            bilinearColorSampler: true,
            temporary.GetTexture(),
            secondaryTexture: null,
            depthTexture: null,
            bloomParameters,
            invInputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);
    }

    private void CopyTexture(RenderBufferManager renderBufferManager, Graphics graphics, RenderBuffer source, RenderBuffer destination)
    {
        renderBufferManager.SetRenderTargets(null, destination);
        renderBufferManager.DrawTexture("Downscale bloom", source.GetTexture()!);
    }

    private void ApplyBloom(RenderBufferManager renderBufferManager, Graphics graphics, RenderBuffer bloom, float intensity)
    {
        var bloomParameters = CreateBloomParameters(Vector3f.Zero, intensity, renderBufferManager.InvOutputSize);
        graphics.DrawFullscreenQuad(
            "Apply bloom",
            "P_Bloom",
            "COMBINE",
            BlendMode.Add,
            bilinearColorSampler: true,
            bloom.GetTexture(),
            secondaryTexture: null,
            depthTexture: null,
            bloomParameters,
            renderBufferManager.InvOutputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);
    }

    private BloomParameters CreateBloomParameters(Vector3f luminanceWeights, float intensity, Vector2f inputInvSize)
    {
        var thresholdGap = Math.Max(0.01f, _currentParameters.MaxBrightness - _currentParameters.MinBrightness);
        return new BloomParameters
        {
            LuminanceWeights = luminanceWeights,
            Intensity = intensity,
            InputInvSize = inputInvSize,
            Threshold = new Vector2f(_currentParameters.MinBrightness, 1.0f / thresholdGap)
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
    private RenderBuffer? _normalBuffer;
    private TextureCache? _textures;
    private Parameters _currentParameters = new();

    public AmbientOcclusionPass()
    {
        SetComment(DefaultComment);
    }

    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<AmbientOcclusionPass>(ObjectCategory.Category_RenderPass);
    }

    public override void CollectParameters(StringVariantMap parameters)
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

    public override void InitializeView(RenderPipelineView view)
    {
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
        if (!settings.RenderBufferManager.ReadableDepth)
        {
            _isEnabledInternally = false;
            return;
        }

        var updated = new Parameters();
        ref readonly var downscale = ref LoadParameter(DownscaleName, parameters);
        if (downscale.Type == VariantType.Int)
            updated.Downscale = downscale.GetInt();
        ref readonly var strength = ref LoadParameter(StrengthName, parameters);
        if (strength.Type == VariantType.Float)
            updated.Strength = strength.GetFloat();
        ref readonly var exponent = ref LoadParameter(ExponentName, parameters);
        if (exponent.Type == VariantType.Float)
            updated.Exponent = exponent.GetFloat();
        ref readonly var radiusNear = ref LoadParameter(RadiusNearName, parameters);
        if (radiusNear.Type == VariantType.Float)
            updated.RadiusNear = radiusNear.GetFloat();
        ref readonly var distanceNear = ref LoadParameter(DistanceNearName, parameters);
        if (distanceNear.Type == VariantType.Float)
            updated.DistanceNear = distanceNear.GetFloat();
        ref readonly var radiusFar = ref LoadParameter(RadiusFarName, parameters);
        if (radiusFar.Type == VariantType.Float)
            updated.RadiusFar = radiusFar.GetFloat();
        ref readonly var distanceFar = ref LoadParameter(DistanceFarName, parameters);
        if (distanceFar.Type == VariantType.Float)
            updated.DistanceFar = distanceFar.GetFloat();
        ref readonly var fadeDistanceBegin = ref LoadParameter(FadeDistanceBeginName, parameters);
        if (fadeDistanceBegin.Type == VariantType.Float)
            updated.FadeDistanceBegin = fadeDistanceBegin.GetFloat();
        ref readonly var fadeDistanceEnd = ref LoadParameter(FadeDistanceEndName, parameters);
        if (fadeDistanceEnd.Type == VariantType.Float)
            updated.FadeDistanceEnd = fadeDistanceEnd.GetFloat();
        ref readonly var blurDepthThreshold = ref LoadParameter(BlurDepthThresholdName, parameters);
        if (blurDepthThreshold.Type == VariantType.Float)
            updated.BlurDepthThreshold = blurDepthThreshold.GetFloat();
        ref readonly var blurNormalThreshold = ref LoadParameter(BlurNormalThresholdName, parameters);
        if (blurNormalThreshold.Type == VariantType.Float)
            updated.BlurNormalThreshold = blurNormalThreshold.GetFloat();

        if (_currentParameters.Downscale != updated.Downscale)
            InvalidateTextureCache();

        _currentParameters = updated;
        _isEnabledInternally = true;
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        base.Update(sharedState);
        RestoreTextureCache(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        ConnectToRenderBuffer(ref _normalBuffer, SharedRenderPassState.NormalBufferId, sharedState, required: false);

        if (sharedState.RenderBufferManager is not { } renderBufferManager
            || sharedState.RenderCamera is not { } cameraReference
            || !cameraReference.TryGetTarget(out var camera)
            || _currentParameters.Strength <= 0.0f)
        {
            return;
        }

        var graphics = GetSubsystem<Graphics>()!;

        RestoreTextureCache(sharedState);
        if (_textures?.CurrentTarget is null || _textures.PreviousTarget is null)
            return;

        var depthTexture = renderBufferManager.DepthStencilTexture;
        if (depthTexture is null)
            return;

        var clipToTextureSpace = Matrix4.Identity;
        clipToTextureSpace.SetScale(new Vector3f(0.5f, 0.5f, 1.0f));
        clipToTextureSpace.SetTranslation(new Vector3f(0.5f, 0.5f, 0.0f));

        var viewToTextureSpace = clipToTextureSpace * camera.GetGPUProjection();
        var textureToViewSpace = viewToTextureSpace.Inverse;

        EvaluateAO(renderBufferManager, graphics, camera, depthTexture, viewToTextureSpace, textureToViewSpace);
        BlurTexture(renderBufferManager, graphics, depthTexture, textureToViewSpace);
        Blit(renderBufferManager, graphics);
    }

    private struct Parameters
    {
        public int Downscale;
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

        public required RenderBuffer CurrentTarget { get; set; }

        public required RenderBuffer PreviousTarget { get; set; }
    }

    private struct AmbientOcclusionParameters
    {
        [ShaderParameterName("InputInvSize")]
        public Vector2f InputInvSize;

        [ShaderParameterName("BlurStep")]
        public Vector2f BlurStep;

        [ShaderParameterName("Strength")]
        public float Strength;

        [ShaderParameterName("Exponent")]
        public float Exponent;

        [ShaderParameterName("RadiusInfo")]
        public Vector4f RadiusInfo;

        [ShaderParameterName("FadeDistance")]
        public Vector2f FadeDistance;

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
        _textures = null;
    }

    private void RestoreTextureCache(SharedRenderPassState sharedState)
    {
        if (_textures is not null || sharedState.RenderBufferManager is not { } renderBufferManager)
            return;

            var resourceCache = GetSubsystem<ResourceCache>();
        var sizeMultiplier = Vector2f.One / (1 << _currentParameters.Downscale);
        var bufferParams = new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm, 1, RenderBufferFlag.BilinearFiltering);
        _textures = new TextureCache
        {
            Noise = resourceCache?.GetResourceOrNull<Texture2D>("Textures/SSAONoise.png"),
            CurrentTarget = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier),
            PreviousTarget = renderBufferManager.CreateColorBuffer(bufferParams, sizeMultiplier)
        };
    }

    private void EvaluateAO(RenderBufferManager renderBufferManager, Graphics graphics, Camera camera, RawTexture depthTexture,
        Matrix4 viewToTextureSpace, Matrix4 textureToViewSpace)
    {
        if (_textures?.CurrentTarget is null)
            return;

        var inputInvSize = renderBufferManager.InvOutputSize;
        var worldToViewSpace = camera.GetView().Matrix4;
        var worldToViewSpaceCorrected = camera.GetFlipVertical() ? FlipMatrix * worldToViewSpace : worldToViewSpace;

        var radiusSlope = (_currentParameters.RadiusFar - _currentParameters.RadiusNear)
            / Math.Max(1.0f, _currentParameters.DistanceFar - _currentParameters.DistanceNear);
        var radiusInfo = new Vector4f(
            radiusSlope,
            _currentParameters.RadiusNear - radiusSlope * _currentParameters.DistanceNear,
            _currentParameters.RadiusNear,
            _currentParameters.RadiusFar);

        var ambientOcclusionParameters = new AmbientOcclusionParameters
        {
            InputInvSize = inputInvSize,
            BlurStep = inputInvSize,
            Strength = _currentParameters.Strength,
            Exponent = _currentParameters.Exponent,
            RadiusInfo = radiusInfo,
            FadeDistance = new Vector2f(_currentParameters.FadeDistanceBegin, _currentParameters.FadeDistanceEnd),
            ViewToTexture = viewToTextureSpace,
            TextureToView = textureToViewSpace,
            WorldToView = worldToViewSpaceCorrected
        };

        renderBufferManager.SetRenderTargets(null, _textures.CurrentTarget);
        graphics.DrawFullscreenQuad(
            "Apply SSAO",
            "P_SSAO",
            _normalBuffer is not null ? "EVALUATE_OCCLUSION DEFERRED" : "EVALUATE_OCCLUSION",
            BlendMode.Replace,
            bilinearColorSampler: true,
            _textures.Noise?.GetRawTexture(),
            secondaryTexture: _normalBuffer?.GetTexture(),
            depthTexture: depthTexture,
            ambientOcclusionParameters,
            inputInvSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);

        (_textures.CurrentTarget, _textures.PreviousTarget) = (_textures.PreviousTarget, _textures.CurrentTarget);
    }

    private void BlurTexture(RenderBufferManager renderBufferManager, Graphics graphics, RawTexture depthTexture, Matrix4 textureToViewSpace)
    {
        if (_textures?.CurrentTarget is null || _textures.PreviousTarget is null)
            return;

        var currentTextureSize = _textures.CurrentTarget.GetTexture()!.GetParams().Size;
        var textureSize = new Vector2i(currentTextureSize.X, currentTextureSize.Y);
        var blurStep = new Vector2f(
            textureSize.X > 0 ? 1.0f / textureSize.X : 0.0f,
            textureSize.Y > 0 ? 1.0f / textureSize.Y : 0.0f);

        var shaderDefines = _normalBuffer is not null ? "BLUR DEFERRED" : "BLUR";
        BlurAxis(renderBufferManager, graphics, depthTexture, textureToViewSpace, new Vector2f(blurStep.X, 0.0f), shaderDefines, "SSAO Blur Horizontally");
        BlurAxis(renderBufferManager, graphics, depthTexture, textureToViewSpace, new Vector2f(0.0f, blurStep.Y), shaderDefines, "SSAO Blur Vertically");
    }

    private void BlurAxis(RenderBufferManager renderBufferManager, Graphics graphics, RawTexture depthTexture,
        Matrix4 textureToViewSpace, Vector2f blurStep, string shaderDefines, string debugComment)
    {
        if (_textures?.CurrentTarget is null || _textures.PreviousTarget is null)
            return;

        var blurParameters = new AmbientOcclusionParameters
        {
            BlurStep = blurStep,
            BlurZThreshold = _currentParameters.BlurDepthThreshold,
            BlurNormalInvThreshold = 1.0f - _currentParameters.BlurNormalThreshold,
            TextureToView = textureToViewSpace
        };

        renderBufferManager.SetRenderTargets(null, _textures.CurrentTarget);
        graphics.DrawFullscreenQuad(
            debugComment,
            "P_SSAO",
            shaderDefines,
            BlendMode.Replace,
            bilinearColorSampler: true,
            _textures.PreviousTarget.GetTexture(),
            secondaryTexture: _normalBuffer?.GetTexture(),
            depthTexture: depthTexture,
            blurParameters,
            blurStep,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);

        (_textures.CurrentTarget, _textures.PreviousTarget) = (_textures.PreviousTarget, _textures.CurrentTarget);
    }

    private void Blit(RenderBufferManager renderBufferManager, Graphics graphics)
    {
        if (_textures?.PreviousTarget is null)
            return;

        renderBufferManager.SetOutputRenderTargets();
        graphics.DrawFullscreenQuad(
            "SSAO Combine",
            "P_SSAO",
            "COMBINE",
            BlendMode.Alpha,
            bilinearColorSampler: true,
            _textures.PreviousTarget.GetTexture(),
            secondaryTexture: null,
            depthTexture: null,
            EmptyParameters,
            renderBufferManager.InvOutputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);
    }
}

/// <summary>
/// Post-processing pass that renders outline from the color buffer.
/// </summary>
public sealed class OutlineRenderPass : RenderPass
{
    private const string DefaultComment = "Draw outline of the contents of the color buffer";
    public static readonly StringHash ColorBufferId = "outline";
    private RenderBuffer? _colorBuffer;

    public OutlineRenderPass()
    {
        SetComment(DefaultComment);
    }

    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<OutlineRenderPass>(ObjectCategory.Category_RenderPass);
    }

    public override void CollectParameters(StringVariantMap parameters)
    {
    }

    public override void InitializeView(RenderPipelineView view)
    {
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        base.Render(sharedState);

        ConnectToRenderBuffer(ref _colorBuffer, ColorBufferId, sharedState, required: false);

        if (_colorBuffer?.GetTexture() is not { } texture
            || sharedState.RenderBufferManager is not { } renderBufferManager)
        {
            return;
        }

        var graphics = GetSubsystem<Graphics>()!;

        var outlineParameters = new OutlineParameters
        {
            InputInvSize = Vector2f.One / new Vector2f(texture.GetParams().Size.X, texture.GetParams().Size.Y)
        };

        renderBufferManager.SetOutputRenderTargets();
        graphics.DrawFullscreenQuad(
            "Apply outline",
            "P_Outline",
            renderBufferManager.IsLinearColorSpace ? "URHO3D_GAMMA_CORRECTION" : string.Empty,
            BlendMode.Alpha,
            bilinearColorSampler: true,
            texture,
            secondaryTexture: null,
            depthTexture: null,
            outlineParameters,
            renderBufferManager.InvOutputSize,
            renderBufferManager.DefaultClipToUVSpaceOffsetAndScale);
    }

    private struct OutlineParameters
    {
        [ShaderParameterName("InputInvSize")]
        public Vector2f InputInvSize;
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
