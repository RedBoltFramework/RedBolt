using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Class that manages all render buffers within viewport and viewport itself.
/// </summary>
public sealed class RenderBufferManager
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyShaderParameters = new Dictionary<string, object?>(0, StringComparer.Ordinal);
    private readonly List<WeakReference<TextureRenderBuffer>> _managedTextureBuffers = [];
    private readonly Action<Texture, TextureFormat> _applyColorTextureMetadata;
    private readonly Graphics? _graphics;
    private readonly Renderer? _renderer;
    private readonly RenderDevice? _renderDevice;
    private readonly RenderContext? _renderContext;
    private readonly DrawCommandQueue? _drawQueue;
    private readonly StaticPipelineStateCache _pipelineStates = new();
    private StaticPipelineStateId _copyTexturePipelineState;
    private StaticPipelineStateId _copyGammaToLinearTexturePipelineState;
    private StaticPipelineStateId _copyLinearToGammaTexturePipelineState;

    private readonly struct ClearFramebufferParameters
    {
        [ShaderParameterName("Color")]
        public Vector4f Color { get; init; }

        [ShaderParameterName("Depth")]
        public float Depth { get; init; }
    }

    private readonly ViewportColorRenderBuffer _viewportColorBuffer = new();
    private readonly ViewportDepthStencilRenderBuffer _viewportDepthBuffer = new();
    private readonly RenderTargetView[] _renderTargetViews = new RenderTargetView[RenderApiLimits.MaxRenderTargets];
    private readonly ShaderResourceDesc[] _copyShaderResources = new ShaderResourceDesc[1];
    private TextureRenderBuffer? _primaryColorBuffer;
    private TextureRenderBuffer? _secondaryColorBuffer;
    private TextureRenderBuffer? _depthBuffer;
    private RenderBufferManagerSettings _settings = new();
    private RenderBufferManagerFrameSettings _frameSettings = new();
    private RenderBufferParams _colorOutputParams = new(TextureFormat.RGBA8);
    private RenderBufferParams _depthStencilOutputParams = new(TextureFormat.Unknown);
    private IntRect _viewportRect;
    private RenderBuffer _depthStencilBuffer;
    private RenderBuffer _writeableColorBuffer;
    private RenderBuffer? _readableColorBuffer;
    private bool _flipColorBuffersNextTime;
    private bool _linearColorSpace;
    private bool _hdr;
    private bool _viewportTargetLinearColorSpace;
    private float _timeStep;
    private Graphics? _lastGraphics;
    private readonly RenderPipelineDebugger? _debugger;

    public RenderBufferManager(IRenderPipelineInterface? renderPipeline = null)
    {
        _graphics = Context.Instance.GetSubsystem<Graphics>();
        _renderer = Context.Instance.GetSubsystem<Renderer>();
        _renderDevice = Context.Instance.GetSubsystem<RenderDevice>();
        _renderContext = _renderDevice?.GetRenderContext();
        _drawQueue = _renderDevice?.GetDefaultQueue();
        _debugger = renderPipeline?.GetDebugger();
        _applyColorTextureMetadata = ApplyColorTextureMetadata;
        _depthStencilBuffer = _viewportDepthBuffer;
        _writeableColorBuffer = _viewportColorBuffer;
    }

    public void SetSettings(RenderBufferManagerSettings settings)
    {
        this._settings = settings;
    }

    public void SetFrameSettings(RenderBufferManagerFrameSettings frameSettings)
    {
        this._frameSettings = frameSettings;
        this._frameSettings.ReadableColor |= this._frameSettings.SupportColorReadWrite;
    }

    public void OnViewportDefined(RenderOutputTargetInfo? renderTarget, IntRect viewportRect)
    {
        var graphics = _lastGraphics ?? Context.Instance.GetSubsystem<Graphics>()!;
        var renderDevice = graphics.GetSubsystem<RenderDevice>();
        var colorTarget = renderTarget?.ColorTarget
            ?? RenderTargetView.SwapChainColor(renderDevice!);
        var depthTarget = renderTarget?.DepthStencilTarget
            ?? RenderTargetView.SwapChainDepthStencil(renderDevice!);
        this._viewportRect = viewportRect;
        _viewportColorBuffer.SetOutput(colorTarget, renderTarget?.ColorTexture, renderDevice, viewportRect);
        _viewportDepthBuffer.SetOutput(depthTarget, renderTarget?.DepthStencilTexture, renderDevice, viewportRect);
        _viewportTargetLinearColorSpace = renderTarget?.LinearColorSpace
            ?? graphics.GetSwapChainOutputTarget().LinearColorSpace;

        ResolveOutputParams(renderTarget);
        RefreshManagedTextureBuffers();

        var needReadableDepth = _settings.ReadableDepth || _frameSettings.ReadableDepth;
        var needSecondaryBuffer = _frameSettings.SupportColorReadWrite;
        var needSimpleTexture = _frameSettings.ReadableColor || needReadableDepth || _settings.ColorUsableWithMultipleRenderTargets;
        var isFullRectOutput = renderTarget is null || renderTarget.FullViewportRect == viewportRect;
        var isSimpleTextureOutput = renderTarget?.ColorTexture is not null && isFullRectOutput;
        var outputMultiSample = renderTarget?.MultiSample ?? _colorOutputParams.MultiSampleLevel;
        var outputHasStencil = renderTarget?.HasStencil ?? true;
        var outputHasReadableDepth = renderTarget?.HasReadableDepth ?? false;
        var isColorFormatMatching = (renderTarget?.ColorFormat ?? _colorOutputParams.TextureFormat) == _colorOutputParams.TextureFormat;
        var isMultiSampleMatching = outputMultiSample == _colorOutputParams.MultiSampleLevel;
        var isFilterMatching = (renderTarget?.BilinearFiltered ?? false)
            == ((_colorOutputParams.Flags & RenderBufferFlag.BilinearFiltering) != 0);
        var isLinearColorSpaceMatching = (renderTarget?.LinearColorSpace ?? _viewportTargetLinearColorSpace) == _linearColorSpace;
        var isColorUsageMatching = isSimpleTextureOutput || !needSimpleTexture;
        var isOutputMatching = isColorFormatMatching && isMultiSampleMatching && isFilterMatching
            && isLinearColorSpaceMatching && isColorUsageMatching;
        var outputHasUsableDepthTexture = renderTarget?.DepthStencilTexture is not null;

        var needSubstituteDepthBuffer = !isMultiSampleMatching || !(renderTarget?.HasDepthStencil ?? true)
            || ((needSecondaryBuffer || !isOutputMatching) && !outputHasUsableDepthTexture)
            || (needReadableDepth && (!outputHasReadableDepth || !isSimpleTextureOutput))
            || (_settings.StencilBuffer && !outputHasStencil);
        var needSubstitutePrimaryBuffer = !isOutputMatching || needSubstituteDepthBuffer;

        _depthStencilBuffer = needSubstituteDepthBuffer || needReadableDepth && !outputHasReadableDepth
            ? EnsureDepthBuffer()
            : _viewportDepthBuffer;
        _writeableColorBuffer = needSubstitutePrimaryBuffer ? EnsurePrimaryColorBuffer() : _viewportColorBuffer;
        _readableColorBuffer = needSecondaryBuffer ? EnsureSecondaryColorBuffer() : null;

        if (_flipColorBuffersNextTime && _readableColorBuffer is not null)
        {
            var previousWriteable = _writeableColorBuffer;
            _writeableColorBuffer = _readableColorBuffer;
            _readableColorBuffer = previousWriteable;
        }
    }

    public void OnRenderBegin(CommonFrameInfo frameInfo, Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(frameInfo);
        ArgumentNullException.ThrowIfNull(graphics);
        _lastGraphics = graphics;
        _timeStep = frameInfo.TimeStep;
        if (_copyTexturePipelineState == StaticPipelineStateId.Invalid)
            InitializePipelineStates();
        OnViewportDefined(frameInfo.RenderTarget, frameInfo.ViewportRect);
    }

    /// <summary>
    /// Create pipeline states for fullscreen quad rendering.
    /// </summary>
    public StaticPipelineStateId CreateQuadPipelineState(GraphicsPipelineStateDesc description)
    {
        var quadGeometry = _renderer?.GetQuadGeometry();
        if (quadGeometry is null)
            return StaticPipelineStateId.Invalid;

        GraphicsUtils.InitializeInputLayoutAndPrimitiveType(description, quadGeometry);
        description.ColorWriteEnabled = true;

        return _pipelineStates.CreateState(description);
    }

    public StaticPipelineStateId CreateQuadPipelineState(BlendMode blendMode, string shaderName,
        string shaderDefines, IReadOnlyList<NamedSamplerStateDesc> samplers)
    {
        var defines = shaderDefines + " URHO3D_GEOMETRY_STATIC";

        var description = new GraphicsPipelineStateDesc
        {
            DebugName = $"Quad with {shaderName}({defines})",
            BlendMode = blendMode,
            VertexShader = _graphics?.GetShader(ShaderType.VS, shaderName, defines),
            PixelShader = _graphics?.GetShader(ShaderType.PS, shaderName, defines)
        };

        foreach (var sampler in samplers)
            description.Samplers.Add(sampler.Name, sampler.Desc);

        return CreateQuadPipelineState(description);
    }

    /// <summary>
    /// Return pipeline state for fullscreen quad rendering for current output.
    /// </summary>
    public PipelineState? GetQuadPipelineState(StaticPipelineStateId id)
    {
        return _renderContext is null
            ? null
            : _pipelineStates.GetState(id, _renderContext.GetCurrentRenderTargetsDesc());
    }

    /// <summary>
    /// Render fullscreen quad with custom parameters into currently bound render buffer.
    /// </summary>
    public void DrawQuad(string debugComment, DrawQuadParams parameters, bool flipVertical = false)
    {
        var pipelineState = parameters.PipelineState ?? GetQuadPipelineState(parameters.PipelineStateId);
        if (pipelineState is null || !pipelineState.IsValid || _renderContext is null || _drawQueue is null)
            return;

        using var renderScope = new RenderScope(_renderContext, debugComment);

        var quadGeometry = _renderer?.GetQuadGeometry();
        if (quadGeometry is null)
            return;
        var modelMatrix = Matrix3x4.Identity;
        var projection = Matrix4.Identity;
        if (flipVertical)
            projection.M11 = -1.0f;

        var isOpenGL = _renderDevice?.GetBackend() == RenderBackend.OpenGL;
        modelMatrix.M23 = isOpenGL ? 0.0f : 0.5f;

        _drawQueue.Reset();
        _drawQueue.SetPipelineState(pipelineState);

        if (_drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Frame))
        {
            _drawQueue.AddShaderParameter(ShaderConsts.Frame_DeltaTime, _timeStep);
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Frame);
        }

        if (_drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Camera))
        {
            _drawQueue.AddShaderParameter(ShaderConsts.Camera_GBufferOffsets, parameters.ClipToUVOffsetAndScale);
            _drawQueue.AddShaderParameter(ShaderConsts.Camera_GBufferInvSize, parameters.InvInputSize);
            _drawQueue.AddShaderParameter(ShaderConsts.Camera_ViewProj, projection);
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Camera);
        }

        if (_drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Object))
        {
            _drawQueue.AddShaderParameter(ShaderConsts.Object_Model, modelMatrix);
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Object);
        }

        if (_drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Custom))
        {
            foreach (var shaderParameter in parameters.Parameters)
                _drawQueue.AddShaderParameter(shaderParameter.Name, shaderParameter.Value);
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Custom);
        }

        if (parameters.BindSecondaryColorToDiffuse)
        {
            var secondaryColor = GetSecondaryColorTexture()?.GetRawTexture();
            if (secondaryColor is not null)
                _drawQueue.AddShaderResource(ShaderResources.Albedo, secondaryColor);
        }
        foreach (var shaderResource in parameters.Resources)
        {
            var canBind = !parameters.BindSecondaryColorToDiffuse || shaderResource.Name != ShaderResources.Albedo;
            var rawTexture = shaderResource.Texture?.GetRawTexture();
            if (canBind && rawTexture is not null)
                _drawQueue.AddShaderResource(shaderResource.Name, rawTexture);
        }
        _drawQueue.CommitShaderResources();

        GraphicsUtils.SetBuffersFromGeometry(_drawQueue, quadGeometry);
        _drawQueue.DrawIndexed(quadGeometry.GetIndexStart(), quadGeometry.GetIndexCount());

        _renderContext.Execute(_drawQueue);

        if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger!.ReportQuad(debugComment, _renderContext.GetCurrentViewport().Size);
    }

    public void DrawViewportQuad(string debugComment, StaticPipelineStateId pipelineStateId,
        IReadOnlyList<ShaderResourceDesc> resources, IReadOnlyList<ShaderParameterDesc> parameters,
        bool flipVertical = false)
    {
        var drawParams = new DrawQuadParams
        {
            PipelineStateId = pipelineStateId,
            ClipToUVOffsetAndScale = GetDefaultClipToUVSpaceOffsetAndScale(),
            InvInputSize = GetInvOutputSize(),
            Resources = resources,
            Parameters = parameters
        };
        DrawQuad(debugComment, drawParams, flipVertical);
    }

    public void DrawFeedbackViewportQuad(string debugComment, StaticPipelineStateId pipelineStateId,
        IReadOnlyList<ShaderResourceDesc> resources, IReadOnlyList<ShaderParameterDesc> parameters,
        bool flipVertical = false)
    {
        var drawParams = new DrawQuadParams
        {
            PipelineStateId = pipelineStateId,
            ClipToUVOffsetAndScale = GetDefaultClipToUVSpaceOffsetAndScale(),
            InvInputSize = GetInvOutputSize(),
            BindSecondaryColorToDiffuse = true,
            Resources = resources,
            Parameters = parameters
        };
        DrawQuad(debugComment, drawParams, flipVertical);
    }

    public void OnRenderEnd(CommonFrameInfo frameInfo)
    {
        ArgumentNullException.ThrowIfNull(frameInfo);

        if (_lastGraphics is not null)
            ResolveToViewport(_lastGraphics);

        _viewportColorBuffer.OnRenderEnd(frameInfo);
        _viewportDepthBuffer.OnRenderEnd(frameInfo);
    }

    public SharedRenderPassState PopulateSharedState(SharedRenderPassState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.RenderBufferManager = this;
        state.RenderBuffers["Viewport.Color"] = _writeableColorBuffer;
        state.RenderBuffers["Viewport.Depth"] = _depthStencilBuffer;
        if (_readableColorBuffer is not null)
            state.RenderBuffers["Viewport.Color.Readable"] = _readableColorBuffer;
        return state;
    }

    public TextureRenderBuffer CreateColorBuffer(RenderBufferParams bufferParams, Vector2f size = default)
    {
        var buffer = new TextureRenderBuffer(bufferParams, size);
        RegisterTextureBuffer(buffer);
        buffer.RefreshForViewport(_viewportRect, _applyColorTextureMetadata);
        return buffer;
    }

    /// <summary>
    /// Swap color buffers used to render to and to read from.
    /// Invalid if SupportOutputColorReadWrite is not requested.
    /// </summary>
    public void SwapColorBuffers(bool synchronizeContents)
    {
        if (!_frameSettings.SupportColorReadWrite)
            throw new InvalidOperationException("Cannot swap color buffers when color read/write is not enabled for the frame.");

        _readableColorBuffer ??= EnsureSecondaryColorBuffer();
        Debug.Assert(_readableColorBuffer is not null && _writeableColorBuffer is not null);
        // ea::swap(writeableColorBuffer_, readableColorBuffer_);
        var previousWriteable = _writeableColorBuffer;
        _writeableColorBuffer = _readableColorBuffer;
        _readableColorBuffer = previousWriteable;

        if (!synchronizeContents || _lastGraphics is null)
            return;

        var readableTexture = _readableColorBuffer.GetTexture();
        if (readableTexture is null)
            return;

        SetRenderTargets(_lastGraphics, _writeableColorBuffer, _depthStencilBuffer);
        DrawTexture(_lastGraphics, "Synchronize readable and writeable color buffers", readableTexture);
    }

    /// <summary>
    /// Bind the currently writable color buffer and matching depth-stencil buffer as render outputs.
    /// </summary>
    public void SetOutputRenderTargets(Graphics graphics, bool readOnlyDepth = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        _lastGraphics = graphics;

        var depthView = readOnlyDepth ? _depthStencilBuffer.GetReadOnlyDepthView() : _depthStencilBuffer.GetView();
        var viewportRect = _depthStencilBuffer.GetViewportRect();
        if (viewportRect == IntRect.Zero)
            viewportRect = _writeableColorBuffer.GetViewportRect();
        graphics.SetRenderTargets(viewportRect, _writeableColorBuffer.GetView(), depthView);
    }

    public void SetOutputRenderTargetsRect(Graphics graphics, IntRect viewportRect, bool readOnlyDepth = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        _lastGraphics = graphics;

        var depthView = readOnlyDepth ? _depthStencilBuffer.GetReadOnlyDepthView() : _depthStencilBuffer.GetView();
        graphics.SetRenderTargets(
            viewportRect == IntRect.Zero ? _viewportRect : viewportRect,
            _writeableColorBuffer.GetView(), depthView);
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
        if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger!.ReportQuad(debugComment ?? shaderName, graphics.GetCurrentRenderTargetSize());
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
        if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger!.ReportQuad(debugComment ?? shaderName, graphics.GetCurrentRenderTargetSize());
    }

    public void CopyTextureRegion(Graphics graphics, string debugComment, Texture sourceTexture, IntRect sourceRect,
        RenderTargetView destinationSurface, IntRect destinationRect, ColorSpaceTransition mode = ColorSpaceTransition.None,
        bool flipVertical = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(sourceTexture);

        var destinationLinearColorSpace = GetDestinationLinearColorSpace(destinationSurface);
        if (_renderContext is null)
            return;
        _renderContext.SetRenderTargets(default, [destinationSurface]);
        _renderContext.SetViewport(destinationRect);
        DrawTextureRegion(graphics, debugComment, sourceTexture, sourceRect, mode, flipVertical, destinationLinearColorSpace);
    }

    /// <summary>
    /// Draw region of input texture into currently bound render buffer. sRGB is taken into account.
    /// </summary>
    public void DrawTextureRegion(Graphics graphics, string debugComment, Texture sourceTexture, IntRect sourceRect,
        ColorSpaceTransition mode = ColorSpaceTransition.None, bool flipVertical = false)
    {
        DrawTextureRegion(graphics, debugComment, sourceTexture, sourceRect, mode, flipVertical, _linearColorSpace);
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
        var invInputSize = new Vector2f(
            sourceTexture.Width > 0 ? 1.0f / sourceTexture.Width : 0.0f,
            sourceTexture.Height > 0 ? 1.0f / sourceTexture.Height : 0.0f);
        var shaderDefines = GetCopyFramebufferShaderDefines(sourceTexture, mode, destinationLinearColorSpace);
        var pipelineStateId = shaderDefines switch
        {
            "URHO3D_GAMMA_TO_LINEAR" => _copyGammaToLinearTexturePipelineState,
            "URHO3D_LINEAR_TO_GAMMA" => _copyLinearToGammaTexturePipelineState,
            _ => _copyTexturePipelineState
        };
        _copyShaderResources[0] = new ShaderResourceDesc(ShaderResources.Albedo, sourceTexture);
        var drawParams = new DrawQuadParams
        {
            PipelineStateId = pipelineStateId,
            InvInputSize = invInputSize,
            ClipToUVOffsetAndScale = GetClipToUvOffsetAndScale(
                sourceTexture.Width, sourceTexture.Height, effectiveSourceRect),
            Resources = _copyShaderResources
        };
        DrawQuad(debugComment, drawParams, flipVertical);
    }

    private void InitializePipelineStates()
    {
        NamedSamplerStateDesc[] samplers =
            [new NamedSamplerStateDesc(new StringHash("Albedo"), SamplerStateDesc.Bilinear())];

        _copyTexturePipelineState = CreateQuadPipelineState(
            BlendMode.Replace, "v2/X_CopyFramebuffer", string.Empty, samplers);
        _copyGammaToLinearTexturePipelineState = CreateQuadPipelineState(
            BlendMode.Replace, "v2/X_CopyFramebuffer", "URHO3D_GAMMA_TO_LINEAR", samplers);
        _copyLinearToGammaTexturePipelineState = CreateQuadPipelineState(
            BlendMode.Replace, "v2/X_CopyFramebuffer", "URHO3D_LINEAR_TO_GAMMA", samplers);
    }

    /// <summary>
    /// Draw input texture into currently bound render buffer. sRGB is taken into account.
    /// </summary>
    public void DrawTexture(Graphics graphics, string debugComment, Texture sourceTexture,
        ColorSpaceTransition mode = ColorSpaceTransition.None, bool flipVertical = false)
    {
        DrawTextureRegion(graphics, debugComment, sourceTexture, IntRect.Zero, mode, flipVertical);
    }

    /// <summary>
    /// Return whether the final output must be copied back to the viewport target.
    /// </summary>
    public bool NeedsViewportResolve() => !ReferenceEquals(_writeableColorBuffer, _viewportColorBuffer);

    /// <summary>
    /// Copy the current color output back to the viewport render target.
    /// </summary>
    public void ResolveToViewport(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (!NeedsViewportResolve())
            return;

        var colorTexture = _writeableColorBuffer.GetTexture();
        if (colorTexture is null)
            return;

        var viewportColorTarget = _viewportColorBuffer.GetView();
        CopyTextureRegion(
            graphics,
            "Copy final color to output RenderSurface",
            colorTexture,
            new IntRect(0, 0, colorTexture.Width, colorTexture.Height),
            viewportColorTarget,
            _viewportColorBuffer.GetViewportRect(),
            ColorSpaceTransition.Automatic);

        if (ReferenceEquals(_viewportColorBuffer, _readableColorBuffer))
            _flipColorBuffersNextTime = !_flipColorBuffersNextTime;
    }

    /// <summary>
    /// Bind an explicit render target buffer for custom intermediate passes.
    /// </summary>
    public void SetRenderTargets(Graphics graphics, RenderBuffer? colorBuffer, RenderBuffer? depthStencilBuffer = null, bool readOnlyDepth = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(colorBuffer);
        _lastGraphics = graphics;

        var viewport = colorBuffer.GetViewportRect();
        var depthView = depthStencilBuffer is null
            ? (RenderTargetView?)null
            : readOnlyDepth
                ? depthStencilBuffer.GetReadOnlyDepthView()
                : depthStencilBuffer.GetView();
        graphics.SetRenderTargets(viewport, colorBuffer.GetView(), depthView);
    }

    public void SetRenderTargetsRect(Graphics graphics, IntRect viewportRect, RenderBuffer? depthStencilBuffer,
        IReadOnlyList<RenderBuffer> colorBuffers, bool readOnlyDepth = false, CubeMapFace face = CubeMapFace.PositiveX)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(colorBuffers);
        _lastGraphics = graphics;

        IntRect? defaultViewportRect = null;
        RenderTargetView? depthStencilView = null;
        if (depthStencilBuffer is not null)
        {
            depthStencilView = readOnlyDepth
                ? depthStencilBuffer.GetReadOnlyDepthView((uint)face)
                : depthStencilBuffer.GetView((uint)face);
            defaultViewportRect = depthStencilBuffer.GetViewportRect();
        }

        for (var i = 0; i < colorBuffers.Count; ++i)
        {
            var colorBuffer = colorBuffers[i];
            _renderTargetViews[i] = colorBuffer.GetView((uint)face);
            if (defaultViewportRect is null)
                defaultViewportRect = colorBuffer.GetViewportRect();
        }

        if (defaultViewportRect is null || defaultViewportRect.Value == IntRect.Zero)
        {
            Log.Logger.LogError("Cannot set null render targets");
            return;
        }

        graphics.SetRenderTargets(
            viewportRect == IntRect.Zero ? defaultViewportRect.Value : viewportRect,
            _renderTargetViews.AsSpan(0, colorBuffers.Count), depthStencilView);
    }

    public void SetRenderTargets(Graphics graphics, RenderBuffer? depthStencilBuffer,
        IReadOnlyList<RenderBuffer> colorBuffers, bool readOnlyDepth = false, CubeMapFace face = CubeMapFace.PositiveX)
    {
        SetRenderTargetsRect(graphics, IntRect.Zero, depthStencilBuffer, colorBuffers, readOnlyDepth, face);
    }

    /// <summary>
    /// Clear depth and/or stencil render target(s).
    /// Changes currently bound depth-stencil and render targets.
    /// </summary>
    public void ClearDepthStencil(Graphics graphics, RenderBuffer depthStencilBuffer, ClearTarget flags, float depth,
        byte stencil = 0, CubeMapFace face = CubeMapFace.PositiveX)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(depthStencilBuffer);

        graphics.SetRenderTargets(depthStencilBuffer.GetViewportRect(), colorTarget: null, depthStencilBuffer.GetView((uint)face));
        graphics.ClearCurrentRenderTargets(
            clearColorTarget: false,
            clearDepth: (flags & ClearTarget.Depth) == ClearTarget.Depth,
            clearStencil: (flags & ClearTarget.Stencil) == ClearTarget.Stencil,
            depth: depth,
            stencil: stencil);
    }

    /// <summary>
    /// Clear color render target.
    /// Changes currently bound render targets.
    /// </summary>
    public void ClearColor(Graphics graphics, RenderBuffer colorBuffer, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(colorBuffer);

        graphics.SetRenderTargets(colorBuffer.GetViewportRect(), colorBuffer.GetView(), depthStencilTarget: null);
        graphics.ClearCurrentRenderTargets(color, clearColorTarget: true, clearDepth: false, clearStencil: false);
    }

    /// <summary>
    /// Clears currently writable color output if more than one color buffers are used.
    /// If rectangle is specified, only part of render target is cleared.
    /// Changes currently bound depth-stencil and render targets.
    /// </summary>
    public void ClearOutputRect(Graphics graphics, IntRect viewportRect, ClearTarget flags, Color color, float depth, byte stencil = 0)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        flags &= ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil;
        if (flags == ClearTarget.None)
            return;

        var effectiveViewportRect = viewportRect != IntRect.Zero ? viewportRect : this._viewportRect;
        var depthView = _depthStencilBuffer.GetView();
        graphics.SetRenderTargets(effectiveViewportRect, _writeableColorBuffer.GetView(), depthView);

        if (!ShouldUseFullscreenQuadClear(effectiveViewportRect, this._viewportRect))
        {
            graphics.ClearCurrentRenderTargets(
                color,
                clearColorTarget: (flags & ClearTarget.Color) == ClearTarget.Color,
                clearDepth: (flags & ClearTarget.Depth) == ClearTarget.Depth,
                clearStencil: (flags & ClearTarget.Stencil) == ClearTarget.Stencil,
                depth: depth,
                stencil: stencil);
            return;
        }

        var clearParameters = new ClearFramebufferParameters
        {
            Color = new Vector4f(color.R, color.G, color.B, color.A),
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

    /// <summary>
    /// Return depth-stencil buffer. Stays the same during the frame.
    /// </summary>
    public RenderBuffer GetDepthStencilOutput() => _depthStencilBuffer;

    /// <summary>
    /// Return color render buffer. Changes on SwapColorBuffers.
    /// </summary>
    public RenderBuffer GetColorOutput() => _writeableColorBuffer;

    /// <summary>
    /// Return readable depth texture.
    /// It's not allowed to write depth and read it from shader input simultaneously for same depth-stencil buffer.
    /// </summary>
    public Texture? GetDepthStencilTexture() => _depthStencilBuffer.GetTexture();

    /// <summary>
    /// Return secondary color render buffer texture that can be used while writing to color render buffer.
    /// Texture content is defined at the moment of previous SwapColorBuffers.
    /// Content is undefined if SwapColorBuffers was not called during current frame.
    /// </summary>
    public Texture? GetSecondaryColorTexture() => _readableColorBuffer?.GetTexture();

    /// <summary>
    /// Return size of output region (not size of output texture itself).
    /// </summary>
    public Vector2i GetOutputSize() => _viewportRect.Size;

    public Vector2f GetInvOutputSize()
    {
        var size = GetOutputSize();
        return new Vector2f(
            size.X > 0 ? 1.0f / size.X : 0.0f,
            size.Y > 0 ? 1.0f / size.Y : 0.0f);
    }

    /// <summary>
    /// Return identity offset and scale used to convert clip space to UV space.
    /// </summary>
    public Vector4f GetDefaultClipToUVSpaceOffsetAndScale() => new(0.5f, 0.5f, 0.5f, 0.5f);

    public TextureFormat GetOutputColorFormat() => _colorOutputParams.TextureFormat;

    public TextureFormat GetOutputDepthStencilFormat() => _depthStencilOutputParams.TextureFormat;

    public bool IsLinearColorSpace() => _linearColorSpace;

    public bool IsHDR() => _hdr;

    public int GetOutputMultiSample() => _colorOutputParams.MultiSampleLevel;

    public RenderBufferManagerSettings GetSettings() => _settings;

    private void ResolveOutputParams(RenderOutputTargetInfo? renderTarget)
    {
        var fallbackFlags = _settings.FilteredColor
            ? RenderBufferFlag.BilinearFiltering
            : RenderBufferFlag.None;
        var graphics = _lastGraphics ?? Context.Instance.GetSubsystem<Graphics>()!;
        var renderDevice = graphics.GetSubsystem<RenderDevice>();
        var outputColorTarget = renderTarget?.ColorTarget ?? RenderTargetView.SwapChainColor(renderDevice!);
        var outputDepthStencilTarget = renderTarget?.DepthStencilTarget ?? RenderTargetView.SwapChainDepthStencil(renderDevice!);
        var outputMultiSample = renderTarget?.MultiSample ?? graphics.GetSwapChainMultiSample();
        var fallbackMultiSample = _settings.InheritMultiSampleLevel ? outputMultiSample : _settings.MultiSampleLevel;
        var fallbackOutputColorFormat = outputColorTarget.GetFormat();
        var nextColorOutputParams = new RenderBufferParams(
            GetColorTextureFormat(_settings.ColorSpace, fallbackOutputColorFormat),
            fallbackMultiSample,
            fallbackFlags);
        var nextDepthStencilOutputParams = new RenderBufferParams(outputDepthStencilTarget.GetFormat(), fallbackMultiSample, RenderBufferFlag.Persistent);
        var nextLinearColorSpace = IsManagedOutputLinearColorSpace(_settings.ColorSpace, nextColorOutputParams.TextureFormat, _viewportTargetLinearColorSpace);
        var nextHdr = TextureFormatUtility.IsFloatColorFormat(nextColorOutputParams.TextureFormat);

        if (renderTarget is not null)
        {
            var multiSample = _settings.InheritMultiSampleLevel ? renderTarget.MultiSample : _settings.MultiSampleLevel;
            var flags = renderTarget.BilinearFiltered || _settings.FilteredColor
                ? RenderBufferFlag.BilinearFiltering
                : RenderBufferFlag.None;
            var outputColorFormat = renderTarget.ColorFormat == TextureFormat.Unknown
                ? nextColorOutputParams.TextureFormat
                : renderTarget.ColorFormat;
            var effectiveColorFormat = GetColorTextureFormat(_settings.ColorSpace, outputColorFormat);

            nextColorOutputParams = new RenderBufferParams(
                effectiveColorFormat,
                Math.Max(multiSample, 1),
                flags);
            nextDepthStencilOutputParams = new RenderBufferParams(
                renderTarget.DepthStencilFormat == TextureFormat.Unknown ? TextureFormat.Depth24Stencil8 : renderTarget.DepthStencilFormat,
                Math.Max(multiSample, 1),
                RenderBufferFlag.Persistent);
            nextLinearColorSpace = IsManagedOutputLinearColorSpace(_settings.ColorSpace, effectiveColorFormat, renderTarget.LinearColorSpace);
            nextHdr = TextureFormatUtility.IsFloatColorFormat(effectiveColorFormat);
        }

        if (_colorOutputParams != nextColorOutputParams || _depthStencilOutputParams != nextDepthStencilOutputParams)
        {
            _colorOutputParams = nextColorOutputParams;
            _depthStencilOutputParams = nextDepthStencilOutputParams;
            ResetCachedRenderBuffers();
        }

        _linearColorSpace = nextLinearColorSpace;
        _hdr = nextHdr;
    }

    private RenderBuffer EnsurePrimaryColorBuffer()
    {
        if (_primaryColorBuffer is null)
        {
            _primaryColorBuffer = CreateColorBuffer(_colorOutputParams) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Primary color buffer creation returned unexpected render buffer type.");
        }

        _primaryColorBuffer.RefreshForViewport(_viewportRect, _applyColorTextureMetadata);
        return _primaryColorBuffer;
    }

    private RenderBuffer EnsureDepthBuffer()
    {
        if (_depthBuffer is null)
        {
            _depthBuffer = new TextureRenderBuffer(_depthStencilOutputParams);
            RegisterTextureBuffer(_depthBuffer);
        }

        _depthBuffer.RefreshForViewport(_viewportRect);
        return _depthBuffer;
    }

    private RenderBuffer EnsureSecondaryColorBuffer()
    {
        if (_secondaryColorBuffer is null)
        {
            _secondaryColorBuffer = CreateColorBuffer(_colorOutputParams) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Color buffer creation returned unexpected render buffer type.");
        }

        _secondaryColorBuffer.RefreshForViewport(_viewportRect, _applyColorTextureMetadata);
        return _secondaryColorBuffer;
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

        var useLinearMetadata = _hdr || TextureFormatUtility.IsFloatColorFormat(textureFormat);
        var useSrgbMetadata = _linearColorSpace && !useLinearMetadata && textureFormat == TextureFormat.RGBA8;
        texture.SetLinear(useLinearMetadata);
        texture.SetSRGB(useSrgbMetadata);
    }

    private void ResetCachedRenderBuffers()
    {
        _primaryColorBuffer = null;
        _secondaryColorBuffer = null;
        _depthBuffer = null;
    }

    private void RefreshManagedTextureBuffers()
    {
        for (var i = _managedTextureBuffers.Count - 1; i >= 0; --i)
        {
            if (!_managedTextureBuffers[i].TryGetTarget(out var buffer))
            {
                _managedTextureBuffers.RemoveAt(i);
                continue;
            }

            if (!buffer.IsEnabled)
                continue;

            buffer.RefreshForViewport(_viewportRect);
            if (RenderAPIUtils.IsDepthTextureFormat(buffer.BufferParams.TextureFormat))
                continue;

            ApplyColorTextureMetadata(buffer.Texture, buffer.Texture.Format);
        }
    }

    private void RegisterTextureBuffer(TextureRenderBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _managedTextureBuffers.Add(new WeakReference<TextureRenderBuffer>(buffer));
    }

    private bool GetDestinationLinearColorSpace(RenderTargetView destinationSurface)
    {
        return destinationSurface.Type switch
        {
            RenderTargetViewType.SwapChainColor => _viewportTargetLinearColorSpace,
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

    private static Vector4f GetClipToUvOffsetAndScale(int width, int height, IntRect sourceRect)
    {
        var safeWidth = Math.Max(width, 1);
        var safeHeight = Math.Max(height, 1);
        var left = sourceRect.Left / (float)safeWidth;
        var right = sourceRect.Right / (float)safeWidth;
        var top = sourceRect.Top / (float)safeHeight;
        var bottom = sourceRect.Bottom / (float)safeHeight;
        return new Vector4f(
            (left + right) * 0.5f,
            (top + bottom) * 0.5f,
            (right - left) * 0.5f,
            (bottom - top) * 0.5f);
    }
}
