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
    private string passName = string.Empty;
    private bool enabledByDefault = true;
    private string comment = string.Empty;
    private bool isEnabledByUser;
    private bool isEnabledInternally = true;
    private RenderPassTraits traits;

    protected RenderPass(string? passName = null)
    {
        this.passName = passName ?? string.Empty;
        isEnabledByUser = enabledByDefault;

        RegisterAttribute(
            "Pass Name",
            typeof(string),
            self => ((RenderPass)self).passName,
            (self, value) => ((RenderPass)self).passName = value as string ?? string.Empty,
            string.Empty);
        RegisterAttribute(
            "Is Enabled By Default",
            typeof(bool),
            self => ((RenderPass)self).enabledByDefault,
            (self, value) => ((RenderPass)self).enabledByDefault = value is bool enabled && enabled,
            true);
        RegisterAttribute(
            "Comment",
            typeof(string),
            self => ((RenderPass)self).comment,
            (self, value) => ((RenderPass)self).comment = value as string ?? string.Empty,
            string.Empty);
    }

    /// <summary>
    /// Return unique pass name.
    /// </summary>
    public virtual string GetPassName() => string.IsNullOrWhiteSpace(passName) ? GetType().Name : passName;

    /// <summary>
    /// Create missing parameters in the global map with default values.
    /// </summary>
    public virtual void CollectParameters(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
    }

    /// <summary>
    /// Initialize render pass before using it in view.
    /// </summary>
    public virtual void InitializeView(RenderPipelineView view)
    {
        ArgumentNullException.ThrowIfNull(view);
    }

    /// <summary>
    /// Update settings and parameters of the pass.
    /// This function is always called before any rendering updates or getters.
    /// </summary>
    public virtual void UpdateParameters(RenderPipelineSettings settings, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(parameters);
    }

    /// <summary>
    /// Perform update that does not invoke any rendering commands.
    /// </summary>
    public virtual void Update(SharedRenderPassState sharedState)
    {
        ArgumentNullException.ThrowIfNull(sharedState);
    }

    /// <summary>
    /// Execute render commands.
    /// </summary>
    public virtual void Render(SharedRenderPassState sharedState)
    {
        ArgumentNullException.ThrowIfNull(sharedState);
    }

    public abstract RenderPass Clone();

    public void SetEnabled(bool enabled)
    {
        isEnabledByUser = enabled;
    }

    public bool IsEnabledEffectively() => isEnabledByUser && isEnabledInternally;

    public RenderPassTraits GetTraits() => traits;

    public bool IsEnabledByDefault() => enabledByDefault;

    public void SetEnabledByDefault(bool enabled)
    {
        enabledByDefault = enabled;
        isEnabledByUser = enabled;
    }

    public string GetComment() => comment;

    public void SetComment(string value)
    {
        comment = value ?? string.Empty;
    }

    protected void SetTraits(RenderPassTraits value)
    {
        traits = value;
    }

    protected void SetInternallyEnabled(bool enabled)
    {
        isEnabledInternally = enabled;
    }

    protected void DeclareParameter(string name, object? value, IDictionary<string, object?> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!parameters.ContainsKey(name))
            parameters[name] = value;
    }

    protected object? LoadParameter(string name, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters.TryGetValue(name, out var value) ? value : null;
    }

    protected bool ConnectToRenderBuffer(ref RenderBuffer? renderBuffer, StringHash name, SharedRenderPassState sharedState, bool required = true)
    {
        ArgumentNullException.ThrowIfNull(sharedState);

        if (sharedState.RenderBuffers.TryGetValue(name, out var value))
        {
            if (!ReferenceEquals(renderBuffer, value))
                renderBuffer = value;
            return true;
        }

        renderBuffer = null;

        if (required)
            Log.Logger.LogWarning("Render buffer '{RenderBuffer}' required by render pass '{RenderPass}' was not found.", name, GetPassName());

        return false;
    }
}
