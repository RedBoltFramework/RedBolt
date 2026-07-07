namespace RedBolt;

public sealed class RenderDevice : Object
{
    private readonly object deviceObjectsLock = new();
    private readonly List<DeviceObject> deviceObjects = [];
    private readonly RenderPool renderPool;
    private readonly RenderContext renderContext;
    private readonly DrawCommandQueue defaultQueue;
    private RenderDeviceStats maxStats = new();

    public RenderDevice()
    {
        renderPool = new RenderPool(this);
        renderContext = new RenderContext(this);
        defaultQueue = new DrawCommandQueue(this);
    }

    public bool Restore()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Restore);
        return true;
    }

    public bool EmulateLossAndRestore()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Invalidate);
        SendDeviceObjectEvent(DeviceObjectEvent.Restore);
        return true;
    }

    public bool IsTextureFormatSupported(TextureFormat format)
    {
        return format != TextureFormat.Unknown;
    }

    public bool IsRenderTargetFormatSupported(TextureFormat format)
    {
        var graphics = GetSubsystem<Graphics>();
        return graphics?.IsRenderTargetFormatSupported(format) ?? false;
    }

    public bool IsUnorderedAccessFormatSupported(TextureFormat format)
    {
        _ = format;
        return false;
    }

    public bool IsMultiSampleSupported(TextureFormat format, int multiSample)
    {
        return GetSupportedMultiSample(format, multiSample) == multiSample;
    }

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
            Size = new IntVector2(
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
        maxStats = Max(maxStats, currentStats);
        return currentStats;
    }

    public RenderDeviceStats GetMaxStats() => new()
    {
        NumPrimitives = maxStats.NumPrimitives,
        NumDraws = maxStats.NumDraws,
        NumDispatches = maxStats.NumDispatches
    };

    public TextureFilterMode GetDefaultTextureFilterMode()
    {
        return GetSubsystem<Renderer>()?.GetTextureFilterMode() ?? TextureFilterMode.Trilinear;
    }

    public int GetDefaultTextureAnisotropy()
    {
        return GetSubsystem<Renderer>()?.GetTextureAnisotropy() ?? 1;
    }

    public TextureFormat GetDefaultDepthStencilFormat()
    {
        var graphics = GetSubsystem<Graphics>();
        if (graphics is { IsInitialized: true })
            return graphics.GetSwapChainOutputTarget().DepthStencilFormat;

        return TextureFormat.Depth24Stencil8;
    }

    public TextureFormat GetDefaultDepthFormat() => GetDefaultDepthStencilFormat();

    public RenderContext GetRenderContext() => renderContext;

    public RenderPool GetRenderPool() => renderPool;

    public IntVector2 GetSwapChainSize()
    {
        var graphics = GetSubsystem<Graphics>();
        return graphics is { IsInitialized: true }
            ? new IntVector2(graphics.Width, graphics.Height)
            : GetWindowSettings().Size;
    }

    public IntVector2 GetWindowSize() => GetSwapChainSize();

    public float GetDpiScale() => 1.0f;

    public FrameIndex GetFrameIndex()
    {
        var frameNumber = GetSubsystem<EngineTime>()?.GetFrameNumber() ?? 0UL;
        return (FrameIndex)Math.Max((long)frameNumber, (long)FrameIndex.None);
    }

    public DrawCommandQueue GetDefaultQueue() => defaultQueue;

    internal void AddDeviceObject(DeviceObject deviceObject)
    {
        ArgumentNullException.ThrowIfNull(deviceObject);

        lock (deviceObjectsLock)
        {
            if (!deviceObjects.Contains(deviceObject))
                deviceObjects.Add(deviceObject);
        }
    }

    internal void RemoveDeviceObject(DeviceObject deviceObject)
    {
        ArgumentNullException.ThrowIfNull(deviceObject);

        lock (deviceObjectsLock)
            deviceObjects.Remove(deviceObject);
    }

    internal void OnEngineShutdown()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Destroy);
        renderPool.Invalidate();
    }

    private void SendDeviceObjectEvent(DeviceObjectEvent deviceObjectEvent)
    {
        DeviceObject[] snapshot;
        lock (deviceObjectsLock)
            snapshot = [.. deviceObjects];

        foreach (var deviceObject in snapshot)
            deviceObject.ProcessDeviceObjectEvent(deviceObjectEvent);
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
