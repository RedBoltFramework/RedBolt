namespace RedBolt;

public abstract class RenderBuffer
{
    public bool IsEnabled { get; private set; } = true;

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }

    public abstract Texture? GetTexture();

    public abstract RenderTargetView GetView(uint slice = 0u);

    public abstract RenderTargetView GetReadOnlyDepthView(uint slice = 0u);

    public abstract IntRect GetViewportRect();
}

public sealed class TextureRenderBuffer : RenderBuffer
{
    public TextureRenderBuffer(Texture texture, IntRect viewportRect)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        ViewportRect = viewportRect;
    }

    public Texture Texture { get; }

    public IntRect ViewportRect { get; private set; }

    public override Texture? GetTexture() => Texture;

    public override RenderTargetView GetView(uint slice = 0u)
    {
        return slice == 0u ? RenderTargetView.Texture(Texture) : RenderTargetView.TextureSlice(Texture, slice);
    }

    public override RenderTargetView GetReadOnlyDepthView(uint slice = 0u)
    {
        return slice == 0u ? RenderTargetView.ReadOnlyDepth(Texture) : RenderTargetView.ReadOnlyDepthSlice(Texture, slice);
    }

    public override IntRect GetViewportRect() => ViewportRect;

    public void SetViewportRect(IntRect viewportRect)
    {
        ViewportRect = viewportRect;
    }
}
