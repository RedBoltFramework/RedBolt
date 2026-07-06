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
    private TextureRenderBuffer? primaryColorBuffer;
    private TextureRenderBuffer? secondaryColorBuffer;
    private TextureRenderBuffer? depthBuffer;
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
        depthStencilOutputParams = new RenderBufferParams(TextureFormat.Depth24Stencil8);

        if (frameSettings.SupportColorReadWrite)
        {
            depthStencilBuffer = EnsureDepthBuffer();
            writeableColorBuffer = EnsurePrimaryColorBuffer();
            readableColorBuffer = EnsureSecondaryColorBuffer();
        }
        else
        {
            depthStencilBuffer = viewportDepthBuffer;
            writeableColorBuffer = viewportColorBuffer;
            readableColorBuffer = frameSettings.ReadableColor ? EnsureSecondaryColorBuffer() : null;
        }
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
        texture.SetFilterMode(bufferParams.Flags.HasFlag(RenderBufferFlag.BilinearFiltering)
            ? TextureFilterMode.Bilinear
            : TextureFilterMode.Nearest);
        return new TextureRenderBuffer(texture, new IntRect(0, 0, width, height));
    }

    public void SwapColorBuffers(bool synchronizeContents)
    {
        if (!frameSettings.SupportColorReadWrite)
            throw new InvalidOperationException("Cannot swap color buffers when color read/write is not enabled for the frame.");

        readableColorBuffer ??= EnsureSecondaryColorBuffer();
        (writeableColorBuffer, readableColorBuffer) = (readableColorBuffer, writeableColorBuffer);
    }

    /// <summary>
    /// Bind the currently writable color buffer and matching depth-stencil buffer as render outputs.
    /// </summary>
    public void SetOutputRenderTargets(Graphics graphics, bool readOnlyDepth = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        var depthView = readOnlyDepth ? depthStencilBuffer.GetReadOnlyDepthView() : depthStencilBuffer.GetView();
        graphics.SetRenderTargets(viewportRect, writeableColorBuffer.GetView(), depthView);
    }

    /// <summary>
    /// Render a fullscreen quad into the currently bound output.
    /// </summary>
    public void DrawViewportQuad(Graphics graphics, string shaderName, string shaderDefines, BlendMode blendMode,
        bool bilinearColorSampler, IReadOnlyDictionary<string, object?> parameters, string? debugComment = null,
        bool flipVertical = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);
        ArgumentNullException.ThrowIfNull(parameters);

        graphics.DrawFullscreenQuad(
            debugComment ?? shaderName,
            shaderName,
            shaderDefines,
            blendMode,
            bilinearColorSampler,
            albedoTexture: null,
            normalTexture: null,
            parameters,
            GetInvOutputSize(),
            GetDefaultClipToUVSpaceOffsetAndScale(),
            flipVertical);
    }

    /// <summary>
    /// Render a fullscreen feedback quad that samples the secondary color texture.
    /// </summary>
    public void DrawFeedbackViewportQuad(Graphics graphics, string shaderName, string shaderDefines, BlendMode blendMode,
        bool bilinearColorSampler, IReadOnlyDictionary<string, object?> parameters, string? debugComment = null,
        bool flipVertical = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);
        ArgumentNullException.ThrowIfNull(parameters);

        graphics.DrawFullscreenQuad(
            debugComment ?? shaderName,
            shaderName,
            shaderDefines,
            blendMode,
            bilinearColorSampler,
            GetSecondaryColorTexture(),
            null,
            parameters,
            GetInvOutputSize(),
            GetDefaultClipToUVSpaceOffsetAndScale(),
            flipVertical);
    }

    /// <summary>
    /// Return whether the final output must be copied back to the viewport target.
    /// </summary>
    public bool NeedsViewportResolve() => !ReferenceEquals(writeableColorBuffer, viewportColorBuffer);

    /// <summary>
    /// Copy the current color output back to the viewport render target.
    /// </summary>
    public void ResolveToViewport(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (!NeedsViewportResolve())
            return;

        var colorTexture = writeableColorBuffer.GetTexture();
        if (colorTexture is null)
            return;

        graphics.SetSwapChainRenderTargets(viewportRect);
        if (!graphics.TryCopyTextureToSwapChain(colorTexture))
            graphics.DrawCopyTexture(colorTexture, bilinearFiltering: colorOutputParams.Flags.HasFlag(RenderBufferFlag.BilinearFiltering));
    }

    /// <summary>
    /// Bind an explicit render target buffer for custom intermediate passes.
    /// </summary>
    public void SetRenderTargets(Graphics graphics, RenderBuffer? colorBuffer, RenderBuffer? depthStencilBuffer = null, bool readOnlyDepth = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(colorBuffer);

        var viewport = colorBuffer.GetViewportRect();
        var depthView = depthStencilBuffer is null
            ? (RenderTargetView?)null
            : readOnlyDepth
                ? depthStencilBuffer.GetReadOnlyDepthView()
                : depthStencilBuffer.GetView();
        graphics.SetRenderTargets(viewport, colorBuffer.GetView(), depthView);
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

    private RenderBuffer EnsurePrimaryColorBuffer()
    {
        var outputSize = viewportRect.Size;
        if (primaryColorBuffer is not null
            && primaryColorBuffer.Texture.Width == outputSize.X
            && primaryColorBuffer.Texture.Height == outputSize.Y
            && primaryColorBuffer.Texture.Format == colorOutputParams.TextureFormat)
        {
            primaryColorBuffer.SetViewportRect(viewportRect);
            return primaryColorBuffer;
        }

        primaryColorBuffer = CreateColorBuffer(colorOutputParams) as TextureRenderBuffer
            ?? throw new InvalidOperationException("Primary color buffer creation returned unexpected render buffer type.");
        primaryColorBuffer.SetViewportRect(viewportRect);
        return primaryColorBuffer;
    }

    private RenderBuffer EnsureDepthBuffer()
    {
        var outputSize = viewportRect.Size;
        if (depthBuffer is not null
            && depthBuffer.Texture.Width == outputSize.X
            && depthBuffer.Texture.Height == outputSize.Y
            && depthBuffer.Texture.Format == depthStencilOutputParams.TextureFormat)
        {
            depthBuffer.SetViewportRect(viewportRect);
            return depthBuffer;
        }

        var texture = new Texture2D($"RenderDepthBuffer.{Guid.NewGuid():N}");
        texture.SetSize(outputSize.X, outputSize.Y, depthStencilOutputParams.TextureFormat);
        depthBuffer = new TextureRenderBuffer(texture, new IntRect(0, 0, outputSize.X, outputSize.Y));
        depthBuffer.SetViewportRect(viewportRect);
        return depthBuffer;
    }

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
            RenderPipelineColorSpace.LinearHDR => TextureFormat.RGBA16F,
            RenderPipelineColorSpace.LinearLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.GammaLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.Optimized => TextureFormat.RGBA8,
            _ => TextureFormat.RGBA8
        };
    }
}
