using System.Threading;

using RedBolt.Bindings.Diligent;
using RedBolt.Math;
using RedBolt.RenderAPI;

namespace RedBolt.Graphics;

/// <summary>Color or depth-stencil surface that can be rendered into.</summary>
public sealed class RenderSurface : IDisposable
{
    /// <summary>Parent texture.</summary>
    private readonly Texture? _parentTexture;

    /// <summary>Slice of the parent texture.</summary>
    private readonly int _slice;

    /// <summary>Diligent render target or depth-stencil view.</summary>
    private ITextureView? _renderTargetView;

    /// <summary>Viewports.</summary>
    private readonly List<Viewport?> _viewports = [];

    /// <summary>Linked color buffer.</summary>
    private RenderSurface? _linkedRenderTarget;

    /// <summary>Linked depth buffer.</summary>
    private RenderSurface? _linkedDepthStencil;

    /// <summary>Update mode for viewports.</summary>
    private RenderSurfaceUpdateMode _updateMode = RenderSurfaceUpdateMode.UpdateVisible;

    /// <summary>Update queued flag.</summary>
    private int _updateQueued;

    /// <summary>Multisampled resolve dirty flag.</summary>
    private bool _resolveDirty;

    public RenderSurface(Texture? parentTexture, int slice)
    {
        _parentTexture = parentTexture;
        _slice = slice;
    }

    public void Dispose()
    {
        _linkedDepthStencil = null;
        _linkedRenderTarget = null;
        _viewports.Clear();
        if (_renderTargetView is { IsNull: false })
            _ = _renderTargetView.Release();
        _renderTargetView = null;
    }

    /// <summary>Internal. Restore GPU resource.</summary>
    public void Restore(ITextureView? view)
    {
        if (ReferenceEquals(_renderTargetView, view))
            return;

        if (view is { IsNull: false })
            _ = view.AddRef();
        if (_renderTargetView is { IsNull: false })
            _ = _renderTargetView.Release();
        _renderTargetView = view;
    }

    /// <summary>Internal. Invalidate GPU resource.</summary>
    public void Invalidate()
    {
        if (_renderTargetView is { IsNull: false })
            _ = _renderTargetView.Release();
        _renderTargetView = null;
    }

    /// <summary>Set number of viewports.</summary>
    public void SetNumViewports(int num)
    {
        if (num < _viewports.Count)
            _viewports.RemoveRange(num, _viewports.Count - num);
        else
        {
            while (_viewports.Count < num)
                _viewports.Add(null);
        }
    }

    /// <summary>Set viewport.</summary>
    public void SetViewport(int index, Viewport? viewport)
    {
        if (index >= _viewports.Count)
            SetNumViewports(index + 1);

        _viewports[index] = viewport;
    }

    /// <summary>Set viewport update mode. Default is to update when visible.</summary>
    public void SetUpdateMode(RenderSurfaceUpdateMode mode)
    {
        _updateMode = mode;
    }

    /// <summary>Set linked color rendertarget.</summary>
    public void SetLinkedRenderTarget(RenderSurface? renderTarget)
    {
        if (!ReferenceEquals(renderTarget, this))
            _linkedRenderTarget = renderTarget;
    }

    /// <summary>Set linked depth-stencil surface.</summary>
    public void SetLinkedDepthStencil(RenderSurface? depthStencil)
    {
        if (!ReferenceEquals(depthStencil, this))
            _linkedDepthStencil = depthStencil;
    }

    /// <summary>Queue manual update of the viewport(s).</summary>
    public void QueueUpdate()
    {
        Interlocked.Exchange(ref _updateQueued, 1);
    }

    /// <summary>Return width.</summary>
    public int GetWidth() => _parentTexture!.Width;

    /// <summary>Return height.</summary>
    public int GetHeight() => _parentTexture!.Height;

    /// <summary>Return size.</summary>
    public Vector2i GetSize() => new(GetWidth(), GetHeight());

    /// <summary>Return multisampling level.</summary>
    public int GetMultiSample() => _parentTexture!.MultiSample;

    /// <summary>Return multisampling autoresolve mode.</summary>
    public bool GetAutoResolve() => _parentTexture!.GetAutoResolve();

    /// <summary>Return number of viewports.</summary>
    public int GetNumViewports() => _viewports.Count;

    /// <summary>Return viewport by index.</summary>
    public Viewport? GetViewport(int index)
    {
        return index < _viewports.Count ? _viewports[index] : null;
    }

    /// <summary>Return viewport update mode.</summary>
    public RenderSurfaceUpdateMode GetUpdateMode() => _updateMode;

    /// <summary>Return linked color rendertarget.</summary>
    public RenderSurface? GetLinkedRenderTarget() => _linkedRenderTarget;

    /// <summary>Return linked depth-stencil surface.</summary>
    public RenderSurface? GetLinkedDepthStencil() => _linkedDepthStencil;

    /// <summary>Return whether manual update queued.</summary>
    public bool IsUpdateQueued() => Volatile.Read(ref _updateQueued) != 0;

    /// <summary>Reset update queued flag.</summary>
    public void ResetUpdateQueued()
    {
        Interlocked.Exchange(ref _updateQueued, 0);
    }

    /// <summary>Return parent texture.</summary>
    public Texture? GetParentTexture() => _parentTexture;

    /// <summary>Return slice of the parent texture.</summary>
    public int GetSlice() => _slice;

    /// <summary>Return render target view for this surface.</summary>
    public RenderTargetView GetView() => RenderTargetView.TextureSlice(_parentTexture!, _slice);

    /// <summary>Return read-only depth view for this surface.</summary>
    public RenderTargetView GetReadOnlyDepthView() => RenderTargetView.ReadOnlyDepthSlice(_parentTexture!, _slice);

    /// <summary>Return whether this surface is a render target.</summary>
    public bool IsRenderTarget() => _parentTexture!.IsRenderTarget();

    /// <summary>Return whether this surface is a depth-stencil.</summary>
    public bool IsDepthStencil() => _parentTexture!.IsDepthStencil();

    /// <summary>Return whether multisampled rendertarget needs resolve.</summary>
    public bool IsResolveDirty() => _resolveDirty;

    /// <summary>Set or clear the need resolve flag.</summary>
    public void SetResolveDirty(bool enable)
    {
        _resolveDirty = enable;
    }

    /// <summary>Property getters that can work with a null RenderSurface corresponding to the main viewport.</summary>
    public static Vector2i GetSize(GraphicsSystem graphics, RenderSurface? renderSurface)
    {
        return renderSurface is not null ? renderSurface.GetSize() : graphics.GetSize();
    }

    /// <summary>Property getter that can work with a null RenderSurface corresponding to the main viewport.</summary>
    public static IntRect GetRect(GraphicsSystem graphics, RenderSurface? renderSurface)
    {
        return new IntRect(Vector2i.Zero, GetSize(graphics, renderSurface));
    }

    /// <summary>Return color format for the surface or main viewport.</summary>
    public static TextureFormat GetColorFormat(GraphicsSystem graphics, RenderSurface? renderSurface)
    {
        var view = renderSurface is not null
            ? renderSurface.GetView()
            : RenderTargetView.SwapChainColor(graphics.GetSubsystem<RenderDevice>()!);
        return view.GetFormat();
    }

    /// <summary>Return depth format for the surface or main viewport.</summary>
    public static TextureFormat GetDepthFormat(GraphicsSystem graphics, RenderSurface? renderSurface)
    {
        var view = renderSurface is not null
            ? renderSurface.GetView()
            : RenderTargetView.SwapChainDepthStencil(graphics.GetSubsystem<RenderDevice>()!);
        return view.GetFormat();
    }

    /// <summary>Return multisampling level for the surface or main viewport.</summary>
    public static int GetMultiSample(GraphicsSystem graphics, RenderSurface? renderSurface)
    {
        return renderSurface is not null ? renderSurface.GetMultiSample() : graphics.GetMultiSample();
    }

    /// <summary>Return sRGB mode for the surface or main viewport.</summary>
    public static bool GetSRGB(GraphicsSystem graphics, RenderSurface? renderSurface)
    {
        if (renderSurface is not null)
            return renderSurface.GetParentTexture()!.GetSRGB();

        return graphics.GetSRGB();
    }
}
