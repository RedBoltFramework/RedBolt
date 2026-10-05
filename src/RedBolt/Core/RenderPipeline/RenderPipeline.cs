// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderPipeline;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;
using RedBolt.Scene;

/// <summary>
/// Scene component that spawns render pipeline instances.
/// </summary>
[Reflected]
public partial class RenderPipeline : Component
{
    private static readonly ResourceRef DefaultRenderPath =
        new(new StringHash(nameof(RenderPath)), "RenderPaths/Default.renderpath");

    /// <summary>Invoked when settings change.</summary>
    public event Action<RenderPipeline, RenderPipelineSettings>? SettingsChanged;

    /// <summary>Invoked when render path changes.</summary>
    public event Action<RenderPipeline, RenderPath?>? RenderPathChanged;

    /// <summary>Invoked when render path parameters change.</summary>
    public event Action<RenderPipeline>? ParametersChanged;

    private RenderPath? _renderPath;
    private IReadOnlyList<(string Name, bool Enabled)> _renderPasses = [];
    private readonly StringVariantMap _renderPathParameters = new();
    private readonly StringVariantMap _frameShaderParameters = new();
    private RenderPipelineSettings _settings = new();

    [ThreadStatic]
    private static VariantVector? _renderPassesAttributeResult;

    public RenderPipeline()
    {
        SetRenderPathAttr(DefaultRenderPath);

        // Enable instancing by default for default render pipeline
        var instancingBuffer = _settings.InstancingBuffer;
        instancingBuffer.EnableInstancing = true;
        _settings.InstancingBuffer = instancingBuffer;
        _settings.Validate();
        _settings.AdjustToSupported(Context.Instance);
    }

    public override void Dispose()
    {
        _frameShaderParameters.Dispose();
        _renderPathParameters.Dispose();
        _renderPasses = [];
        _renderPath = null;
        ParametersChanged = null;
        RenderPathChanged = null;
        SettingsChanged = null;
        base.Dispose();
    }

    private static object? GetRenderPathAttributeDefault() => DefaultRenderPath;

    [Serialized("Render Path", typeof(ResourceRef), DefaultSource = nameof(GetRenderPathAttributeDefault), Setter = nameof(SetRenderPathAttr))]
    private object? GetSerializedRenderPath() => GetRenderPathAttr();

    private static object? GetRenderPassesAttributeDefault() => new VariantVector();

    [Serialized("Render Passes", typeof(VariantVector), DefaultSource = nameof(GetRenderPassesAttributeDefault), Setter = nameof(SetRenderPassesAttr))]
    private object? GetSerializedRenderPasses() => GetRenderPassesAttr();

    private static object? GetRenderPathParametersAttributeDefault() => new StringVariantMap();

    private static void ConfigureRenderPathParametersAttribute(AttributeHandle attribute)
    {
        attribute.SetMetadata(AttributeMetadata.DynamicMetadata, true);
    }

    [Serialized("Render Path Parameters", typeof(StringVariantMap), Configure = nameof(ConfigureRenderPathParametersAttribute), DefaultSource = nameof(GetRenderPathParametersAttributeDefault), Setter = nameof(SetRenderPathParameters))]
    private object? GetSerializedRenderPathParameters() => GetRenderPathParameters();

    private static object? GetFrameShaderParametersAttributeDefault() => new StringVariantMap();

    [Serialized("Frame Shader Parameters", typeof(StringVariantMap), DefaultSource = nameof(GetFrameShaderParametersAttributeDefault), Setter = nameof(SetFrameShaderParameters))]
    private object? GetSerializedFrameShaderParameters() => GetFrameShaderParameters();

    private static object? GetColorSpaceAttributeDefault() => RenderPipelineColorSpace.GammaLDR;

    [Serialized("Color Space", DefaultSource = nameof(GetColorSpaceAttributeDefault), EnumNames = ["LDR Gamma", "LDR Linear", "HDR Linear", "Optimized"])]
    private RenderPipelineColorSpace SerializedColorSpace
    {
        get => _settings.RenderBufferManager.ColorSpace;
        set
        {
            var settings = _settings.RenderBufferManager;
            settings.ColorSpace = value;
            _settings.RenderBufferManager = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetMaterialQualityAttributeDefault() => new SceneProcessorSettings().MaterialQuality;

    [Serialized("Material Quality", DefaultSource = nameof(GetMaterialQualityAttributeDefault), EnumNames = ["Low", "Medium", "High"])]
    private MaterialQuality SerializedMaterialQuality
    {
        get => _settings.SceneProcessor.MaterialQuality;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.MaterialQuality = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetSpecularQualityAttributeDefault() => new SceneProcessorSettings().SpecularQuality;

    [Serialized("Specular Quality", DefaultSource = nameof(GetSpecularQualityAttributeDefault), EnumNames = ["Disabled", "Simple", "Antialiased"])]
    private SpecularQuality SerializedSpecularQuality
    {
        get => _settings.SceneProcessor.SpecularQuality;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.SpecularQuality = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetReflectionQualityAttributeDefault() => new SceneProcessorSettings().ReflectionQuality;

    [Serialized("Reflection Quality", DefaultSource = nameof(GetReflectionQualityAttributeDefault), EnumNames = ["Vertex", "Pixel"])]
    private ReflectionQuality SerializedReflectionQuality
    {
        get => _settings.SceneProcessor.ReflectionQuality;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.ReflectionQuality = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetLinearReflectionMapsAttributeDefault() => new SceneProcessorSettings().LinearReflectionMaps;

    [Serialized("Linear Space Reflection Maps", DefaultSource = nameof(GetLinearReflectionMapsAttributeDefault))]
    private bool SerializedLinearReflectionMaps
    {
        get => _settings.SceneProcessor.LinearReflectionMaps;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.LinearReflectionMaps = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetReadableDepthAttributeDefault() => new RenderBufferManagerSettings().ReadableDepth;

    [Serialized("Readable Depth", DefaultSource = nameof(GetReadableDepthAttributeDefault))]
    private bool SerializedReadableDepth
    {
        get => _settings.RenderBufferManager.ReadableDepth;
        set
        {
            var settings = _settings.RenderBufferManager;
            settings.ReadableDepth = value;
            _settings.RenderBufferManager = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetMaxVertexLightsAttributeDefault() => new DrawableProcessorSettings().MaxVertexLights;

    [Serialized("Max Vertex Lights", DefaultSource = nameof(GetMaxVertexLightsAttributeDefault))]
    private int SerializedMaxVertexLights
    {
        get => _settings.SceneProcessor.MaxVertexLights;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.MaxVertexLights = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetMaxPixelLightsAttributeDefault() => new DrawableProcessorSettings().MaxPixelLights;

    [Serialized("Max Pixel Lights", DefaultSource = nameof(GetMaxPixelLightsAttributeDefault))]
    private int SerializedMaxPixelLights
    {
        get => _settings.SceneProcessor.MaxPixelLights;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.MaxPixelLights = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetAmbientModeAttributeDefault() => DrawableAmbientMode.Directional;

    [Serialized("Ambient Mode", DefaultSource = nameof(GetAmbientModeAttributeDefault), EnumNames = ["Constant", "Flat", "Directional"])]
    private DrawableAmbientMode SerializedAmbientMode
    {
        get => _settings.SceneProcessor.AmbientMode;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.AmbientMode = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetEnableInstancingAttributeDefault() => true;

    [Serialized("Enable Instancing", DefaultSource = nameof(GetEnableInstancingAttributeDefault))]
    private bool SerializedEnableInstancing
    {
        get => _settings.InstancingBuffer.EnableInstancing;
        set
        {
            var settings = _settings.InstancingBuffer;
            settings.EnableInstancing = value;
            _settings.InstancingBuffer = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetDepthPrePassAttributeDefault() => false;

    [Serialized("Depth Pre-Pass", DefaultSource = nameof(GetDepthPrePassAttributeDefault))]
    private bool SerializedDepthPrePass
    {
        get => _settings.SceneProcessor.DepthPrePass;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.DepthPrePass = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetLightingModeAttributeDefault() => DirectLightingMode.Forward;

    [Serialized("Lighting Mode", DefaultSource = nameof(GetLightingModeAttributeDefault), EnumNames = ["Forward", "Deferred Blinn-Phong", "Deferred PBR"])]
    private DirectLightingMode SerializedLightingMode
    {
        get => _settings.SceneProcessor.LightingMode;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.LightingMode = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetDirectionalShadowResolutionAttributeDefault() => new SceneProcessorSettings().DirectionalShadowSize;

    [Serialized("Directional Light Shadow Resolution", DefaultSource = nameof(GetDirectionalShadowResolutionAttributeDefault))]
    private int SerializedDirectionalShadowResolution
    {
        get => _settings.SceneProcessor.DirectionalShadowSize;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.DirectionalShadowSize = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetSpotShadowResolutionAttributeDefault() => new SceneProcessorSettings().SpotShadowSize;

    [Serialized("Spot Light Shadow Resolution", DefaultSource = nameof(GetSpotShadowResolutionAttributeDefault))]
    private int SerializedSpotShadowResolution
    {
        get => _settings.SceneProcessor.SpotShadowSize;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.SpotShadowSize = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetPointShadowResolutionAttributeDefault() => new SceneProcessorSettings().PointShadowSize;

    [Serialized("Point Light Shadow Resolution", DefaultSource = nameof(GetPointShadowResolutionAttributeDefault))]
    private int SerializedPointShadowResolution
    {
        get => _settings.SceneProcessor.PointShadowSize;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.PointShadowSize = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetEnableShadowsAttributeDefault() => true;

    [Serialized("Enable Shadows", DefaultSource = nameof(GetEnableShadowsAttributeDefault))]
    private bool SerializedEnableShadows
    {
        get => _settings.SceneProcessor.EnableShadows;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.EnableShadows = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetCubemapBoxProjectionAttributeDefault() => false;

    [Serialized("Cubemap Box Projection", DefaultSource = nameof(GetCubemapBoxProjectionAttributeDefault))]
    private bool SerializedCubemapBoxProjection
    {
        get => _settings.SceneProcessor.CubemapBoxProjection;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.CubemapBoxProjection = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetPcfKernelSizeAttributeDefault() => 1;

    [Serialized("PCF Kernel Size", DefaultSource = nameof(GetPcfKernelSizeAttributeDefault))]
    private int SerializedPcfKernelSize
    {
        get => _settings.SceneProcessor.PcfKernelSize;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.PcfKernelSize = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetEnableVarianceShadowMapsAttributeDefault() => false;

    [Serialized("Use Variance Shadow Maps", DefaultSource = nameof(GetEnableVarianceShadowMapsAttributeDefault))]
    private bool SerializedEnableVarianceShadowMaps
    {
        get => _settings.ShadowMapAllocator.EnableVarianceShadowMaps;
        set
        {
            var settings = _settings.ShadowMapAllocator;
            settings.EnableVarianceShadowMaps = value;
            _settings.ShadowMapAllocator = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetVarianceShadowMapParamsAttributeDefault() => new BatchRendererSettings().VarianceShadowMapParams;

    [Serialized("VSM Shadow Settings", DefaultSource = nameof(GetVarianceShadowMapParamsAttributeDefault))]
    private Vector2f SerializedVarianceShadowMapParams
    {
        get => _settings.SceneProcessor.VarianceShadowMapParams;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.VarianceShadowMapParams = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetVarianceShadowMapMultiSampleAttributeDefault() => 1;

    [Serialized("VSM Multi Sample", DefaultSource = nameof(GetVarianceShadowMapMultiSampleAttributeDefault))]
    private int SerializedVarianceShadowMapMultiSample
    {
        get => _settings.ShadowMapAllocator.VarianceShadowMapMultiSample;
        set
        {
            var settings = _settings.ShadowMapAllocator;
            settings.VarianceShadowMapMultiSample = value;
            _settings.ShadowMapAllocator = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetUse16BitShadowMapsAttributeDefault() => false;

    [Serialized("16-bit Shadow Maps", DefaultSource = nameof(GetUse16BitShadowMapsAttributeDefault))]
    private bool SerializedUse16BitShadowMaps
    {
        get => _settings.ShadowMapAllocator.Use16BitShadowMaps;
        set
        {
            var settings = _settings.ShadowMapAllocator;
            settings.Use16BitShadowMaps = value;
            _settings.ShadowMapAllocator = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetDrawDebugGeometryAttributeDefault() => true;

    [Serialized("Draw Debug Geometry", DefaultSource = nameof(GetDrawDebugGeometryAttributeDefault))]
    private bool SerializedDrawDebugGeometry
    {
        get => _settings.DrawDebugGeometry;
        set
        {
            _settings.DrawDebugGeometry = value;
            MarkSettingsDirty();
        }
    }

    private static object? GetDepthBiasScaleAttributeDefault() => 1.0f;

    [Serialized("Depth Bias Scale", DefaultSource = nameof(GetDepthBiasScaleAttributeDefault))]
    private float SerializedDepthBiasScale
    {
        get => _settings.ShadowMapAllocator.DepthBiasScale;
        set
        {
            var settings = _settings.ShadowMapAllocator;
            settings.DepthBiasScale = value;
            _settings.ShadowMapAllocator = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetDepthBiasOffsetAttributeDefault() => 0.0f;

    [Serialized("Depth Bias Offset", DefaultSource = nameof(GetDepthBiasOffsetAttributeDefault))]
    private float SerializedDepthBiasOffset
    {
        get => _settings.ShadowMapAllocator.DepthBiasOffset;
        set
        {
            var settings = _settings.ShadowMapAllocator;
            settings.DepthBiasOffset = value;
            _settings.ShadowMapAllocator = settings;
            MarkSettingsDirty();
        }
    }

    private static object? GetNormalOffsetScaleAttributeDefault() => 1.0f;

    [Serialized("Normal Offset Scale", DefaultSource = nameof(GetNormalOffsetScaleAttributeDefault))]
    private float SerializedNormalOffsetScale
    {
        get => _settings.SceneProcessor.NormalOffsetScale;
        set
        {
            var settings = _settings.SceneProcessor;
            settings.NormalOffsetScale = value;
            _settings.SceneProcessor = settings;
            MarkSettingsDirty();
        }
    }


    public ref readonly RenderPipelineSettings Settings => ref _settings;

    public void SetSettings(in RenderPipelineSettings settings)
    {
        _settings = settings;
        _settings.Validate();
        _settings.AdjustToSupported(Context.Instance);
        MarkSettingsDirty();
    }

    public ResourceRef GetRenderPathAttr() => ResourceType.GetResourceRef(_renderPath, new StringHash(nameof(RenderPath)));

    public void SetRenderPathAttr(ResourceRef value)
    {
        // This function may be called from constructor, check for cache just in case.
        if (GetSubsystem<ResourceCache>() is { } cache)
            SetRenderPath(cache.GetResourceOrNull<RenderPath>(value.Name));
    }

    public RenderPath? GetRenderPath() => _renderPath;

    public void SetRenderPath(RenderPath? renderPath)
    {
        if (_renderPath is not null)
            UnsubscribeFromEvent<ResourceEvents.ReloadFinished>(_renderPath);

        _renderPath = renderPath;
        OnRenderPathReloaded();

        if (_renderPath is not null)
            SubscribeToEvent<ResourceEvents.ReloadFinished>(_renderPath, OnRenderPathReloadFinished);
    }

    public IReadOnlyList<(string Name, bool Enabled)> GetRenderPasses() => _renderPasses;

    public void SetRenderPasses(IReadOnlyList<(string Name, bool Enabled)> renderPasses)
    {
        var sourcePasses = new (string Name, bool Enabled)[renderPasses.Count];
        for (var i = 0; i < renderPasses.Count; ++i)
            sourcePasses[i] = renderPasses[i];
        _renderPasses = _renderPath?.RepairEnabledRenderPasses(sourcePasses) ?? sourcePasses;
        ParametersChanged?.Invoke(this);
    }

    public VariantVector GetRenderPassesAttr()
    {
        var result = _renderPassesAttributeResult ??= new VariantVector();
        result.Clear();
        foreach (var pass in _renderPasses)
        {
            result.Add(new Variant(pass.Name));
            result.Add(new Variant(pass.Enabled));
        }
        return result;
    }

    public void SetRenderPassesAttr(VariantVector value)
    {
        var numPasses = value.Count / 2;
        var renderPasses = new (string Name, bool Enabled)[numPasses];
        for (var i = 0; i < numPasses; ++i)
            renderPasses[i] = (value[i * 2].GetString(), value[i * 2 + 1].GetBool());
        SetRenderPasses(renderPasses);
    }

    public StringVariantMap GetRenderPathParameters() => _renderPathParameters;

    public void SetRenderPathParameters(StringVariantMap parameters)
    {
        if (!ReferenceEquals(parameters, _renderPathParameters))
        {
            _renderPathParameters.Clear();
            foreach (var (name, value) in parameters)
                _renderPathParameters[name] = value;
        }
        ParametersChanged?.Invoke(this);
    }

    public StringVariantMap GetFrameShaderParameters() => _frameShaderParameters;

    public void SetFrameShaderParameters(StringVariantMap parameters)
    {
        if (ReferenceEquals(parameters, _frameShaderParameters))
            return;

        _frameShaderParameters.Clear();
        foreach (var (name, value) in parameters)
            _frameShaderParameters[name] = value;
    }

    /// <summary>
    /// Update existing render path parameters.
    /// </summary>
    public void UpdateRenderPathParameters(VariantMap parameters)
    {
        var modified = false;
        foreach (var (nameHash, newValue) in parameters)
        {
            foreach (var (name, currentValue) in _renderPathParameters)
            {
                if (StringHash.Calculate(name) != nameHash.Value)
                    continue;

                if (currentValue != newValue)
                {
                    _renderPathParameters[name] = newValue;
                    modified = true;
                }
                break;
            }
        }

        if (modified)
            ParametersChanged?.Invoke(this);
    }

    /// <summary>
    /// Update existing render path parameter.
    /// </summary>
    public void UpdateRenderPathParameter(StringHash nameHash, Variant value)
    {
        foreach (var (name, currentValue) in _renderPathParameters)
        {
            if (StringHash.Calculate(name) != nameHash.Value)
                continue;

            if (currentValue != value)
            {
                _renderPathParameters[name] = value;
                ParametersChanged?.Invoke(this);
            }
            return;
        }
    }

    /// <summary>
    /// Update render pass enabled state.
    /// </summary>
    public void SetRenderPassEnabled(string passName, bool enabled)
    {
        var index = -1;
        for (var i = 0; i < _renderPasses.Count; ++i)
        {
            if (_renderPasses[i].Name == passName)
            {
                index = i;
                break;
            }
        }

        var isUpdated = false;
        if (index < 0)
        {
            isUpdated = true;
            var updated = new (string Name, bool Enabled)[_renderPasses.Count + 1];
            for (var i = 0; i < _renderPasses.Count; ++i)
                updated[i] = _renderPasses[i];
            updated[^1] = (passName, enabled);
            _renderPasses = _renderPath?.RepairEnabledRenderPasses(updated) ?? updated;
        }
        else if (_renderPasses[index].Enabled != enabled)
        {
            isUpdated = true;
            var updated = new (string Name, bool Enabled)[_renderPasses.Count];
            for (var i = 0; i < _renderPasses.Count; ++i)
                updated[i] = _renderPasses[i];
            updated[index] = (updated[index].Name, enabled);
            _renderPasses = updated;
        }

        if (isUpdated)
            ParametersChanged?.Invoke(this);
    }

    /// <summary>
    /// Update existing global parameters.
    /// </summary>
    public void UpdateFrameShaderParameters(VariantMap parameters)
    {
        foreach (var (nameHash, newValue) in parameters)
        {
            foreach (var name in _frameShaderParameters.Keys)
            {
                if (StringHash.Calculate(name) != nameHash.Value)
                    continue;

                _frameShaderParameters[name] = newValue;
                break;
            }
        }
    }

    /// <summary>
    /// Update existing render path parameter.
    /// </summary>
    public void UpdateFrameShaderParameter(StringHash nameHash, Variant value)
    {
        foreach (var name in _frameShaderParameters.Keys)
        {
            if (StringHash.Calculate(name) != nameHash.Value)
                continue;

            _frameShaderParameters[name] = value;
            return;
        }
    }

    /// <summary>
    /// Add or update render path parameter.
    /// </summary>
    public void SetFrameShaderParameter(string name, Variant value)
    {
        _frameShaderParameters[name] = value;
    }

    /// <summary>
    /// Create new instance of render pipeline.
    /// </summary>
    public virtual RenderPipelineView Instantiate() => new DefaultRenderPipelineView(this);

    private void MarkSettingsDirty()
    {
        _settings.Validate();
        _settings.AdjustToSupported(Context.Instance);
        SettingsChanged?.Invoke(this, _settings);
    }

    private void OnRenderPathReloaded()
    {
        if (_renderPath is not null)
        {
            _renderPath.CollectParameters(_renderPathParameters);
            _renderPasses = _renderPath.RepairEnabledRenderPasses(_renderPasses);
        }
        RenderPathChanged?.Invoke(this, _renderPath);
    }

    private void OnRenderPathReloadFinished(ref ResourceEvents.ReloadFinished eventData)
    {
        OnRenderPathReloaded();
    }
}
