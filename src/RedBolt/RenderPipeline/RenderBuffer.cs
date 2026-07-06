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
    private readonly RenderBufferParams bufferParams;
    private readonly Vector2 sizeMultiplier = Vector2.One;
    private readonly IntVector2 fixedSize;

    public TextureRenderBuffer(Texture texture, IntRect viewportRect)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        ViewportRect = viewportRect;
        bufferParams = new RenderBufferParams(texture.Format);
    }

    public TextureRenderBuffer(RenderBufferParams bufferParams, Vector2 size = default)
    {
        this.bufferParams = bufferParams;
        Texture = bufferParams.Flags.HasFlag(RenderBufferFlag.CubeMap)
            ? new TextureCube($"RenderBuffer.{Guid.NewGuid():N}")
            : new Texture2D($"RenderBuffer.{Guid.NewGuid():N}");

        var effectiveSize = size == default ? Vector2.One : size;
        if (bufferParams.Flags.HasFlag(RenderBufferFlag.FixedTextureSize))
            fixedSize = new IntVector2(Math.Max(1, (int)Math.Round(effectiveSize.X)), Math.Max(1, (int)Math.Round(effectiveSize.Y)));
        else
            sizeMultiplier = effectiveSize;
    }

    public Texture Texture { get; }

    public IntRect ViewportRect { get; private set; }

    public RenderBufferParams BufferParams => bufferParams;

    public override Texture? GetTexture() => Texture;

    public override RenderTargetView GetView(uint slice = 0u)
    {
        if (Texture is Texture2D && slice == 0u)
            return RenderTargetView.Texture(Texture);

        return RenderTargetView.TextureSlice(Texture, slice);
    }

    public override RenderTargetView GetReadOnlyDepthView(uint slice = 0u)
    {
        if (Texture is Texture2D && slice == 0u)
            return RenderTargetView.ReadOnlyDepth(Texture);

        return RenderTargetView.ReadOnlyDepthSlice(Texture, slice);
    }

    public override IntRect GetViewportRect() => ViewportRect;

    public void SetViewportRect(IntRect viewportRect)
    {
        ViewportRect = viewportRect;
    }

    public void RefreshForViewport(IntRect outputViewportRect, Action<Texture, TextureFormat>? configureTexture = null)
    {
        var nextSize = CalculateTextureSize(outputViewportRect.Size);
        var nextFormat = bufferParams.TextureFormat == TextureFormat.Unknown ? TextureFormat.RGBA8 : bufferParams.TextureFormat;
        if (Texture.Width != nextSize.X || Texture.Height != nextSize.Y || Texture.Format != nextFormat)
        {
            switch (Texture)
            {
                case Texture2D texture2D when texture2D.SetSize(nextSize.X, nextSize.Y, nextFormat):
                    break;

                case TextureCube textureCube when textureCube.SetSize(Math.Max(nextSize.X, nextSize.Y), nextFormat):
                    nextSize = new IntVector2(textureCube.Width, textureCube.Height);
                    break;

                default:
                    throw new InvalidOperationException("Failed to resize texture render buffer.");
            }
        }

        if (!IsDepthTextureFormat(nextFormat))
        {
            Texture.SetFilterMode(bufferParams.Flags.HasFlag(RenderBufferFlag.BilinearFiltering)
                ? TextureFilterMode.Bilinear
                : TextureFilterMode.Nearest);
            configureTexture?.Invoke(Texture, nextFormat);
        }

        ViewportRect = new IntRect(0, 0, nextSize.X, nextSize.Y);
    }

    private IntVector2 CalculateTextureSize(IntVector2 outputSize)
    {
        if (bufferParams.Flags.HasFlag(RenderBufferFlag.FixedTextureSize))
            return new IntVector2(Math.Max(fixedSize.X, 1), Math.Max(fixedSize.Y, 1));

        var width = sizeMultiplier.X <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.X * sizeMultiplier.X))
            : Math.Max(1, (int)Math.Round(sizeMultiplier.X));
        var height = sizeMultiplier.Y <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.Y * sizeMultiplier.Y))
            : Math.Max(1, (int)Math.Round(sizeMultiplier.Y));
        return new IntVector2(width, height);
    }

    private static bool IsDepthTextureFormat(TextureFormat format) => format == TextureFormat.Depth24Stencil8;
}
