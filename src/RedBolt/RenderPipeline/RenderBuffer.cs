namespace RedBolt;

/// <summary>
/// Base class for writable texture or texture region. Readability is not guaranteed.
/// </summary>
public abstract class RenderBuffer
{
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Set whether the render buffer is enabled. Disabled buffers should not be accessed during the frame.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }

    /// <summary>
    /// Return readable texture. May return null if not supported.
    /// </summary>
    public abstract Texture? GetTexture();

    /// <summary>
    /// Return render surface. Face could be specified for cubemap texture.
    /// </summary>
    public abstract RenderTargetView GetView(uint slice = 0u);

    public abstract RenderTargetView GetReadOnlyDepthView(uint slice = 0u);

    /// <summary>
    /// Return effective viewport rectangle.
    /// Always equal to whole texture for TextureRenderBuffer, not so for viewport buffers.
    /// </summary>
    public abstract IntRect GetViewportRect();
}

/// <summary>
/// Writable and readable render buffer texture (2D or cubemap).
/// </summary>
public sealed class TextureRenderBuffer : RenderBuffer
{
    private readonly RenderBufferParams bufferParams;
    private readonly Vector2 sizeMultiplier = Vector2.One;
    private readonly IntVector2 fixedSize;

    public TextureRenderBuffer(Texture texture, IntRect viewportRect)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        ViewportRect = viewportRect;
        bufferParams = new RenderBufferParams(texture.Format, texture.MultiSample);
    }

    public TextureRenderBuffer(RenderBufferParams bufferParams, Vector2 size = default)
    {
        this.bufferParams = bufferParams;
        Texture = (bufferParams.Flags & RenderBufferFlag.CubeMap) != 0
            ? new TextureCube($"RenderBuffer.{Guid.NewGuid():N}")
            : new Texture2D($"RenderBuffer.{Guid.NewGuid():N}");

        var effectiveSize = size == default ? Vector2.One : size;
        if ((bufferParams.Flags & RenderBufferFlag.FixedTextureSize) != 0)
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
        if (Texture.Width != nextSize.X || Texture.Height != nextSize.Y || Texture.Format != nextFormat || Texture.MultiSample != bufferParams.MultiSampleLevel)
        {
            switch (Texture)
            {
                case Texture2D texture2D when texture2D.SetSize(nextSize.X, nextSize.Y, nextFormat, bufferParams.MultiSampleLevel,
                    (bufferParams.Flags & RenderBufferFlag.NoMultiSampledAutoResolve) != 0):
                    break;

                case TextureCube textureCube when textureCube.SetSize(Math.Max(nextSize.X, nextSize.Y), nextFormat, bufferParams.MultiSampleLevel,
                    (bufferParams.Flags & RenderBufferFlag.NoMultiSampledAutoResolve) != 0):
                    nextSize = new IntVector2(textureCube.Width, textureCube.Height);
                    break;

                default:
                    throw new InvalidOperationException("Failed to resize texture render buffer.");
            }
        }

        if (!IsDepthTextureFormat(nextFormat))
        {
            Texture.SetFilterMode((bufferParams.Flags & RenderBufferFlag.BilinearFiltering) != 0
                ? TextureFilterMode.Bilinear
                : TextureFilterMode.Nearest);
            configureTexture?.Invoke(Texture, nextFormat);
        }

        ViewportRect = new IntRect(0, 0, nextSize.X, nextSize.Y);
    }

    private IntVector2 CalculateTextureSize(IntVector2 outputSize)
    {
        if ((bufferParams.Flags & RenderBufferFlag.FixedTextureSize) != 0)
            return new IntVector2(Math.Max(fixedSize.X, 1), Math.Max(fixedSize.Y, 1));

        var width = sizeMultiplier.X <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.X * sizeMultiplier.X))
            : Math.Max(1, (int)Math.Round(sizeMultiplier.X));
        var height = sizeMultiplier.Y <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.Y * sizeMultiplier.Y))
            : Math.Max(1, (int)Math.Round(sizeMultiplier.Y));
        return new IntVector2(width, height);
    }

    private static bool IsDepthTextureFormat(TextureFormat format)
        => format is TextureFormat.Depth32 or TextureFormat.Depth24Stencil8;
}
