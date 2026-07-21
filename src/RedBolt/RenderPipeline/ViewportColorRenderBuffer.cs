using System.Diagnostics;

namespace RedBolt;

/// <summary>
/// Write-only viewport color render buffer.
/// </summary>
public sealed class ViewportColorRenderBuffer : RenderBuffer
{
    private RenderTargetView? _renderTarget;
    private Texture? _texture;
    private RenderDevice? _renderDevice;
    private IntRect _viewportRect;

    public void SetOutput(RenderTargetView? renderTarget, Texture? texture, RenderDevice? renderDevice, IntRect viewportRect)
    {
        _renderTarget = renderTarget;
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
        return _renderTarget is not null ? _texture : null;
    }

    public override RenderTargetView GetView(int slice = 0)
    {
        CheckIfBufferIsReady();
        return _renderTarget ?? RenderTargetView.SwapChainColor(_renderDevice!);
    }

    public override RenderTargetView GetReadOnlyDepthView(int slice = 0)
    {
        Debug.Assert(false);
        return GetView(slice);
    }

    public override IntRect GetViewportRect() => CheckIfBufferIsReady() ? _viewportRect : IntRect.Zero;
}
