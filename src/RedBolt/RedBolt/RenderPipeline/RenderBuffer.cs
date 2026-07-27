using Microsoft.Extensions.Logging;
using System.Diagnostics;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;

namespace RedBolt.RenderPipeline;

/// <summary>
/// Base class for writable texture or texture region. Readability is not guaranteed.
/// </summary>
public abstract class RenderBuffer : RedObject
{
    protected readonly RenderDevice RenderDevice;
    protected bool BufferIsReady;
    protected bool IsEnabledValue = true;

    protected RenderBuffer(IRenderPipelineInterface renderPipeline)
    {
        RenderDevice = GetSubsystem<RenderDevice>()!;
        renderPipeline.RenderBegin += OnRenderBegin;
        renderPipeline.RenderEnd += OnRenderEnd;
        RenderPipeline = renderPipeline;
    }

    private IRenderPipelineInterface RenderPipeline { get; }

    /// <summary>
    /// Return readable texture. May return null if not supported.
    /// </summary>
    public abstract RawTexture? GetTexture();

    /// <summary>
    /// Return render surface. Face could be specified for cubemap texture.
    /// </summary>
    public abstract RenderTargetView GetView(int slice = 0);

    public abstract RenderTargetView GetReadOnlyDepthView(int slice = 0);

    /// <summary>
    /// Return effective viewport rectangle.
    /// Always equal to whole texture for TextureRenderBuffer, not so for viewport buffers.
    /// </summary>
    public abstract IntRect GetViewportRect();

    /// <summary>
    /// Set whether the render buffer is enabled. Disabled buffers should not be accessed during the frame.
    /// </summary>
    public void SetEnabled(bool enabled) => IsEnabledValue = enabled;

    public bool IsEnabled => IsEnabledValue;

    protected bool CheckIfBufferIsReady()
    {
        if (!BufferIsReady)
        {
            Log.GetLogger().LogError("RenderBuffer is not available");
            return false;
        }
        return true;
    }

    protected abstract void OnRenderBegin(CommonFrameInfo frameInfo);
    protected abstract void OnRenderEnd(CommonFrameInfo frameInfo);

    internal void BeginCurrentFrame(CommonFrameInfo frameInfo) => OnRenderBegin(frameInfo);

    public override void Dispose()
    {
        RenderPipeline.RenderEnd -= OnRenderEnd;
        RenderPipeline.RenderBegin -= OnRenderBegin;
        base.Dispose();
    }
}

/// <summary>
/// Writable and readable render buffer texture (2D or cubemap).
/// </summary>
public sealed class TextureRenderBuffer : RenderBuffer
{
    private readonly RenderBufferParams _params;
    private Vector2f _sizeMultiplier = Vector2f.One;
    private Vector2i _fixedSize;
    private Vector2i _currentSize;
    private RawTexture? _currentTexture;
    private GCHandle _persistenceHandle;

    public TextureRenderBuffer(IRenderPipelineInterface renderPipeline, RenderBufferParams parameters)
        : this(renderPipeline, parameters, Vector2f.One)
    {
    }

    public TextureRenderBuffer(IRenderPipelineInterface renderPipeline, RenderBufferParams parameters, Vector2f size)
        : base(renderPipeline)
    {
        _params = parameters;
        if ((_params.Flags & RenderBufferFlag.FixedTextureSize) != 0)
            _fixedSize = new Vector2i((int)MathF.Round(size.X), (int)MathF.Round(size.Y));
        else
            _sizeMultiplier = size;

        if ((_params.Flags & RenderBufferFlag.Persistent) != 0)
            _persistenceHandle = GCHandle.Alloc(this, GCHandleType.Weak);
    }

    protected override void OnRenderBegin(CommonFrameInfo frameInfo)
    {
        if (!IsEnabledValue)
            return;

        var renderPool = RenderDevice.GetRenderPool();
        _currentSize = CalculateRenderTargetSize(frameInfo.ViewportRect, _sizeMultiplier, _fixedSize);

        var noAutoResolve = (_params.Flags & RenderBufferFlag.NoMultiSampledAutoResolve) != 0;
        var isCubemap = (_params.Flags & RenderBufferFlag.CubeMap) != 0;
        var isFiltered = (_params.Flags & RenderBufferFlag.BilinearFiltering) != 0;
        var isPersistent = (_params.Flags & RenderBufferFlag.Persistent) != 0;
        var isDepthStencil = RenderAPIUtils.IsDepthTextureFormat(_params.TextureFormat);

        var flags = isDepthStencil ? TextureFlag.BindDepthStencil : TextureFlag.BindRenderTarget;
        if (noAutoResolve)
            flags |= TextureFlag.NoMultiSampledAutoResolve;

        var parameters = new RawTextureParams(
            isCubemap ? TextureType.TextureCube : TextureType.Texture2D,
            _params.TextureFormat,
            flags,
            new Vector3i(_currentSize.X, _currentSize.Y, 1),
            NumLevels: 1,
            MultiSample: _params.MultiSampleLevel);

        var persistenceKey = isPersistent ? GCHandle.ToIntPtr(_persistenceHandle) : nint.Zero;
        _currentTexture = renderPool.GetTexture(parameters, persistenceKey);
        _currentTexture.SetSamplerStateDesc(isFiltered ? SamplerStateDesc.Bilinear() : SamplerStateDesc.Nearest());
        BufferIsReady = true;
    }

    protected override void OnRenderEnd(CommonFrameInfo frameInfo)
    {
        _currentTexture = null;
        BufferIsReady = false;
    }

    public override RawTexture? GetTexture()
    {
        Debug.Assert(CheckIfBufferIsReady());
        return _currentTexture;
    }

    public override RenderTargetView GetView(int slice = 0)
    {
        Debug.Assert(CheckIfBufferIsReady());
        return RenderTargetView.TextureSlice(_currentTexture!, slice);
    }

    public override RenderTargetView GetReadOnlyDepthView(int slice = 0)
    {
        Debug.Assert(CheckIfBufferIsReady());
        return RenderTargetView.ReadOnlyDepthSlice(_currentTexture!, slice);
    }

    public override IntRect GetViewportRect()
    {
        Debug.Assert(CheckIfBufferIsReady());
        return new IntRect(0, 0, _currentSize.X, _currentSize.Y);
    }

    public override void Dispose()
    {
        _currentTexture = null;
        if (_persistenceHandle.IsAllocated)
            _persistenceHandle.Free();
        base.Dispose();
    }

    private static Vector2i CalculateRenderTargetSize(IntRect viewportRect, Vector2f sizeMultiplier, Vector2i explicitSize)
    {
        if (explicitSize != Vector2i.Zero)
            return explicitSize;
        var viewportSize = viewportRect.Size;
        return new Vector2i(
            SystemMath.Max(1, (int)MathF.Round(viewportSize.X * sizeMultiplier.X)),
            SystemMath.Max(1, (int)MathF.Round(viewportSize.Y * sizeMultiplier.Y)));
    }
}
