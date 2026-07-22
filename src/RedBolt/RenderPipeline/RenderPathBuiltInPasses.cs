using Microsoft.Extensions.Logging;

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
public class AmbientOcclusionPass : RenderPass
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
    private static readonly NamedSamplerStateDesc[] SsaoSamplers =
    [
        ("Albedo", SamplerStateDesc.Bilinear()),
        ("Normal", SamplerStateDesc.Bilinear()),
        ("DepthBuffer", SamplerStateDesc.Nearest())
    ];
    private static readonly NamedSamplerStateDesc[] BlurSamplers =
    [
        ("Albedo", SamplerStateDesc.Bilinear()),
        ("Normal", SamplerStateDesc.Bilinear()),
        ("DepthBuffer", SamplerStateDesc.Nearest())
    ];
    private static readonly NamedSamplerStateDesc[] ApplySamplers =
        [("Albedo", SamplerStateDesc.Bilinear())];
    private static readonly ShaderParameterDesc[] EmptyShaderParameters = [];
    private readonly ShaderParameterDesc[] _evaluateParameters =
    [
        new() { Name = new StringHash("InputInvSize") },
        new() { Name = new StringHash("BlurStep") },
        new() { Name = new StringHash("Strength") },
        new() { Name = new StringHash("Exponent") },
        new() { Name = new StringHash("RadiusInfo") },
        new() { Name = new StringHash("FadeDistance") },
        new() { Name = new StringHash("ViewToTexture") },
        new() { Name = new StringHash("TextureToView") },
        new() { Name = new StringHash("WorldToView") }
    ];
    private readonly ShaderResourceDesc[] _evaluateResources =
    [
        new() { Name = ShaderResources.DepthBuffer },
        new() { Name = ShaderResources.Albedo },
        new() { Name = ShaderResources.Normal }
    ];
    private readonly ShaderParameterDesc[] _blurParameters =
    [
        new() { Name = new StringHash("BlurStep") },
        new() { Name = new StringHash("BlurZThreshold") },
        new() { Name = new StringHash("BlurNormalInvThreshold") },
        new() { Name = new StringHash("TextureToView") }
    ];
    private readonly ShaderResourceDesc[] _blurResources =
    [
        new() { Name = ShaderResources.Albedo },
        new() { Name = ShaderResources.DepthBuffer },
        new() { Name = ShaderResources.Normal }
    ];
    private readonly ShaderResourceDesc[] _blitResources = [new() { Name = ShaderResources.Albedo }];

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

        var newParameters = new Parameters();
        ref readonly var downscale = ref LoadParameter(DownscaleName, parameters);
        if (downscale.Type == VariantType.Int)
            newParameters.Downscale = downscale.GetInt();
        ref readonly var strength = ref LoadParameter(StrengthName, parameters);
        if (strength.Type == VariantType.Float)
            newParameters.Strength = strength.GetFloat();
        ref readonly var exponent = ref LoadParameter(ExponentName, parameters);
        if (exponent.Type == VariantType.Float)
            newParameters.Exponent = exponent.GetFloat();
        ref readonly var radiusNear = ref LoadParameter(RadiusNearName, parameters);
        if (radiusNear.Type == VariantType.Float)
            newParameters.RadiusNear = radiusNear.GetFloat();
        ref readonly var distanceNear = ref LoadParameter(DistanceNearName, parameters);
        if (distanceNear.Type == VariantType.Float)
            newParameters.DistanceNear = distanceNear.GetFloat();
        ref readonly var radiusFar = ref LoadParameter(RadiusFarName, parameters);
        if (radiusFar.Type == VariantType.Float)
            newParameters.RadiusFar = radiusFar.GetFloat();
        ref readonly var distanceFar = ref LoadParameter(DistanceFarName, parameters);
        if (distanceFar.Type == VariantType.Float)
            newParameters.DistanceFar = distanceFar.GetFloat();
        ref readonly var fadeDistanceBegin = ref LoadParameter(FadeDistanceBeginName, parameters);
        if (fadeDistanceBegin.Type == VariantType.Float)
            newParameters.FadeDistanceBegin = fadeDistanceBegin.GetFloat();
        ref readonly var fadeDistanceEnd = ref LoadParameter(FadeDistanceEndName, parameters);
        if (fadeDistanceEnd.Type == VariantType.Float)
            newParameters.FadeDistanceEnd = fadeDistanceEnd.GetFloat();
        ref readonly var blurDepthThreshold = ref LoadParameter(BlurDepthThresholdName, parameters);
        if (blurDepthThreshold.Type == VariantType.Float)
            newParameters.BlurDepthThreshold = blurDepthThreshold.GetFloat();
        ref readonly var blurNormalThreshold = ref LoadParameter(BlurNormalThresholdName, parameters);
        if (blurNormalThreshold.Type == VariantType.Float)
            newParameters.BlurNormalThreshold = blurNormalThreshold.GetFloat();

        if (_parameters.Downscale != newParameters.Downscale)
            InvalidateTextureCache();

        _isEnabledInternally = true;
        _parameters = newParameters;
    }

    public override void Update(SharedRenderPassState sharedState)
    {
        RestoreTextureCache(sharedState);
    }

    public override void Render(SharedRenderPassState sharedState)
    {
        ConnectToRenderBuffer(ref _normalBuffer, SharedRenderPassState.NormalBufferId, sharedState, required: false);

        RestorePipelineStateCache(sharedState);
        if (_parameters.Strength <= 0.0f)
            return;

        var renderBufferManager = sharedState.RenderBufferManager!;
        var depthTexture = renderBufferManager.DepthStencilTexture;
        if (depthTexture!.GetParams().MultiSample != 1)
        {
            Log.GetLogger().LogError("AmbientOcclusionPass: MSAA is not supported");
            return;
        }

        if (sharedState.RenderCamera is null || !sharedState.RenderCamera.TryGetTarget(out var camera))
        {
            Log.GetLogger().LogError("AmbientOcclusionPass: Render camera is not available");
            return;
        }

        // Convert texture coordinates into clip space
        var clipToTextureSpace = Matrix4.Identity;
        clipToTextureSpace.SetScale(new Vector3f(0.5f, 0.5f, 1.0f));
        clipToTextureSpace.SetTranslation(new Vector3f(0.5f, 0.5f, 0.0f));

        var viewToTextureSpace = clipToTextureSpace * camera.GetGPUProjection(true);
        var textureToViewSpace = viewToTextureSpace.Inverse;

        // TODO: Support preview
        EvaluateAO(renderBufferManager, camera, viewToTextureSpace, textureToViewSpace);
        BlurTexture(renderBufferManager, textureToViewSpace);
        Blit(renderBufferManager, _pipelineStates!.Value.Combine);
    }

    protected struct Parameters : IEquatable<Parameters>
    {
        public int Downscale { get; set; }
        public float Strength { get; set; } = 0.7f;
        public float Exponent { get; set; } = 1.5f;
        public float RadiusNear { get; set; } = 0.05f;
        public float DistanceNear { get; set; } = 1.0f;
        public float RadiusFar { get; set; } = 1.0f;
        public float DistanceFar { get; set; } = 100.0f;
        public float FadeDistanceBegin { get; set; } = 100.0f;
        public float FadeDistanceEnd { get; set; } = 200.0f;
        public float BlurDepthThreshold { get; set; } = 0.1f;
        public float BlurNormalThreshold { get; set; } = 0.2f;

        public Parameters()
        {
        }

        /// <summary>
        /// Utility operators.
        /// </summary>
        public readonly (int, float, float, float, float, float, float, float, float, float, float) Tie()
        {
            return (Downscale, Strength, Exponent, RadiusNear, DistanceNear, RadiusFar, DistanceFar,
                FadeDistanceBegin, FadeDistanceEnd, BlurDepthThreshold, BlurNormalThreshold);
        }

        public readonly bool Equals(Parameters other) => Tie() == other.Tie();

        public override readonly bool Equals(object? obj) => obj is Parameters other && Equals(other);

        public override readonly int GetHashCode() => Tie().GetHashCode();

        public static bool operator ==(Parameters left, Parameters right) => left.Tie() == right.Tie();

        public static bool operator !=(Parameters left, Parameters right) => left.Tie() != right.Tie();
    }

    protected void InvalidateTextureCache()
    {
        _textures = null;
    }

    protected void InvalidatePipelineStateCache()
    {
        _pipelineStates = null;
    }

    protected void RestoreTextureCache(SharedRenderPassState sharedState)
    {
        if (_textures.HasValue)
            return;

        var resourceCache = GetSubsystem<ResourceCache>()!;
        var renderBufferManager = sharedState.RenderBufferManager!;
        var sizeMultiplier = Vector2f.One / (1 << _parameters.Downscale);
        var parameters = new RenderBufferParams(
            TextureFormat.TexFormatRgba8Unorm, 1, RenderBufferFlag.BilinearFiltering);
        _textures = new TextureCache
        {
            Noise = resourceCache.GetResource<Texture2D>("Textures/SSAONoise.png"),
            CurrentTarget = renderBufferManager.CreateColorBuffer(parameters, sizeMultiplier),
            PreviousTarget = renderBufferManager.CreateColorBuffer(parameters, sizeMultiplier)
        };
    }

    protected void RestorePipelineStateCache(SharedRenderPassState sharedState)
    {
        if (_pipelineStates.HasValue)
            return;

        var renderBufferManager = sharedState.RenderBufferManager!;
        _pipelineStates = new PipelineStateCache
        {
            SsaoForward = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Replace, "v2/P_SSAO", "EVALUATE_OCCLUSION", SsaoSamplers),
            SsaoDeferred = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Replace, "v2/P_SSAO", "EVALUATE_OCCLUSION DEFERRED", SsaoSamplers),
            BlurForward = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Replace, "v2/P_SSAO", "BLUR", BlurSamplers),
            BlurDeferred = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Replace, "v2/P_SSAO", "BLUR DEFERRED", BlurSamplers),
            Combine = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Alpha, "v2/P_SSAO", "COMBINE", ApplySamplers),
            Preview = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Replace, "v2/P_SSAO", "PREVIEW", ApplySamplers)
        };
    }

    protected void EvaluateAO(RenderBufferManager renderBufferManager, Camera camera,
        Matrix4 viewToTextureSpace, Matrix4 textureToViewSpace)
    {
        var renderDevice = GetSubsystem<RenderDevice>()!;
        var isOpenGl = renderDevice.GetBackend() == RenderBackend.OpenGL;
        var inputInvSize = renderBufferManager.InvOutputSize;

        var invertY = isOpenGl == camera.GetFlipVertical();
        var worldToViewSpace = camera.GetView().Matrix4;
        var worldToViewSpaceCorrected = invertY ? FlipMatrix * worldToViewSpace : worldToViewSpace;

        var radiusSlope = (_parameters.RadiusFar - _parameters.RadiusNear)
            / Math.Max(1.0f, _parameters.DistanceFar - _parameters.DistanceNear);
        var radiusInfo = new Vector4f(
            radiusSlope,
            _parameters.RadiusNear - radiusSlope * _parameters.DistanceNear,
            _parameters.RadiusNear,
            _parameters.RadiusFar);

        SetShaderParameter(_evaluateParameters, 0, new Variant(inputInvSize));
        SetShaderParameter(_evaluateParameters, 1, new Variant(inputInvSize));
        SetShaderParameter(_evaluateParameters, 2, new Variant(_parameters.Strength));
        SetShaderParameter(_evaluateParameters, 3, new Variant(_parameters.Exponent));
        SetShaderParameter(_evaluateParameters, 4, new Variant(radiusInfo));
        SetShaderParameter(_evaluateParameters, 5,
            new Variant(new Vector2f(_parameters.FadeDistanceBegin, _parameters.FadeDistanceEnd)));
        SetShaderParameter(_evaluateParameters, 6, new Variant(viewToTextureSpace));
        SetShaderParameter(_evaluateParameters, 7, new Variant(textureToViewSpace));
        SetShaderParameter(_evaluateParameters, 8, new Variant(worldToViewSpaceCorrected));

        var normalBuffer = GetNormalBuffer();
        var textures = _textures!.Value;
        _evaluateResources[0].Texture = renderBufferManager.DepthStencilTexture;
        _evaluateResources[1].Texture = textures.Noise?.GetRawTexture();
        _evaluateResources[2].Texture = normalBuffer?.GetTexture();
        var drawParameters = new DrawQuadParams
        {
            Resources = _evaluateResources,
            Parameters = _evaluateParameters,
            ClipToUVOffsetAndScale = renderBufferManager.DefaultClipToUVSpaceOffsetAndScale,
            PipelineStateId = normalBuffer is not null
                ? _pipelineStates!.Value.SsaoDeferred
                : _pipelineStates!.Value.SsaoForward
        };

        renderBufferManager.SetRenderTargets(null, textures.CurrentTarget!);
        renderBufferManager.DrawQuad("Apply SSAO", drawParameters);
        // renderBufferManager.SetOutputRenderTargets();

        (textures.CurrentTarget, textures.PreviousTarget) = (textures.PreviousTarget, textures.CurrentTarget);
        _textures = textures;
    }

    protected void BlurTexture(RenderBufferManager renderBufferManager, Matrix4 textureToViewSpace)
    {
        var textures = _textures!.Value;
        var textureSize3 = textures.CurrentTarget!.GetTexture()!.GetParams().Size;
        var textureSize = new Vector2i(textureSize3.X, textureSize3.Y);
        var blurStep = Vector2f.One / textureSize.ToVector2();

        SetShaderParameter(_blurParameters, 0, new Variant(blurStep));
        SetShaderParameter(_blurParameters, 1, new Variant(_parameters.BlurDepthThreshold));
        SetShaderParameter(_blurParameters, 2, new Variant(1.0f - _parameters.BlurNormalThreshold));
        SetShaderParameter(_blurParameters, 3, new Variant(textureToViewSpace));

        var normalBuffer = GetNormalBuffer();
        var drawParameters = new DrawQuadParams
        {
            Parameters = _blurParameters,
            ClipToUVOffsetAndScale = renderBufferManager.DefaultClipToUVSpaceOffsetAndScale,
            PipelineStateId = normalBuffer is not null
                ? _pipelineStates!.Value.BlurDeferred
                : _pipelineStates!.Value.BlurForward
        };

        renderBufferManager.SetRenderTargets(null, textures.CurrentTarget!);
        _blurResources[0].Texture = textures.PreviousTarget!.GetTexture();
        _blurResources[1].Texture = renderBufferManager.DepthStencilTexture;
        _blurResources[2].Texture = normalBuffer?.GetTexture();
        drawParameters.Resources = _blurResources;
        SetShaderParameter(_blurParameters, 0, new Variant(new Vector2f(blurStep.X, 0.0f)));
        renderBufferManager.DrawQuad("SSAO Blur Horizontally", drawParameters);
        (textures.CurrentTarget, textures.PreviousTarget) = (textures.PreviousTarget, textures.CurrentTarget);

        renderBufferManager.SetRenderTargets(null, textures.CurrentTarget!);
        _blurResources[0].Texture = textures.PreviousTarget!.GetTexture();
        _blurResources[1].Texture = renderBufferManager.DepthStencilTexture;
        _blurResources[2].Texture = normalBuffer?.GetTexture();
        drawParameters.Resources = _blurResources;
        SetShaderParameter(_blurParameters, 0, new Variant(new Vector2f(0.0f, blurStep.Y)));
        renderBufferManager.DrawQuad("SSAO Blur Vertically", drawParameters);
        (textures.CurrentTarget, textures.PreviousTarget) = (textures.PreviousTarget, textures.CurrentTarget);
        _textures = textures;
    }

    protected void Blit(RenderBufferManager renderBufferManager, StaticPipelineStateId pipelineStateId)
    {
        renderBufferManager.SetOutputRenderTargets();

        _blitResources[0].Texture = _textures!.Value.PreviousTarget!.GetTexture();
        renderBufferManager.DrawViewportQuad(
            "SSAO Combine", pipelineStateId, _blitResources, EmptyShaderParameters);
    }

    private RenderBuffer? GetNormalBuffer()
    {
        return _normalBuffer is not null && _normalBuffer.TryGetTarget(out var normalBuffer)
            ? normalBuffer
            : null;
    }

    private static void SetShaderParameter(ShaderParameterDesc[] parameters, int index, Variant value)
    {
        var parameter = parameters[index];
        var oldValue = parameter.Value;
        oldValue.Dispose();
        parameter.Value = value;
        parameters[index] = parameter;
    }

    protected struct TextureCache
    {
        public Texture2D? Noise { get; set; }
        public RenderBuffer? CurrentTarget { get; set; }
        public RenderBuffer? PreviousTarget { get; set; }
    }

    protected struct PipelineStateCache
    {
        public StaticPipelineStateId SsaoForward { get; set; }
        public StaticPipelineStateId SsaoDeferred { get; set; }
        public StaticPipelineStateId BlurForward { get; set; }
        public StaticPipelineStateId BlurDeferred { get; set; }
        public StaticPipelineStateId Combine { get; set; }
        public StaticPipelineStateId Preview { get; set; }
    }

    public override void Dispose()
    {
        _normalBuffer = null;
        _textures = null;
        _pipelineStates = null;
        ClearShaderResources(_evaluateResources);
        ClearShaderResources(_blurResources);
        ClearShaderResources(_blitResources);
        DisposeShaderParameters(_evaluateParameters);
        DisposeShaderParameters(_blurParameters);
        base.Dispose();
    }

    private static void ClearShaderResources(ShaderResourceDesc[] resources)
    {
        for (var i = 0; i < resources.Length; ++i)
            resources[i].Texture = null;
    }

    private static void DisposeShaderParameters(ShaderParameterDesc[] parameters)
    {
        for (var i = 0; i < parameters.Length; ++i)
        {
            var parameter = parameters[i];
            parameter.Dispose();
            parameters[i] = parameter;
        }
    }

    private Parameters _parameters = new();
    private WeakReference<RenderBuffer>? _normalBuffer;
    private TextureCache? _textures;
    private PipelineStateCache? _pipelineStates;
}

/// <summary>
/// Post-processing pass that renders outline from the color buffer.
/// </summary>
public class OutlineRenderPass : RenderPass
{
    private const string DefaultComment = "Draw outline of the contents of the color buffer";
    public static readonly StringHash ColorBufferId = "outline";
    private static readonly NamedSamplerStateDesc[] Samplers =
        [(ShaderResources.Albedo, SamplerStateDesc.Bilinear())];
    private readonly ShaderParameterDesc[] _parameters =
        [new() { Name = new StringHash("InputInvSize") }];
    private readonly ShaderResourceDesc[] _resources =
        [new() { Name = ShaderResources.Albedo }];

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
        ConnectToRenderBuffer(ref _colorBuffer, ColorBufferId, sharedState);

        if (_colorBuffer is null || !_colorBuffer.TryGetTarget(out var colorBuffer) || !colorBuffer.IsEnabled)
            return;

        RestoreCache(sharedState);

        var renderBufferManager = sharedState.RenderBufferManager!;
        var inLinearSpace = renderBufferManager.IsLinearColorSpace;
        var pipelineState = inLinearSpace ? _pipelineStates!.Value.Linear : _pipelineStates!.Value.Gamma;

        var texture = colorBuffer.GetTexture()!;
        var textureSize = texture.GetParams().Size;
        var inputInvSize = Vector2f.One / new Vector2f(textureSize.X, textureSize.Y);

        var parameter = _parameters[0];
        parameter.Value.Dispose();
        parameter.Value = new Variant(inputInvSize);
        _parameters[0] = parameter;
        _resources[0].Texture = texture;

        renderBufferManager.SetOutputRenderTargets();
        renderBufferManager.DrawViewportQuad("Apply outline", pipelineState, _resources, _parameters);
    }

    private void InvalidateCache()
    {
        _pipelineStates = null;
    }

    private void RestoreCache(SharedRenderPassState sharedState)
    {
        if (_pipelineStates.HasValue)
            return;

        _pipelineStates = new PipelineStateCache();

        var pipelineStates = _pipelineStates.Value;
        pipelineStates.Linear = sharedState.RenderBufferManager!.CreateQuadPipelineState(
            BlendMode.Alpha, "v2/P_Outline", "URHO3D_GAMMA_CORRECTION", Samplers);
        _pipelineStates = pipelineStates;
        pipelineStates.Gamma = sharedState.RenderBufferManager.CreateQuadPipelineState(
            BlendMode.Alpha, "v2/P_Outline", string.Empty, Samplers);
        _pipelineStates = pipelineStates;
    }

    private struct PipelineStateCache
    {
        public StaticPipelineStateId Gamma { get; set; }
        public StaticPipelineStateId Linear { get; set; }
    }

    public override void Dispose()
    {
        _colorBuffer = null;
        _pipelineStates = null;
        _resources[0].Texture = null;
        var parameter = _parameters[0];
        parameter.Dispose();
        _parameters[0] = parameter;
        base.Dispose();
    }

    private PipelineStateCache? _pipelineStates;
    private WeakReference<RenderBuffer>? _colorBuffer;
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
