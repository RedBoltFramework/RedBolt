using RedBolt.Bindings.Diligent;
using RedBolt.Graphics;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

namespace RedBolt.RenderAPI;

internal enum RenderTargetViewType
{
    Resource,
    ResourceSlice,
    ReadOnlyResource,
    ReadOnlyResourceSlice,
    SwapChainColor,
    SwapChainDepthStencil
}

/// <summary>
/// Lightweight reference to a render target view in texture or in swap chain.
/// </summary>
public readonly struct RenderTargetView
{
    private readonly RenderDevice? _renderDevice;
    private readonly RawTexture? _rawTexture;
    private readonly Texture? _renderTexture;
    private readonly RenderTargetViewType _type;
    private readonly int _slice;

    private RenderTargetView(Texture? renderTexture, RawTexture? rawTexture, RenderTargetViewType type, int slice,
        RenderDevice? renderDevice)
    {
        _renderDevice = renderDevice;
        _rawTexture = rawTexture;
        _renderTexture = renderTexture;
        _type = type;
        _slice = slice;
    }

    public static RenderTargetView SwapChainColor(RenderDevice renderDevice)
    {
        System.Diagnostics.Debug.Assert(renderDevice is not null);
        return new RenderTargetView(null, null, RenderTargetViewType.SwapChainColor, 0, renderDevice);
    }

    public static RenderTargetView SwapChainDepthStencil(RenderDevice renderDevice)
    {
        System.Diagnostics.Debug.Assert(renderDevice is not null);
        return new RenderTargetView(null, null, RenderTargetViewType.SwapChainDepthStencil, 0, renderDevice);
    }

    public static RenderTargetView Texture(RawTexture texture)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        return new RenderTargetView(null, texture, RenderTargetViewType.Resource, 0,
            texture.GetRenderDevice()!);
    }

    public static RenderTargetView Texture(Texture texture)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        return new RenderTargetView(texture, null, RenderTargetViewType.Resource, 0,
            texture.GetSubsystem<RenderDevice>()!);
    }

    public static RenderTargetView TextureSlice(RawTexture texture, int slice)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        return new RenderTargetView(null, texture, RenderTargetViewType.ResourceSlice, slice,
            texture.GetRenderDevice()!);
    }

    public static RenderTargetView TextureSlice(Texture texture, int slice)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        return new RenderTargetView(texture, null, RenderTargetViewType.ResourceSlice, slice,
            texture.GetSubsystem<RenderDevice>()!);
    }

    public static RenderTargetView ReadOnlyDepth(RawTexture texture)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        System.Diagnostics.Debug.Assert((texture.GetParams().Flags & TextureFlag.BindDepthStencil) != 0);
        return new RenderTargetView(null, texture, RenderTargetViewType.ReadOnlyResource, 0,
            texture.GetRenderDevice()!);
    }

    public static RenderTargetView ReadOnlyDepth(Texture texture)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        System.Diagnostics.Debug.Assert(texture.IsDepthStencil());
        return new RenderTargetView(texture, null, RenderTargetViewType.ReadOnlyResource, 0,
            texture.GetSubsystem<RenderDevice>()!);
    }

    public static RenderTargetView ReadOnlyDepthSlice(RawTexture texture, int slice)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        System.Diagnostics.Debug.Assert((texture.GetParams().Flags & TextureFlag.BindDepthStencil) != 0);
        return new RenderTargetView(null, texture, RenderTargetViewType.ReadOnlyResourceSlice, slice,
            texture.GetRenderDevice()!);
    }

    public static RenderTargetView ReadOnlyDepthSlice(Texture texture, int slice)
    {
        System.Diagnostics.Debug.Assert(texture is not null);
        System.Diagnostics.Debug.Assert(texture.IsDepthStencil());
        return new RenderTargetView(texture, null, RenderTargetViewType.ReadOnlyResourceSlice, slice,
            texture.GetSubsystem<RenderDevice>()!);
    }

    /// <summary>
    /// Mark referenced texture as dirty.
    /// </summary>
    public void MarkDirty()
    {
        if (IsSwapChain)
            return;

        if (GetRawTexture() is { } rawTexture)
            rawTexture.MarkDirty();
        else
            ResolveRenderDevice().MarkRenderTargetDirty(this);
    }

    /// <summary>
    /// Return effective view handle.
    /// </summary>
    public ITextureView GetView() => ResolveRenderDevice().ResolveRenderTargetView(this);

    /// <summary>
    /// Return texture format.
    /// </summary>
    public TextureFormat GetFormat()
    {
        if (_rawTexture is not null)
            return _rawTexture.GetParams().Format;
        if (_renderTexture is not null)
            return _renderTexture.Format;

        var outputTarget = ResolveRenderDevice().GetSwapChainOutputTarget();
        return _type == RenderTargetViewType.SwapChainDepthStencil
            ? outputTarget.DepthStencilFormat
            : outputTarget.ColorFormat;
    }

    /// <summary>
    /// Return multi-sample level.
    /// </summary>
    public int GetMultiSample()
    {
        if (_rawTexture is not null)
            return checked((int)_rawTexture.GetParams().MultiSample);
        if (_renderTexture is not null)
            return _renderTexture.MultiSample;

        return ResolveRenderDevice().GetWindowSettings().MultiSample;
    }

    /// <summary>
    /// Return whether the view belongs to swap chain.
    /// </summary>
    public bool IsSwapChain => _type is RenderTargetViewType.SwapChainColor or RenderTargetViewType.SwapChainDepthStencil;

    private RenderDevice ResolveRenderDevice() => _renderDevice!;

    internal Texture? RenderTexture => _renderTexture;
    internal RenderTargetViewType Type => _type;
    internal int Slice => _slice;
    internal RawTexture? GetRawTexture() => _rawTexture;
}
