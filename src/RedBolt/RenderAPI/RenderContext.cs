using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

/// <summary>
/// Render context that consumes render commands.
/// </summary>
public class RenderContext : Object
{
    private const uint GlClipDistance0 = 0x3000;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OpenGlStateFunction(uint capability);

    private readonly RenderDevice _renderDevice;
    private readonly RenderPool _renderPool;
    private readonly bool _debugScopeEnabled;
    private RenderDeviceStats _stats = new();
    private readonly RenderTargetView[] _swapChainColorTarget;
    private readonly RenderTargetView[] _currentRenderTargets = new RenderTargetView[RenderApiLimits.MaxRenderTargets];
    // C++: currentDepthStencil_ / currentRenderTargets_ store Diligent ITextureView*.
    private readonly ITextureView?[] _currentRenderTargetHandles = new ITextureView?[RenderApiLimits.MaxRenderTargets];
    private ITextureView? _currentDepthStencilHandle;
    private global::RedBolt.Bindings.Diligent.Viewport? _diligentViewport;
    private int _numCurrentRenderTargets;
    private RenderTargetView? _currentDepthStencil;
    private PipelineStateOutputDesc _currentOutputDesc = new();
    private bool _isSwapChain;
    private Vector2i _currentDimensions;
    private IntRect _currentViewport;
    private uint _clipPlaneMask;
    private OpenGlStateFunction? _glEnable;
    private OpenGlStateFunction? _glDisable;

    public RenderContext(RenderDevice renderDevice)
    {
        this._renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
        _renderPool = renderDevice.GetRenderPool();
        _debugScopeEnabled = ProcessUtils.GetPlatform() != PlatformId.Web;
        _swapChainColorTarget = [RenderTargetView.SwapChainColor(renderDevice)];
        SubscribeToEvent<GraphicsEvents.DeviceLostEvent>(renderDevice,
            (ref GraphicsEvents.DeviceLostEvent _) => ResetCachedContextState());
    }

    public override void Dispose()
    {
        _diligentViewport?.Dispose();
        _diligentViewport = null;
        Array.Clear(_currentRenderTargetHandles);
        _currentDepthStencilHandle = null;
        base.Dispose();
    }

    /// <summary>
    /// Reset statistics.
    /// </summary>
    public void ResetStats()
    {
        _stats.NumPrimitives = 0;
        _stats.NumDraws = 0;
        _stats.NumDispatches = 0;
    }

    /// <summary>
    /// Reset render targets and depth-stencil buffer.
    /// </summary>
    public void ResetRenderTargets()
    {
        // C++: SetRenderTargets(ea::nullopt, {});
        SetRenderTargets(default, ReadOnlySpan<RenderTargetView>.Empty);
    }

    /// <summary>
    /// Set backbuffer render target and associated depth-stencil buffer.
    /// </summary>
    public void SetSwapChainRenderTargets()
    {
        SetRenderTargets(RenderTargetView.SwapChainDepthStencil(_renderDevice), _swapChainColorTarget);
    }

    /// <summary>
    /// Set specified render targets and depth-stencil buffer. Either can be null.
    /// </summary>
    public void SetRenderTargets(RenderTargetView? depthStencil, ReadOnlySpan<RenderTargetView> renderTargets)
    {
        // C++ RenderContext::SetRenderTargets — bind via handle_ directly, do not set viewport.
        if (renderTargets.Length > RenderApiLimits.MaxRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(renderTargets));

        var isDepthSwapChain = depthStencil.GetValueOrDefault().IsSwapChain;
        var isColorSwapChain = renderTargets.Length == 1 && renderTargets[0].IsSwapChain;
        var isSingleView = renderTargets.Length + (depthStencil.HasValue ? 1 : 0) == 1;
        if (isDepthSwapChain != isColorSwapChain && !isSingleView)
        {
            Log.GetLogger().LogError("Cannot mix swap chain and non-swap chain views");
            return;
        }

        // It's pointless to mark swap chain views as dirty because they cannot be read.
        _isSwapChain = isDepthSwapChain || isColorSwapChain;
        if (!_isSwapChain)
        {
            if (depthStencil is { } depthStencilView)
                depthStencilView.MarkDirty();
            for (var i = 0; i < renderTargets.Length; ++i)
                renderTargets[i].MarkDirty();
        }

        Array.Clear(_currentRenderTargets, 0, _numCurrentRenderTargets);
        Array.Clear(_currentRenderTargetHandles, 0, _currentRenderTargetHandles.Length);
        renderTargets.CopyTo(_currentRenderTargets);
        _numCurrentRenderTargets = renderTargets.Length;
        _currentDepthStencil = depthStencil;
        _currentDepthStencilHandle = null;

        UpdateCurrentRenderTargetInfo();

        // C++ always has a live device context; managed Graphics may be uninitialized
        // (e.g. Renderer.Render before Graphics.Initialize). Skip GPU bind until ready.
        var handle = GetHandle();
        if (handle is null || handle.IsNull)
            return;

        // C++: currentDepthStencil_ = depthStencil ? depthStencil->GetView() : nullptr;
        _currentDepthStencilHandle = depthStencil is { } depthView
            ? depthView.GetView()
            : null;
        for (var i = 0; i < _numCurrentRenderTargets; ++i)
            _currentRenderTargetHandles[i] = _currentRenderTargets[i].GetView();

        // C++: handle_->SetRenderTargets(..., TRANSITION); — no viewport change here.
        handle.SetRenderTargets(
            (uint)_numCurrentRenderTargets,
            _currentRenderTargetHandles.AsSpan(0, _numCurrentRenderTargets),
            _currentDepthStencilHandle!,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
    }

    /// <summary>
    /// Set viewport that covers the entirety of currently bound render target.
    /// </summary>
    public void SetFullViewport()
    {
        var size = GetCurrentRenderTargetSize();
        if (size.X <= 0 || size.Y <= 0)
        {
            Log.GetLogger().LogError("Depth-stencil buffer or render target must be bound to call SetFullViewport");
            return;
        }

        SetViewport(new IntRect(0, 0, size.X, size.Y));
    }

    /// <summary>
    /// Set viewport that covers the specified area of currently bound render targets.
    /// </summary>
    public void SetViewport(IntRect viewport)
    {
        // C++ RenderContext::SetViewport — clamp then handle_->SetViewports.
        var viewportMin = viewport.Min;
        var viewportMax = viewport.Max;
        _currentViewport = new IntRect(
            new Vector2i(Math.Max(viewportMin.X, 0), Math.Max(viewportMin.Y, 0)),
            new Vector2i(Math.Min(viewportMax.X, _currentDimensions.X), Math.Min(viewportMax.Y, _currentDimensions.Y)));

        var handle = GetHandle();
        if (handle is null || handle.IsNull)
            return;

        var diligentViewport = _diligentViewport ??= new global::RedBolt.Bindings.Diligent.Viewport();
        diligentViewport.TopLeftX = _currentViewport.Left;
        diligentViewport.TopLeftY = _currentViewport.Top;
        diligentViewport.Width = _currentViewport.Width;
        diligentViewport.Height = _currentViewport.Height;
        diligentViewport.MinDepth = 0.0f;
        diligentViewport.MaxDepth = 1.0f;
        handle.SetViewports(1, diligentViewport, 0, 0);
    }

    /// <summary>
    /// Clear depth-stencil buffer. Viewport and scissor are ignored.
    /// </summary>
    public void ClearDepthStencil(ClearTarget flags, float depth = 1.0f, uint stencil = 0)
    {
        // C++ RenderContext::ClearDepthStencil clears currentDepthStencil_ via handle_ directly.
        if ((flags & ClearTarget.Depth) == 0 && (flags & ClearTarget.Stencil) == 0)
        {
            Log.GetLogger().LogError("At least one of CLEAR_DEPTH or CLEAR_STENCIL must be set to call ClearDepthStencil");
            return;
        }

        if (_currentDepthStencilHandle is null || _currentDepthStencilHandle.IsNull)
        {
            Log.GetLogger().LogError("Depth-stencil buffer must be bound to call ClearDepthStencil");
            return;
        }

        var handle = GetHandle();
        if (handle is null || handle.IsNull)
            return;

        var internalFlags = ClearDepthStencilFlags.ClearDepthFlagNone;
        if ((flags & ClearTarget.Depth) != 0)
            internalFlags |= ClearDepthStencilFlags.ClearDepthFlag;
        // C++: only clear stencil when the bound view format is depth-stencil.
        if ((flags & ClearTarget.Stencil) != 0 && IsBoundDepthStencilFormat(_currentDepthStencilHandle))
            internalFlags |= ClearDepthStencilFlags.ClearStencilFlag;

        handle.ClearDepthStencil(
            _currentDepthStencilHandle,
            internalFlags,
            depth,
            checked((byte)stencil),
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
    }

    /// <summary>
    /// Clear render target at given index. Viewport and scissor are ignored.
    /// </summary>
    public void ClearRenderTarget(uint index, Color color)
    {
        if (index >= _numCurrentRenderTargets)
        {
            Log.GetLogger().LogError("Render target must be bound to call ClearRenderTarget");
            return;
        }

        var rtHandle = _currentRenderTargetHandles[index];
        if (rtHandle is null || rtHandle.IsNull)
        {
            Log.GetLogger().LogError("Render target must be bound to call ClearRenderTarget");
            return;
        }

        // C++ RenderContext::ClearRenderTarget clears via handle_ directly.
        var handle = GetHandle();
        if (handle is null || handle.IsNull)
            return;

        Span<float> rgba = stackalloc float[4];
        rgba[0] = color.R;
        rgba[1] = color.G;
        rgba[2] = color.B;
        rgba[3] = color.A;
        unsafe
        {
            fixed (float* pRgba = rgba)
            {
                handle.ClearRenderTarget(
                    rtHandle,
                    (nint)pRgba,
                    ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            }
        }
    }

    /// <summary>
    /// Set whether the clip plane is enabled. Only used for OpenGL.
    /// </summary>
    public void SetClipPlaneMask(uint mask)
    {
        if (_clipPlaneMask == mask || !_renderDevice.GetCaps().ClipDistance)
            return;

        _clipPlaneMask = mask;
        if (_renderDevice.GetBackend() != RenderBackend.OpenGL)
            return;
        if (GetGraphics() is null)
            return;

        _glEnable ??= TryLoadOpenGlProcedure<OpenGlStateFunction>("glEnable");
        _glDisable ??= TryLoadOpenGlProcedure<OpenGlStateFunction>("glDisable");
        if (_glEnable is null || _glDisable is null)
            return;

        for (var i = 0; i < 8; ++i)
        {
            var capability = GlClipDistance0 + (uint)i;
            if ((mask & (1u << i)) != 0)
                _glEnable(capability);
            else
                _glDisable(capability);
        }
    }

    /// <summary>
    /// Execute draw commands from queue.
    /// </summary>
    public void Execute(DrawCommandQueue drawQueue)
    {
        ArgumentNullException.ThrowIfNull(drawQueue);
        drawQueue.ExecuteInContext(this);
    }

    /// <summary>
    /// Check if the texture is already bound as a render target.
    /// </summary>
    public bool IsBoundAsRenderTarget(RawTexture? texture)
    {
        // C++ only checks color RTs (currentRenderTargets_), not depth-stencil.
        if (texture is null)
            return false;

        var handle = texture.GetHandles().Texture;
        for (var i = 0; i < _numCurrentRenderTargets; ++i)
        {
            if (_currentRenderTargetHandles[i]?.GetTexture() == handle)
                return true;
        }

        return false;
    }

    public IDeviceContext? GetHandle() => GetGraphics()?.GetImmediateContext();

    public RenderDeviceStats GetStats() => _stats;

    public ref RenderDeviceStats GetMutableStats() => ref _stats;

    public RenderDevice GetRenderDevice() => _renderDevice;

    public RenderPool GetRenderPool() => _renderPool;

    public bool IsDebugScopeEnabled() => _debugScopeEnabled;

    public Vector2i GetCurrentRenderTargetSize()
    {
        return _currentDimensions;
    }

    public PipelineStateOutputDesc GetCurrentRenderTargetsDesc() => _currentOutputDesc;

    public bool IsSwapChainRenderTarget() => _isSwapChain;

    public IntRect GetCurrentViewport() => _currentViewport;

    private void ResetCachedContextState()
    {
        _clipPlaneMask = 0;
        _glEnable = null;
        _glDisable = null;
    }

    /// <summary>
    /// C++ IsDepthStencilTextureFormat(currentDepthStencil_->GetTexture()->GetDesc().Format).
    /// </summary>
    private static bool IsBoundDepthStencilFormat(ITextureView depthStencilView)
    {
        using var texture = depthStencilView.GetTexture();
        if (texture is null || texture.IsNull)
            return false;
        using var desc = texture.GetDesc();
        return desc.Format is global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD24UnormS8Uint
            or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32FloatS8x24Uint;
    }

    private static T? TryLoadOpenGlProcedure<T>(string name)
        where T : Delegate
    {
        using var pinnedName = new CString(name);
        var procedure = SdlNativeBindings.SdlGlGetProcAddress(pinnedName.Bytes);
        if (procedure == IntPtr.Zero)
            return null;

        return Marshal.GetDelegateForFunctionPointer<T>(procedure);
    }

    private void UpdateCurrentRenderTargetInfo()
    {
        var graphics = GetGraphics();
        if (graphics is null)
            return;

        var depthStencilView = _currentDepthStencil;
        _currentOutputDesc.DepthStencilFormat = depthStencilView is { } depthView
            ? GetRenderTargetFormat(depthView, graphics)
            : TextureFormat.TexFormatUnknown;

        Span<TextureFormat> renderTargetFormats = stackalloc TextureFormat[checked((int)RenderApiLimits.MaxRenderTargets)];
        for (var i = 0; i < _numCurrentRenderTargets; ++i)
            renderTargetFormats[i] = GetRenderTargetFormat(_currentRenderTargets[i], graphics);
        _currentOutputDesc.NumRenderTargets = _numCurrentRenderTargets;
        renderTargetFormats[.._numCurrentRenderTargets].CopyTo(_currentOutputDesc.RenderTargetFormats);

        RenderTargetView? view = _numCurrentRenderTargets != 0
            ? _currentRenderTargets[0]
            : depthStencilView;
        _currentDimensions = view is { } target ? GetRenderTargetDimensions(target) : default;
        _currentOutputDesc.MultiSample = view is { } multisampledTarget
            ? GetRenderTargetMultiSample(multisampledTarget, graphics)
            : 1;
    }

    private Graphics? GetGraphics() => GetSubsystem<Graphics>();

    private Vector2i ResolveRenderTargetSize(ReadOnlySpan<RenderTargetView> colorTargets, RenderTargetView? depthStencil)
    {
        var depthStencilView = depthStencil;
        if ((colorTargets.Length != 0 && colorTargets[0].IsSwapChain) || depthStencilView?.IsSwapChain == true)
            return _renderDevice.GetSwapChainSize();
        if (colorTargets.Length != 0 && colorTargets[0].RenderTexture is { } colorTexture)
            return new Vector2i(colorTexture.Width, colorTexture.Height);
        if (colorTargets.Length != 0 && colorTargets[0].GetRawTexture() is { } colorRawTexture)
            return new Vector2i(colorRawTexture.GetParams().Size.X, colorRawTexture.GetParams().Size.Y);
        if (depthStencilView?.RenderTexture is { } depthTexture)
            return new Vector2i(depthTexture.Width, depthTexture.Height);
        if (depthStencilView?.GetRawTexture() is { } depthRawTexture)
            return new Vector2i(depthRawTexture.GetParams().Size.X, depthRawTexture.GetParams().Size.Y);
        return default;
    }

    private TextureFormat GetRenderTargetFormat(RenderTargetView view, Graphics graphics)
    {
        if (view.GetRawTexture() is { } rawTexture)
            return rawTexture.GetParams().Format;
        if (view.RenderTexture is { } texture)
            return texture.Format;

        return view.Type == RenderTargetViewType.SwapChainDepthStencil
            ? graphics.GetSwapChainDepthTextureFormat()
            : graphics.GetSwapChainColorTextureFormat(out _);
    }

    private int GetRenderTargetMultiSample(RenderTargetView view, Graphics graphics)
    {
        if (view.GetRawTexture() is { } rawTexture)
            return checked((int)rawTexture.GetParams().MultiSample);
        return view.RenderTexture is { } texture
            ? texture.MultiSample
            : graphics.GetSwapChainMultiSample();
    }

    private Vector2i GetRenderTargetDimensions(RenderTargetView view)
    {
        if (view.IsSwapChain)
            return _renderDevice.GetSwapChainSize();
        if (view.GetRawTexture() is { } rawTexture)
            return new Vector2i(rawTexture.GetParams().Size.X, rawTexture.GetParams().Size.Y);
        if (view.RenderTexture is { } texture)
            return new Vector2i(texture.Width, texture.Height);
        return default;
    }
}
