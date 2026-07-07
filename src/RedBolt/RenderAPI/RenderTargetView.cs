using RedBolt.Bindings.Diligent;

namespace RedBolt;

public readonly struct OptionalRawTextureRTV : IEquatable<OptionalRawTextureRTV>
{
    private readonly RenderTargetView? value;

    public OptionalRawTextureRTV(RenderTargetView value)
    {
        this.value = value;
    }

    public bool HasValue => value.HasValue;

    public RenderTargetView Value => value ?? throw new InvalidOperationException("Optional render target view has no value.");

    public RenderTargetView? GetValueOrDefault() => value;

    public bool Equals(OptionalRawTextureRTV other) => Nullable.Equals(value, other.value);

    public override bool Equals(object? obj) => obj is OptionalRawTextureRTV other && Equals(other);

    public override int GetHashCode() => value?.GetHashCode() ?? 0;

    public static implicit operator OptionalRawTextureRTV(RenderTargetView value) => new(value);

    public static implicit operator OptionalRawTextureRTV(RenderTargetView? value) => value.HasValue ? new OptionalRawTextureRTV(value.Value) : default;

    public static implicit operator RenderTargetView?(OptionalRawTextureRTV value) => value.value;
}

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

    public static RenderTargetView Texture(RawTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return Texture(texture.GetTexture() ?? throw new InvalidOperationException("Raw texture is not initialized."));
    }

    public static RenderTargetView TextureSlice(Texture texture, uint slice)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new RenderTargetView(texture, RenderTargetViewType.ResourceSlice, slice);
    }

    public static RenderTargetView TextureSlice(RawTexture texture, uint slice)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return TextureSlice(texture.GetTexture() ?? throw new InvalidOperationException("Raw texture is not initialized."), slice);
    }

    public static RenderTargetView ReadOnlyDepth(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new RenderTargetView(texture, RenderTargetViewType.ReadOnlyResource);
    }

    public static RenderTargetView ReadOnlyDepth(RawTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return ReadOnlyDepth(texture.GetTexture() ?? throw new InvalidOperationException("Raw texture is not initialized."));
    }

    public static RenderTargetView ReadOnlyDepthSlice(Texture texture, uint slice)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new RenderTargetView(texture, RenderTargetViewType.ReadOnlyResourceSlice, slice);
    }

    public static RenderTargetView ReadOnlyDepthSlice(RawTexture texture, uint slice)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return ReadOnlyDepthSlice(texture.GetTexture() ?? throw new InvalidOperationException("Raw texture is not initialized."), slice);
    }

    public bool IsSwapChain => Type is RenderTargetViewType.SwapChainColor or RenderTargetViewType.SwapChainDepthStencil;

    public void MarkDirty()
    {
        if (IsSwapChain)
            return;

        ResolveRenderDevice().MarkRenderTargetDirty(this);
    }

    public ITextureView GetView() => ResolveRenderDevice().ResolveRenderTargetView(this);

    public TextureFormat GetFormat()
    {
        if (RenderTexture is not null)
            return RenderTexture.Format;

        var outputTarget = ResolveRenderDevice().GetSwapChainOutputTarget();
        return Type == RenderTargetViewType.SwapChainDepthStencil ? outputTarget.DepthStencilFormat : outputTarget.ColorFormat;
    }

    public int GetMultiSample()
    {
        if (RenderTexture is not null)
            return RenderTexture.MultiSample;

        return ResolveRenderDevice().GetSwapChainOutputTarget().MultiSample;
    }

    private static RenderDevice ResolveRenderDevice()
    {
        var context = Context.TryGetInstance() ?? throw new InvalidOperationException("No active Context instance is available.");
        return context.GetSubsystem<RenderDevice>() ?? throw new InvalidOperationException("RenderDevice subsystem is not available.");
    }
}
