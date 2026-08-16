// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderPipeline;

using System.Diagnostics;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

/// <summary>Write-only viewport depth-stencil texture.</summary>
public sealed class ViewportDepthStencilRenderBuffer : RenderBuffer
{
    private IntRect _viewportRect;
    private RenderSurface? _depthStencil;

    public ViewportDepthStencilRenderBuffer(IRenderPipelineInterface renderPipeline)
        : base(renderPipeline)
    {
    }

    public override RawTexture? GetTexture()
    {
        Debug.Assert(CheckIfBufferIsReady());
        return _depthStencil?.GetParentTexture()?.GetRawTexture();
    }

    public override RenderTargetView GetView(int slice = 0)
    {
        Debug.Assert(CheckIfBufferIsReady());
        return _depthStencil?.GetView() ?? RenderTargetView.SwapChainDepthStencil(RenderDevice);
    }

    public override RenderTargetView GetReadOnlyDepthView(int slice = 0)
    {
        Debug.Assert(CheckIfBufferIsReady());
        return _depthStencil?.GetReadOnlyDepthView() ?? RenderTargetView.SwapChainDepthStencil(RenderDevice);
    }

    public override IntRect GetViewportRect() => CheckIfBufferIsReady() ? _viewportRect : IntRect.Zero;

    protected override void OnRenderBegin(CommonFrameInfo frameInfo)
    {
        _viewportRect = frameInfo.ViewportRect;
        if (frameInfo.RenderTarget is null)
        {
            _depthStencil = null;
            BufferIsReady = true;
        }
        else if (frameInfo.RenderTarget.GetLinkedDepthStencil() is { } depthStencil)
        {
            _depthStencil = depthStencil;
            BufferIsReady = true;
        }
        else
        {
            _depthStencil = null;
            BufferIsReady = false;
        }
    }

    protected override void OnRenderEnd(CommonFrameInfo frameInfo) => BufferIsReady = false;
}
