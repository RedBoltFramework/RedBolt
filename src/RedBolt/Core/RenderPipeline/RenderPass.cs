// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderPipeline;

using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Scene;

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
[Reflected]
public abstract partial class RenderPass : Serializable
{
    [Serialized("Pass Name")]
    protected string _passName = string.Empty;
    [Serialized("Is Enabled By Default")]
    protected bool _enabledByDefault = true;
    [Serialized("Comment")]
    protected string _comment = string.Empty;
    protected bool _isEnabledByUser;
    protected bool _isEnabledInternally = true;
    protected RenderPassTraits _traits;

    public RenderPass()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
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
