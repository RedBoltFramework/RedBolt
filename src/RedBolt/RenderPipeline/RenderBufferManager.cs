namespace RedBolt;

public sealed class RenderBufferManager
{
    private sealed class ViewportOutputRenderBuffer : RenderBuffer
    {
        private readonly RenderTargetViewType type;
        private IntRect viewportRect;

        public ViewportOutputRenderBuffer(RenderTargetViewType type)
        {
            this.type = type;
        }

        public void SetViewportRect(IntRect rect)
        {
            viewportRect = rect;
        }

        public override Texture? GetTexture() => null;

        public override RenderTargetView GetView(uint slice = 0u)
        {
            return type switch
            {
                RenderTargetViewType.SwapChainColor => RenderTargetView.SwapChainColor(),
                RenderTargetViewType.SwapChainDepthStencil => RenderTargetView.SwapChainDepthStencil(),
                _ => throw new InvalidOperationException($"Unsupported viewport render target view type {type}.")
            };
        }

        public override RenderTargetView GetReadOnlyDepthView(uint slice = 0u)
        {
            return GetView(slice);
        }

        public override IntRect GetViewportRect() => viewportRect;
    }

    private readonly ViewportOutputRenderBuffer viewportColorBuffer = new(RenderTargetViewType.SwapChainColor);
    private readonly ViewportOutputRenderBuffer viewportDepthBuffer = new(RenderTargetViewType.SwapChainDepthStencil);
    private TextureRenderBuffer? secondaryColorBuffer;
    private RenderBufferManagerSettings settings = new();
    private RenderBufferManagerFrameSettings frameSettings = new();
    private RenderBufferParams colorOutputParams = new(TextureFormat.RGBA8);
    private RenderBufferParams depthStencilOutputParams = new(TextureFormat.Unknown);
    private IntRect viewportRect;
    private RenderBuffer depthStencilBuffer;
    private RenderBuffer writeableColorBuffer;
    private RenderBuffer? readableColorBuffer;
    private bool linearColorSpace;
    private bool hdr;

    public RenderBufferManager()
    {
        depthStencilBuffer = viewportDepthBuffer;
        writeableColorBuffer = viewportColorBuffer;
    }

    public void SetSettings(RenderBufferManagerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
    }

    public void SetFrameSettings(RenderBufferManagerFrameSettings frameSettings)
    {
        ArgumentNullException.ThrowIfNull(frameSettings);

        this.frameSettings = new RenderBufferManagerFrameSettings
        {
            ReadableColor = frameSettings.ReadableColor || frameSettings.SupportColorReadWrite,
            SupportColorReadWrite = frameSettings.SupportColorReadWrite
        };
    }

    public void OnViewportDefined(object? renderTarget, IntRect viewportRect)
    {
        this.viewportRect = viewportRect;
        viewportColorBuffer.SetViewportRect(viewportRect);
        viewportDepthBuffer.SetViewportRect(viewportRect);

        linearColorSpace = settings.ColorSpace is not RenderPipelineColorSpace.GammaLDR;
        hdr = settings.ColorSpace == RenderPipelineColorSpace.LinearHDR;
        colorOutputParams = new RenderBufferParams(GetColorTextureFormat(settings.ColorSpace));
        depthStencilOutputParams = new RenderBufferParams(TextureFormat.Unknown);

        depthStencilBuffer = viewportDepthBuffer;
        writeableColorBuffer = viewportColorBuffer;
        readableColorBuffer = frameSettings.ReadableColor ? EnsureSecondaryColorBuffer() : null;
    }

    public SharedRenderPassState PopulateSharedState(SharedRenderPassState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.RenderBufferManager = this;
        state.RenderBuffers["Viewport.Color"] = writeableColorBuffer;
        state.RenderBuffers["Viewport.Depth"] = depthStencilBuffer;
        if (readableColorBuffer is not null)
            state.RenderBuffers["Viewport.Color.Readable"] = readableColorBuffer;
        return state;
    }

    public TextureRenderBuffer CreateColorBuffer(RenderBufferParams bufferParams, Vector2 size = default)
    {
        var effectiveSize = size == default ? Vector2.One : size;
        var outputSize = viewportRect.Size;
        var width = effectiveSize.X <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.X * effectiveSize.X))
            : Math.Max(1, (int)Math.Round(effectiveSize.X));
        var height = effectiveSize.Y <= 1.0f
            ? Math.Max(1, (int)Math.Round(outputSize.Y * effectiveSize.Y))
            : Math.Max(1, (int)Math.Round(effectiveSize.Y));

        var texture = new Texture2D($"RenderBuffer.{Guid.NewGuid():N}");
        texture.SetSize(width, height, bufferParams.TextureFormat == TextureFormat.Unknown ? TextureFormat.RGBA8 : bufferParams.TextureFormat);
        return new TextureRenderBuffer(texture, new IntRect(0, 0, width, height));
    }

    public void SwapColorBuffers(bool synchronizeContents)
    {
        if (!frameSettings.SupportColorReadWrite)
            throw new InvalidOperationException("Cannot swap color buffers when color read/write is not enabled for the frame.");

        readableColorBuffer ??= EnsureSecondaryColorBuffer();
        (writeableColorBuffer, readableColorBuffer) = (readableColorBuffer, writeableColorBuffer);
    }

    public RenderBuffer GetDepthStencilOutput() => depthStencilBuffer;

    public RenderBuffer GetColorOutput() => writeableColorBuffer;

    public Texture? GetSecondaryColorTexture() => readableColorBuffer?.GetTexture();

    public IntVector2 GetOutputSize() => viewportRect.Size;

    public Vector2 GetInvOutputSize()
    {
        var size = GetOutputSize();
        return new Vector2(
            size.X > 0 ? 1.0f / size.X : 0.0f,
            size.Y > 0 ? 1.0f / size.Y : 0.0f);
    }

    public Vector4 GetDefaultClipToUVSpaceOffsetAndScale() => new(0.5f, 0.5f, 0.5f, 0.5f);

    public TextureFormat GetOutputColorFormat() => colorOutputParams.TextureFormat;

    public TextureFormat GetOutputDepthStencilFormat() => depthStencilOutputParams.TextureFormat;

    public bool IsLinearColorSpace() => linearColorSpace;

    public bool IsHDR() => hdr;

    public int GetOutputMultiSample() => colorOutputParams.MultiSampleLevel;

    public RenderBufferManagerSettings GetSettings() => settings;

    private RenderBuffer EnsureSecondaryColorBuffer()
    {
        var outputSize = viewportRect.Size;
        if (secondaryColorBuffer is not null
            && secondaryColorBuffer.Texture.Width == outputSize.X
            && secondaryColorBuffer.Texture.Height == outputSize.Y
            && secondaryColorBuffer.Texture.Format == colorOutputParams.TextureFormat)
        {
            secondaryColorBuffer.SetViewportRect(viewportRect);
            return secondaryColorBuffer;
        }

        secondaryColorBuffer = CreateColorBuffer(colorOutputParams) as TextureRenderBuffer
            ?? throw new InvalidOperationException("Color buffer creation returned unexpected render buffer type.");
        secondaryColorBuffer.SetViewportRect(viewportRect);
        return secondaryColorBuffer;
    }

    private static TextureFormat GetColorTextureFormat(RenderPipelineColorSpace colorSpace)
    {
        return colorSpace switch
        {
            RenderPipelineColorSpace.LinearHDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.LinearLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.GammaLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.Optimized => TextureFormat.RGBA8,
            _ => TextureFormat.RGBA8
        };
    }
}
