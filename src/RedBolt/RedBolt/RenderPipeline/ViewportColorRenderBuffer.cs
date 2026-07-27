using System.Diagnostics;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
namespace RedBolt.RenderPipeline;

/// <summary>Write-only viewport color render buffer.</summary>
public sealed class ViewportColorRenderBuffer : RenderBuffer
{
    private IntRect _viewportRect;
    private RenderSurface? _renderTarget;

    public ViewportColorRenderBuffer(IRenderPipelineInterface renderPipeline)
        : base(renderPipeline)
    {
    }

    public override RawTexture? GetTexture()
    {
        Debug.Assert(CheckIfBufferIsReady());
        return _renderTarget?.GetParentTexture()?.GetRawTexture();
    }

    public override RenderTargetView GetView(int slice = 0)
    {
        Debug.Assert(CheckIfBufferIsReady());
        return _renderTarget?.GetView() ?? RenderTargetView.SwapChainColor(RenderDevice);
    }

    public override RenderTargetView GetReadOnlyDepthView(int slice = 0)
    {
        Debug.Assert(false);
        return GetView(slice);
    }

    public override IntRect GetViewportRect() => CheckIfBufferIsReady() ? _viewportRect : IntRect.Zero;

    protected override void OnRenderBegin(CommonFrameInfo frameInfo)
    {
        _renderTarget = frameInfo.RenderTarget;
        _viewportRect = frameInfo.ViewportRect;
        BufferIsReady = true;
    }

    protected override void OnRenderEnd(CommonFrameInfo frameInfo) => BufferIsReady = false;
}
