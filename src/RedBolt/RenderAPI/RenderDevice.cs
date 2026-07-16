using Microsoft.Extensions.Logging;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using System.Runtime.InteropServices;

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
    private readonly HashSet<DeviceObject> _deviceObjects = [];
    private readonly RenderPool _renderPool;
    private readonly RenderContext _renderContext;
    private readonly DrawCommandQueue _defaultQueue;
    private readonly RawTexture?[] _defaultTextures = new RawTexture?[(int)TextureType.Texture2DArray + 1];
    private readonly List<WeakReference<PipelineState>> _pipelineStatesToReload = [];
    private FrameIndex _frameIndex = FrameIndex.First;
    private WindowSettings? _windowSettings;
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

    public ISwapChain? CreateSecondarySwapChain(SdlWindow sdlWindow, bool hasDepthBuffer)
    {
        var graphics = GetSubsystem<Graphics>();
        if (graphics is null)
            return null;

        var renderDevice = graphics.GetNativeRenderDevice();
        var deviceContext = graphics.GetImmediateContext();
        var swapChain = graphics.GetNativeSwapChain();
        if (renderDevice is null || deviceContext is null || swapChain is null)
            return null;

        var swapChainDesc = new SwapChainDesc
        {
            IsPrimary = false,
            ColorBufferFormat = swapChain.GetDesc().ColorBufferFormat,
            DepthBufferFormat = hasDepthBuffer
                ? swapChain.GetDesc().DepthBufferFormat
                : global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatUnknown
        };

        switch (GetDeviceSettings().Backend)
        {
        case RenderBackend.OpenGL:
        {
            var currentContext = SdlNativeBindings.SdlGlGetCurrentContext();
            System.Diagnostics.Debug.Assert(currentContext != IntPtr.Zero && currentContext != graphics.GetGLContext());
            return DiligentNativeBindings.CreateSecondaryProxySwapChainGL(
                renderDevice, deviceContext, swapChainDesc, sdlWindow.Handle);
        }
        default:
            System.Diagnostics.Debug.Assert(false, "Unsupported render backend");
            return null;
        }
    }

    /// <summary>
    /// Update swap chain size according to current dimensions of the window.
    /// </summary>
    public void UpdateSwapChainSize()
    {
        var oldWindowSize = GetWindowSize();
        var oldSwapChainSize = GetSwapChainSize();
        var graphics = GetSubsystem<Graphics>();
        graphics?.UpdateSwapChainSize();

        var newWindowSize = GetWindowSize();
        var newSwapChainSize = GetSwapChainSize();
        if (oldWindowSize != newWindowSize || oldSwapChainSize != newSwapChainSize)
        {
            Log.Logger.LogInformation(
                "Swap chain is resized to {SwapChainWidth}x{SwapChainHeight}px ({WindowWidth}x{WindowHeight}dp).",
                newSwapChainSize.X, newSwapChainSize.Y, newWindowSize.X, newWindowSize.Y);
        }
    }

    /// <summary>
    /// Change window settings.
    /// </summary>
    public void UpdateWindowSettings(WindowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var oldSettings = GetWindowSettings();
        var newSettings = CloneWindowSettings(settings);
        ValidateWindowSettings(newSettings);
        var graphics = GetSubsystem<Graphics>();
        graphics?.UpdateWindowSettings(oldSettings, newSettings);

        var sizeChanged = oldSettings.Size != newSettings.Size;
        var modeChanged = oldSettings.Mode != newSettings.Mode;
        var refreshRateChanged = oldSettings.RefreshRate != newSettings.RefreshRate;
        _windowSettings = newSettings;

        if (sizeChanged || modeChanged || refreshRateChanged)
            UpdateSwapChainSize();
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
#if REDBOLT_PLATFORM_ANDROID
        if (GetDeviceSettings().Backend == RenderBackend.Vulkan)
            return true;

        if (GetDeviceSettings().Backend == RenderBackend.OpenGL)
        {
            if (SdlNativeBindings.SdlGlGetCurrentContext() == IntPtr.Zero)
            {
                InvalidateGLESContext();
                return RestoreGLESContext();
            }
            return true;
        }

        System.Diagnostics.Debug.Assert(false, "Unsupported render backend");
#endif
        return true;
    }

    public void InvalidateDeviceState()
    {
        ReleaseDefaultObjects();
        SendDeviceObjectEvent(DeviceObjectEvent.Invalidate);
        SendEvent<GraphicsEvents.DeviceLostEvent>();
    }

    public void RestoreDeviceState()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Restore);
        SendEvent<GraphicsEvents.DeviceResetEvent>();
        InitializeDefaultObjects();
    }

    /// <summary>
    /// Emulate device loss and restore. Only applicable for Android.
    /// </summary>
    public bool EmulateLossAndRestore()
    {
        const uint delayMs = 250;
        if (GetDeviceSettings().Backend == RenderBackend.Vulkan)
        {
            InvalidateVulkanContext();
            Log.Logger.LogInformation("Emulated context lost");
            SdlNativeBindings.SdlDelay(delayMs);
            return RestoreVulkanContext();
        }
        else if (ProcessUtils.GetPlatform() == PlatformId.Android)
        {
            System.Diagnostics.Debug.Assert(GetDeviceSettings().Backend == RenderBackend.OpenGL);

            InvalidateGLESContext();
            Log.Logger.LogInformation("Emulated context lost");
            SdlNativeBindings.SdlDelay(delayMs);
            return RestoreGLESContext();
        }
        else
        {
            InvalidateDeviceState();
            Log.Logger.LogInformation("Emulated context lost");
            RestoreDeviceState();
            return true;
        }
    }

    /// <summary>
    /// Queue pipeline state reload at the end of the frame.
    /// </summary>
    public void QueuePipelineStateReload(PipelineState pipelineState)
    {
        _pipelineStatesToReload.Add(new WeakReference<PipelineState>(pipelineState));
    }

    /// <summary>
    /// Take the screenshot from current back buffer.
    /// </summary>
    public bool TakeScreenShot(out Vector2i size, out byte[] data)
    {
        size = Vector2i.Zero;
        data = [];

        var flipY = GetDeviceSettings().Backend == RenderBackend.OpenGL;

        var resolvedBackBuffer = GetResolvedBackBuffer();
        if (resolvedBackBuffer is null)
        {
            Log.Logger.LogError("Failed to create resolve texture for RenderDevice.TakeScreenShot");
            return false;
        }

        var stagingTexture = ReadTextureToStaging(resolvedBackBuffer);
        _ = resolvedBackBuffer.Release();
        if (stagingTexture is null)
        {
            Log.Logger.LogError("Failed to create staging texture for RenderDevice.TakeScreenShot");
            return false;
        }

        var deviceContext = GetImmediateContext()!;
        using var mappedData = new MappedTextureSubresource();
        deviceContext.MapTextureSubresource(
            stagingTexture, 0, 0, MapType.MapRead, MapFlags.MapFlagNone, null!, mappedData);

        if (mappedData.PData == IntPtr.Zero)
        {
            Log.Logger.LogError("Failed to map staging texture for RenderDevice.TakeScreenShot");
            _ = stagingTexture.Release();
            return false;
        }

        using var desc = stagingTexture.GetDesc();
        using var formatInfo = TextureFormatAttribs.CreateManagedWrapper(
            DiligentNativeBindings.GetTextureFormatAttribs(desc.Format), false);
        if (formatInfo.GetElementSize() != 4 || formatInfo.ComponentType == ComponentType.ComponentTypeCompressed)
        {
            Log.Logger.LogError("Unexpected backbuffer for RenderDevice.TakeScreenShot");
            _ = stagingTexture.Release();
            return false;
        }

        size = new Vector2i(checked((int)desc.Width), checked((int)desc.Height));

        var elementSize = checked((int)formatInfo.GetElementSize());
        var rowSize = checked(size.X * elementSize);
        data = new byte[checked(size.X * size.Y * elementSize)];

        for (var i = 0; i < size.Y; ++i)
        {
            var row = flipY ? size.Y - i - 1 : i;
            Marshal.Copy(IntPtr.Add(mappedData.PData, checked((int)((nuint)row * mappedData.Stride))), data, i * rowSize, rowSize);
        }

        deviceContext.UnmapTextureSubresource(stagingTexture, 0, 0);
        _ = stagingTexture.Release();
        return true;
    }

    private ITexture? GetResolvedBackBuffer()
    {
        var swapChain = GetSwapChain()!;
        var backBuffer = swapChain.GetCurrentBackBufferRTV().GetTexture();
        using var backBufferDesc = backBuffer.GetDesc();

        if (backBufferDesc.SampleCount == 1)
        {
            _ = backBuffer.AddRef();
            return backBuffer;
        }

        using var textureDesc = new TextureDesc();
        textureDesc.Type = ResourceDimension.ResourceDimTex2d;
        textureDesc.Usage = Usage.UsageDefault;
        textureDesc.Format = backBufferDesc.Format;
        textureDesc.Width = backBufferDesc.Width;
        textureDesc.Height = backBufferDesc.Height;
        textureDesc.BindFlags = BindFlags.BindRenderTarget;

        GetRenderDevice()!.CreateTexture(textureDesc, null!, out var resolvedBackBuffer);
        if (resolvedBackBuffer is null || resolvedBackBuffer.IsNull)
            return null;

        using var attribs = new ResolveTextureSubresourceAttribs();
        attribs.SrcTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition;
        attribs.DstTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition;
        GetImmediateContext()!.ResolveTextureSubresource(backBuffer, resolvedBackBuffer, attribs);

        return resolvedBackBuffer;
    }

    private ITexture? ReadTextureToStaging(ITexture sourceTexture)
    {
        using var sourceTextureDesc = sourceTexture.GetDesc();

        using var textureDesc = new TextureDesc();
        textureDesc.Type = ResourceDimension.ResourceDimTex2d;
        textureDesc.Usage = Usage.UsageStaging;
        textureDesc.CPUAccessFlags = CpuAccessFlags.CpuAccessRead;
        textureDesc.Format = sourceTextureDesc.Format;
        textureDesc.Width = sourceTextureDesc.Width;
        textureDesc.Height = sourceTextureDesc.Height;

        GetRenderDevice()!.CreateTexture(textureDesc, null!, out var stagingTexture);
        if (stagingTexture is null || stagingTexture.IsNull)
            return null;

        using var attribs = new CopyTextureAttribs(
            sourceTexture,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
            stagingTexture,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        var deviceContext = GetImmediateContext()!;
        deviceContext.CopyTexture(attribs);
        deviceContext.WaitForIdle();

        return stagingTexture;
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
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(Graphics.MapTextureFormat(format));
        return formatInfo.BindFlags != BindFlags.BindNone;
    }

    /// <summary>
    /// Check if texture format is supported on hardware as render target or depth stencil.
    /// </summary>
    public bool IsRenderTargetFormatSupported(TextureFormat format)
    {
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(Graphics.MapTextureFormat(format));
        return (formatInfo.BindFlags & (BindFlags.BindRenderTarget | BindFlags.BindDepthStencil)) != BindFlags.BindNone;
    }

    /// <summary>
    /// Check if texture format is supported as UAV.
    /// </summary>
    public bool IsUnorderedAccessFormatSupported(TextureFormat format)
    {
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(Graphics.MapTextureFormat(format));
        return (formatInfo.BindFlags & BindFlags.BindUnorderedAccess) != BindFlags.BindNone;
    }

    /// <summary>
    /// Check if given level of MSAA is supported on hardware.
    /// </summary>
    public bool IsMultiSampleSupported(TextureFormat format, int multiSample)
    {
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(Graphics.MapTextureFormat(format));
        return ((int)formatInfo.SampleCounts & multiSample) != 0;
    }

    /// <summary>
    /// Select supported multi-sample level for given format.
    /// </summary>
    public int GetSupportedMultiSample(TextureFormat format, int multiSample)
    {
        multiSample = (int)MathDefs.NextPowerOfTwo((uint)Math.Clamp(multiSample, 1, 16));
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(Graphics.MapTextureFormat(format));
        while (multiSample > 1 && ((int)formatInfo.SampleCounts & multiSample) == 0)
            multiSample >>= 1;
        return Math.Max(1, multiSample);
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
        var graphics = GetSubsystem<Graphics>();
        if (_windowSettings is not null && graphics is { IsInitialized: true })
            return _windowSettings;

        _windowSettings = null;

        var engine = GetSubsystem<Engine>();
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
        if (graphics is not { IsInitialized: true })
            return graphics?.GetRenderDeviceCaps() ?? new RenderDeviceCaps();

        var renderDevice = GetRenderDevice();
        if (renderDevice is null || renderDevice.IsNull)
            return new RenderDeviceCaps();

        using var adapterInfo = renderDevice.GetAdapterInfo();
        var caps = new RenderDeviceCaps
        {
            ComputeShaders = adapterInfo.FeaturesComputeShaders == DeviceFeatureState.DeviceFeatureStateEnabled,
            DrawBaseVertex = (adapterInfo.DrawCommandCapFlags & DrawCommandCapFlags.DrawCommandCapFlagBaseVertex) != 0,
            DrawBaseInstance = !RenderAPIUtils.IsOpenGLESBackend(GetBackend()) && !OperatingSystem.IsMacOS(),
            ReadOnlyDepth = !OperatingSystem.IsBrowser(),
            SrgbOutput = IsRenderTargetFormatSupported(TextureFormat.RGBA8),
            HdrOutput = IsRenderTargetFormatSupported(TextureFormat.RGBA16F),
            ConstantBufferOffsetAlignment = adapterInfo.BufferConstantBufferOffsetAlignment,
            MaxTextureSize = adapterInfo.TextureMaxTexture2DDimension,
            MaxRenderTargetSize = adapterInfo.TextureMaxTexture2DDimension
        };

        caps.ClipDistance = !RenderAPIUtils.IsOpenGLESBackend(GetBackend());
        return caps;
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
        if (OperatingSystem.IsBrowser())
            return [];

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

    internal void ResetWindowSettings()
    {
        _windowSettings = null;
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

    private void InvalidateGLESContext()
    {
#if REDBOLT_PLATFORM_ANDROID && REDBOLT_GLES_SUPPORTED
        Log.Logger.LogInformation("OpenGL context is lost");
        InvalidateDeviceState();

        var deviceContextGL = IDeviceContextGL.CreateManagedWrapper(GetImmediateContext()!.Handle, ownsHandle: false);
        deviceContextGL.InvalidateState();

        var renderDeviceGLES = IRenderDeviceGLES.CreateManagedWrapper(GetRenderDevice()!.Handle, ownsHandle: false);
        renderDeviceGLES.Invalidate();

        GetSubsystem<Graphics>()!.ReleaseGLContext();
#else
        Log.Logger.LogWarning("RenderDevice::InvalidateGLESContext is supported only for Android platform");
#endif
    }

    private bool RestoreGLESContext()
    {
#if REDBOLT_PLATFORM_ANDROID && REDBOLT_GLES_SUPPORTED
        if (!GetSubsystem<Graphics>()!.RestoreGLContext())
        {
            var error = System.Text.Encoding.UTF8.GetString(SdlNativeBindings.SdlGetError()).TrimEnd('\0');
            Log.Logger.LogError("Could not restore OpenGL context: {Error}", error);
            return false;
        }

        var renderDeviceGLES = IRenderDeviceGLES.CreateManagedWrapper(GetRenderDevice()!.Handle, ownsHandle: false);
        renderDeviceGLES.Resume(IntPtr.Zero);
        RestoreDeviceState();
        Log.Logger.LogInformation("OpenGL context is restored");
        return true;
#else
        Log.Logger.LogWarning("RenderDevice::RestoreGLESContext is supported only for Android platform");
        return true;
#endif
    }

    private static void InvalidateVulkanContext()
    {
        Log.Logger.LogWarning("RenderDevice::InvalidateVulkanContext is supported only for Vulkan backend");
    }

    private static bool RestoreVulkanContext()
    {
        Log.Logger.LogWarning("RenderDevice::RestoreVulkanContext is supported only for Vulkan backend");
        return true;
    }

    private static IReadOnlyList<string> ParseOrientations(string orientations)
    {
        if (string.IsNullOrWhiteSpace(orientations))
            return [];

        return orientations.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static WindowSettings CloneWindowSettings(WindowSettings settings)
    {
        return new WindowSettings
        {
            Mode = settings.Mode,
            Size = settings.Size,
            Title = settings.Title,
            Resizable = settings.Resizable,
            Monitor = settings.Monitor,
            VSync = settings.VSync,
            RefreshRate = settings.RefreshRate,
            MultiSample = settings.MultiSample,
            SRgb = settings.SRgb,
            Orientations = [.. settings.Orientations]
        };
    }

    private static void ValidateWindowSettings(WindowSettings settings)
    {
        var numMonitors = SdlNativeBindings.SdlGetNumVideoDisplays();
        if (settings.Monitor < 0 || settings.Monitor >= numMonitors)
            settings.Monitor = 0;

        settings.MultiSample = (int)Math.Clamp(
            MathDefs.ClosestPowerOfTwo((uint)Math.Max(settings.MultiSample, 1)), 1u, 16u);

        if (settings.Mode != WindowMode.Windowed)
            settings.Resizable = false;

        const int defaultWindowWidth = 1024;
        const int defaultWindowHeight = 768;
        using var desktopMode = new SdlDisplayMode();
        if (SdlNativeBindings.SdlGetDesktopDisplayMode(settings.Monitor, desktopMode) != 0)
        {
            settings.Mode = WindowMode.Windowed;
            settings.Size = new Vector2i(defaultWindowWidth, defaultWindowHeight);
            settings.RefreshRate = 60;
        }
        else
        {
            if (settings.Size == Vector2i.Zero)
            {
                settings.Size = settings.Mode == WindowMode.Windowed
                    ? new Vector2i(defaultWindowWidth, defaultWindowHeight)
                    : new Vector2i(desktopMode.W, desktopMode.H);
            }

            if (settings.RefreshRate == 0 || settings.Mode != WindowMode.Fullscreen)
                settings.RefreshRate = desktopMode.RefreshRate;
        }

        if (settings.Mode == WindowMode.Fullscreen)
        {
            var modes = GetFullscreenModes(settings.Monitor);
            if (modes.Count > 0)
            {
                var closestMode = GetClosestFullscreenMode(
                    modes, new FullscreenMode(settings.Size, settings.RefreshRate));
                settings.Size = closestMode.Size;
                settings.RefreshRate = closestMode.RefreshRate;
            }
        }
    }

    private static WindowMode GetWindowMode(EngineParameters parameters)
    {
        if (parameters.FullScreen)
            return WindowMode.Fullscreen;
        if (parameters.Borderless)
            return WindowMode.Borderless;
        return WindowMode.Windowed;
    }

    private static RenderBackend MapRenderBackend(GraphicsBackend backend)
    {
        var requestedBackend = backend switch
        {
            GraphicsBackend.Auto => (RenderBackend?)null,
            GraphicsBackend.OpenGL => RenderBackend.OpenGL,
            GraphicsBackend.Vulkan => RenderBackend.Vulkan,
            GraphicsBackend.D3D11 => RenderBackend.D3D11,
            GraphicsBackend.D3D12 => RenderBackend.D3D12,
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
        };
        return RenderAPIUtils.SelectRenderBackend(requestedBackend);
    }

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
