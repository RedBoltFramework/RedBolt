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
    private readonly RenderTargetView[] _currentRenderTargets = new RenderTargetView[RenderApiLimits.MaxRenderTargets];
    private int _numCurrentRenderTargets;
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
        Array.Clear(_currentRenderTargets, 0, _numCurrentRenderTargets);
        _numCurrentRenderTargets = 0;
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
        _currentRenderTargets[0] = SwapChainColorTarget[0];
        _numCurrentRenderTargets = 1;
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

        if (renderTargets.Length > RenderApiLimits.MaxRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(renderTargets));
        var isDepthSwapChain = depthStencil.GetValueOrDefault()?.IsSwapChain == true;
        var isColorSwapChain = renderTargets.Length == 1 && renderTargets[0].IsSwapChain;
        var isSingleView = renderTargets.Length + (depthStencil.HasValue ? 1 : 0) == 1;
        if (isDepthSwapChain != isColorSwapChain && !isSingleView)
            throw new InvalidOperationException("Cannot mix swap chain and non-swap-chain render target views.");

        var viewportSize = ResolveRenderTargetSize(renderTargets, depthStencil);
        graphics.SetRenderTargets(new IntRect(0, 0, viewportSize.X, viewportSize.Y), renderTargets, depthStencil.GetValueOrDefault());
        Array.Clear(_currentRenderTargets, 0, _numCurrentRenderTargets);
        renderTargets.CopyTo(_currentRenderTargets);
        _numCurrentRenderTargets = renderTargets.Length;
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
        if (index >= _numCurrentRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(index));

        GetGraphics()?.ClearRenderTarget(_currentRenderTargets[index], color);
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

        for (var i = 0; i < _numCurrentRenderTargets; ++i)
        {
            var view = _currentRenderTargets[i];
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

    private Vector2i ResolveRenderTargetSize(ReadOnlySpan<RenderTargetView> colorTargets, OptionalRawTextureRTV depthStencil)
    {
        var depthStencilView = depthStencil.GetValueOrDefault();
        if ((colorTargets.Length != 0 && colorTargets[0].IsSwapChain) || depthStencilView?.IsSwapChain == true)
            return _renderDevice.GetSwapChainSize();
        if (colorTargets.Length != 0 && colorTargets[0].RenderTexture is { } colorTexture)
            return new Vector2i(colorTexture.Width, colorTexture.Height);
        if (depthStencilView?.RenderTexture is { } depthTexture)
            return new Vector2i(depthTexture.Width, depthTexture.Height);
        return _renderDevice.GetSwapChainSize();
    }
}
