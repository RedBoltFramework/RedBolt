using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Base class for writable texture or texture region. Readability is not guaranteed.
/// </summary>
// TODO: port - C++ code:
// C++ RenderBuffer inherits from Object and takes RenderPipelineInterface* in constructor.
// C++ has virtual OnRenderBegin(const CommonFrameInfo&) and OnRenderEnd(const CommonFrameInfo&) callbacks.
// C++ subscribes to renderPipeline->OnRenderBegin and OnRenderEnd in constructor.
public abstract class RenderBuffer
{
    protected bool _bufferIsReady;

    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Set whether the render buffer is enabled. Disabled buffers should not be accessed during the frame.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }

    protected bool CheckIfBufferIsReady()
    {
        if (!_bufferIsReady)
        {
            Log.Logger.LogError("RenderBuffer is not available");
            return false;
        }

        return true;
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
// TODO: port - C++ code:
// C++ TextureRenderBuffer constructor takes RenderPipelineInterface* + RenderBufferParams + Vector2.
// C++ OnRenderBegin allocates textures via RenderPool::GetTexture (transient/persistent).
// C++ OnRenderEnd sets currentTexture_ = nullptr and bufferIsReady_ = false.
// Texture lifetime is frame-only in C++.
public sealed class TextureRenderBuffer : RenderBuffer
{
    private readonly RenderBufferParams _bufferParams;
    private readonly Vector2f _sizeMultiplier = Vector2f.One;
    private readonly Vector2i _fixedSize;

    public TextureRenderBuffer(Texture texture, IntRect viewportRect)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        ViewportRect = viewportRect;
        _bufferParams = new RenderBufferParams(texture.Format, texture.MultiSample);
    }

    public TextureRenderBuffer(RenderBufferParams bufferParams, Vector2f size = default)
    {
        this._bufferParams = bufferParams;
        Texture = (bufferParams.Flags & RenderBufferFlag.CubeMap) != 0
            ? new TextureCube($"RenderBuffer.{Guid.NewGuid():N}")
            : new Texture2D($"RenderBuffer.{Guid.NewGuid():N}");

        var effectiveSize = size == default ? Vector2f.One : size;
        if ((bufferParams.Flags & RenderBufferFlag.FixedTextureSize) != 0)
            _fixedSize = new Vector2i(Math.Max(1, (int)Math.Round(effectiveSize.X)), Math.Max(1, (int)Math.Round(effectiveSize.Y)));
        else
            _sizeMultiplier = effectiveSize;
    }

    public Texture Texture { get; }

    public IntRect ViewportRect { get; private set; }

    public RenderBufferParams BufferParams => _bufferParams;

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
        var nextFormat = _bufferParams.TextureFormat == TextureFormat.Unknown ? TextureFormat.RGBA8 : _bufferParams.TextureFormat;
        if (Texture.Width != nextSize.X || Texture.Height != nextSize.Y || Texture.Format != nextFormat || Texture.MultiSample != _bufferParams.MultiSampleLevel)
        {
            switch (Texture)
            {
                case Texture2D texture2D when texture2D.SetSize(nextSize.X, nextSize.Y, nextFormat, _bufferParams.MultiSampleLevel,
                    (_bufferParams.Flags & RenderBufferFlag.NoMultiSampledAutoResolve) != 0):
                    break;

                case TextureCube textureCube when textureCube.SetSize(Math.Max(nextSize.X, nextSize.Y), nextFormat, _bufferParams.MultiSampleLevel,
                    (_bufferParams.Flags & RenderBufferFlag.NoMultiSampledAutoResolve) != 0):
                    nextSize = new Vector2i(textureCube.Width, textureCube.Height);
                    break;

                default:
                    throw new InvalidOperationException("Failed to resize texture render buffer.");
            }
        }

        if (!IsDepthTextureFormat(nextFormat))
        {
            Texture.SetFilterMode((_bufferParams.Flags & RenderBufferFlag.BilinearFiltering) != 0
                ? TextureFilterMode.Bilinear
                : TextureFilterMode.Nearest);
            configureTexture?.Invoke(Texture, nextFormat);
        }

        ViewportRect = new IntRect(0, 0, nextSize.X, nextSize.Y);
    }

    private Vector2i CalculateTextureSize(Vector2i outputSize)
    {
        if ((_bufferParams.Flags & RenderBufferFlag.FixedTextureSize) != 0)
            return new Vector2i(Math.Max(_fixedSize.X, 1), Math.Max(_fixedSize.Y, 1));

        var width = _sizeMultiplier.X <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.X * _sizeMultiplier.X))
            : Math.Max(1, (int)Math.Round(_sizeMultiplier.X));
        var height = _sizeMultiplier.Y <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.Y * _sizeMultiplier.Y))
            : Math.Max(1, (int)Math.Round(_sizeMultiplier.Y));
        return new Vector2i(width, height);
    }

    private static bool IsDepthTextureFormat(TextureFormat format)
        => format is TextureFormat.Depth32 or TextureFormat.Depth24Stencil8;
}
