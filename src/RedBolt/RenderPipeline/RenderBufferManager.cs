namespace RedBolt;

public sealed class RenderBufferManager
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyShaderParameters = new Dictionary<string, object?>(0, StringComparer.Ordinal);

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
    private Graphics? lastGraphics;

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
        this.frameSettings = frameSettings;
        this.frameSettings.ReadableColor |= this.frameSettings.SupportColorReadWrite;
        this.frameSettings.FilteredColor |= this.frameSettings.ReadableColor;
    }

    public void OnViewportDefined(object? renderTarget, IntRect viewportRect)
    {
        this.viewportRect = viewportRect;
        viewportColorBuffer.SetViewportRect(viewportRect);
        viewportDepthBuffer.SetViewportRect(viewportRect);

        ResolveOutputParams(renderTarget);

        var needReadableDepth = settings.ReadableDepth || frameSettings.ReadableDepth;
        if (frameSettings.SupportColorReadWrite)
        {
            depthStencilBuffer = EnsureDepthBuffer();
            writeableColorBuffer = EnsurePrimaryColorBuffer();
            readableColorBuffer = EnsureSecondaryColorBuffer();
        }
        else
        {
            depthStencilBuffer = needReadableDepth ? EnsureDepthBuffer() : viewportDepthBuffer;
            writeableColorBuffer = viewportColorBuffer;
            readableColorBuffer = frameSettings.ReadableColor ? EnsureSecondaryColorBuffer() : null;
        }
    }

    public void OnRenderBegin(CommonFrameInfo frameInfo)
    {
        ArgumentNullException.ThrowIfNull(frameInfo);
        OnViewportDefined(frameInfo.RenderTarget, frameInfo.ViewportRect);
    }

    public void OnRenderEnd(CommonFrameInfo frameInfo)
    {
        ArgumentNullException.ThrowIfNull(frameInfo);

        if (lastGraphics is not null)
            ResolveToViewport(lastGraphics);
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
        var previousWriteable = writeableColorBuffer;
        writeableColorBuffer = readableColorBuffer;
        readableColorBuffer = previousWriteable;

        if (!synchronizeContents || lastGraphics is null)
            return;

        var readableTexture = readableColorBuffer.GetTexture();
        if (readableTexture is null)
            return;

        SetRenderTargets(lastGraphics, writeableColorBuffer, depthStencilBuffer);
        lastGraphics.DrawCopyTexture(readableTexture, bilinearFiltering: colorOutputParams.Flags.HasFlag(RenderBufferFlag.BilinearFiltering));
    }

    /// <summary>
    /// Bind the currently writable color buffer and matching depth-stencil buffer as render outputs.
    /// </summary>
    public void SetOutputRenderTargets(Graphics graphics, bool readOnlyDepth = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        lastGraphics = graphics;

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
            secondaryTexture: null,
            depthTexture: null,
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
            secondaryTexture: null,
            depthTexture: null,
            parameters,
            GetInvOutputSize(),
            GetDefaultClipToUVSpaceOffsetAndScale(),
            flipVertical);
    }

    public void CopyTextureRegion(Graphics graphics, string debugComment, Texture sourceTexture, IntRect sourceRect,
        RenderTargetView destinationSurface, IntRect destinationRect, ColorSpaceTransition mode = ColorSpaceTransition.None,
        bool flipVertical = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(sourceTexture);

        graphics.SetRenderTargets(destinationRect, destinationSurface, depthStencilTarget: null);
        DrawTextureRegion(graphics, debugComment, sourceTexture, sourceRect, mode, flipVertical);
    }

    public void DrawTextureRegion(Graphics graphics, string debugComment, Texture sourceTexture, IntRect sourceRect,
        ColorSpaceTransition mode = ColorSpaceTransition.None, bool flipVertical = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(sourceTexture);
        ArgumentException.ThrowIfNullOrWhiteSpace(debugComment);

        var effectiveSourceRect = sourceRect != IntRect.Zero
            ? sourceRect
            : new IntRect(0, 0, sourceTexture.Width, sourceTexture.Height);
        var sourceIsLinear = sourceTexture.GetLinear() || sourceTexture.Format == TextureFormat.RGBA16F;
        var shaderDefines = string.Empty;
        if (mode == ColorSpaceTransition.Automatic && sourceIsLinear != linearColorSpace)
            shaderDefines = linearColorSpace ? "URHO3D_GAMMA_TO_LINEAR" : "URHO3D_LINEAR_TO_GAMMA";

        var invInputSize = new Vector2(
            sourceTexture.Width > 0 ? 1.0f / sourceTexture.Width : 0.0f,
            sourceTexture.Height > 0 ? 1.0f / sourceTexture.Height : 0.0f);
        graphics.DrawFullscreenQuad(
            debugComment,
            "v2/X_CopyFramebuffer",
            shaderDefines,
            BlendMode.Replace,
            sourceTexture.GetFilterMode() is not TextureFilterMode.Nearest,
            sourceTexture,
            secondaryTexture: null,
            depthTexture: null,
            EmptyShaderParameters,
            invInputSize,
            GetClipToUvOffsetAndScale(sourceTexture.Width, sourceTexture.Height, effectiveSourceRect),
            flipVertical);
    }

    public void DrawTexture(Graphics graphics, string debugComment, Texture sourceTexture,
        ColorSpaceTransition mode = ColorSpaceTransition.None, bool flipVertical = false)
    {
        DrawTextureRegion(graphics, debugComment, sourceTexture, IntRect.Zero, mode, flipVertical);
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
        lastGraphics = graphics;

        var viewport = colorBuffer.GetViewportRect();
        var depthView = depthStencilBuffer is null
            ? (RenderTargetView?)null
            : readOnlyDepth
                ? depthStencilBuffer.GetReadOnlyDepthView()
                : depthStencilBuffer.GetView();
        graphics.SetRenderTargets(viewport, colorBuffer.GetView(), depthView);
    }

    public void ClearDepthStencil(Graphics graphics, RenderBuffer depthStencilBuffer, ClearTarget flags, float depth, byte stencil = 0)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(depthStencilBuffer);

        graphics.SetRenderTargets(depthStencilBuffer.GetViewportRect(), writeableColorBuffer.GetView(), depthStencilBuffer.GetView());
        graphics.ClearCurrentRenderTargets(
            clearColorTarget: false,
            clearDepth: flags.HasFlag(ClearTarget.Depth),
            clearStencil: flags.HasFlag(ClearTarget.Stencil),
            depth: depth,
            stencil: stencil);
    }

    public void ClearColor(Graphics graphics, RenderBuffer colorBuffer, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(colorBuffer);

        graphics.SetRenderTargets(colorBuffer.GetViewportRect(), colorBuffer.GetView(), depthStencilTarget: null);
        graphics.ClearCurrentRenderTargets(color, clearColorTarget: true, clearDepth: false, clearStencil: false);
    }

    public void ClearOutputRect(Graphics graphics, IntRect viewportRect, ClearTarget flags, Color color, float depth, byte stencil = 0)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        flags &= ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil;
        if (flags == ClearTarget.None)
            return;

        var effectiveViewportRect = viewportRect != IntRect.Zero ? viewportRect : this.viewportRect;
        var depthView = depthStencilBuffer.GetView();
        graphics.SetRenderTargets(effectiveViewportRect, writeableColorBuffer.GetView(), depthView);

        var isFullRect = effectiveViewportRect == this.viewportRect;
        if (isFullRect || !flags.HasFlag(ClearTarget.Color))
        {
            graphics.ClearCurrentRenderTargets(
                color,
                clearColorTarget: flags.HasFlag(ClearTarget.Color),
                clearDepth: flags.HasFlag(ClearTarget.Depth),
                clearStencil: flags.HasFlag(ClearTarget.Stencil),
                depth: depth,
                stencil: stencil);
            return;
        }

        var clearParameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Color"] = new Vector4(color.R, color.G, color.B, color.A),
            ["Depth"] = depth
        };
        graphics.DrawFullscreenQuad(
            "Clear output subregion",
            "v2/X_ClearFramebuffer",
            string.Empty,
            BlendMode.Replace,
            bilinearColorSampler: false,
            albedoTexture: null,
            secondaryTexture: null,
            depthTexture: null,
            clearParameters,
            GetInvOutputSize(),
            GetDefaultClipToUVSpaceOffsetAndScale());

        if (flags.HasFlag(ClearTarget.Stencil))
        {
            graphics.ClearCurrentRenderTargets(
                clearColorTarget: false,
                clearDepth: false,
                clearStencil: true,
                depth: depth,
                stencil: stencil);
        }
    }

    public void ClearOutput(Graphics graphics, ClearTarget flags, Color color, float depth, byte stencil = 0)
    {
        ClearOutputRect(graphics, IntRect.Zero, flags, color, depth, stencil);
    }

    public void ClearOutput(Graphics graphics, Color color, float depth = 1.0f, byte stencil = 0)
    {
        ClearOutput(graphics, ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil, color, depth, stencil);
    }

    public RenderBuffer GetDepthStencilOutput() => depthStencilBuffer;

    public RenderBuffer GetColorOutput() => writeableColorBuffer;

    public Texture? GetDepthStencilTexture() => depthStencilBuffer.GetTexture();

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

    private void ResolveOutputParams(object? renderTarget)
    {
        var fallbackFlags = settings.FilteredColor || frameSettings.FilteredColor
            ? RenderBufferFlag.BilinearFiltering
            : RenderBufferFlag.None;
        var fallbackMultiSample = settings.InheritMultiSampleLevel ? 1 : settings.MultiSampleLevel;
        colorOutputParams = new RenderBufferParams(GetColorTextureFormat(settings.ColorSpace), fallbackMultiSample, fallbackFlags);
        depthStencilOutputParams = new RenderBufferParams(TextureFormat.Depth24Stencil8, fallbackMultiSample);
        linearColorSpace = settings.ColorSpace is not RenderPipelineColorSpace.GammaLDR;
        hdr = settings.ColorSpace == RenderPipelineColorSpace.LinearHDR;

        if (!TryGetRenderTargetTexture(renderTarget, out var texture, out var multiSample))
            return;

        var flags = texture.GetFilterMode() is TextureFilterMode.Bilinear or TextureFilterMode.Trilinear or TextureFilterMode.Anisotropic
            ? RenderBufferFlag.BilinearFiltering
            : RenderBufferFlag.None;

        colorOutputParams = new RenderBufferParams(texture.Format, multiSample, flags);
        linearColorSpace = texture.GetSRGB() || texture.GetLinear() || texture.Format == TextureFormat.RGBA16F;
        hdr = texture.Format == TextureFormat.RGBA16F;
    }

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

    private static bool TryGetRenderTargetTexture(object? renderTarget, out Texture texture, out int multiSample)
    {
        switch (renderTarget)
        {
        case Texture typedTexture:
            texture = typedTexture;
            multiSample = 1;
            return true;

        case RenderTargetView { RenderTexture: Texture typedTexture } typedView:
            texture = typedTexture;
            multiSample = Math.Max(typedView.GetMultiSample(), 1);
            return true;

        case TextureRenderBuffer typedBuffer:
            texture = typedBuffer.Texture;
            multiSample = 1;
            return true;

        default:
            texture = null!;
            multiSample = 1;
            return false;
        }
    }

    private static Vector4 GetClipToUvOffsetAndScale(int width, int height, IntRect sourceRect)
    {
        var safeWidth = Math.Max(width, 1);
        var safeHeight = Math.Max(height, 1);
        var left = sourceRect.Left / (float)safeWidth;
        var right = sourceRect.Right / (float)safeWidth;
        var top = sourceRect.Top / (float)safeHeight;
        var bottom = sourceRect.Bottom / (float)safeHeight;
        return new Vector4(
            (left + right) * 0.5f,
            (top + bottom) * 0.5f,
            (right - left) * 0.5f,
            (bottom - top) * 0.5f);
    }
}
