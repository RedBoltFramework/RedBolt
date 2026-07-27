using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Class that manages all render buffers within viewport and viewport itself.
/// </summary>
public class RenderBufferManager : RedBolt.Object
{
    public RenderBufferManager(IRenderPipelineInterface renderPipeline)
    {
        _renderPipeline = renderPipeline;
        _graphics = GetSubsystem<Graphics>()!;
        _renderer = GetSubsystem<Renderer>()!;
        _renderDevice = GetSubsystem<RenderDevice>()!;
        _renderContext = _renderDevice.GetRenderContext();
        _debugger = renderPipeline.GetDebugger();
        _drawQueue = _renderDevice.GetDefaultQueue();

        // Order is important. RenderBufferManager should receive callbacks before any of render buffers
        _renderPipeline.PipelineStatesInvalidated += OnPipelineStatesInvalidated;
        _renderPipeline.RenderBegin += OnRenderBegin;
        _renderPipeline.RenderEnd += OnRenderEnd;

        _viewportColorBuffer = new ViewportColorRenderBuffer(_renderPipeline);
        _viewportDepthBuffer = new ViewportDepthStencilRenderBuffer(_renderPipeline);
        InitializePipelineStates();
    }

    public override void Dispose()
    {
        _substituteDepthBuffer?.Dispose();
        _substituteRenderBuffers[1]?.Dispose();
        _substituteRenderBuffers[0]?.Dispose();
        _substituteDepthBuffer = null;
        _substituteRenderBuffers[1] = null;
        _substituteRenderBuffers[0] = null;
        _viewportDepthBuffer.Dispose();
        _viewportColorBuffer.Dispose();
        _pipelineStates.Dispose();
        _renderPipeline.RenderEnd -= OnRenderEnd;
        _renderPipeline.RenderBegin -= OnRenderBegin;
        _renderPipeline.PipelineStatesInvalidated -= OnPipelineStatesInvalidated;
        base.Dispose();
    }

    public void SetSettings(RenderBufferManagerSettings settings)
    {
        _settings = settings;
    }

    public void SetFrameSettings(RenderBufferManagerFrameSettings frameSettings)
    {
        _frameSettings = frameSettings;
        _frameSettings.ReadableColor |= _frameSettings.SupportColorReadWrite;
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

    public RenderBuffer CreateColorBuffer(RenderBufferParams bufferParams)
        => new TextureRenderBuffer(_renderPipeline, bufferParams, Vector2f.One);

    public RenderBuffer CreateColorBuffer(RenderBufferParams bufferParams, Vector2f size)
        => new TextureRenderBuffer(_renderPipeline, bufferParams, size);

    /// <summary>
    /// Swap color buffers used to render to and to read from.
    /// Invalid if SupportOutputColorReadWrite is not requested.
    /// </summary>
    public void SwapColorBuffers(bool synchronizeContents)
    {
        if (!_frameSettings.SupportColorReadWrite)
        {
            Log.GetLogger().LogError("Cannot call SwapColorBuffers if 'supportColorReadWrite' flag is not set");
            Debug.Assert(false);
            return;
        }

        Debug.Assert(_readableColorBuffer is not null && _writeableColorBuffer is not null);
        var previousWriteable = _writeableColorBuffer;
        _writeableColorBuffer = _readableColorBuffer;
        _readableColorBuffer = previousWriteable;

        if (!synchronizeContents)
            return;

        SetRenderTargets(_depthStencilBuffer, _writeableColorBuffer!);
        DrawTexture("Synchronize readable and writeable color buffers", _readableColorBuffer.GetTexture()!);
    }

    /// <summary>
    /// Bind an explicit render target buffer for custom intermediate passes.
    /// </summary>
    public void SetRenderTargetsRect(IntRect viewportRect, RenderBuffer? depthStencilBuffer,
        ReadOnlySpan<RenderBuffer> colorBuffers, bool readOnlyDepth = false, CubeMapFace face = CubeMapFace.PositiveX)
    {
        IntRect? defaultViewportRect = null;
        RenderTargetView? depthStencilRef = default;
        if (depthStencilBuffer is not null)
        {
            depthStencilRef = readOnlyDepth
                ? depthStencilBuffer.GetReadOnlyDepthView((int)face)
                : depthStencilBuffer.GetView((int)face);
            defaultViewportRect = depthStencilBuffer.GetViewportRect();
        }

        for (var i = 0; i < colorBuffers.Length; ++i)
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

        _renderContext.SetRenderTargets(depthStencilRef, _renderTargetViews.AsSpan(0, colorBuffers.Length));
        if (viewportRect == IntRect.Zero)
            _renderContext.SetViewport(defaultViewportRect.Value);
        else
            _renderContext.SetViewport(viewportRect);
    }

    public void SetRenderTargets(RenderBuffer? depthStencilBuffer,
        ReadOnlySpan<RenderBuffer> colorBuffers, bool readOnlyDepth = false, CubeMapFace face = CubeMapFace.PositiveX)
    {
        SetRenderTargetsRect(IntRect.Zero, depthStencilBuffer, colorBuffers, readOnlyDepth, face);
    }

    public void SetRenderTargetsRect(IntRect viewportRect, RenderBuffer? depthStencilBuffer, RenderBuffer colorBuffer,
        bool readOnlyDepth = false, CubeMapFace face = CubeMapFace.PositiveX)
    {
        IntRect? defaultViewportRect = null;
        RenderTargetView? depthStencilRef = default;
        if (depthStencilBuffer is not null)
        {
            defaultViewportRect ??= depthStencilBuffer.GetViewportRect();
            depthStencilRef = readOnlyDepth
                ? depthStencilBuffer.GetReadOnlyDepthView((int)face)
                : depthStencilBuffer.GetView((int)face);
        }

        defaultViewportRect ??= colorBuffer.GetViewportRect();
        if (defaultViewportRect.Value == IntRect.Zero)
        {
            Log.GetLogger().LogError("Cannot set null render targets");
            return;
        }

        _renderTargetViews[0] = colorBuffer.GetView((int)face);
        _renderContext.SetRenderTargets(depthStencilRef, _renderTargetViews.AsSpan(0, 1));
        _renderContext.SetViewport(viewportRect == IntRect.Zero ? defaultViewportRect.Value : viewportRect);
    }

    public void SetRenderTargets(RenderBuffer? depthStencilBuffer, RenderBuffer colorBuffer,
        bool readOnlyDepth = false, CubeMapFace face = CubeMapFace.PositiveX)
    {
        SetRenderTargetsRect(IntRect.Zero, depthStencilBuffer, colorBuffer, readOnlyDepth, face);
    }

    /// <summary>
    /// Bind the currently writable color buffer and matching depth-stencil buffer as render outputs.
    /// </summary>
    public void SetOutputRenderTargetsRect(IntRect viewportRect, bool readOnlyDepth = false)
    {
        SetRenderTargetsRect(viewportRect, DepthStencilOutput, ColorOutput!, readOnlyDepth);
    }

    public void SetOutputRenderTargets(bool readOnlyDepth = false)
    {
        SetRenderTargets(DepthStencilOutput, ColorOutput!, readOnlyDepth);
    }

    /// <summary>
    /// Clear depth and/or stencil render target(s).
    /// Changes currently bound depth-stencil and render targets.
    /// </summary>
    public void ClearDepthStencil(RenderBuffer depthStencilBuffer, ClearTarget flags, float depth,
        uint stencil, CubeMapFace face = CubeMapFace.PositiveX)
    {
        _renderContext.SetRenderTargets(depthStencilBuffer.GetView((int)face), ReadOnlySpan<RenderTargetView>.Empty);
        _renderContext.SetFullViewport();
        _renderContext.ClearDepthStencil(ClearTarget.Depth | ClearTarget.Stencil, depth, stencil);
    }

    /// <summary>
    /// Clear color render target.
    /// Changes currently bound render targets.
    /// </summary>
    public void ClearColor(RenderBuffer colorBuffer, Color color, CubeMapFace face = CubeMapFace.PositiveX)
    {
        _renderTargetViews[0] = colorBuffer.GetView((int)face);
        _renderContext.SetRenderTargets(default, _renderTargetViews.AsSpan(0, 1));
        _renderContext.SetFullViewport();
        _renderContext.ClearRenderTarget(0, color);
    }

    /// <summary>
    /// Clears currently writable color output if more than one color buffers are used.
    /// If rectangle is specified, only part of render target is cleared.
    /// Changes currently bound depth-stencil and render targets.
    /// </summary>
    public void ClearOutputRect(IntRect viewportRect, ClearTarget flags, Color color, float depth, uint stencil)
    {
        // Do it just in case because we cannot handle any noise below
        flags &= ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil;
        SetRenderTargetsRect(viewportRect, DepthStencilOutput, ColorOutput!);
        var fullViewportRect = new IntRect(Vector2i.Zero, _renderContext.GetCurrentRenderTargetSize());
        if (_renderContext.GetCurrentViewport() == fullViewportRect)
        {
            if ((flags & ClearTarget.Color) != 0)
                _renderContext.ClearRenderTarget(0, color);
            if ((flags & ClearTarget.Depth) != 0 || (flags & ClearTarget.Stencil) != 0)
                _renderContext.ClearDepthStencil(flags, depth, stencil);
        }
        else
        {
            var isOpenGL = _renderContext.GetRenderDevice().GetBackend() == RenderBackend.OpenGL;
            _clearShaderParameters[0].Value = new Variant(color);
            _clearShaderParameters[1].Value = new Variant(isOpenGL ? depth * 2.0f - 1.0f : depth);
            DrawViewportQuad(
                "Clear output subregion",
                _clearPipelineState[(int)flags],
                Array.Empty<ShaderResourceDesc>(),
                _clearShaderParameters);
        }
    }

    public void ClearOutput(ClearTarget flags, Color color, float depth, uint stencil)
    {
        ClearOutputRect(IntRect.Zero, flags, color, depth, stencil);
    }

    public void ClearOutput(Color color, float depth = 1.0f, uint stencil = 0)
    {
        ClearOutput(ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil, color, depth, stencil);
    }

    /// <summary>
    /// Create pipeline states for fullscreen quad rendering.
    /// </summary>
    public StaticPipelineStateId CreateQuadPipelineState(GraphicsPipelineStateDesc description)
    {
        var quadGeometry = _renderer.GetQuadGeometry()!;

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
            VertexShader = _graphics.GetShader(ShaderType.VS, shaderName, defines),
            PixelShader = _graphics.GetShader(ShaderType.PS, shaderName, defines)
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
        return _pipelineStates.GetState(id, _renderContext.GetCurrentRenderTargetsDesc());
    }

    /// <summary>
    /// Render fullscreen quad with custom parameters into currently bound render buffer.
    /// </summary>
    public void DrawQuad(string debugComment, in DrawQuadParams parameters, bool flipVertical = false)
    {
        var pipelineState = parameters.PipelineState ?? GetQuadPipelineState(parameters.PipelineStateId);
        if (pipelineState is null || !pipelineState.IsValid)
            return;

        using var renderScope = RenderScope.Acquire(_renderContext, debugComment);

        var quadGeometry = _renderer.GetQuadGeometry()!;
        var modelMatrix = Matrix3x4.Identity;
        var projection = Matrix4.Identity;
        if (flipVertical)
            projection.M11 = -1.0f;

        // OpenGL z range is [-1, 1] instead of [0, 1], draw quad at z = 0.5 for consistency
        var isOpenGL = _renderDevice.GetBackend() == RenderBackend.OpenGL;
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
            for (var i = 0; i < parameters.Parameters.Count; ++i)
            {
                var shaderParameter = parameters.Parameters[i];
                _drawQueue.AddShaderParameter(shaderParameter.Name, shaderParameter.Value);
            }
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Custom);
        }

        if (parameters.BindSecondaryColorToDiffuse)
        {
            var secondaryColor = SecondaryColorTexture;
            if (secondaryColor is not null)
                _drawQueue.AddShaderResource(ShaderResources.Albedo, secondaryColor);
        }
        for (var i = 0; i < parameters.Resources.Count; ++i)
        {
            var shaderResource = parameters.Resources[i];
            var canBind = !parameters.BindSecondaryColorToDiffuse || shaderResource.Name != ShaderResources.Albedo;
            if (canBind && shaderResource.Texture is not null)
                _drawQueue.AddShaderResource(shaderResource.Name, shaderResource.Texture);
        }
        _drawQueue.CommitShaderResources();

        GraphicsUtils.SetBuffersFromGeometry(_drawQueue, quadGeometry);
        _drawQueue.DrawIndexed((int)quadGeometry.GetIndexStart(), (int)quadGeometry.GetIndexCount());

        _renderContext.Execute(_drawQueue);

        if (_debugger is not null && RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger.ReportQuad(debugComment, _renderContext.GetCurrentViewport().Size);
    }

    public void DrawViewportQuad(string debugComment, StaticPipelineStateId pipelineStateId,
        IReadOnlyList<ShaderResourceDesc> resources, IReadOnlyList<ShaderParameterDesc> parameters,
        bool flipVertical = false)
    {
        var drawParams = new DrawQuadParams
        {
            PipelineStateId = pipelineStateId,
            ClipToUVOffsetAndScale = DefaultClipToUVSpaceOffsetAndScale,
            InvInputSize = InvOutputSize,
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
            ClipToUVOffsetAndScale = DefaultClipToUVSpaceOffsetAndScale,
            InvInputSize = InvOutputSize,
            BindSecondaryColorToDiffuse = true,
            Resources = resources,
            Parameters = parameters
        };
        DrawQuad(debugComment, drawParams, flipVertical);
    }

    /// <summary>
    /// Draw region of input texture into currently bound render buffer. sRGB is taken into account.
    /// </summary>
    public void DrawTextureRegion(string debugComment, RawTexture sourceTexture, IntRect sourceRect,
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
        var invInputSize = Vector2f.One / new Vector2f(size.X, size.Y);
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

    /// <summary>
    /// Draw input texture into currently bound render buffer. sRGB is taken into account.
    /// </summary>
    public void DrawTexture(string debugComment, RawTexture sourceTexture,
        ColorSpaceTransition mode = ColorSpaceTransition.None, bool flipVertical = false)
    {
        DrawTextureRegion(debugComment, sourceTexture, IntRect.Zero, mode, flipVertical);
    }

    /// <summary>
    /// Return depth-stencil buffer. Stays the same during the frame.
    /// </summary>
    public RenderBuffer? DepthStencilOutput => _depthStencilBuffer;

    /// <summary>
    /// Return color render buffer. Changes on SwapColorBuffers.
    /// </summary>
    public RenderBuffer? ColorOutput => _writeableColorBuffer;

    /// <summary>
    /// Return readable depth texture.
    /// It's not allowed to write depth and read it from shader input simultaneously for same depth-stencil buffer.
    /// </summary>
    public RawTexture? DepthStencilTexture => _depthStencilBuffer!.GetTexture();

    /// <summary>
    /// Return secondary color render buffer texture that can be used while writing to color render buffer.
    /// Texture content is defined at the moment of previous SwapColorBuffers.
    /// Content is undefined if SwapColorBuffers was not called during current frame.
    /// </summary>
    public RawTexture? SecondaryColorTexture => _readableColorBuffer?.GetTexture();

    /// <summary>
    /// Return size of output region (not size of output texture itself).
    /// </summary>
    public Vector2i OutputSize => _viewportRect.Size;

    public Vector2f InvOutputSize
    {
        get
        {
            var size = OutputSize;
            return Vector2f.One / new Vector2f(size.X, size.Y);
        }
    }

    /// <summary>
    /// Return identity offset and scale used to convert clip space to UV space.
    /// </summary>
    public Vector4f DefaultClipToUVSpaceOffsetAndScale
    {
        get
        {
            var size = OutputSize;
            return GetClipToUvOffsetAndScale(size.X, size.Y, new IntRect(Vector2i.Zero, size));
        }
    }

    public TextureFormat OutputColorFormat => _colorOutputParams.TextureFormat;

    public TextureFormat OutputDepthStencilFormat => _depthStencilOutputParams.TextureFormat;

    public bool IsLinearColorSpace => _linearColorSpace;

    public bool IsHDR => _hdr;

    public int OutputMultiSample => _colorOutputParams.MultiSampleLevel;

    public ref readonly RenderBufferManagerSettings Settings => ref _settings;

    private const int MaxClearVariants = (int)(ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil) + 1;

    private void OnPipelineStatesInvalidated() => _pipelineStates.Invalidate();

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

        if (needSubstitutePrimaryBuffer && _substituteRenderBuffers[0] is null)
        {
            _substituteRenderBuffers[0] = new TextureRenderBuffer(_renderPipeline, _colorOutputParams);
            _substituteRenderBuffers[0]!.BeginCurrentFrame(frameInfo);
        }
        if (needSecondaryBuffer && _substituteRenderBuffers[1] is null)
        {
            _substituteRenderBuffers[1] = new TextureRenderBuffer(_renderPipeline, _colorOutputParams);
            _substituteRenderBuffers[1]!.BeginCurrentFrame(frameInfo);
        }
        if (needSubstituteDepthBuffer && _substituteDepthBuffer is null)
        {
            _substituteDepthBuffer = new TextureRenderBuffer(_renderPipeline, _depthStencilOutputParams);
            _substituteDepthBuffer.BeginCurrentFrame(frameInfo);
        }

        _depthStencilBuffer = needSubstituteDepthBuffer ? _substituteDepthBuffer : _viewportDepthBuffer;
        _writeableColorBuffer = needSubstitutePrimaryBuffer ? _substituteRenderBuffers[0] : _viewportColorBuffer;
        _readableColorBuffer = needSecondaryBuffer ? _substituteRenderBuffers[1] : null;

        if (_flipColorBuffersNextTime && _readableColorBuffer is not null)
        {
            var previousWriteable = _writeableColorBuffer;
            _writeableColorBuffer = _readableColorBuffer;
            _readableColorBuffer = previousWriteable;
        }
    }

    private void OnRenderEnd(CommonFrameInfo frameInfo)
    {
        if (ReferenceEquals(_writeableColorBuffer, _viewportColorBuffer))
            return;

        var colorTexture = _writeableColorBuffer!.GetTexture()!;
        var textureSize = colorTexture.GetParams().Size;
        CopyTextureRegion(
            "Copy final color to output RenderSurface",
            colorTexture,
            new IntRect(0, 0, textureSize.X, textureSize.Y),
            _viewportColorBuffer.GetView(),
            _viewportColorBuffer.GetViewportRect(),
            ColorSpaceTransition.Automatic,
            false);

        _flipColorBuffersNextTime ^= ReferenceEquals(_viewportColorBuffer, _readableColorBuffer);
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

        for (var i = 0; i < MaxClearVariants; ++i)
            _clearPipelineState[i] = CreateQuadPipelineState(GetClearPipelineStateDesc((ClearTarget)i));
    }

    private void ResetCachedRenderBuffers()
    {
        _substituteRenderBuffers[0]?.Dispose();
        _substituteRenderBuffers[1]?.Dispose();
        _substituteDepthBuffer?.Dispose();
        _substituteRenderBuffers[0] = null;
        _substituteRenderBuffers[1] = null;
        _substituteDepthBuffer = null;
    }

    private void CopyTextureRegion(string debugComment, RawTexture sourceTexture, IntRect sourceRect,
        RenderTargetView destinationSurface, IntRect destinationRect, ColorSpaceTransition mode,
        bool flipVertical = false)
    {
        _renderTargetViews[0] = destinationSurface;
        _renderContext.SetRenderTargets(default, _renderTargetViews.AsSpan(0, 1));
        _renderContext.SetViewport(destinationRect);
        DrawTextureRegion(debugComment, sourceTexture, sourceRect, mode, flipVertical);
    }

    private GraphicsPipelineStateDesc GetClearPipelineStateDesc(ClearTarget flags)
    {
        const string shaderName = "v2/X_ClearFramebuffer";
        return new GraphicsPipelineStateDesc
        {
            DebugName = shaderName,
            VertexShader = _graphics.GetShader(ShaderType.VS, shaderName, string.Empty),
            PixelShader = _graphics.GetShader(ShaderType.PS, shaderName, string.Empty),
            ColorWriteEnabled = (flags & ClearTarget.Color) != 0,
            BlendMode = BlendMode.Replace,
            DepthWriteEnabled = (flags & ClearTarget.Depth) != 0,
            StencilWriteMask = (flags & ClearTarget.Stencil) != 0 ? 0xffu : 0u,
            StencilOperationOnPassed = StencilOp.Ref,
        };
    }

    private static TextureFormat GetColorTextureFormat(RenderPipelineColorSpace colorSpace, TextureFormat outputTextureFormat)
    {
        return colorSpace switch
        {
            RenderPipelineColorSpace.LinearHDR => TextureFormat.TexFormatRgba16Float,
            RenderPipelineColorSpace.LinearLDR => TextureFormat.TexFormatRgba8UnormSrgb,
            RenderPipelineColorSpace.GammaLDR => TextureFormat.TexFormatRgba8Unorm,
            RenderPipelineColorSpace.Optimized => outputTextureFormat,
            _ => outputTextureFormat
        };
    }

    private static Vector4f GetClipToUvOffsetAndScale(int width, int height, IntRect sourceRect)
    {
        var left = sourceRect.Left / (float)width;
        var right = sourceRect.Right / (float)width;
        var top = sourceRect.Top / (float)height;
        var bottom = sourceRect.Bottom / (float)height;
        return new Vector4f(
            (left + right) * 0.5f,
            (top + bottom) * 0.5f,
            (right - left) * 0.5f,
            (bottom - top) * 0.5f);
    }

    private readonly IRenderPipelineInterface _renderPipeline;
    private readonly Graphics _graphics;
    private readonly Renderer _renderer;
    private readonly RenderDevice _renderDevice;
    private readonly RenderContext _renderContext;
    private readonly RenderPipelineDebugger? _debugger;
    private readonly DrawCommandQueue _drawQueue;
    private RenderBufferManagerSettings _settings = new();
    private readonly StaticPipelineStateCache _pipelineStates = new();
    private readonly ViewportColorRenderBuffer _viewportColorBuffer;
    private readonly ViewportDepthStencilRenderBuffer _viewportDepthBuffer;
    private StaticPipelineStateId _copyTexturePipelineState;
    private StaticPipelineStateId _copyGammaToLinearTexturePipelineState;
    private StaticPipelineStateId _copyLinearToGammaTexturePipelineState;
    private readonly StaticPipelineStateId[] _clearPipelineState = new StaticPipelineStateId[MaxClearVariants];
    private readonly RenderBuffer?[] _substituteRenderBuffers = new RenderBuffer?[2];
    private RenderBuffer? _substituteDepthBuffer;
    private RenderBufferParams _colorOutputParams = new();
    private RenderBufferParams _depthStencilOutputParams = new();
    private bool _linearColorSpace;
    private bool _hdr;
    private RenderBufferManagerFrameSettings _frameSettings = new();
    private float _timeStep;
    private IntRect _viewportRect;
    private RenderBuffer? _depthStencilBuffer;
    private RenderBuffer? _writeableColorBuffer;
    private RenderBuffer? _readableColorBuffer;
    private bool _flipColorBuffersNextTime;

    private readonly ShaderParameterDesc[] _clearShaderParameters =
    [
        new() { Name = new StringHash("Color") },
        new() { Name = new StringHash("Depth") }
    ];

    private readonly RenderTargetView[] _renderTargetViews = new RenderTargetView[RenderApiLimits.MaxRenderTargets];
    private readonly ShaderResourceDesc[] _copyShaderResources = new ShaderResourceDesc[1];
}
