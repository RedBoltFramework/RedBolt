using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Class that manages all render buffers within viewport and viewport itself.
/// </summary>
public sealed class RenderBufferManager : Object
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyShaderParameters = new Dictionary<string, object?>(0, StringComparer.Ordinal);
    private readonly IRenderPipelineInterface _renderPipeline;
    private readonly Graphics _graphics;
    private readonly Renderer _renderer;
    private readonly RenderDevice _renderDevice;
    private readonly RenderContext _renderContext;
    private readonly DrawCommandQueue _drawQueue;
    private readonly StaticPipelineStateCache _pipelineStates = new();
    private StaticPipelineStateId _copyTexturePipelineState;
    private StaticPipelineStateId _copyGammaToLinearTexturePipelineState;
    private StaticPipelineStateId _copyLinearToGammaTexturePipelineState;
    // C++: MaxClearVariants = (CLEAR_COLOR | CLEAR_DEPTH | CLEAR_STENCIL) + 1
    private const int MaxClearVariants = (int)(ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil) + 1;
    private readonly StaticPipelineStateId[] _clearPipelineState = new StaticPipelineStateId[MaxClearVariants];

    private readonly struct ClearFramebufferParameters
    {
        [ShaderParameterName("Color")]
        public Vector4f Color { get; init; }

        [ShaderParameterName("Depth")]
        public float Depth { get; init; }
    }

    private readonly ViewportColorRenderBuffer _viewportColorBuffer;
    private readonly ViewportDepthStencilRenderBuffer _viewportDepthBuffer;
    private readonly RenderTargetView[] _renderTargetViews = new RenderTargetView[RenderApiLimits.MaxRenderTargets];
    private readonly ShaderResourceDesc[] _copyShaderResources = new ShaderResourceDesc[1];
    private TextureRenderBuffer? _primaryColorBuffer;
    private TextureRenderBuffer? _secondaryColorBuffer;
    private TextureRenderBuffer? _depthBuffer;
    private RenderBufferManagerSettings _settings = new();
    private RenderBufferManagerFrameSettings _frameSettings = new();
    private RenderBufferParams _colorOutputParams = new(TextureFormat.TexFormatRgba8Unorm);
    private RenderBufferParams _depthStencilOutputParams = new(TextureFormat.TexFormatUnknown);
    private IntRect _viewportRect;
    private RenderBuffer _depthStencilBuffer;
    private RenderBuffer _writeableColorBuffer;
    private RenderBuffer? _readableColorBuffer;
    private bool _flipColorBuffersNextTime;
    private bool _linearColorSpace;
    private bool _hdr;
    private float _timeStep;
    private readonly RenderPipelineDebugger _debugger;

    public RenderBufferManager(IRenderPipelineInterface renderPipeline)
    {
        _renderPipeline = renderPipeline;
        _graphics = GetSubsystem<Graphics>()!;
        _renderer = GetSubsystem<Renderer>()!;
        _renderDevice = GetSubsystem<RenderDevice>()!;
        _renderContext = _renderDevice.GetRenderContext();
        _debugger = renderPipeline.GetDebugger();
        _drawQueue = _renderDevice.GetDefaultQueue();

        _renderPipeline.PipelineStatesInvalidated += OnPipelineStatesInvalidated;
        _renderPipeline.RenderBegin += OnRenderBegin;
        _renderPipeline.RenderEnd += OnRenderEnd;

        _viewportColorBuffer = new ViewportColorRenderBuffer(_renderPipeline);
        _viewportDepthBuffer = new ViewportDepthStencilRenderBuffer(_renderPipeline);
        _depthStencilBuffer = _viewportDepthBuffer;
        _writeableColorBuffer = _viewportColorBuffer;
        InitializePipelineStates();
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

    public void OnViewportDefined(RenderSurface? renderTarget, IntRect viewportRect)
    {
        var outputTexture = renderTarget?.GetParentTexture() as Texture2D;
        var outputDepthStencil = renderTarget is null
            ? null
            : renderTarget.GetLinkedDepthStencil();
        var isBilinearFilteredOutput = outputTexture is not null
            && outputTexture.GetFilterMode() != TextureFilterMode.Nearest;
        var outputMultiSample = RenderSurface.GetMultiSample(_graphics, renderTarget);
        var outputColorFormat = RenderSurface.GetColorFormat(_graphics, renderTarget);

        var colorParams = new RenderBufferParams
        {
            MultiSampleLevel = _settings.InheritMultiSampleLevel ? outputMultiSample : _settings.MultiSampleLevel,
            TextureFormat = GetColorTextureFormat(_settings.ColorSpace, outputColorFormat),
            Flags = _settings.FilteredColor || isBilinearFilteredOutput
                ? RenderBufferFlag.BilinearFiltering
                : RenderBufferFlag.None
        };
        var depthParams = colorParams;
        depthParams.Flags |= RenderBufferFlag.Persistent;
        depthParams.TextureFormat = RenderSurface.GetDepthFormat(_graphics, outputDepthStencil);

        if (_colorOutputParams != colorParams || _depthStencilOutputParams != depthParams)
        {
            _colorOutputParams = colorParams;
            _depthStencilOutputParams = depthParams;
            ResetCachedRenderBuffers();
        }

        var isLinearTextureFormat = RenderAPIUtils.SetTextureFormatSRGB(_colorOutputParams.TextureFormat)
            == _colorOutputParams.TextureFormat;
        var isLinearTextureMetadata = outputTexture is not null && outputTexture.GetLinear();
        _linearColorSpace = isLinearTextureFormat || isLinearTextureMetadata;
        var componentType = RenderAPIUtils.GetTextureFormatInfo(_colorOutputParams.TextureFormat).ComponentType;
        _hdr = componentType is not global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeUnorm
            and not global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeUnormSrgb;
    }

    private void OnRenderBegin(CommonFrameInfo frameInfo)
    {
        _timeStep = frameInfo.TimeStep;
        _viewportRect = frameInfo.ViewportRect;

        var renderTarget = frameInfo.RenderTarget;
        var outputFormat = RenderSurface.GetColorFormat(_graphics, renderTarget);
        var outputMultiSample = RenderSurface.GetMultiSample(_graphics, renderTarget);

        var outputDepthStencil = renderTarget is null
            ? null
            : renderTarget.GetLinkedDepthStencil();
        var outputHasStencil = renderTarget is null || outputDepthStencil is not null
            && RenderAPIUtils.IsDepthStencilTextureFormat(outputDepthStencil.GetParentTexture()!.Format);
        var outputHasReadableDepth = outputDepthStencil is not null;

        var outputTexture = renderTarget?.GetParentTexture() as Texture2D;
        var isFullRectOutput = _viewportRect == IntRect.Zero
            || _viewportRect == RenderSurface.GetRect(_graphics, renderTarget);
        var isSimpleTextureOutput = outputTexture is not null && isFullRectOutput;
        var isBilinearFilteredOutput = outputTexture is not null
            && outputTexture.GetFilterMode() != TextureFilterMode.Nearest;

        var needSimpleTexture = _frameSettings.ReadableColor || _settings.ReadableDepth
            || _settings.ColorUsableWithMultipleRenderTargets;

        var isColorFormatMatching = outputFormat == _colorOutputParams.TextureFormat;
        var isMultiSampleMatching = outputMultiSample == _colorOutputParams.MultiSampleLevel;
        var isFilterMatching = isBilinearFilteredOutput
            == ((_colorOutputParams.Flags & RenderBufferFlag.BilinearFiltering) != 0);
        var isColorUsageMatching = isSimpleTextureOutput || !needSimpleTexture;
        var isOutputMatching = isColorFormatMatching && isMultiSampleMatching && isFilterMatching && isColorUsageMatching;

        var needSecondaryBuffer = _frameSettings.SupportColorReadWrite;
        var needSubstituteDepthBuffer = !isMultiSampleMatching || renderTarget is not null && outputDepthStencil is null
            || ((needSecondaryBuffer || !isOutputMatching) && outputDepthStencil is null)
            || (_settings.ReadableDepth && (!outputHasReadableDepth || !isSimpleTextureOutput))
            || (_settings.StencilBuffer && !outputHasStencil);
        var needSubstitutePrimaryBuffer = !isOutputMatching || needSubstituteDepthBuffer;

        if (needSubstitutePrimaryBuffer && _primaryColorBuffer is null)
        {
            _primaryColorBuffer = new TextureRenderBuffer(_renderPipeline, _colorOutputParams);
            _primaryColorBuffer.BeginCurrentFrame(frameInfo);
        }
        if (needSecondaryBuffer && _secondaryColorBuffer is null)
        {
            _secondaryColorBuffer = new TextureRenderBuffer(_renderPipeline, _colorOutputParams);
            _secondaryColorBuffer.BeginCurrentFrame(frameInfo);
        }
        if (needSubstituteDepthBuffer && _depthBuffer is null)
        {
            _depthBuffer = new TextureRenderBuffer(_renderPipeline, _depthStencilOutputParams);
            _depthBuffer.BeginCurrentFrame(frameInfo);
        }

        _depthStencilBuffer = needSubstituteDepthBuffer ? _depthBuffer! : _viewportDepthBuffer;
        _writeableColorBuffer = needSubstitutePrimaryBuffer ? _primaryColorBuffer! : _viewportColorBuffer;
        _readableColorBuffer = needSecondaryBuffer ? _secondaryColorBuffer : null;

        if (_flipColorBuffersNextTime && _readableColorBuffer is not null)
        {
            var previousWriteable = _writeableColorBuffer;
            _writeableColorBuffer = _readableColorBuffer;
            _readableColorBuffer = previousWriteable;
        }
    }

    /// <summary>
    /// Create pipeline states for fullscreen quad rendering.
    /// </summary>
    public StaticPipelineStateId CreateQuadPipelineState(GraphicsPipelineStateDesc description)
    {
        var quadGeometry = _renderer?.GetQuadGeometry();
        if (quadGeometry is null)
            return StaticPipelineStateId.Invalid;

        GraphicsUtils.InitializeInputLayoutAndPrimitiveType(ref description, quadGeometry);
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
            description.Samplers.Add(new StringHash(sampler.Name), sampler.Desc);

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
            var secondaryColor = GetSecondaryColorTexture();
            if (secondaryColor is not null)
                _drawQueue.AddShaderResource(ShaderResources.Albedo, secondaryColor);
        }
        foreach (var shaderResource in parameters.Resources)
        {
            var canBind = !parameters.BindSecondaryColorToDiffuse || shaderResource.Name != ShaderResources.Albedo;
            if (canBind && shaderResource.Texture is not null)
                _drawQueue.AddShaderResource(shaderResource.Name, shaderResource.Texture);
        }
        _drawQueue.CommitShaderResources();

        GraphicsUtils.SetBuffersFromGeometry(_drawQueue, quadGeometry);
        _drawQueue.DrawIndexed(checked((int)quadGeometry.GetIndexStart()), checked((int)quadGeometry.GetIndexCount()));

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

    private void OnPipelineStatesInvalidated() => _pipelineStates.Invalidate();

    private void OnRenderEnd(CommonFrameInfo frameInfo)
    {
        ResolveToViewport(_graphics);
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
        return size == default
            ? new TextureRenderBuffer(_renderPipeline, bufferParams)
            : new TextureRenderBuffer(_renderPipeline, bufferParams, size);
    }

    /// <summary>
    /// Swap color buffers used to render to and to read from.
    /// Invalid if SupportOutputColorReadWrite is not requested.
    /// </summary>
    public void SwapColorBuffers(bool synchronizeContents)
    {
        if (!_frameSettings.SupportColorReadWrite)
            throw new InvalidOperationException("Cannot swap color buffers when color read/write is not enabled for the frame.");

        Debug.Assert(_readableColorBuffer is not null && _writeableColorBuffer is not null);
        // ea::swap(writeableColorBuffer_, readableColorBuffer_);
        var previousWriteable = _writeableColorBuffer;
        _writeableColorBuffer = _readableColorBuffer;
        _readableColorBuffer = previousWriteable;

        if (!synchronizeContents)
            return;

        var readableTexture = _readableColorBuffer.GetTexture();
        if (readableTexture is null)
            return;

        SetRenderTargets(_graphics, _writeableColorBuffer, _depthStencilBuffer);
        DrawTexture(_graphics, "Synchronize readable and writeable color buffers", readableTexture);
    }

    /// <summary>
    /// Bind the currently writable color buffer and matching depth-stencil buffer as render outputs.
    /// </summary>
    public void SetOutputRenderTargets(Graphics graphics, bool readOnlyDepth = false)
    {
        // C++: SetRenderTargetsRect(ZERO, GetDepthStencilOutput(), { GetColorOutput() }, readOnlyDepth)
        ArgumentNullException.ThrowIfNull(graphics);
        SetRenderTargets(graphics, GetDepthStencilOutput(), [GetColorOutput()], readOnlyDepth);
    }

    public void SetOutputRenderTargetsRect(Graphics graphics, IntRect viewportRect, bool readOnlyDepth = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        // C++: SetRenderTargetsRect(viewportRect, GetDepthStencilOutput(), { GetColorOutput() }, readOnlyDepth)
        SetRenderTargetsRect(graphics, viewportRect, GetDepthStencilOutput(), [GetColorOutput()], readOnlyDepth);
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

    public void CopyTextureRegion(Graphics graphics, string debugComment, RawTexture sourceTexture, IntRect sourceRect,
        RenderTargetView destinationSurface, IntRect destinationRect, ColorSpaceTransition mode = ColorSpaceTransition.None,
        bool flipVertical = false)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(sourceTexture);

        _renderContext.SetRenderTargets(default, [destinationSurface]);
        _renderContext.SetViewport(destinationRect);
        DrawTextureRegion(graphics, debugComment, sourceTexture, sourceRect, mode, flipVertical);
    }

    /// <summary>
    /// Draw region of input texture into currently bound render buffer. sRGB is taken into account.
    /// </summary>
    public void DrawTextureRegion(Graphics graphics, string debugComment, RawTexture sourceTexture, IntRect sourceRect,
        ColorSpaceTransition mode = ColorSpaceTransition.None, bool flipVertical = false)
    {
        var textureParams = sourceTexture.GetParams();
        if (textureParams.Type != TextureType.Texture2D)
        {
            Log.GetLogger().LogError("Draw texture is supported only for Texture2D");
            return;
        }
        var size = new Vector2i(textureParams.Size.X, textureParams.Size.Y);
        var effectiveSourceRect = sourceRect != IntRect.Zero
            ? sourceRect
            : new IntRect(0, 0, size.X, size.Y);
        var invInputSize = new Vector2f(
            size.X > 0 ? 1.0f / size.X : 0.0f,
            size.Y > 0 ? 1.0f / size.Y : 0.0f);
        var destinationFormat = _renderContext.GetCurrentRenderTargetsDesc().RenderTargetFormats[0];
        var isSrgbSource = RenderAPIUtils.IsTextureFormatSRGB(textureParams.Format);
        var isSrgbDestination = RenderAPIUtils.IsTextureFormatSRGB(destinationFormat);
        var pipelineStateId = mode == ColorSpaceTransition.None || isSrgbSource == isSrgbDestination
            ? _copyTexturePipelineState
            : isSrgbDestination ? _copyGammaToLinearTexturePipelineState : _copyLinearToGammaTexturePipelineState;
        _copyShaderResources[0] = new ShaderResourceDesc
        {
            Name = ShaderResources.Albedo,
            Texture = sourceTexture
        };
        var drawParams = new DrawQuadParams
        {
            PipelineStateId = pipelineStateId,
            InvInputSize = invInputSize,
            ClipToUVOffsetAndScale = GetClipToUvOffsetAndScale(size.X, size.Y, effectiveSourceRect),
            Resources = _copyShaderResources
        };
        DrawQuad(debugComment, drawParams, flipVertical);
    }

    private void InitializePipelineStates()
    {
        NamedSamplerStateDesc[] samplers =
            [("Albedo", SamplerStateDesc.Bilinear())];

        _copyTexturePipelineState = CreateQuadPipelineState(
            BlendMode.Replace, "v2/X_CopyFramebuffer", string.Empty, samplers);
        _copyGammaToLinearTexturePipelineState = CreateQuadPipelineState(
            BlendMode.Replace, "v2/X_CopyFramebuffer", "URHO3D_GAMMA_TO_LINEAR", samplers);
        _copyLinearToGammaTexturePipelineState = CreateQuadPipelineState(
            BlendMode.Replace, "v2/X_CopyFramebuffer", "URHO3D_LINEAR_TO_GAMMA", samplers);

        // C++ InitializePipelineStates: clearPipelineState_[i] for each ClearTargetFlags variant
        for (var i = 0; i < MaxClearVariants; ++i)
            _clearPipelineState[i] = CreateQuadPipelineState(GetClearPipelineStateDesc((ClearTarget)i));
    }

    /// <summary>
    /// C++ GetClearPipelineStateDesc — v2/X_ClearFramebuffer with color/depth/stencil write flags.
    /// </summary>
    private GraphicsPipelineStateDesc GetClearPipelineStateDesc(ClearTarget flags)
    {
        const string shaderName = "v2/X_ClearFramebuffer";
        return new GraphicsPipelineStateDesc
        {
            DebugName = shaderName,
            VertexShader = _graphics?.GetShader(ShaderType.VS, shaderName, string.Empty),
            PixelShader = _graphics?.GetShader(ShaderType.PS, shaderName, string.Empty),
            ColorWriteEnabled = (flags & ClearTarget.Color) != 0,
            BlendMode = BlendMode.Replace,
            DepthWriteEnabled = (flags & ClearTarget.Depth) != 0,
            DepthCompareFunction = CompareMode.Always,
            StencilWriteMask = (flags & ClearTarget.Stencil) != 0 ? 0xffu : 0u,
            StencilOperationOnPassed = StencilOp.Ref,
            StencilTestEnabled = (flags & ClearTarget.Stencil) != 0,
            StencilCompareFunction = CompareMode.Always,
        };
    }

    /// <summary>
    /// Draw input texture into currently bound render buffer. sRGB is taken into account.
    /// </summary>
    public void DrawTexture(Graphics graphics, string debugComment, RawTexture sourceTexture,
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
            new IntRect(0, 0, colorTexture.GetParams().Size.X, colorTexture.GetParams().Size.Y),
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
        // C++: SetRenderTargets(depth, { color }) via renderContext_
        SetRenderTargets(graphics, depthStencilBuffer, [colorBuffer], readOnlyDepth);
    }

    public void SetRenderTargetsRect(Graphics graphics, IntRect viewportRect, RenderBuffer? depthStencilBuffer,
        IReadOnlyList<RenderBuffer> colorBuffers, bool readOnlyDepth = false, CubeMapFace face = CubeMapFace.PositiveX)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(colorBuffers);
        // C++ RenderBufferManager::SetRenderTargetsRect — bind via renderContext_, then SetViewport.
        if (_renderContext is null)
            return;

        IntRect? defaultViewportRect = null;
        RenderTargetView? depthStencilRef = default;
        if (depthStencilBuffer is not null)
        {
            depthStencilRef = readOnlyDepth
                ? depthStencilBuffer.GetReadOnlyDepthView((int)face)
                : depthStencilBuffer.GetView((int)face);
            defaultViewportRect = depthStencilBuffer.GetViewportRect();
        }

        for (var i = 0; i < colorBuffers.Count; ++i)
        {
            var colorBuffer = colorBuffers[i];
            _renderTargetViews[i] = colorBuffer.GetView((int)face);
            if (defaultViewportRect is null)
                defaultViewportRect = colorBuffer.GetViewportRect();
        }

        if (defaultViewportRect is null || defaultViewportRect.Value == IntRect.Zero)
        {
            Log.GetLogger().LogError("Cannot set null render targets");
            return;
        }

        _renderContext.SetRenderTargets(depthStencilRef, _renderTargetViews.AsSpan(0, colorBuffers.Count));
        if (viewportRect == IntRect.Zero)
            _renderContext.SetViewport(defaultViewportRect.Value);
        else
            _renderContext.SetViewport(viewportRect);
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
        if (_renderContext is null)
            return;

        // C++: SetRenderTargets(depth, {}); SetFullViewport(); ClearDepthStencil(...)
        _renderContext.SetRenderTargets(depthStencilBuffer.GetView((int)face), ReadOnlySpan<RenderTargetView>.Empty);
        _renderContext.SetFullViewport();
        _renderContext.ClearDepthStencil(flags, depth, stencil);
    }

    /// <summary>
    /// Clear color render target.
    /// Changes currently bound render targets.
    /// </summary>
    public void ClearColor(Graphics graphics, RenderBuffer colorBuffer, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(colorBuffer);
        if (_renderContext is null)
            return;

        // C++: SetRenderTargets(nullopt, {color}); SetFullViewport(); ClearRenderTarget(0, color)
        _renderTargetViews[0] = colorBuffer.GetView();
        _renderContext.SetRenderTargets(default, _renderTargetViews.AsSpan(0, 1));
        _renderContext.SetFullViewport();
        _renderContext.ClearRenderTarget(0, color);
    }

    /// <summary>
    /// Clears currently writable color output if more than one color buffers are used.
    /// If rectangle is specified, only part of render target is cleared.
    /// Changes currently bound depth-stencil and render targets.
    /// </summary>
    public void ClearOutputRect(Graphics graphics, IntRect viewportRect, ClearTarget flags, Color color, float depth, byte stencil = 0)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (_renderContext is null)
            return;

        // C++ ClearOutputRect
        flags &= ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil;
        if (flags == ClearTarget.None)
            return;

        SetRenderTargetsRect(graphics, viewportRect, GetDepthStencilOutput(), [GetColorOutput()]);
        var fullViewportRect = new IntRect(Vector2i.Zero, _renderContext.GetCurrentRenderTargetSize());
        if (_renderContext.GetCurrentViewport() == fullViewportRect)
        {
            if ((flags & ClearTarget.Color) != 0)
                _renderContext.ClearRenderTarget(0, color);
            if ((flags & ClearTarget.Depth) != 0 || (flags & ClearTarget.Stencil) != 0)
                _renderContext.ClearDepthStencil(flags, depth, stencil);
            return;
        }

        // C++ ClearOutputRect subregion: DrawViewportQuad with clearPipelineState_[flags]
        var clearParams = new ShaderParameterDesc[]
        {
            new()
            {
                Name = new StringHash("Color"),
                Value = new Variant(color)
            },
            new()
            {
                Name = new StringHash("Depth"),
                Value = new Variant(GetFullscreenQuadClearDepth(depth))
            },
        };
        DrawViewportQuad(
            "Clear output subregion",
            _clearPipelineState[(int)flags],
            Array.Empty<ShaderResourceDesc>(),
            clearParams);
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
    public RawTexture? GetDepthStencilTexture() => _depthStencilBuffer.GetTexture();

    /// <summary>
    /// Return secondary color render buffer texture that can be used while writing to color render buffer.
    /// Texture content is defined at the moment of previous SwapColorBuffers.
    /// Content is undefined if SwapColorBuffers was not called during current frame.
    /// </summary>
    public RawTexture? GetSecondaryColorTexture() => _readableColorBuffer?.GetTexture();

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

    private static TextureFormat GetColorTextureFormat(RenderPipelineColorSpace colorSpace, TextureFormat outputTextureFormat)
    {
        return colorSpace switch
        {
            RenderPipelineColorSpace.LinearHDR => TextureFormat.TexFormatRgba16Float,
            RenderPipelineColorSpace.LinearLDR => TextureFormat.TexFormatRgba8UnormSrgb,
            RenderPipelineColorSpace.GammaLDR => TextureFormat.TexFormatRgba8Unorm,
            RenderPipelineColorSpace.Optimized => outputTextureFormat,
            _ => TextureFormat.TexFormatRgba8Unorm
        };
    }

    private void ResetCachedRenderBuffers()
    {
        _depthBuffer?.Dispose();
        _secondaryColorBuffer?.Dispose();
        _primaryColorBuffer?.Dispose();
        _depthBuffer = null;
        _secondaryColorBuffer = null;
        _primaryColorBuffer = null;
    }

    public override void Dispose()
    {
        ResetCachedRenderBuffers();
        _viewportDepthBuffer.Dispose();
        _viewportColorBuffer.Dispose();
        _renderPipeline.RenderEnd -= OnRenderEnd;
        _renderPipeline.RenderBegin -= OnRenderBegin;
        _renderPipeline.PipelineStatesInvalidated -= OnPipelineStatesInvalidated;
        _pipelineStates.Dispose();
        base.Dispose();
    }

    // C++ ClearOutputRect: Depth = isOpenGL ? (depth * 2.0 - 1.0) : depth
    private float GetFullscreenQuadClearDepth(float depth)
        => MapFullscreenQuadClearDepth(depth, _renderDevice?.GetBackend() == RenderBackend.OpenGL);

    /// <summary>C++ ClearOutputRect depth remap for fullscreen-quad subrect clears.</summary>
    private static float MapFullscreenQuadClearDepth(float depth, bool isOpenGl)
        => isOpenGl ? depth * 2.0f - 1.0f : depth;

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
