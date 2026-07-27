using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Scene;
namespace RedBolt.RenderPipeline;

/// <summary>
/// Render pass traits that are important for render pipeline configuration.
/// </summary>
/// <param name="NeedReadWriteColorBuffer">Whether it's required to read from and write to color buffer at the same time.</param>
/// <param name="NeedBilinearColorSampler">Whether it's required that color sampling is at least bilinear.</param>
public readonly record struct RenderPassTraits(
    bool NeedReadWriteColorBuffer,
    bool NeedBilinearColorSampler);

/// <summary>
/// Render pass, component of render path.
/// </summary>
public abstract class RenderPass : Serializable
{
    protected string _passName = string.Empty;
    protected bool _enabledByDefault = true;
    protected string _comment = string.Empty;
    protected bool _isEnabledByUser;
    protected bool _isEnabledInternally = true;
    protected RenderPassTraits _traits;

    public RenderPass()
    {
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

    public override void Dispose()
    {
        base.Dispose();
    }

    public static void RegisterObject(Context context)
    {
        context.AddAbstractReflection<RenderPass>();
    }

    /// <summary>
    /// Return unique pass name.
    /// </summary>
    public virtual string GetPassName() => string.IsNullOrEmpty(_passName) ? GetTypeName() : _passName;

    /// <summary>
    /// Create missing parameters in the global map with default values.
    /// </summary>
    public virtual void CollectParameters(StringVariantMap parameters)
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
    public virtual void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
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

    public virtual RenderPass? Clone() => base.Clone() as RenderPass;

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
    }

    public string GetComment() => _comment;

    public void SetComment(string value)
    {
        _comment = value;
    }

    protected void DeclareParameter(string name, Variant value, StringVariantMap parameters)
    {
        parameters.TryAdd(name, value);
    }

    protected ref readonly Variant LoadParameter(string name, StringVariantMap parameters)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(parameters, name);
        if (!Unsafe.IsNullRef(ref value))
            return ref value;
        return ref Variant.Empty;
    }

    protected void ConnectToRenderBuffer(ref WeakReference<RenderBuffer>? renderBuffer, StringHash name,
        SharedRenderPassState sharedState, bool required = true)
    {
        if (renderBuffer is null || !renderBuffer.TryGetTarget(out _))
        {
            if (!sharedState.RenderBuffers.TryGetValue(name, out var value) || value is null)
            {
                if (required)
                {
                    Log.GetLogger().LogError(
                        "Render buffer '{RenderBuffer}' required by render pass '{RenderPass}' is not found.",
                        name,
                        GetPassName());
                }
                return;
            }

            renderBuffer = new WeakReference<RenderBuffer>(value);
        }
    }
}
