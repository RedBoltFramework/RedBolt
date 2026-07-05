namespace RedBolt;

public enum RenderTargetViewType
{
    Resource,
    ResourceSlice,
    ReadOnlyResource,
    ReadOnlyResourceSlice,
    SwapChainColor,
    SwapChainDepthStencil
}

public readonly record struct RenderTargetView(Texture? RenderTexture, RenderTargetViewType Type, uint Slice = 0u)
{
    public static RenderTargetView SwapChainColor() => new(null, RenderTargetViewType.SwapChainColor);

    public static RenderTargetView SwapChainDepthStencil() => new(null, RenderTargetViewType.SwapChainDepthStencil);

    public static RenderTargetView Texture(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new RenderTargetView(texture, RenderTargetViewType.Resource);
    }

    public static RenderTargetView TextureSlice(Texture texture, uint slice)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new RenderTargetView(texture, RenderTargetViewType.ResourceSlice, slice);
    }

    public static RenderTargetView ReadOnlyDepth(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new RenderTargetView(texture, RenderTargetViewType.ReadOnlyResource);
    }

    public static RenderTargetView ReadOnlyDepthSlice(Texture texture, uint slice)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new RenderTargetView(texture, RenderTargetViewType.ReadOnlyResourceSlice, slice);
    }

    public bool IsSwapChain => Type is RenderTargetViewType.SwapChainColor or RenderTargetViewType.SwapChainDepthStencil;

    public void MarkDirty()
    {
    }

    public TextureFormat GetFormat() => RenderTexture?.Format ?? TextureFormat.Unknown;

    public int GetMultiSample() => 1;
}
