using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

/// <summary>
/// Wrapper for window and GAPI backend.
/// </summary>
public sealed class RenderDevice : Object
{
    private bool _defaultTextureParametersDirty;
    private TextureFilterMode _defaultTextureFilterMode = TextureFilterMode.Trilinear;
    private int _defaultTextureAnisotropy = 4;
    private readonly object _deviceObjectsLock = new();
    private readonly List<DeviceObject> _deviceObjects = [];
    private readonly RenderPool _renderPool;
    private readonly RenderContext _renderContext;
    private readonly DrawCommandQueue _defaultQueue;
    private readonly RawTexture?[] _defaultTextures = new RawTexture?[(int)TextureType.Texture2DArray + 1];
    private readonly List<WeakReference<PipelineState>> _pipelineStatesToReload = [];
    private FrameIndex _frameIndex = FrameIndex.First;
    private readonly Timer _statsTimer = new();
    private RenderDeviceStats _stats = new();
    private RenderDeviceStats _maxStats = new();
    private RenderDeviceStats _previousMaxStats = new();
    private const uint StatsPeriodMs = 333;

    public RenderDevice()
    {
        _renderPool = new RenderPool(this);
        _renderContext = new RenderContext(this);
        _defaultQueue = new DrawCommandQueue(this);
    }

    public override void Dispose()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Destroy);
        GetSubsystem<Graphics>()?.GetImmediateContext()?.WaitForIdle();
        ReleaseDefaultObjects();
        _renderContext.Dispose();
        _renderPool.Dispose();
        _defaultQueue.Dispose();
        base.Dispose();
    }

    public void PostInitialize()
    {
        InitializeDefaultObjects();
    }

    public void SetDefaultTextureFilterMode(TextureFilterMode filterMode)
    {
        if (filterMode == TextureFilterMode.Default)
            throw new ArgumentException("Invalid texture filter mode.", nameof(filterMode));

        if (_defaultTextureFilterMode == filterMode)
            return;

        _defaultTextureFilterMode = filterMode;
        _defaultTextureParametersDirty = true;
    }

    public void SetDefaultTextureAnisotropy(int anisotropy)
    {
        anisotropy = Math.Max(1, anisotropy);

        if (_defaultTextureAnisotropy == anisotropy)
            return;

        _defaultTextureAnisotropy = anisotropy;
        _defaultTextureParametersDirty = true;
    }

    /// <summary>
    /// Restore device if is was lost. Only applicable for Android.
    /// </summary>
    public bool Restore()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Restore);
        SendEvent<GraphicsEvents.DeviceResetEvent>();
        InitializeDefaultObjects();
        return true;
    }

    /// <summary>
    /// Emulate device loss and restore. Only applicable for Android.
    /// </summary>
    public bool EmulateLossAndRestore()
    {
        ReleaseDefaultObjects();
        SendDeviceObjectEvent(DeviceObjectEvent.Invalidate);
        SendEvent<GraphicsEvents.DeviceLostEvent>();
        SendDeviceObjectEvent(DeviceObjectEvent.Restore);
        SendEvent<GraphicsEvents.DeviceResetEvent>();
        InitializeDefaultObjects();
        return true;
    }

    /// <summary>
    /// Queue pipeline state reload at the end of the frame.
    /// </summary>
    public void QueuePipelineStateReload(PipelineState pipelineState)
    {
        _pipelineStatesToReload.Add(new WeakReference<PipelineState>(pipelineState));
    }

    /// <summary>
    /// Present the frame. Should be called between engine frames.
    /// </summary>
    public void Present()
    {
        var swapChain = GetSwapChain();
        if (swapChain is not null && !swapChain.IsNull)
            swapChain.Present(GetWindowSettings().VSync ? 1u : 0u);

        _renderPool.OnFrameEnd();

        foreach (var weakPipelineState in _pipelineStatesToReload)
        {
            if (weakPipelineState.TryGetTarget(out var pipelineState))
                pipelineState.Restore();
        }
        _pipelineStatesToReload.Clear();

        if (_defaultTextureParametersDirty)
        {
            _defaultTextureParametersDirty = false;

            DeviceObject[] snapshot;
            lock (_deviceObjectsLock)
                snapshot = [.. _deviceObjects];

            foreach (var deviceObject in snapshot)
            {
                if (deviceObject is PipelineState pipelineState)
                {
                    pipelineState.Invalidate();
                    pipelineState.Restore();
                }
            }
        }

        var contextStats = _renderContext.GetStats();
        _stats.NumPrimitives = contextStats.NumPrimitives;
        _stats.NumDraws = contextStats.NumDraws;
        _stats.NumDispatches = contextStats.NumDispatches;
        _renderContext.ResetStats();

        if (_statsTimer.GetMSec() >= StatsPeriodMs)
        {
            _statsTimer.Reset();
            CopyStats(_previousMaxStats, _maxStats);
            CopyStats(_maxStats, _stats);
        }
        else
        {
            _maxStats.NumPrimitives = Math.Max(_maxStats.NumPrimitives, _stats.NumPrimitives);
            _maxStats.NumDraws = Math.Max(_maxStats.NumDraws, _stats.NumDraws);
            _maxStats.NumDispatches = Math.Max(_maxStats.NumDispatches, _stats.NumDispatches);
        }

        _frameIndex = (FrameIndex)((long)_frameIndex + 1);
    }

    /// <summary>
    /// Check if texture format is supported on hardware.
    /// </summary>
    public bool IsTextureFormatSupported(TextureFormat format)
    {
        return format != TextureFormat.Unknown;
    }

    /// <summary>
    /// Check if texture format is supported on hardware as render target or depth stencil.
    /// </summary>
    public bool IsRenderTargetFormatSupported(TextureFormat format)
    {
        var graphics = GetSubsystem<Graphics>();
        return graphics?.IsRenderTargetFormatSupported(format) ?? false;
    }

    /// <summary>
    /// Check if texture format is supported as UAV.
    /// </summary>
    public bool IsUnorderedAccessFormatSupported(TextureFormat format)
    {
        return format is TextureFormat.R8 or TextureFormat.RG16F or TextureFormat.RGBA8 or TextureFormat.RGBA16F;
    }

    /// <summary>
    /// Check if given level of MSAA is supported on hardware.
    /// </summary>
    public bool IsMultiSampleSupported(TextureFormat format, int multiSample)
    {
        return GetSupportedMultiSample(format, multiSample) == multiSample;
    }

    /// <summary>
    /// Select supported multi-sample level for given format.
    /// </summary>
    public int GetSupportedMultiSample(TextureFormat format, int multiSample)
    {
        var graphics = GetSubsystem<Graphics>();
        return graphics?.GetSupportedMultiSample(format, multiSample) ?? 1;
    }

    public RenderBackend GetBackend() => GetDeviceSettings().Backend;

    public RenderDeviceSettings GetDeviceSettings()
    {
        var engine = GetSubsystem<Engine>();
        var parameters = engine?.Parameters ?? new EngineParameters();

        return new RenderDeviceSettings
        {
            Backend = MapRenderBackend(parameters.RenderBackend),
            ExternalWindowHandle = parameters.ExternalWindow,
            GpuDebug = parameters.GPUDebug,
            AdapterId = parameters.RenderAdapterId
        };
    }

    public WindowSettings GetWindowSettings()
    {
        var engine = GetSubsystem<Engine>();
        var graphics = GetSubsystem<Graphics>();
        var parameters = engine?.Parameters ?? new EngineParameters();

        return new WindowSettings
        {
            Mode = GetWindowMode(parameters),
            Size = new Vector2i(
                graphics is { IsInitialized: true } ? graphics.Width : parameters.WindowWidth,
                graphics is { IsInitialized: true } ? graphics.Height : parameters.WindowHeight),
            Title = parameters.WindowTitle,
            Resizable = parameters.WindowResizable,
            Monitor = parameters.Monitor,
            VSync = parameters.VSync,
            RefreshRate = parameters.RefreshRate,
            MultiSample = parameters.MultiSample,
            SRgb = true,
            Orientations = ParseOrientations(parameters.Orientations)
        };
    }

    public RenderDeviceCaps GetCaps()
    {
        var graphics = GetSubsystem<Graphics>();
        return graphics?.GetRenderDeviceCaps() ?? new RenderDeviceCaps();
    }

    public RenderDeviceStats GetStats()
    {
        return _stats;
    }

    public RenderDeviceStats GetMaxStats() => _previousMaxStats;

    public TextureFilterMode GetDefaultTextureFilterMode()
    {
        return _defaultTextureFilterMode;
    }

    public int GetDefaultTextureAnisotropy()
    {
        return _defaultTextureAnisotropy;
    }

    public TextureFormat GetDefaultDepthStencilFormat()
    {
        var graphics = GetSubsystem<Graphics>();
        if (graphics is { IsInitialized: true })
            return graphics.GetSwapChainOutputTarget().DepthStencilFormat;

        return TextureFormat.Depth24Stencil8;
    }

    public TextureFormat GetDefaultDepthFormat() => GetDefaultDepthStencilFormat();

    public RenderContext GetRenderContext() => _renderContext;

    public RenderPool GetRenderPool() => _renderPool;

    public SdlWindow? GetSDLWindow() => GetSubsystem<Graphics>()?.GetNativeWindow();

    public nint GetMetalView() => nint.Zero;

    public IEngineFactory? GetFactory() => GetSubsystem<Graphics>()?.GetNativeFactory();

    public IRenderDevice? GetRenderDevice() => GetSubsystem<Graphics>()?.GetNativeRenderDevice();

    public IDeviceContext? GetImmediateContext() => GetSubsystem<Graphics>()?.GetImmediateContext();

    public ISwapChain? GetSwapChain() => GetSubsystem<Graphics>()?.GetNativeSwapChain();

    public Vector2i GetSwapChainSize()
    {
        var swapChain = GetSwapChain();
        if (swapChain is null || swapChain.IsNull)
            return Vector2i.Zero;

        using var desc = swapChain.GetDesc();
        return new Vector2i((int)desc.Width, (int)desc.Height);
    }

    public Vector2i GetWindowSize() => GetWindowSettings().Size;

    public float GetDpiScale()
    {
        var swapChainSize = GetSwapChainSize();
        var windowSize = GetWindowSize();
        var ratioX = (float)swapChainSize.X / windowSize.X;
        var ratioY = (float)swapChainSize.Y / windowSize.Y;
        return MathDefs.SnapRound((ratioX + ratioY) / 2.0f, 0.05f);
    }

    public FrameIndex GetFrameIndex()
    {
        return _frameIndex;
    }

    public RawTexture? GetDefaultTexture(TextureType type) => _defaultTextures[(int)type];

    public DrawCommandQueue GetDefaultQueue() => _defaultQueue;

    public static IReadOnlyList<FullscreenMode> GetFullscreenModes(int monitor)
    {
        var result = new List<FullscreenMode>();
        var numModes = SdlNativeBindings.SdlGetNumDisplayModes(monitor);
        for (var i = 0; i < numModes; ++i)
        {
            using var mode = new SdlDisplayMode();
            SdlNativeBindings.SdlGetDisplayMode(monitor, i, mode);
            result.Add(new FullscreenMode(new Vector2i(mode.W, mode.H), mode.RefreshRate));
        }

        result.Sort(static (left, right) =>
        {
            var comparison = left.Size.X.CompareTo(right.Size.X);
            if (comparison != 0)
                return comparison;

            comparison = left.Size.Y.CompareTo(right.Size.Y);
            return comparison != 0 ? comparison : left.RefreshRate.CompareTo(right.RefreshRate);
        });

        for (var i = result.Count - 1; i > 0; --i)
        {
            if (result[i] == result[i - 1])
                result.RemoveAt(i);
        }

        return result;
    }

    public static uint GetClosestFullscreenModeIndex(IReadOnlyList<FullscreenMode> modes, FullscreenMode desiredMode)
    {
        for (var i = 0; i < modes.Count; ++i)
        {
            if (modes[i] == desiredMode)
                return (uint)i;
        }

        var upperBound = 0;
        while (upperBound < modes.Count && CompareFullscreenMode(modes[upperBound], desiredMode.Size, int.MaxValue) <= 0)
            ++upperBound;

        if (upperBound > 0 && modes[upperBound - 1].Size == desiredMode.Size)
            return (uint)(upperBound - 1);

        if (upperBound < modes.Count)
        {
            for (var i = upperBound; i < modes.Count; ++i)
            {
                if (modes[i].RefreshRate >= desiredMode.RefreshRate)
                    return (uint)i;
            }

            return (uint)upperBound;
        }

        return (uint)(modes.Count - 1);
    }

    public static FullscreenMode GetClosestFullscreenMode(IReadOnlyList<FullscreenMode> modes, FullscreenMode desiredMode)
    {
        var index = GetClosestFullscreenModeIndex(modes, desiredMode);
        return modes[(int)index];
    }

    internal ITextureView ResolveRenderTargetView(RenderTargetView view)
    {
        var graphics = GetSubsystem<Graphics>() ?? throw new InvalidOperationException("Graphics subsystem is not available.");
        return graphics.ResolveRenderTargetView(view);
    }

    internal ITexture? GetTextureHandle(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        var graphics = GetSubsystem<Graphics>();
        return graphics?.GetTextureHandle(texture);
    }

    internal ITextureView? CreateTextureUnorderedAccessView(RawTexture texture, RawTextureUAVKey key)
    {
        ArgumentNullException.ThrowIfNull(texture);
        var graphics = GetSubsystem<Graphics>();
        return graphics?.CreateTextureUnorderedAccessView(texture, key);
    }

    internal void MarkRenderTargetDirty(RenderTargetView view)
    {
        var graphics = GetSubsystem<Graphics>() ?? throw new InvalidOperationException("Graphics subsystem is not available.");
        graphics.MarkRenderTargetDirty(view);
    }

    internal RenderOutputTargetInfo GetSwapChainOutputTarget()
    {
        var graphics = GetSubsystem<Graphics>() ?? throw new InvalidOperationException("Graphics subsystem is not available.");
        return graphics.GetSwapChainOutputTarget();
    }

    internal void AddDeviceObject(DeviceObject deviceObject)
    {
        ArgumentNullException.ThrowIfNull(deviceObject);

        lock (_deviceObjectsLock)
        {
            if (!_deviceObjects.Contains(deviceObject))
                _deviceObjects.Add(deviceObject);
        }
    }

    internal void RemoveDeviceObject(DeviceObject deviceObject)
    {
        ArgumentNullException.ThrowIfNull(deviceObject);

        lock (_deviceObjectsLock)
            _deviceObjects.Remove(deviceObject);
    }

    internal void OnEngineShutdown()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Destroy);
        _renderPool.Invalidate();
    }

    private void SendDeviceObjectEvent(DeviceObjectEvent deviceObjectEvent)
    {
        DeviceObject[] snapshot;
        lock (_deviceObjectsLock)
            snapshot = [.. _deviceObjects];

        foreach (var deviceObject in snapshot)
            deviceObject.ProcessDeviceObjectEvent(deviceObjectEvent);
    }

    private void InitializeDefaultObjects()
    {
        var data = new byte[4];
        InitializeDefaultTexture(TextureType.Texture2D, data);
        InitializeDefaultTexture(TextureType.TextureCube, data);
        InitializeDefaultTexture(TextureType.Texture3D, data);
        InitializeDefaultTexture(TextureType.Texture2DArray, data);
        _renderPool.Restore();
    }

    private void InitializeDefaultTexture(TextureType type, ReadOnlySpan<byte> data)
    {
        var texture = new RawTexture(new RawTextureParams(type, TextureFormat.RGBA8, TextureFlag.None, Vector3i.One));
        if (!texture.IsValid())
        {
            texture.Dispose();
            return;
        }

        texture.Update(0u, Vector3i.Zero, Vector3i.One, 0u, data);
        _defaultTextures[(int)type] = texture;
    }

    private void ReleaseDefaultObjects()
    {
        for (var i = 0; i < _defaultTextures.Length; ++i)
        {
            _defaultTextures[i]?.Dispose();
            _defaultTextures[i] = null;
        }

        _renderPool.Invalidate();
    }

    private static IReadOnlyList<string> ParseOrientations(string orientations)
    {
        if (string.IsNullOrWhiteSpace(orientations))
            return [];

        return orientations.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static WindowMode GetWindowMode(EngineParameters parameters)
    {
        if (parameters.FullScreen)
            return WindowMode.Fullscreen;
        if (parameters.Borderless)
            return WindowMode.Borderless;
        return WindowMode.Windowed;
    }

    private static RenderBackend MapRenderBackend(GraphicsBackend backend) => backend switch
    {
        GraphicsBackend.Auto => RenderBackend.OpenGL,
        GraphicsBackend.OpenGL => RenderBackend.OpenGL,
        GraphicsBackend.Vulkan => RenderBackend.Vulkan,
        GraphicsBackend.D3D11 => RenderBackend.D3D11,
        GraphicsBackend.D3D12 => RenderBackend.D3D12,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
    };

    private static int CompareFullscreenMode(FullscreenMode mode, Vector2i size, int refreshRate)
    {
        var comparison = mode.Size.X.CompareTo(size.X);
        if (comparison != 0)
            return comparison;

        comparison = mode.Size.Y.CompareTo(size.Y);
        return comparison != 0 ? comparison : mode.RefreshRate.CompareTo(refreshRate);
    }

    private static void CopyStats(RenderDeviceStats destination, RenderDeviceStats source)
    {
        destination.NumPrimitives = source.NumPrimitives;
        destination.NumDraws = source.NumDraws;
        destination.NumDispatches = source.NumDispatches;
    }
}
