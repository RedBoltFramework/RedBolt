using System.Runtime.InteropServices;
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
    private readonly RenderDeviceStats _stats = new();
    private readonly RenderTargetView[] _swapChainColorTarget;
    private readonly RenderTargetView[] _currentRenderTargets = new RenderTargetView[RenderApiLimits.MaxRenderTargets];
    private int _numCurrentRenderTargets;
    private OptionalRawTextureRTV _currentDepthStencil;
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
        _swapChainColorTarget = [RenderTargetView.SwapChainColor(renderDevice)];
        SubscribeToEvent<GraphicsEvents.DeviceLostEvent>(renderDevice,
            (ref GraphicsEvents.DeviceLostEvent _) => ResetCachedContextState());
    }

    public override void Dispose()
    {
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
        GetGraphics()?.UnbindRenderTargets();
        Array.Clear(_currentRenderTargets, 0, _numCurrentRenderTargets);
        _numCurrentRenderTargets = 0;
        _currentDepthStencil = default;
        _isSwapChain = false;
        UpdateCurrentRenderTargetInfo();
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
    public void SetRenderTargets(OptionalRawTextureRTV depthStencil, ReadOnlySpan<RenderTargetView> renderTargets)
    {
        var graphics = GetGraphics();
        if (graphics is null)
            return;

        if (renderTargets.Length > RenderApiLimits.MaxRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(renderTargets));
        var isDepthSwapChain = depthStencil.GetValueOrDefault()?.IsSwapChain == true;
        var isColorSwapChain = renderTargets.Length == 1 && renderTargets[0].IsSwapChain;
        var isSingleView = renderTargets.Length + (depthStencil.HasValue ? 1 : 0) == 1;
        if (isDepthSwapChain != isColorSwapChain && !isSingleView)
            throw new InvalidOperationException("Cannot mix swap chain and non-swap-chain render target views.");

        var viewportSize = ResolveRenderTargetSize(renderTargets, depthStencil);
        Array.Clear(_currentRenderTargets, 0, _numCurrentRenderTargets);
        renderTargets.CopyTo(_currentRenderTargets);
        _numCurrentRenderTargets = renderTargets.Length;
        _currentDepthStencil = depthStencil;
        _isSwapChain = isDepthSwapChain || isColorSwapChain;
        if (!_isSwapChain)
        {
            if (depthStencil.GetValueOrDefault() is { } depthStencilView)
                _renderDevice.MarkRenderTargetDirty(depthStencilView);
            for (var i = 0; i < renderTargets.Length; ++i)
                _renderDevice.MarkRenderTargetDirty(renderTargets[i]);
        }
        UpdateCurrentRenderTargetInfo();

        if (_numCurrentRenderTargets == 0 && !_currentDepthStencil.HasValue)
        {
            graphics.UnbindRenderTargets();
            _currentViewport = default;
        }
        else
        {
            graphics.SetRenderTargets(new IntRect(0, 0, viewportSize.X, viewportSize.Y), renderTargets, depthStencil.GetValueOrDefault());
            _currentViewport = new IntRect(0, 0, viewportSize.X, viewportSize.Y);
        }
    }

    /// <summary>
    /// Set viewport that covers the entirety of currently bound render target.
    /// </summary>
    public void SetFullViewport()
    {
        var size = GetCurrentRenderTargetSize();
        if (size.X <= 0 || size.Y <= 0)
            return;

        SetViewport(new IntRect(0, 0, size.X, size.Y));
    }

    /// <summary>
    /// Set viewport that covers the specified area of currently bound render targets.
    /// </summary>
    public void SetViewport(IntRect viewport)
    {
        var viewportMin = viewport.Min;
        var viewportMax = viewport.Max;
        _currentViewport = new IntRect(
            new Vector2i(Math.Max(viewportMin.X, 0), Math.Max(viewportMin.Y, 0)),
            new Vector2i(Math.Min(viewportMax.X, _currentDimensions.X), Math.Min(viewportMax.Y, _currentDimensions.Y)));
        GetGraphics()?.SetViewport(_currentViewport);
    }

    /// <summary>
    /// Clear depth-stencil buffer. Viewport and scissor are ignored.
    /// </summary>
    public void ClearDepthStencil(ClearTarget flags, float depth = 1.0f, uint stencil = 0)
    {
        GetGraphics()?.ClearCurrentRenderTargets(
            color: null,
            clearColorTarget: false,
            clearDepth: (flags & ClearTarget.Depth) != 0,
            clearStencil: (flags & ClearTarget.Stencil) != 0,
            depth: depth,
            stencil: checked((byte)stencil));
    }

    /// <summary>
    /// Clear render target at given index. Viewport and scissor are ignored.
    /// </summary>
    public void ClearRenderTarget(uint index, Color color)
    {
        if (index >= _numCurrentRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(index));

        GetGraphics()?.ClearRenderTarget(_currentRenderTargets[index], color);
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
    public bool IsBoundAsRenderTarget(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (_currentDepthStencil.GetValueOrDefault()?.RenderTexture == texture)
            return true;

        for (var i = 0; i < _numCurrentRenderTargets; ++i)
        {
            var view = _currentRenderTargets[i];
            if (view.RenderTexture == texture)
                return true;
        }

        return false;
    }

    public IDeviceContext? GetHandle() => GetGraphics()?.GetImmediateContext();

    public RenderDeviceStats GetStats() => _stats;

    public RenderDeviceStats GetMutableStats() => _stats;

    public RenderDevice GetRenderDevice() => _renderDevice;

    public RenderPool GetRenderPool() => _renderPool;

    public bool IsDebugScopeEnabled() => true;

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

        var depthStencilView = _currentDepthStencil.GetValueOrDefault();
        _currentOutputDesc.DepthStencilFormat = depthStencilView is { } depthView
            ? GetRenderTargetFormat(depthView, graphics)
            : TextureFormat.Unknown;

        Span<TextureFormat> renderTargetFormats = stackalloc TextureFormat[checked((int)RenderApiLimits.MaxRenderTargets)];
        for (var i = 0; i < _numCurrentRenderTargets; ++i)
            renderTargetFormats[i] = GetRenderTargetFormat(_currentRenderTargets[i], graphics);
        _currentOutputDesc.SetRenderTargetFormats(renderTargetFormats[.._numCurrentRenderTargets]);

        RenderTargetView? view = _numCurrentRenderTargets != 0
            ? _currentRenderTargets[0]
            : depthStencilView;
        _currentDimensions = view is { } target ? GetRenderTargetDimensions(target) : default;
        _currentOutputDesc.MultiSample = view is { } multisampledTarget
            ? checked((uint)GetRenderTargetMultiSample(multisampledTarget, graphics))
            : 1u;
    }

    private Graphics? GetGraphics() => GetSubsystem<Graphics>();

    private Vector2i ResolveRenderTargetSize(ReadOnlySpan<RenderTargetView> colorTargets, OptionalRawTextureRTV depthStencil)
    {
        var depthStencilView = depthStencil.GetValueOrDefault();
        if ((colorTargets.Length != 0 && colorTargets[0].IsSwapChain) || depthStencilView?.IsSwapChain == true)
            return _renderDevice.GetSwapChainSize();
        if (colorTargets.Length != 0 && colorTargets[0].RenderTexture is { } colorTexture)
            return new Vector2i(colorTexture.Width, colorTexture.Height);
        if (depthStencilView?.RenderTexture is { } depthTexture)
            return new Vector2i(depthTexture.Width, depthTexture.Height);
        return default;
    }

    private TextureFormat GetRenderTargetFormat(RenderTargetView view, Graphics graphics)
    {
        if (view.RenderTexture is { } texture)
            return texture.Format;

        return view.Type == RenderTargetViewType.SwapChainDepthStencil
            ? graphics.GetSwapChainDepthTextureFormat()
            : graphics.GetSwapChainColorTextureFormat(out _);
    }

    private int GetRenderTargetMultiSample(RenderTargetView view, Graphics graphics)
    {
        return view.RenderTexture is { } texture
            ? texture.MultiSample
            : graphics.GetSwapChainMultiSample();
    }

    private Vector2i GetRenderTargetDimensions(RenderTargetView view)
    {
        if (view.IsSwapChain)
            return _renderDevice.GetSwapChainSize();
        if (view.RenderTexture is { } texture)
            return new Vector2i(texture.Width, texture.Height);
        return default;
    }
}
