using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Resource;
using RedBolt.Scene;
namespace RedBolt.RenderPipeline;
using Resource = global::RedBolt.Resource.Resource;
using Scene = global::RedBolt.Scene.Scene;


/// <summary>
/// Scene component that spawns render pipeline instances.
/// </summary>
public class RenderPipeline : Component
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

    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<RenderPipeline>();
    }

    public ref readonly RenderPipelineSettings Settings => ref _settings;

    public void SetSettings(in RenderPipelineSettings settings)
    {
        _settings = settings;
        _settings.Validate();
        _settings.AdjustToSupported(Context.Instance);
        MarkSettingsDirty();
    }

    public ResourceRef GetRenderPathAttr() => Resource.GetResourceRef(_renderPath, new StringHash(nameof(RenderPath)));

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
