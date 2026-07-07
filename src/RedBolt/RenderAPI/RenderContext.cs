using RedBolt.Bindings.Diligent;

namespace RedBolt;

using OptionalRawTextureRTV = RenderTargetView?;

public class RenderContext : Object
{
    private readonly RenderDevice renderDevice;
    private readonly RenderPool renderPool;
    private readonly RenderDeviceStats stats = new();
    private RenderTargetView[] currentRenderTargets = [];
    private OptionalRawTextureRTV currentDepthStencil;
    private uint clipPlaneMask;

    public RenderContext(RenderDevice renderDevice)
    {
        this.renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
        renderPool = renderDevice.GetRenderPool();
    }

    public void ResetStats()
    {
        stats.NumPrimitives = 0;
        stats.NumDraws = 0;
        stats.NumDispatches = 0;
    }

    public void ResetRenderTargets()
    {
        GetGraphics()?.UnbindRenderTargets();
        currentRenderTargets = [];
        currentDepthStencil = null;
        UpdateCurrentRenderTargetInfo();
    }

    public void SetSwapChainRenderTargets()
    {
        var graphics = GetGraphics();
        if (graphics is null)
            return;

        var size = renderDevice.GetSwapChainSize();
        graphics.SetSwapChainRenderTargets(new IntRect(0, 0, size.X, size.Y));
        currentRenderTargets = [RenderTargetView.SwapChainColor()];
        currentDepthStencil = RenderTargetView.SwapChainDepthStencil();
        UpdateCurrentRenderTargetInfo();
    }

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
        currentRenderTargets = renderTargets.ToArray();
        currentDepthStencil = depthStencil;
        UpdateCurrentRenderTargetInfo();
    }

    public void SetFullViewport()
    {
        var size = GetCurrentRenderTargetSize();
        if (size.X <= 0 || size.Y <= 0)
            return;

        SetViewport(new IntRect(0, 0, size.X, size.Y));
    }

    public void SetViewport(IntRect viewport)
    {
        GetGraphics()?.SetViewport(viewport);
    }

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

    public void ClearRenderTarget(uint index, Color color)
    {
        if (index >= currentRenderTargets.Length)
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

    public void SetClipPlaneMask(uint mask)
    {
        clipPlaneMask = mask;
    }

    public void Execute(DrawCommandQueue drawQueue)
    {
        ArgumentNullException.ThrowIfNull(drawQueue);
        drawQueue.ExecuteInContext(this);
    }

    public bool IsBoundAsRenderTarget(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (currentDepthStencil?.RenderTexture == texture)
            return true;

        foreach (var view in currentRenderTargets)
        {
            if (view.RenderTexture == texture)
                return true;
        }

        return false;
    }

    public IDeviceContext? GetHandle() => GetGraphics()?.GetImmediateContext();

    public RenderDeviceStats GetStats() => stats;

    public RenderDeviceStats GetMutableStats() => stats;

    public RenderDevice GetRenderDevice() => renderDevice;

    public RenderPool GetRenderPool() => renderPool;

    public bool IsDebugScopeEnabled() => false;

    public IntVector2 GetCurrentRenderTargetSize()
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

    internal void BeginGroup(string name)
    {
        _ = name;
    }

    internal void EndGroup()
    {
    }

    private void UpdateCurrentRenderTargetInfo()
    {
        if (GetGraphics() is null)
            return;
    }

    private Graphics? GetGraphics() => GetSubsystem<Graphics>();

    private IntVector2 ResolveRenderTargetSize(RenderTargetView? colorTarget, OptionalRawTextureRTV depthStencil)
    {
        if (colorTarget?.IsSwapChain == true || depthStencil?.IsSwapChain == true)
            return renderDevice.GetSwapChainSize();
        if (colorTarget?.RenderTexture is { } colorTexture)
            return new IntVector2(colorTexture.Width, colorTexture.Height);
        if (depthStencil?.RenderTexture is { } depthTexture)
            return new IntVector2(depthTexture.Width, depthTexture.Height);
        return renderDevice.GetSwapChainSize();
    }
}
