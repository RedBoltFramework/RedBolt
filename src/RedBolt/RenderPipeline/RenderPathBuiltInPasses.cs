namespace RedBolt;

/// <summary>
/// Render pass that applies a shader to the entire output area.
/// </summary>
public sealed class FullScreenShaderPass : RenderPass
{
    public FullScreenShaderPass()
    {
        _attributes = new Attributes
        {
            ShaderName = string.Empty,
            ShaderDefines = string.Empty,
            BlendMode = BlendMode.Replace,
            ParametersPrefix = string.Empty,
            Parameters = new StringVariantMap()
        };
        RegisterAttribute(
            "Shader Name",
            typeof(string),
            self => ((FullScreenShaderPass)self)._attributes.ShaderName,
            (self, value) => ((FullScreenShaderPass)self).SetShaderName(value as string ?? string.Empty),
            string.Empty);
        RegisterAttribute(
            "Shader Defines",
            typeof(string),
            self => ((FullScreenShaderPass)self)._attributes.ShaderDefines,
            (self, value) => ((FullScreenShaderPass)self).SetShaderDefines(value as string ?? string.Empty),
            string.Empty);
        RegisterAttribute(
            "Blend Mode",
            typeof(BlendMode),
            self => ((FullScreenShaderPass)self)._attributes.BlendMode,
            (self, value) => ((FullScreenShaderPass)self).SetBlendMode(ParseBlendMode(value)),
            BlendMode.Replace);
        RegisterAttribute(
            "Read+Write Color Output",
            typeof(bool),
            self => ((FullScreenShaderPass)self)._attributes.NeedReadWriteColorBuffer,
            (self, value) => ((FullScreenShaderPass)self).SetNeedReadWriteColorBuffer(value is bool enabled && enabled),
            false);
        RegisterAttribute(
            "Bilinear Color Sampler",
            typeof(bool),
            self => ((FullScreenShaderPass)self)._attributes.NeedBilinearColorSampler,
            (self, value) => ((FullScreenShaderPass)self)._attributes.NeedBilinearColorSampler = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Disable On Default Parameters",
            typeof(bool),
            self => ((FullScreenShaderPass)self)._attributes.DisableOnDefaultParameters,
            (self, value) => ((FullScreenShaderPass)self)._attributes.DisableOnDefaultParameters = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Parameters Prefix",
            typeof(string),
            self => ((FullScreenShaderPass)self)._attributes.ParametersPrefix,
            (self, value) => ((FullScreenShaderPass)self)._attributes.ParametersPrefix = value as string ?? string.Empty,
            string.Empty);
        RegisterAttribute(
            "Parameters",
            typeof(StringVariantMap),
            self => ((FullScreenShaderPass)self)._attributes.Parameters,
            (self, value) => ((FullScreenShaderPass)self).SetParameters(value),
            null);
    }

    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<FullScreenShaderPass>(ObjectCategory.Category_RenderPass);
    }

    public override void CollectParameters(StringVariantMap parameters)
    {
        foreach (var (name, defaultValue) in _attributes.Parameters)
            DeclareParameter(_attributes.ParametersPrefix + name, defaultValue, parameters);
    }

    public override void InitializeView(RenderPipelineView view)
    {
        ClearShaderParameterStorage();

        foreach (var (name, defaultValue) in _attributes.Parameters)
        {
            _shaderParametersSources.Add(new StringHash(_attributes.ParametersPrefix + name));
            _shaderParameterDefaults.Add(defaultValue.Clone());
            _shaderParameters.Add(new ShaderParameterDesc
            {
                Name = new StringHash(name),
                Value = defaultValue.Clone()
            });
        }
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
        var shouldBeDisabled = _attributes.DisableOnDefaultParameters;
        for (var i = 0; i < _shaderParameters.Count; ++i)
        {
            foreach (var (name, value) in parameters)
            {
                if (StringHash.Calculate(name) != _shaderParametersSources[i].Value)
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

        _traits = new RenderPassTraits(
            _attributes.NeedReadWriteColorBuffer,
            _attributes.NeedBilinearColorSampler);
        _isEnabledInternally = !shouldBeDisabled;
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        RestoreCache(sharedState);
        var renderBufferManager = sharedState.RenderBufferManager!;
        var cache = _cache!.Value;

        if (_attributes.NeedReadWriteColorBuffer)
            renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        renderBufferManager.SetOutputRenderTargets();
        if (_attributes.NeedReadWriteColorBuffer)
        {
            renderBufferManager.DrawFeedbackViewportQuad(
                cache.DebugComment, cache.PipelineStateId, _shaderResources, _shaderParameters);
        }
        else
        {
            renderBufferManager.DrawViewportQuad(
                cache.DebugComment, cache.PipelineStateId, _shaderResources, _shaderParameters);
        }
    }

    public override void Dispose()
    {
        ClearShaderParameterStorage();
        _attributes.Parameters.Dispose();
        base.Dispose();
    }

    private void InvalidateCache()
    {
        _cache = null;
    }

    private void RestoreCache(SharedRenderPassState sharedState)
    {
        if (_cache.HasValue)
            return;

        var cache = new Cache
        {
            DebugComment = $"Apply shader \"{_attributes.ShaderName}\"({_attributes.ShaderDefines})"
        };
        _cache = cache;

        IReadOnlyList<NamedSamplerStateDesc> samplersAdjusted = _attributes.NeedReadWriteColorBuffer
            ? [("Albedo", SamplerStateDesc.Bilinear())]
            : Array.Empty<NamedSamplerStateDesc>();

        cache.PipelineStateId = sharedState.RenderBufferManager!.CreateQuadPipelineState(
            _attributes.BlendMode,
            _attributes.ShaderName,
            _attributes.ShaderDefines,
            samplersAdjusted);
        _cache = cache;
    }

    private void SetParameters(object? value)
    {
        _attributes.Parameters.Clear();
        if (value is not IReadOnlyDictionary<string, Variant> values)
            return;

        foreach (var (name, parameterValue) in values)
            _attributes.Parameters[name] = parameterValue;
    }

    private void SetShaderName(string value)
    {
        _attributes.ShaderName = value;
        InvalidateCache();
    }

    private void SetShaderDefines(string value)
    {
        _attributes.ShaderDefines = value;
        InvalidateCache();
    }

    private void SetBlendMode(BlendMode value)
    {
        _attributes.BlendMode = value;
        InvalidateCache();
    }

    private void SetNeedReadWriteColorBuffer(bool value)
    {
        _attributes.NeedReadWriteColorBuffer = value;
        InvalidateCache();
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

        _shaderParametersSources.Clear();
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

    private new struct Attributes
    {
        public string ShaderName { get; set; }
        public string ShaderDefines { get; set; }
        public BlendMode BlendMode { get; set; }
        public bool NeedReadWriteColorBuffer { get; set; }
        public bool NeedBilinearColorSampler { get; set; }
        public bool DisableOnDefaultParameters { get; set; }
        public string ParametersPrefix { get; set; }
        public StringVariantMap Parameters { get; set; }
    }

    private struct Cache
    {
        public StaticPipelineStateId PipelineStateId { get; set; }
        public string DebugComment { get; set; }
    }

    private Attributes _attributes;
    private Cache? _cache;
    private readonly List<StringHash> _shaderParametersSources = [];
    private readonly List<Variant> _shaderParameterDefaults = [];
    private readonly List<ShaderParameterDesc> _shaderParameters = [];
    private readonly List<ShaderResourceDesc> _shaderResources = [];
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

    public enum Mode
    {
        None,
        Reinhard,
        ReinhardWhite,
        Uncharted2,
        Count
    }

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
        DeclareParameter(ModeName, (int)Mode.None, parameters);
        using var modeMetadata = new Variant(ModeMetadata);
        DeclareParameter(ModeMetadataName, modeMetadata, parameters);
    }

    public override void InitializeView(RenderPipelineView view)
    {
    }

    public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
    {
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
        RestoreCache(sharedState);
        var renderBufferManager = sharedState.RenderBufferManager!;
        renderBufferManager.SwapColorBuffers(synchronizeContents: false);
        renderBufferManager.SetOutputRenderTargets();
        renderBufferManager.DrawFeedbackViewportQuad(
            "Apply tone mapping",
            _pipelineStates!.Value.Default,
            EmptyResources,
            EmptyParameters);
    }

    private void InvalidateCache()
    {
        _pipelineStates = null;
    }

    private void RestoreCache(SharedRenderPassState sharedState)
    {
        if (_pipelineStates.HasValue)
            return;

        var renderBufferManager = sharedState.RenderBufferManager!;
        var defines = _mode switch
        {
            Mode.Reinhard => "REINHARD ",
            Mode.ReinhardWhite => "REINHARDWHITE ",
            Mode.Uncharted2 => "UNCHARTED2 ",
            _ => string.Empty
        };
        _pipelineStates = new PipelineStateCache
        {
            Default = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Replace,
                "v2/P_ToneMapping",
                defines,
                Samplers)
        };
    }

    private struct PipelineStateCache
    {
        public StaticPipelineStateId Default { get; set; }
    }

    private PipelineStateCache? _pipelineStates;
    private Mode _mode;
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
    private bool _isAdaptedLuminanceInitialized;
    private Parameters _parameters = new();
    private CachedTextures? _textures;
    private CachedStates? _pipelineStates;
    private static readonly NamedSamplerStateDesc[] LuminanceSamplers =
        [("Albedo", SamplerStateDesc.Bilinear())];
    private static readonly NamedSamplerStateDesc[] AdaptedLuminanceSamplers =
    [
        ("Albedo", SamplerStateDesc.Bilinear()),
        ("Normal", SamplerStateDesc.Bilinear())
    ];
    private static readonly NamedSamplerStateDesc[] ApplyExposureSamplers =
    [
        ("Albedo", SamplerStateDesc.Bilinear()),
        ("Normal", SamplerStateDesc.Bilinear())
    ];
    private readonly ShaderResourceDesc[] _luminanceResources = [new() { Name = ShaderResources.Albedo }];
    private readonly ShaderParameterDesc[] _luminanceParameters =
        [new() { Name = new StringHash("InputInvSize") }];
    private readonly ShaderResourceDesc[] _adaptedLuminanceResources =
    [
        new() { Name = ShaderResources.Albedo },
        new() { Name = ShaderResources.Normal }
    ];
    private readonly ShaderParameterDesc[] _adaptedLuminanceParameters =
        [new() { Name = new StringHash("AdaptRate") }];
    private readonly ShaderResourceDesc[] _applyExposureResources =
        [new() { Name = ShaderResources.Normal }];
    private readonly ShaderParameterDesc[] _applyExposureParameters =
    [
        new() { Name = new StringHash("MinMaxExposure") },
        new() { Name = new StringHash("AutoExposureMiddleGrey") }
    ];

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

        if (_parameters.AutoExposure != updated.AutoExposure)
        {
            InvalidateTextureCache();
            InvalidatePipelineStateCache();
        }

        _isEnabledInternally = true;
        _parameters = updated;
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        RestoreTextureCache(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        RestorePipelineStateCache(sharedState);
        var renderBufferManager = sharedState.RenderBufferManager!;
        renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        if (_parameters.AutoExposure)
        {
            EvaluateDownsampledColorBuffer(renderBufferManager);
            EvaluateLuminance(renderBufferManager);
            EvaluateAdaptedLuminance(renderBufferManager);
        }

        _applyExposureResources[0].Texture = _parameters.AutoExposure
            ? _textures!.Value.AdaptedLuminance!.GetTexture()
            : null;
        SetShaderParameter(_applyExposureParameters, 0,
            new Variant(new Vector2f(_parameters.MinExposure, _parameters.MaxExposure)));
        SetShaderParameter(_applyExposureParameters, 1, new Variant(0.6f));
        renderBufferManager.SetOutputRenderTargets();
        renderBufferManager.DrawFeedbackViewportQuad(
            "Apply exposure",
            _pipelineStates!.Value.AutoExposure,
            _applyExposureResources,
            _applyExposureParameters);
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

    private struct CachedTextures
    {
        public RenderBuffer? Color128 { get; set; }
        public RenderBuffer? Lum64 { get; set; }
        public RenderBuffer? Lum16 { get; set; }
        public RenderBuffer? Lum4 { get; set; }
        public RenderBuffer? Lum1 { get; set; }
        public RenderBuffer? AdaptedLuminance { get; set; }
        public RenderBuffer? PreviousAdaptedLuminance { get; set; }
    }

    private struct CachedStates
    {
        public StaticPipelineStateId Lum64;
        public StaticPipelineStateId Lum16;
        public StaticPipelineStateId Lum4;
        public StaticPipelineStateId Lum1;
        public StaticPipelineStateId AdaptedLuminance;
        public StaticPipelineStateId AutoExposure;
    }

    private void InvalidateTextureCache()
    {
        _textures = null;
    }

    private void InvalidatePipelineStateCache()
    {
        _pipelineStates = null;
    }

    private void RestoreTextureCache(SharedRenderPassState sharedState)
    {
        if (_textures is not null)
            return;

        var manager = sharedState.RenderBufferManager!;
        var textures = new CachedTextures();
        if (!_parameters.AutoExposure)
        {
            _textures = textures;
            return;
        }

        textures.Color128 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRgba16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(128, 128));
        textures.Lum64 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(64, 64));
        textures.Lum16 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(16, 16));
        textures.Lum4 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.BilinearFiltering | RenderBufferFlag.FixedTextureSize), new Vector2f(4, 4));
        textures.Lum1 = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.FixedTextureSize), new Vector2f(1, 1));
        textures.AdaptedLuminance = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.FixedTextureSize | RenderBufferFlag.Persistent), new Vector2f(1, 1));
        textures.PreviousAdaptedLuminance = manager.CreateColorBuffer(new RenderBufferParams(TextureFormat.TexFormatRg16Float, 1, RenderBufferFlag.FixedTextureSize), new Vector2f(1, 1));
        _textures = textures;
    }

    private void RestorePipelineStateCache(SharedRenderPassState sharedState)
    {
        if (_pipelineStates.HasValue)
            return;

        var manager = sharedState.RenderBufferManager!;
        _pipelineStates = new CachedStates
        {
            Lum64 = manager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_AutoExposure", "LUMINANCE64", LuminanceSamplers),
            Lum16 = manager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_AutoExposure", "LUMINANCE16", LuminanceSamplers),
            Lum4 = manager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_AutoExposure", "LUMINANCE4", LuminanceSamplers),
            Lum1 = manager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_AutoExposure", "LUMINANCE1", LuminanceSamplers),
            AdaptedLuminance = manager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_AutoExposure", "ADAPTLUMINANCE", AdaptedLuminanceSamplers),
            AutoExposure = manager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_AutoExposure",
                _parameters.AutoExposure ? "EXPOSURE AUTOEXPOSURE " : "EXPOSURE ", ApplyExposureSamplers)
        };
    }

    private void EvaluateDownsampledColorBuffer(RenderBufferManager renderBufferManager)
    {
        var textures = _textures!.Value;
        var viewportTexture = renderBufferManager.SecondaryColorTexture;
        renderBufferManager.SetRenderTargets(null, textures.Color128!);
        renderBufferManager.DrawTexture("Downsample color buffer", viewportTexture!);
    }

    private void EvaluateLuminance(RenderBufferManager renderBufferManager)
    {
        var textures = _textures!.Value;
        DrawLuminanceStep(renderBufferManager, textures.Color128!, textures.Lum64!, _pipelineStates!.Value.Lum64, 128.0f);
        DrawLuminanceStep(renderBufferManager, textures.Lum64!, textures.Lum16!, _pipelineStates.Value.Lum16, 64.0f);
        DrawLuminanceStep(renderBufferManager, textures.Lum16!, textures.Lum4!, _pipelineStates.Value.Lum4, 16.0f);
        DrawLuminanceStep(renderBufferManager, textures.Lum4!, textures.Lum1!, _pipelineStates.Value.Lum1, 4.0f);
    }

    private void EvaluateAdaptedLuminance(RenderBufferManager renderBufferManager)
    {
        var textures = _textures!.Value;
        renderBufferManager.SetRenderTargets(null, textures.PreviousAdaptedLuminance!);
        var sourceBuffer = _isAdaptedLuminanceInitialized ? textures.AdaptedLuminance! : textures.Lum1!;
        renderBufferManager.DrawTexture("Store previous luminance", sourceBuffer.GetTexture()!);

        _adaptedLuminanceResources[0].Texture = textures.PreviousAdaptedLuminance!.GetTexture();
        _adaptedLuminanceResources[1].Texture = textures.Lum1!.GetTexture();
        SetShaderParameter(_adaptedLuminanceParameters, 0, new Variant(_parameters.AdaptRate));
        var drawParams = new DrawQuadParams
        {
            Resources = _adaptedLuminanceResources,
            Parameters = _adaptedLuminanceParameters,
            PipelineStateId = _pipelineStates!.Value.AdaptedLuminance,
            ClipToUVOffsetAndScale = renderBufferManager.DefaultClipToUVSpaceOffsetAndScale
        };
        renderBufferManager.SetRenderTargets(null, textures.AdaptedLuminance!);
        renderBufferManager.DrawQuad("Adapt luminosity", drawParams);

        _isAdaptedLuminanceInitialized = true;
    }

    private void DrawLuminanceStep(RenderBufferManager renderBufferManager, RenderBuffer source,
        RenderBuffer target, StaticPipelineStateId pipelineState, float inputSize)
    {
        _luminanceResources[0].Texture = source.GetTexture();
        SetShaderParameter(_luminanceParameters, 0, new Variant(Vector2f.One / inputSize));
        var drawParams = new DrawQuadParams
        {
            Resources = _luminanceResources,
            Parameters = _luminanceParameters,
            PipelineStateId = pipelineState,
            ClipToUVOffsetAndScale = renderBufferManager.DefaultClipToUVSpaceOffsetAndScale
        };
        renderBufferManager.SetRenderTargets(null, target);
        renderBufferManager.DrawQuad("Downsample luminosity buffer", drawParams);
    }

    private static void SetShaderParameter(ShaderParameterDesc[] parameters, int index, Variant value)
    {
        var parameter = parameters[index];
        var oldValue = parameter.Value;
        oldValue.Dispose();
        parameter.Value = value;
        parameters[index] = parameter;
    }

    public override void Dispose()
    {
        _pipelineStates = null;
        _textures = null;
        for (var i = 0; i < _luminanceParameters.Length; ++i)
        {
            var parameter = _luminanceParameters[i];
            parameter.Dispose();
            _luminanceParameters[i] = parameter;
        }
        for (var i = 0; i < _adaptedLuminanceParameters.Length; ++i)
        {
            var parameter = _adaptedLuminanceParameters[i];
            parameter.Dispose();
            _adaptedLuminanceParameters[i] = parameter;
        }
        for (var i = 0; i < _applyExposureParameters.Length; ++i)
        {
            var parameter = _applyExposureParameters[i];
            parameter.Dispose();
            _applyExposureParameters[i] = parameter;
        }
        base.Dispose();
    }
}

/// <summary>
/// Post-processing pass that applies bloom to the scene.
/// </summary>
public class BloomPass : RenderPass
{
    private const string DefaultComment = "Create bloom around bright parts of the image";
    private const string NumIterationsName = "Bloom: Num Iterations";
    private const string MinBrightnessName = "Bloom: Min Brightness";
    private const string MaxBrightnessName = "Bloom: Max Brightness";
    private const string IntensityName = "Bloom: Base Intensity";
    private const string IterationFactorName = "Bloom: Iteration Intensity Factor";
    private static readonly NamedSamplerStateDesc[] Samplers =
        [("Albedo", SamplerStateDesc.Bilinear())];
    private readonly ShaderResourceDesc[] _shaderResources = [new() { Name = ShaderResources.Albedo }];
    private readonly ShaderParameterDesc[] _shaderParameters =
    [
        new() { Name = new StringHash("LuminanceWeights") },
        new() { Name = new StringHash("Threshold") },
        new() { Name = new StringHash("InputInvSize") }
    ];
    private readonly ShaderParameterDesc[] _applyParameters =
    [
        new() { Name = new StringHash("LuminanceWeights") },
        new() { Name = new StringHash("Intensity") }
    ];

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
        var newParameters = new Parameters();
        ref readonly var numIterations = ref LoadParameter(NumIterationsName, parameters);
        if (numIterations.Type == VariantType.Int)
            newParameters.NumIterations = Math.Clamp(numIterations.GetInt(), 1, MaxIterations);
        ref readonly var minBrightness = ref LoadParameter(MinBrightnessName, parameters);
        if (minBrightness.Type == VariantType.Float)
            newParameters.MinBrightness = minBrightness.GetFloat();
        ref readonly var maxBrightness = ref LoadParameter(MaxBrightnessName, parameters);
        if (maxBrightness.Type == VariantType.Float)
            newParameters.MaxBrightness = maxBrightness.GetFloat();
        ref readonly var intensity = ref LoadParameter(IntensityName, parameters);
        if (intensity.Type == VariantType.Float)
            newParameters.Intensity = intensity.GetFloat();
        ref readonly var iterationFactor = ref LoadParameter(IterationFactorName, parameters);
        if (iterationFactor.Type == VariantType.Float)
            newParameters.IterationFactor = iterationFactor.GetFloat();

        if (_parameters.NumIterations != newParameters.NumIterations)
            InvalidateTextureCache();

        _parameters = newParameters;
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        var isHdr = sharedState.RenderBufferManager!.IsHDR;
        if (_isHdr != isHdr)
        {
            _isHdr = isHdr;
            InvalidateTextureCache();
        }

        RestoreTextureCache(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        RestorePipelineStateCache(sharedState);
        var renderBufferManager = sharedState.RenderBufferManager!;

        _luminanceWeights = renderBufferManager.IsLinearColorSpace
            ? Color.LuminosityLinear.ToVector3()
            : Color.LuminosityGamma.ToVector3();

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);

        var numIterations = GatherBrightRegions(renderBufferManager, _textures[0].Final!);
        for (var i = 0; i < numIterations; ++i)
        {
            if (i > 0)
                CopyTexture(renderBufferManager, _textures[i - 1].Final!, _textures[i].Final!);
            BlurTexture(renderBufferManager, _textures[i].Final!, _textures[i].Temporary!);
        }

        _intensityMultipliers.Clear();
        for (var i = 0; i < numIterations; ++i)
            _intensityMultipliers.Add(MathF.Pow(_parameters.IterationFactor, i));
        var totalIntensity = 0.0f;
        for (var i = 0; i < _intensityMultipliers.Count; ++i)
            totalIntensity += _intensityMultipliers[i];
        for (var i = 0; i < numIterations; ++i)
            _intensityMultipliers[i] *= _parameters.Intensity / totalIntensity;

        renderBufferManager.SwapColorBuffers(synchronizeContents: false);
        renderBufferManager.SetOutputRenderTargets();
        for (var i = 0; i < numIterations; ++i)
            ApplyBloom(renderBufferManager, _textures[i].Final!, _intensityMultipliers[i]);
    }

    protected const int MaxIterations = 16;

    protected struct Parameters : IEquatable<Parameters>
    {
        public int NumIterations { get; set; } = 5;
        public float MinBrightness { get; set; } = 0.8f;
        public float MaxBrightness { get; set; } = 1.0f;
        public float Intensity { get; set; } = 1.0f;
        public float IterationFactor { get; set; } = 1.0f;

        public Parameters()
        {
        }

        /// <summary>
        /// Utility operators.
        /// </summary>
        public readonly (int, float, float, float, float) Tie()
        {
            return (NumIterations, MinBrightness, MaxBrightness, Intensity, IterationFactor);
        }

        public readonly bool Equals(Parameters other) => Tie() == other.Tie();

        public override readonly bool Equals(object? obj) => obj is Parameters other && Equals(other);

        public override readonly int GetHashCode() => Tie().GetHashCode();

        public static bool operator ==(Parameters left, Parameters right) => left.Tie() == right.Tie();

        public static bool operator !=(Parameters left, Parameters right) => left.Tie() != right.Tie();
    }

    protected void InvalidateTextureCache()
    {
        _textures.Clear();
    }

    protected void InvalidatePipelineStateCache()
    {
        _pipelineStates = null;
    }

    protected void RestoreTextureCache(SharedRenderPassState sharedState)
    {
        if (_textures.Count != 0)
            return;

        var renderBufferManager = sharedState.RenderBufferManager!;
        for (var i = 0; i < _parameters.NumIterations; ++i)
        {
            var format = _isHdr ? TextureFormat.TexFormatRgba16Float : TextureFormat.TexFormatRgba8Unorm;
            var parameters = new RenderBufferParams(format, 1, RenderBufferFlag.BilinearFiltering);
            // Start scale is 1
            // It's okay to do it unchecked because render buffers are never smaller than 1x1
            var sizeMultiplier = Vector2f.One / (1 << i);
            _textures.Add(new TextureCache
            {
                Final = renderBufferManager.CreateColorBuffer(parameters, sizeMultiplier),
                Temporary = renderBufferManager.CreateColorBuffer(parameters, sizeMultiplier)
            });
        }
    }

    protected void RestorePipelineStateCache(SharedRenderPassState sharedState)
    {
        if (_pipelineStates.HasValue)
            return;

        var renderBufferManager = sharedState.RenderBufferManager!;
        _pipelineStates = new PipelineStateCache
        {
            Bright = renderBufferManager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_Bloom", "BRIGHT", Samplers),
            BlurH = renderBufferManager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_Bloom", "BLURH", Samplers),
            BlurV = renderBufferManager.CreateQuadPipelineState(BlendMode.Replace, "v2/P_Bloom", "BLURV", Samplers),
            Bloom = renderBufferManager.CreateQuadPipelineState(BlendMode.Add, "v2/P_Bloom", "COMBINE", Samplers)
        };
    }

    protected int GatherBrightRegions(RenderBufferManager renderBufferManager, RenderBuffer destination)
    {
        var viewportTexture = renderBufferManager.SecondaryColorTexture!;
        var inputSize = viewportTexture.GetParams().Size;
        var inputInvSize = Vector2f.One / new Vector2f(inputSize.X, inputSize.Y);

        _shaderResources[0].Texture = viewportTexture;
        GetShaderParameters(inputInvSize);
        var drawParameters = new DrawQuadParams
        {
            Resources = _shaderResources,
            Parameters = _shaderParameters,
            ClipToUVOffsetAndScale = renderBufferManager.DefaultClipToUVSpaceOffsetAndScale,
            PipelineStateId = _pipelineStates!.Value.Bright
        };

        renderBufferManager.SetRenderTargets(null, destination);
        renderBufferManager.DrawQuad("Gather bright regions", drawParameters);

        return Math.Clamp(
            (int)MathDefs.LogBaseTwo((uint)Math.Min(inputSize.X, inputSize.Y)),
            1,
            _parameters.NumIterations);
    }

    protected void BlurTexture(RenderBufferManager renderBufferManager, RenderBuffer final, RenderBuffer temporary)
    {
        var inputInvSize = Vector2f.One / final.GetTexture()!.GetParams().Size.ToVector2();
        GetShaderParameters(inputInvSize);
        var drawParameters = new DrawQuadParams
        {
            Resources = _shaderResources,
            Parameters = _shaderParameters,
            ClipToUVOffsetAndScale = renderBufferManager.DefaultClipToUVSpaceOffsetAndScale
        };

        _shaderResources[0].Texture = final.GetTexture();
        drawParameters.PipelineStateId = _pipelineStates!.Value.BlurH;
        renderBufferManager.SetRenderTargets(null, temporary);
        renderBufferManager.DrawQuad("Blur vertically", drawParameters);

        _shaderResources[0].Texture = temporary.GetTexture();
        drawParameters.PipelineStateId = _pipelineStates.Value.BlurV;
        renderBufferManager.SetRenderTargets(null, final);
        renderBufferManager.DrawQuad("Blur horizontally", drawParameters);
    }

    protected void CopyTexture(RenderBufferManager renderBufferManager, RenderBuffer source, RenderBuffer destination)
    {
        renderBufferManager.SetRenderTargets(null, destination);
        renderBufferManager.DrawTexture("Downscale bloom", source.GetTexture()!);
    }

    protected void ApplyBloom(RenderBufferManager renderBufferManager, RenderBuffer bloom, float intensity)
    {
        _shaderResources[0].Texture = bloom.GetTexture();
        SetShaderParameter(_applyParameters, 0, new Variant(_luminanceWeights));
        SetShaderParameter(_applyParameters, 1, new Variant(intensity));
        renderBufferManager.DrawViewportQuad(
            "Apply bloom", _pipelineStates!.Value.Bloom, _shaderResources, _applyParameters);
    }

    protected void GetShaderParameters(Vector2f inputInvSize)
    {
        var thresholdGap = Math.Max(0.01f, _parameters.MaxBrightness - _parameters.MinBrightness);
        SetShaderParameter(_shaderParameters, 0, new Variant(_luminanceWeights));
        SetShaderParameter(_shaderParameters, 1,
            new Variant(new Vector2f(_parameters.MinBrightness, 1.0f / thresholdGap)));
        SetShaderParameter(_shaderParameters, 2, new Variant(inputInvSize));
    }

    protected struct PipelineStateCache
    {
        public StaticPipelineStateId Bright { get; set; }
        public StaticPipelineStateId BlurV { get; set; }
        public StaticPipelineStateId BlurH { get; set; }
        public StaticPipelineStateId Bloom { get; set; }
    }

    protected struct TextureCache
    {
        public RenderBuffer? Final { get; set; }
        public RenderBuffer? Temporary { get; set; }
    }

    private static void SetShaderParameter(ShaderParameterDesc[] parameters, int index, Variant value)
    {
        var parameter = parameters[index];
        var oldValue = parameter.Value;
        oldValue.Dispose();
        parameter.Value = value;
        parameters[index] = parameter;
    }

    public override void Dispose()
    {
        _pipelineStates = null;
        _textures.Clear();
        _shaderResources[0].Texture = null;
        for (var i = 0; i < _shaderParameters.Length; ++i)
        {
            var parameter = _shaderParameters[i];
            parameter.Dispose();
            _shaderParameters[i] = parameter;
        }
        for (var i = 0; i < _applyParameters.Length; ++i)
        {
            var parameter = _applyParameters[i];
            parameter.Dispose();
            _applyParameters[i] = parameter;
        }
        _intensityMultipliers.Clear();
        base.Dispose();
    }

    private PipelineStateCache? _pipelineStates;
    private readonly List<TextureCache> _textures = [];
    private bool _isHdr;
    private Parameters _parameters = new();
    private Vector3f _luminanceWeights;
    private readonly List<float> _intensityMultipliers = [];
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
