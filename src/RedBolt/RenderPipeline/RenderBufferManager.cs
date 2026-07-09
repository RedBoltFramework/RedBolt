namespace RedBolt;

public sealed class RenderBufferManager
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyShaderParameters = new Dictionary<string, object?>(0, StringComparer.Ordinal);
    private readonly List<WeakReference<TextureRenderBuffer>> managedTextureBuffers = [];
    private readonly Action<Texture, TextureFormat> applyColorTextureMetadata;

    private readonly struct ClearFramebufferParameters
    {
        [ShaderParameterName("Color")]
        public Vector4 Color { get; init; }

        [ShaderParameterName("Depth")]
        public float Depth { get; init; }
    }

    private sealed class ViewportOutputRenderBuffer : RenderBuffer
    {
        private RenderTargetView? targetView;
        private Texture? texture;
        private IntRect viewportRect;

        public ViewportOutputRenderBuffer(RenderTargetView targetView)
        {
            this.targetView = targetView;
        }

        public void SetOutput(RenderTargetView? targetView, Texture? texture)
        {
            this.targetView = targetView;
            this.texture = texture;
        }

        public void SetViewportRect(IntRect rect)
        {
            viewportRect = rect;
        }

        public override Texture? GetTexture() => texture;

        public override RenderTargetView GetView(uint slice = 0u)
        {
            return targetView ?? throw new InvalidOperationException("Viewport output render buffer has no bound render target view.");
        }

        public override RenderTargetView GetReadOnlyDepthView(uint slice = 0u)
        {
            return GetView(slice);
        }

        public override IntRect GetViewportRect() => viewportRect;
    }

    private readonly ViewportOutputRenderBuffer viewportColorBuffer = new(RenderTargetView.SwapChainColor());
    private readonly ViewportOutputRenderBuffer viewportDepthBuffer = new(RenderTargetView.SwapChainDepthStencil());
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
    private bool flipColorBuffersNextTime;
    private bool linearColorSpace;
    private bool hdr;
    private bool viewportTargetLinearColorSpace;
    private Graphics? lastGraphics;

    public RenderBufferManager()
    {
        applyColorTextureMetadata = ApplyColorTextureMetadata;
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
    }

    public void OnViewportDefined(RenderOutputTargetInfo? renderTarget, IntRect viewportRect)
    {
        this.viewportRect = viewportRect;
        viewportColorBuffer.SetViewportRect(viewportRect);
        viewportDepthBuffer.SetViewportRect(viewportRect);
        viewportColorBuffer.SetOutput(renderTarget?.ColorTarget ?? RenderTargetView.SwapChainColor(), renderTarget?.ColorTexture);
        viewportDepthBuffer.SetOutput(
            renderTarget is null ? RenderTargetView.SwapChainDepthStencil() : renderTarget.DepthStencilTarget,
            renderTarget?.DepthStencilTexture);
        viewportTargetLinearColorSpace = renderTarget?.LinearColorSpace ?? false;

        ResolveOutputParams(renderTarget);
        RefreshManagedTextureBuffers();

        var needReadableDepth = settings.ReadableDepth || frameSettings.ReadableDepth;
        var needSecondaryBuffer = frameSettings.SupportColorReadWrite;
        var needSimpleTexture = frameSettings.ReadableColor || needReadableDepth || settings.ColorUsableWithMultipleRenderTargets;
        var isFullRectOutput = renderTarget is null || renderTarget.FullViewportRect == viewportRect;
        var isSimpleTextureOutput = renderTarget?.ColorTexture is not null && isFullRectOutput;
        var outputMultiSample = renderTarget?.MultiSample ?? colorOutputParams.MultiSampleLevel;
        var outputHasStencil = renderTarget?.HasStencil ?? true;
        var outputHasReadableDepth = renderTarget?.HasReadableDepth ?? false;
        var isColorFormatMatching = (renderTarget?.ColorFormat ?? colorOutputParams.TextureFormat) == colorOutputParams.TextureFormat;
        var isMultiSampleMatching = outputMultiSample == colorOutputParams.MultiSampleLevel;
        var isFilterMatching = (renderTarget?.BilinearFiltered ?? false)
            == ((colorOutputParams.Flags & RenderBufferFlag.BilinearFiltering) != 0);
        var isLinearColorSpaceMatching = (renderTarget?.LinearColorSpace ?? false) == linearColorSpace;
        var isColorUsageMatching = isSimpleTextureOutput || !needSimpleTexture;
        var isOutputMatching = isColorFormatMatching && isMultiSampleMatching && isFilterMatching
            && isLinearColorSpaceMatching && isColorUsageMatching;
        var outputHasUsableDepthTexture = renderTarget?.DepthStencilTexture is not null;

        var needSubstituteDepthBuffer = !isMultiSampleMatching || !(renderTarget?.HasDepthStencil ?? true)
            || ((needSecondaryBuffer || !isOutputMatching) && !outputHasUsableDepthTexture)
            || (needReadableDepth && (!outputHasReadableDepth || !isSimpleTextureOutput))
            || (settings.StencilBuffer && !outputHasStencil);
        var needSubstitutePrimaryBuffer = !isOutputMatching || needSubstituteDepthBuffer;

        depthStencilBuffer = needSubstituteDepthBuffer || needReadableDepth && !outputHasReadableDepth
            ? EnsureDepthBuffer()
            : viewportDepthBuffer;
        writeableColorBuffer = needSubstitutePrimaryBuffer ? EnsurePrimaryColorBuffer() : viewportColorBuffer;
        readableColorBuffer = needSecondaryBuffer ? EnsureSecondaryColorBuffer() : null;

        if (flipColorBuffersNextTime && readableColorBuffer is not null)
        {
            var previousWriteable = writeableColorBuffer;
            writeableColorBuffer = readableColorBuffer;
            readableColorBuffer = previousWriteable;
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
        var buffer = new TextureRenderBuffer(bufferParams, size);
        RegisterTextureBuffer(buffer);
        buffer.RefreshForViewport(viewportRect, applyColorTextureMetadata);
        return buffer;
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
        DrawTexture(lastGraphics, "Synchronize readable and writeable color buffers", readableTexture);
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

        var destinationLinearColorSpace = GetDestinationLinearColorSpace(destinationSurface);
        graphics.SetRenderTargets(destinationRect, destinationSurface, depthStencilTarget: null);
        DrawTextureRegion(graphics, debugComment, sourceTexture, sourceRect, mode, flipVertical, destinationLinearColorSpace);
    }

    public void DrawTextureRegion(Graphics graphics, string debugComment, Texture sourceTexture, IntRect sourceRect,
        ColorSpaceTransition mode = ColorSpaceTransition.None, bool flipVertical = false)
    {
        DrawTextureRegion(graphics, debugComment, sourceTexture, sourceRect, mode, flipVertical, linearColorSpace);
    }

    private void DrawTextureRegion(Graphics graphics, string debugComment, Texture sourceTexture, IntRect sourceRect,
        ColorSpaceTransition mode, bool flipVertical, bool destinationLinearColorSpace)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(sourceTexture);
        ArgumentException.ThrowIfNullOrWhiteSpace(debugComment);

        var effectiveSourceRect = sourceRect != IntRect.Zero
            ? sourceRect
            : new IntRect(0, 0, sourceTexture.Width, sourceTexture.Height);
        var shaderDefines = GetCopyFramebufferShaderDefines(sourceTexture, mode, destinationLinearColorSpace);

        var invInputSize = new Vector2(
            sourceTexture.Width > 0 ? 1.0f / sourceTexture.Width : 0.0f,
            sourceTexture.Height > 0 ? 1.0f / sourceTexture.Height : 0.0f);
        graphics.DrawFullscreenQuad(
            debugComment,
            "X_CopyFramebuffer",
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

        var viewportColorTarget = viewportColorBuffer.GetView();
        CopyTextureRegion(
            graphics,
            "Copy final color to output RenderSurface",
            colorTexture,
            new IntRect(0, 0, colorTexture.Width, colorTexture.Height),
            viewportColorTarget,
            viewportColorBuffer.GetViewportRect(),
            ColorSpaceTransition.Automatic);

        if (ReferenceEquals(viewportColorBuffer, readableColorBuffer))
            flipColorBuffersNextTime = !flipColorBuffersNextTime;
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

        graphics.SetRenderTargets(depthStencilBuffer.GetViewportRect(), colorTarget: null, depthStencilBuffer.GetView());
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

        if (!ShouldUseFullscreenQuadClear(effectiveViewportRect, this.viewportRect))
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

        var clearParameters = new ClearFramebufferParameters
        {
            Color = new Vector4(color.R, color.G, color.B, color.A),
            Depth = GetFullscreenQuadClearDepth(depth)
        };
        graphics.DrawClearFramebufferQuad(
            "Clear output subregion",
            flags,
            clearParameters,
            GetInvOutputSize(),
            GetDefaultClipToUVSpaceOffsetAndScale(),
            stencil);
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

    private void ResolveOutputParams(RenderOutputTargetInfo? renderTarget)
    {
        var fallbackFlags = settings.FilteredColor
            ? RenderBufferFlag.BilinearFiltering
            : RenderBufferFlag.None;
        var fallbackMultiSample = settings.InheritMultiSampleLevel ? 1 : settings.MultiSampleLevel;
        var fallbackOutputColorFormat = renderTarget?.ColorFormat ?? TextureFormat.RGBA8;
        var nextColorOutputParams = new RenderBufferParams(
            GetColorTextureFormat(settings.ColorSpace, fallbackOutputColorFormat),
            fallbackMultiSample,
            fallbackFlags);
        var nextDepthStencilOutputParams = new RenderBufferParams(TextureFormat.Depth24Stencil8, fallbackMultiSample | 0, RenderBufferFlag.Persistent);
        var nextLinearColorSpace = IsManagedOutputLinearColorSpace(settings.ColorSpace, nextColorOutputParams.TextureFormat, viewportTargetLinearColorSpace);
        var nextHdr = TextureFormatUtility.IsFloatColorFormat(nextColorOutputParams.TextureFormat);

        if (renderTarget is not null)
        {
            var multiSample = settings.InheritMultiSampleLevel ? renderTarget.MultiSample : settings.MultiSampleLevel;
            var flags = renderTarget.BilinearFiltered || settings.FilteredColor
                ? RenderBufferFlag.BilinearFiltering
                : RenderBufferFlag.None;
            var outputColorFormat = renderTarget.ColorFormat == TextureFormat.Unknown
                ? nextColorOutputParams.TextureFormat
                : renderTarget.ColorFormat;
            var effectiveColorFormat = GetColorTextureFormat(settings.ColorSpace, outputColorFormat);

            nextColorOutputParams = new RenderBufferParams(
                effectiveColorFormat,
                Math.Max(multiSample, 1),
                flags);
            nextDepthStencilOutputParams = new RenderBufferParams(
                renderTarget.DepthStencilFormat == TextureFormat.Unknown ? TextureFormat.Depth24Stencil8 : renderTarget.DepthStencilFormat,
                Math.Max(multiSample, 1),
                RenderBufferFlag.Persistent);
            nextLinearColorSpace = IsManagedOutputLinearColorSpace(settings.ColorSpace, effectiveColorFormat, renderTarget.LinearColorSpace);
            nextHdr = TextureFormatUtility.IsFloatColorFormat(effectiveColorFormat);
        }

        if (colorOutputParams != nextColorOutputParams || depthStencilOutputParams != nextDepthStencilOutputParams)
        {
            colorOutputParams = nextColorOutputParams;
            depthStencilOutputParams = nextDepthStencilOutputParams;
            ResetCachedRenderBuffers();
        }

        linearColorSpace = nextLinearColorSpace;
        hdr = nextHdr;
    }

    private RenderBuffer EnsurePrimaryColorBuffer()
    {
        if (primaryColorBuffer is null)
        {
            primaryColorBuffer = CreateColorBuffer(colorOutputParams) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Primary color buffer creation returned unexpected render buffer type.");
        }

        primaryColorBuffer.RefreshForViewport(viewportRect, applyColorTextureMetadata);
        return primaryColorBuffer;
    }

    private RenderBuffer EnsureDepthBuffer()
    {
        if (depthBuffer is null)
        {
            depthBuffer = new TextureRenderBuffer(depthStencilOutputParams);
            RegisterTextureBuffer(depthBuffer);
        }

        depthBuffer.RefreshForViewport(viewportRect);
        return depthBuffer;
    }

    private RenderBuffer EnsureSecondaryColorBuffer()
    {
        if (secondaryColorBuffer is null)
        {
            secondaryColorBuffer = CreateColorBuffer(colorOutputParams) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Color buffer creation returned unexpected render buffer type.");
        }

        secondaryColorBuffer.RefreshForViewport(viewportRect, applyColorTextureMetadata);
        return secondaryColorBuffer;
    }

    private static TextureFormat GetColorTextureFormat(RenderPipelineColorSpace colorSpace, TextureFormat outputTextureFormat)
    {
        return colorSpace switch
        {
            RenderPipelineColorSpace.LinearHDR => TextureFormat.RGBA16F,
            RenderPipelineColorSpace.LinearLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.GammaLDR => TextureFormat.RGBA8,
            RenderPipelineColorSpace.Optimized => outputTextureFormat,
            _ => TextureFormat.RGBA8
        };
    }

    private static bool IsManagedOutputLinearColorSpace(RenderPipelineColorSpace colorSpace, TextureFormat textureFormat,
        bool outputLinearColorSpace)
    {
        return colorSpace switch
        {
            RenderPipelineColorSpace.LinearHDR => true,
            RenderPipelineColorSpace.LinearLDR => true,
            RenderPipelineColorSpace.Optimized => outputLinearColorSpace || TextureFormatUtility.IsFloatColorFormat(textureFormat),
            _ => false
        };
    }

    private void ApplyColorTextureMetadata(Texture texture, TextureFormat textureFormat)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var useLinearMetadata = hdr || TextureFormatUtility.IsFloatColorFormat(textureFormat);
        var useSrgbMetadata = linearColorSpace && !useLinearMetadata && textureFormat == TextureFormat.RGBA8;
        texture.SetLinear(useLinearMetadata);
        texture.SetSRGB(useSrgbMetadata);
    }

    private void ResetCachedRenderBuffers()
    {
        primaryColorBuffer = null;
        secondaryColorBuffer = null;
        depthBuffer = null;
    }

    private void RefreshManagedTextureBuffers()
    {
        for (var i = managedTextureBuffers.Count - 1; i >= 0; --i)
        {
            if (!managedTextureBuffers[i].TryGetTarget(out var buffer))
            {
                managedTextureBuffers.RemoveAt(i);
                continue;
            }

            if (!buffer.IsEnabled)
                continue;

            buffer.RefreshForViewport(viewportRect);
            if (buffer.BufferParams.TextureFormat is TextureFormat.Depth32 or TextureFormat.Depth24Stencil8)
                continue;

            ApplyColorTextureMetadata(buffer.Texture, buffer.Texture.Format);
        }
    }

    private void RegisterTextureBuffer(TextureRenderBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        managedTextureBuffers.Add(new WeakReference<TextureRenderBuffer>(buffer));
    }

    private bool GetDestinationLinearColorSpace(RenderTargetView destinationSurface)
    {
        return destinationSurface.Type switch
        {
            RenderTargetViewType.SwapChainColor => viewportTargetLinearColorSpace,
            RenderTargetViewType.Resource or RenderTargetViewType.ResourceSlice => destinationSurface.RenderTexture is not null
                && IsTextureLinearColorSpace(destinationSurface.RenderTexture),
            _ => false
        };
    }

    private static string GetCopyFramebufferShaderDefines(Texture sourceTexture, ColorSpaceTransition mode,
        bool destinationLinearColorSpace)
    {
        if (mode != ColorSpaceTransition.Automatic)
            return string.Empty;

        var sourceIsLinear = IsTextureLinearColorSpace(sourceTexture);
        if (sourceIsLinear == destinationLinearColorSpace)
            return string.Empty;

        return destinationLinearColorSpace ? "URHO3D_GAMMA_TO_LINEAR" : "URHO3D_LINEAR_TO_GAMMA";
    }

    private static bool IsTextureLinearColorSpace(Texture texture)
    {
        return texture.GetLinear() || texture.GetSRGB() || TextureFormatUtility.IsFloatColorFormat(texture.Format);
    }

    // Fullscreen-quad subrect clears run in the current managed OpenGL backend and therefore
    // need the same [0, 1] -> [-1, 1] depth remap that rbfx applies before drawing the clear quad.
    private static float GetFullscreenQuadClearDepth(float depth) => depth * 2.0f - 1.0f;

    private static bool ShouldUseFullscreenQuadClear(IntRect viewportRect, IntRect outputViewportRect) => viewportRect != outputViewportRect;

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
