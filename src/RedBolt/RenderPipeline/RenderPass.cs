using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Render pass traits that are important for render pipeline configuration.
/// </summary>
/// <param name="NeedReadWriteColorBuffer">Whether it's required to read from and write to color buffer at the same time.</param>
/// <param name="NeedBilinearColorSampler">Whether it's required that color sampling is at least bilinear.</param>
public readonly record struct RenderPassTraits(
    bool NeedReadWriteColorBuffer,
    bool NeedBilinearColorSampler,
    bool NeedReadableDepth = false);

/// <summary>
/// Render pass, component of render path.
/// </summary>
public abstract class RenderPass : Serializable
{
    private string _passName = string.Empty;
    private bool _enabledByDefault = true;
    private string _comment = string.Empty;
    private bool _isEnabledByUser;
    private bool _isEnabledInternally = true;
    private RenderPassTraits _traits;

    protected RenderPass(string? passName = null)
    {
        this._passName = passName ?? string.Empty;
        _isEnabledByUser = _enabledByDefault;

        RegisterAttribute(
            "Pass Name",
            typeof(string),
            self => ((RenderPass)self)._passName,
            (self, value) => ((RenderPass)self)._passName = value as string ?? string.Empty,
            string.Empty);
        RegisterAttribute(
            "Is Enabled By Default",
            typeof(bool),
            self => ((RenderPass)self)._enabledByDefault,
            (self, value) => ((RenderPass)self)._enabledByDefault = value is bool enabled && enabled,
            true);
        RegisterAttribute(
            "Comment",
            typeof(string),
            self => ((RenderPass)self)._comment,
            (self, value) => ((RenderPass)self)._comment = value as string ?? string.Empty,
            string.Empty);
    }

    /// <summary>
    /// Return unique pass name.
    /// </summary>
    public virtual string GetPassName() => string.IsNullOrEmpty(_passName) ? GetType().Name : _passName;

    /// <summary>
    /// Create missing parameters in the global map with default values.
    /// </summary>
    public virtual void CollectParameters(IDictionary<string, object?> parameters)
    {
    }

    /// <summary>
    /// Initialize render pass before using it in view.
    /// </summary>
    public virtual void InitializeView(RenderPipelineView view)
    {
    }

    /// <summary>
    /// Update settings and parameters of the pass.
    /// This function is always called before any rendering updates or getters.
    /// </summary>
    public virtual void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
    {
    }

    /// <summary>
    /// Perform update that does not invoke any rendering commands.
    /// </summary>
    public virtual void Update(SharedRenderPassState sharedState)
    {
    }

    /// <summary>
    /// Execute render commands.
    /// </summary>
    public virtual void Render(SharedRenderPassState sharedState)
    {
    }

    public abstract RenderPass Clone();

    public void SetEnabled(bool enabled)
    {
        _isEnabledByUser = enabled;
    }

    public bool IsEnabledEffectively() => _isEnabledByUser && _isEnabledInternally;

    public RenderPassTraits GetTraits() => _traits;

    public bool IsEnabledByDefault() => _enabledByDefault;

    public void SetEnabledByDefault(bool enabled)
    {
        _enabledByDefault = enabled;
        _isEnabledByUser = enabled;
    }

    public string GetComment() => _comment;

    public void SetComment(string value)
    {
        _comment = value ?? string.Empty;
    }

    protected void SetTraits(RenderPassTraits value)
    {
        _traits = value;
    }

    protected void SetInternallyEnabled(bool enabled)
    {
        _isEnabledInternally = enabled;
    }

    protected void DeclareParameter(string name, object? value, IDictionary<string, object?> parameters)
    {
        if (!parameters.ContainsKey(name))
            parameters[name] = value;
    }

    protected object? LoadParameter(string name, IReadOnlyDictionary<string, object?> parameters)
    {
        return parameters.TryGetValue(name, out var value) ? value : null;
    }

    protected bool ConnectToRenderBuffer(ref RenderBuffer? renderBuffer, StringHash name, SharedRenderPassState sharedState, bool required = true)
    {
        if (renderBuffer is null)
        {
            if (!sharedState.RenderBuffers.TryGetValue(name, out var value))
            {
                if (required)
                {
                    Log.Logger.LogError("Render buffer '{RenderBuffer}' required by render pass '{RenderPass}' was not found.", name, GetPassName());
                }
                return false;
            }

            renderBuffer = value;
        }

        return true;
    }
}
