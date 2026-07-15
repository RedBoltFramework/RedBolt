namespace RedBolt;

/// <summary>
/// Write-only viewport depth-stencil texture.
/// </summary>
public sealed class ViewportDepthStencilRenderBuffer : RenderBuffer
{
    private RenderTargetView? _depthStencil;
    private Texture? _texture;
    private RenderDevice? _renderDevice;
    private IntRect _viewportRect;

    public void SetOutput(RenderTargetView? depthStencil, Texture? texture, RenderDevice? renderDevice, IntRect viewportRect)
    {
        _depthStencil = depthStencil;
        _texture = texture;
        _renderDevice = renderDevice;
        _viewportRect = viewportRect;
        _bufferIsReady = true;
    }

    public override void OnRenderEnd(CommonFrameInfo frameInfo)
    {
        _bufferIsReady = false;
    }

    public override Texture? GetTexture()
    {
        CheckIfBufferIsReady();
        return _depthStencil is not null ? _texture : null;
    }

    public override RenderTargetView GetView(uint slice = 0u)
    {
        CheckIfBufferIsReady();
        return _depthStencil ?? RenderTargetView.SwapChainDepthStencil(_renderDevice);
    }

    public override RenderTargetView GetReadOnlyDepthView(uint slice = 0u)
    {
        CheckIfBufferIsReady();
        return _depthStencil ?? RenderTargetView.SwapChainDepthStencil(_renderDevice);
    }

    public override IntRect GetViewportRect() => CheckIfBufferIsReady() ? _viewportRect : IntRect.Zero;
}
