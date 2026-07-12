using RedBolt.Bindings.Diligent;

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
    private RenderDeviceStats _maxStats = new();

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
        var currentStats = new RenderDeviceStats();
        _maxStats = Max(_maxStats, currentStats);
        return currentStats;
    }

    public RenderDeviceStats GetMaxStats() => new()
    {
        NumPrimitives = _maxStats.NumPrimitives,
        NumDraws = _maxStats.NumDraws,
        NumDispatches = _maxStats.NumDispatches
    };

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

    public IRenderDevice? GetRenderDevice() => GetSubsystem<Graphics>()?.GetNativeRenderDevice();

    public IDeviceContext? GetImmediateContext() => GetSubsystem<Graphics>()?.GetImmediateContext();

    public ISwapChain? GetSwapChain() => GetSubsystem<Graphics>()?.GetNativeSwapChain();

    public Vector2i GetSwapChainSize()
    {
        var graphics = GetSubsystem<Graphics>();
        return graphics is { IsInitialized: true }
            ? new Vector2i(graphics.Width, graphics.Height)
            : GetWindowSettings().Size;
    }

    public Vector2i GetWindowSize() => GetSwapChainSize();

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
        var frameNumber = GetSubsystem<Time>()?.GetFrameNumber() ?? 0U;
        return (FrameIndex)Math.Max((long)frameNumber, (long)FrameIndex.None);
    }

    public RawTexture? GetDefaultTexture(TextureType type) => _defaultTextures[(int)type];

    public DrawCommandQueue GetDefaultQueue() => _defaultQueue;

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

    private static RenderDeviceStats Max(RenderDeviceStats left, RenderDeviceStats right) => new()
    {
        NumPrimitives = Math.Max(left.NumPrimitives, right.NumPrimitives),
        NumDraws = Math.Max(left.NumDraws, right.NumDraws),
        NumDispatches = Math.Max(left.NumDispatches, right.NumDispatches)
    };
}
