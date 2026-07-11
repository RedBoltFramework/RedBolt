using RedBolt.Bindings.Diligent;

namespace RedBolt;

/// <summary>
/// Render context that consumes render commands.
/// </summary>
public class RenderContext : Object
{
    private static readonly RenderTargetView[] SwapChainColorTarget = [RenderTargetView.SwapChainColor()];

    private readonly RenderDevice _renderDevice;
    private readonly RenderPool _renderPool;
    private readonly RenderDeviceStats _stats = new();
    private RenderTargetView[] _currentRenderTargets = [];
    private readonly RenderTargetView[] _singleRenderTarget = new RenderTargetView[1];
    private OptionalRawTextureRTV _currentDepthStencil;
    private uint _clipPlaneMask;

    public RenderContext(RenderDevice renderDevice)
    {
        this._renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
        _renderPool = renderDevice.GetRenderPool();
    }

    /// <summary>
    /// Reset statistics.
    /// </summary>
    public void ResetStats()
    {
        _stats.NumPrimitives = 0;
        _stats.NumDraws = 0;
        _stats.NumDispatches = 0;
    }

    /// <summary>
    /// Reset render targets and depth-stencil buffer.
    /// </summary>
    public void ResetRenderTargets()
    {
        GetGraphics()?.UnbindRenderTargets();
        _currentRenderTargets = [];
        _currentDepthStencil = default;
        UpdateCurrentRenderTargetInfo();
    }

    /// <summary>
    /// Set backbuffer render target and associated depth-stencil buffer.
    /// </summary>
    public void SetSwapChainRenderTargets()
    {
        var graphics = GetGraphics();
        if (graphics is null)
            return;

        var size = _renderDevice.GetSwapChainSize();
        graphics.SetSwapChainRenderTargets(new IntRect(0, 0, size.X, size.Y));
        _currentRenderTargets = SwapChainColorTarget;
        _currentDepthStencil = RenderTargetView.SwapChainDepthStencil();
        UpdateCurrentRenderTargetInfo();
    }

    /// <summary>
    /// Set specified render targets and depth-stencil buffer. Either can be null.
    /// </summary>
    public void SetRenderTargets(OptionalRawTextureRTV depthStencil, ReadOnlySpan<RenderTargetView> renderTargets)
    {
        var graphics = GetGraphics();
        if (graphics is null)
            return;

        if (renderTargets.Length > 1)
            throw new NotSupportedException("The current managed renderer supports only one bound color render target in this slice.");

        var colorTarget = renderTargets.Length > 0 ? renderTargets[0] : (RenderTargetView?)null;
        var viewportSize = ResolveRenderTargetSize(colorTarget, depthStencil);
        graphics.SetRenderTargets(new IntRect(0, 0, viewportSize.X, viewportSize.Y), colorTarget, depthStencil);
        if (renderTargets.Length == 0)
            _currentRenderTargets = [];
        else
        {
            _singleRenderTarget[0] = renderTargets[0];
            _currentRenderTargets = _singleRenderTarget;
        }
        _currentDepthStencil = depthStencil;
        UpdateCurrentRenderTargetInfo();
    }

    /// <summary>
    /// Set viewport that covers the entirety of currently bound render target.
    /// </summary>
    public void SetFullViewport()
    {
        var size = GetCurrentRenderTargetSize();
        if (size.X <= 0 || size.Y <= 0)
            return;

        SetViewport(new IntRect(0, 0, size.X, size.Y));
    }

    /// <summary>
    /// Set viewport that covers the specified area of currently bound render targets.
    /// </summary>
    public void SetViewport(IntRect viewport)
    {
        GetGraphics()?.SetViewport(viewport);
    }

    /// <summary>
    /// Clear depth-stencil buffer. Viewport and scissor are ignored.
    /// </summary>
    public void ClearDepthStencil(ClearTarget flags, float depth = 1.0f, uint stencil = 0)
    {
        GetGraphics()?.ClearCurrentRenderTargets(
            color: null,
            clearColorTarget: false,
            clearDepth: (flags & ClearTarget.Depth) != 0,
            clearStencil: (flags & ClearTarget.Stencil) != 0,
            depth: depth,
            stencil: checked((byte)stencil));
    }

    /// <summary>
    /// Clear render target at given index. Viewport and scissor are ignored.
    /// </summary>
    public void ClearRenderTarget(uint index, Color color)
    {
        if (index >= _currentRenderTargets.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index != 0)
            throw new NotSupportedException("The current managed renderer supports only one bound color render target in this slice.");

        GetGraphics()?.ClearCurrentRenderTargets(
            color,
            clearColorTarget: true,
            clearDepth: false,
            clearStencil: false,
            depth: 1.0f,
            stencil: 0);
    }

    /// <summary>
    /// Set whether the clip plane is enabled. Only used for OpenGL.
    /// </summary>
    public void SetClipPlaneMask(uint mask)
    {
        _clipPlaneMask = mask;
    }

    /// <summary>
    /// Execute draw commands from queue.
    /// </summary>
    public void Execute(DrawCommandQueue drawQueue)
    {
        ArgumentNullException.ThrowIfNull(drawQueue);
        drawQueue.ExecuteInContext(this);
    }

    /// <summary>
    /// Check if the texture is already bound as a render target.
    /// </summary>
    public bool IsBoundAsRenderTarget(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (_currentDepthStencil.GetValueOrDefault()?.RenderTexture == texture)
            return true;

        foreach (var view in _currentRenderTargets)
        {
            if (view.RenderTexture == texture)
                return true;
        }

        return false;
    }

    public IDeviceContext? GetHandle() => GetGraphics()?.GetImmediateContext();

    public RenderDeviceStats GetStats() => _stats;

    public RenderDeviceStats GetMutableStats() => _stats;

    public RenderDevice GetRenderDevice() => _renderDevice;

    public RenderPool GetRenderPool() => _renderPool;

    public bool IsDebugScopeEnabled() => true;

    public Vector2i GetCurrentRenderTargetSize()
    {
        UpdateCurrentRenderTargetInfo();
        return GetGraphics()?.GetCurrentRenderTargetSize() ?? default;
    }

    public PipelineStateOutputDesc GetCurrentRenderTargetsDesc()
    {
        UpdateCurrentRenderTargetInfo();
        return GetGraphics()?.GetCurrentRenderTargetsDesc() ?? new PipelineStateOutputDesc();
    }

    public bool IsSwapChainRenderTarget()
    {
        UpdateCurrentRenderTargetInfo();
        return GetGraphics()?.IsSwapChainRenderTarget() ?? false;
    }

    public IntRect GetCurrentViewport()
    {
        UpdateCurrentRenderTargetInfo();
        return GetGraphics()?.GetCurrentViewportRect() ?? default;
    }

    private void UpdateCurrentRenderTargetInfo()
    {
        if (GetGraphics() is null)
            return;
    }

    private Graphics? GetGraphics() => GetSubsystem<Graphics>();

    private Vector2i ResolveRenderTargetSize(RenderTargetView? colorTarget, OptionalRawTextureRTV depthStencil)
    {
        var depthStencilView = depthStencil.GetValueOrDefault();
        if (colorTarget?.IsSwapChain == true || depthStencilView?.IsSwapChain == true)
            return _renderDevice.GetSwapChainSize();
        if (colorTarget?.RenderTexture is { } colorTexture)
            return new Vector2i(colorTexture.Width, colorTexture.Height);
        if (depthStencilView?.RenderTexture is { } depthTexture)
            return new Vector2i(depthTexture.Width, depthTexture.Height);
        return _renderDevice.GetSwapChainSize();
    }
}
