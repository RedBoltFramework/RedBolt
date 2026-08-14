namespace RedBolt.RenderAPI;

using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using Timer = global::RedBolt.Core.Timer;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderPipeline;

/// <summary>
/// Wrapper for window and GAPI backend.
/// </summary>
public sealed class RenderDevice : RedObject
{
    private readonly RenderDeviceSettings _deviceSettings;
    private WindowSettings _windowSettings;
    private bool _defaultTextureParametersDirty;
    private TextureFilterMode _defaultTextureFilterMode = TextureFilterMode.Trilinear;
    private int _defaultTextureAnisotropy = 4;
    private RenderDeviceCaps _caps;
    private TextureFormat _defaultDepthStencilFormat = TextureFormat.TexFormatD24UnormS8Uint;
    private TextureFormat _defaultDepthFormat = TextureFormat.TexFormatD24UnormS8Uint;
    private SdlWindow? _window;
    private IntPtr _glContext;
    private IEngineFactory? _factory;
    private IRenderDevice? _renderDevice;
    private IDeviceContext? _deviceContext;
    private ISwapChain? _swapChain;
    private ITexture? _lastPresentedBackBuffer;
    private IGraphicsBackendLifetime? _graphicsBackendLifetime;
    private readonly RenderContext _renderContext;
    private FrameIndex _frameIndex = FrameIndex.First;
    private readonly object _deviceObjectsLock = new();
    private readonly HashSet<DeviceObject> _deviceObjects = [];
    private readonly RawTexture?[] _defaultTextures = new RawTexture?[(int)TextureType.Count];
    private readonly RawTexture?[] _defaultMaterialTextures = new RawTexture?[4];
    private readonly RenderPool _renderPool;
    private readonly DrawCommandQueue _defaultQueue;
#if REDBOLT_VULKAN_SUPPORTED
    private SwapChainDesc? _oldNativeSwapChainDesc;
#endif
    private readonly List<WeakReference<PipelineState>> _pipelineStatesToReload = [];
    private const uint StatsPeriodMs = 333;
    private readonly Timer _statsTimer = new();
    private RenderDeviceStats _stats = new();
    private RenderDeviceStats _maxStats = new();
    private RenderDeviceStats _previousMaxStats = new();
#if REDBOLT_VULKAN_SUPPORTED
    private ISwapChainVk? _swapChainVulkan;
#endif

    internal RenderDevice(RenderDeviceSettings deviceSettings, WindowSettings windowSettings)
    {
        _deviceSettings = deviceSettings;
        if (_deviceSettings.ExternalWindowHandle != IntPtr.Zero)
            windowSettings.Mode = WindowMode.Windowed;
        ValidateWindowSettings(ref windowSettings);
        _windowSettings = windowSettings;
        _renderPool = new RenderPool(this);
        _defaultQueue = new DrawCommandQueue(this);

        var graphics = GetSubsystem<Graphics>()!;
        var input = GetSubsystem<Input>()!;
        _window = input.Window;
        _glContext = graphics.GetGLContext();
        _factory = graphics.GetNativeFactory();
        _renderDevice = graphics.GetNativeRenderDevice();
        _deviceContext = graphics.GetImmediateContext();
        _swapChain = graphics.GetNativeSwapChain();
        _graphicsBackendLifetime = graphics.GetGraphicsBackendLifetime();
#if REDBOLT_VULKAN_SUPPORTED
        _swapChainVulkan = graphics.GetNativeVulkanSwapChain();
#endif
        InitializeDefaultDepthFormats();
        _renderContext = new RenderContext(this);
        InitializeCaps();
        _window = input.TransferWindowOwnership();
        graphics.TransferRenderDeviceOwnership();
    }

    public void PostInitialize()
    {
        InitializeDefaultObjects();
    }

    public override void Dispose()
    {
        SendDeviceObjectEvent(DeviceObjectEvent.Destroy);
        _deviceContext?.WaitForIdle();
        // Vulkan keeps the immediate context alive across test fixtures. Clear its
        // cached bindings before releasing the fixture-owned resources so the next
        // swap chain/device wrapper cannot inherit stale native state.
        if (GetDeviceSettings().Backend == RenderBackend.Vulkan)
            _deviceContext?.InvalidateState();
        ReleaseLastPresentedBackBuffer();

#if REDBOLT_VULKAN_SUPPORTED
        _oldNativeSwapChainDesc?.Dispose();
        _oldNativeSwapChainDesc = null;
#endif
        _pipelineStatesToReload.Clear();
        _renderPool.Dispose();
        for (var i = 0; i < _defaultTextures.Length; ++i)
        {
            _defaultTextures[i]?.Dispose();
            _defaultTextures[i] = null;
        }
        for (var i = 0; i < _defaultMaterialTextures.Length; ++i)
        {
            _defaultMaterialTextures[i]?.Dispose();
            _defaultMaterialTextures[i] = null;
        }
        lock (_deviceObjectsLock)
            _deviceObjects.Clear();
        _renderContext.Dispose();

        var graphics = GetSubsystem<Graphics>();
        if (graphics is not null)
        {
            _glContext = graphics.GetGLContext();
            graphics.Shutdown();
        }

        _graphicsBackendLifetime?.ReleaseBeforeCommonResources();
        ReleaseIfNotNull(_swapChain);
        _swapChain = null;
        _factory = null;

        // Resource releases above may enqueue Vulkan destruction work on the
        // shared device. Wait again before releasing the shared immediate
        // context so the next fixture cannot observe recycled native resources.
        if (GetDeviceSettings().Backend == RenderBackend.Vulkan)
            _deviceContext?.WaitForIdle();

        ReleaseIfNotNull(_deviceContext);
        _deviceContext = null;
        ReleaseIfNotNull(_renderDevice);
        _renderDevice = null;

        _graphicsBackendLifetime?.ReleaseAfterCommonResources(_window);
        _graphicsBackendLifetime = null;
        _glContext = IntPtr.Zero;

        if (_window is not null && !_window.IsNull)
            Sdl.SdlDestroyWindow(_window);
        _window = null;

        base.Dispose();
    }

    public ISwapChain? CreateSecondarySwapChain(SdlWindow sdlWindow, bool hasDepthBuffer)
    {
        using var currentSwapChainDesc = _swapChain!.GetDesc();
        using var swapChainDesc = new SwapChainDesc
        {
            IsPrimary = false,
            ColorBufferFormat = currentSwapChainDesc.ColorBufferFormat,
            DepthBufferFormat = hasDepthBuffer
                ? currentSwapChainDesc.DepthBufferFormat
                : global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatUnknown
        };

        switch (GetDeviceSettings().Backend)
        {
        case RenderBackend.OpenGL:
        {
            var currentContext = Sdl.SdlGlGetCurrentContext();
            System.Diagnostics.Debug.Assert(currentContext != IntPtr.Zero && currentContext != _glContext);
            return Diligent.CreateSecondaryProxySwapChainGL(
                _renderDevice!, _deviceContext!, swapChainDesc, sdlWindow.Handle);
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
        ReleaseLastPresentedBackBuffer();
        var oldWindowSize = GetWindowSize();
        var oldSwapChainSize = GetSwapChainSize();

        var window = GetSDLWindow();
        if (window is not null && !window.IsNull)
        {
            Sdl.SdlGetWindowSize(window, out var width, out var height);
            var windowSettings = GetWindowSettings();
            windowSettings.Size = new Vector2i(width, height);
            _windowSettings = windowSettings;
        }

        var graphics = GetSubsystem<Graphics>();
        graphics?.UpdateSwapChainSize();

        var newWindowSize = GetWindowSize();
        var newSwapChainSize = GetSwapChainSize();
        if (oldWindowSize != newWindowSize || oldSwapChainSize != newSwapChainSize)
        {
            Log.GetLogger().LogInformation(
                "Swap chain is resized to {SwapChainWidth}x{SwapChainHeight}px ({WindowWidth}x{WindowHeight}dp).",
                newSwapChainSize.X, newSwapChainSize.Y, newWindowSize.X, newWindowSize.Y);
        }
    }

    /// <summary>
    /// Change window settings.
    /// </summary>
    public void UpdateWindowSettings(WindowSettings settings)
    {
        var oldSettings = GetWindowSettings();
        var newSettings = settings;
        ValidateWindowSettings(ref newSettings);
        var graphics = GetSubsystem<Graphics>();
        graphics?.UpdateWindowSettings(oldSettings, newSettings);

        var sizeChanged = oldSettings.Size != newSettings.Size;
        var modeChanged = oldSettings.Mode != newSettings.Mode;
        var refreshRateChanged = oldSettings.RefreshRate != newSettings.RefreshRate;
        if (sizeChanged || modeChanged || refreshRateChanged)
        {
            oldSettings.Size = newSettings.Size;
            oldSettings.Mode = newSettings.Mode;
            oldSettings.RefreshRate = newSettings.RefreshRate;
        }

        if (oldSettings.Monitor != newSettings.Monitor)
            oldSettings.Monitor = newSettings.Monitor;
        if (oldSettings.Title != newSettings.Title)
            oldSettings.Title = newSettings.Title;
        if (oldSettings.Resizable != newSettings.Resizable)
            oldSettings.Resizable = newSettings.Resizable;
        if (oldSettings.VSync != newSettings.VSync)
            oldSettings.VSync = newSettings.VSync;

        _windowSettings = oldSettings;

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
        anisotropy = SystemMath.Max(1, anisotropy);

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
            if (Sdl.SdlGlGetCurrentContext() == IntPtr.Zero)
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
            Log.GetLogger().LogInformation("Emulated context lost");
            Sdl.SdlDelay(delayMs);
            return RestoreVulkanContext();
        }
#if REDBOLT_PLATFORM_ANDROID
        else
        {
            System.Diagnostics.Debug.Assert(GetDeviceSettings().Backend == RenderBackend.OpenGL);

            InvalidateGLESContext();
            Log.GetLogger().LogInformation("Emulated context lost");
            Sdl.SdlDelay(delayMs);
            return RestoreGLESContext();
        }
#else
        else
        {
            InvalidateDeviceState();
            Log.GetLogger().LogInformation("Emulated context lost");
            RestoreDeviceState();
            return true;
        }
#endif
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
            Log.GetLogger().LogError("Failed to create resolve texture for RenderDevice.TakeScreenShot");
            return false;
        }

        var stagingTexture = ReadTextureToStaging(resolvedBackBuffer);
        _ = resolvedBackBuffer.Release();
        if (stagingTexture is null)
        {
            Log.GetLogger().LogError("Failed to create staging texture for RenderDevice.TakeScreenShot");
            return false;
        }

        var deviceContext = GetImmediateContext()!;
        using var mappedData = new MappedTextureSubresource();
        deviceContext.MapTextureSubresource(
            stagingTexture, 0, 0, MapType.MapRead, MapFlags.MapFlagNone, null!, mappedData);

        if (mappedData.PData == IntPtr.Zero)
        {
            Log.GetLogger().LogError("Failed to map staging texture for RenderDevice.TakeScreenShot");
            _ = stagingTexture.Release();
            return false;
        }

        using var desc = stagingTexture.GetDesc();
        using var formatInfo = Diligent.GetTextureFormatAttribs(desc.Format);
        if (formatInfo.GetElementSize() != 4 || formatInfo.ComponentType == ComponentType.ComponentTypeCompressed)
        {
            Log.GetLogger().LogError("Unexpected backbuffer for RenderDevice.TakeScreenShot");
            _ = stagingTexture.Release();
            return false;
        }

        size = new Vector2i(checked((int)desc.Width), checked((int)desc.Height));

        var elementSize = checked((int)formatInfo.GetElementSize());
        var rowSize = checked(size.X * elementSize);
        var isBgra = desc.Format is TextureFormat.TexFormatBgra8Unorm
            or TextureFormat.TexFormatBgra8UnormSrgb;
        data = new byte[checked(size.X * size.Y * elementSize)];

        for (var i = 0; i < size.Y; ++i)
        {
            var row = flipY ? size.Y - i - 1 : i;
            Marshal.Copy(IntPtr.Add(mappedData.PData, checked((int)((nuint)row * mappedData.Stride))), data, i * rowSize, rowSize);

            var rowOffset = i * rowSize;
            for (var pixel = 0; pixel < size.X; ++pixel)
            {
                var pixelOffset = rowOffset + pixel * elementSize;
                if (isBgra)
                {
                    (data[pixelOffset], data[pixelOffset + 2]) =
                        (data[pixelOffset + 2], data[pixelOffset]);
                }
                data[pixelOffset + 3] = byte.MaxValue;
            }
        }

        deviceContext.UnmapTextureSubresource(stagingTexture, 0, 0);
        _ = stagingTexture.Release();
        return true;
    }

    private ITexture? GetResolvedBackBuffer()
    {
        var swapChain = GetSwapChain()!;
        var backBuffer = GetBackend() == RenderBackend.Vulkan && _lastPresentedBackBuffer is { IsNull: false }
            ? _lastPresentedBackBuffer
            : swapChain.GetCurrentBackBufferRTV().GetTexture();
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

    private void RememberCurrentBackBuffer(ISwapChain swapChain)
    {
        var backBuffer = swapChain.GetCurrentBackBufferRTV().GetTexture();
        if (backBuffer.IsNull)
            return;

        _ = backBuffer.AddRef();
        ReleaseLastPresentedBackBuffer();
        _lastPresentedBackBuffer = backBuffer;
    }

    private void ReleaseLastPresentedBackBuffer()
    {
        if (_lastPresentedBackBuffer is { IsNull: false } backBuffer)
            _ = backBuffer.Release();
        _lastPresentedBackBuffer = null;
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
        {
            if (GetBackend() == RenderBackend.Vulkan)
                RememberCurrentBackBuffer(swapChain);
            swapChain.Present(GetWindowSettings().VSync ? 1u : 0u);
        }

        if (_deviceSettings.ExternalWindowHandle != IntPtr.Zero && _window is not null && !_window.IsNull)
        {
            Sdl.SdlGetWindowSize(_window, out var width, out var height);
            if (GetWindowSettings().Size != new Vector2i(width, height))
                UpdateSwapChainSize();
        }

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

            lock (_deviceObjectsLock)
            {
                foreach (var deviceObject in _deviceObjects)
                {
                    if (deviceObject is PipelineState pipelineState)
                    {
                        pipelineState.Invalidate();
                        pipelineState.Restore();
                    }
                }
            }
        }

        _stats = _renderContext.GetStats();
        _renderContext.ResetStats();

        if (_statsTimer.GetMSec() >= StatsPeriodMs)
        {
            _statsTimer.Reset();
            _previousMaxStats = _maxStats;
            _maxStats = _stats;
        }
        else
        {
            _maxStats.NumPrimitives = SystemMath.Max(_maxStats.NumPrimitives, _stats.NumPrimitives);
            _maxStats.NumDraws = SystemMath.Max(_maxStats.NumDraws, _stats.NumDraws);
            _maxStats.NumDispatches = SystemMath.Max(_maxStats.NumDispatches, _stats.NumDispatches);
        }

        _frameIndex = (FrameIndex)((long)_frameIndex + 1);
        System.Diagnostics.Debug.Assert(_frameIndex > FrameIndex.None, "How did you exhaust 2^63 frames?");
    }

    /// <summary>
    /// Check if texture format is supported on hardware.
    /// </summary>
    public bool IsTextureFormatSupported(TextureFormat format)
    {
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(format);
        return formatInfo.BindFlags != BindFlags.BindNone;
    }

    /// <summary>
    /// Check if texture format is supported on hardware as render target or depth stencil.
    /// </summary>
    public bool IsRenderTargetFormatSupported(TextureFormat format)
    {
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(format);
        return (formatInfo.BindFlags & (BindFlags.BindRenderTarget | BindFlags.BindDepthStencil)) != BindFlags.BindNone;
    }

    /// <summary>
    /// Check if texture format is supported as UAV.
    /// </summary>
    public bool IsUnorderedAccessFormatSupported(TextureFormat format)
    {
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(format);
        return (formatInfo.BindFlags & BindFlags.BindUnorderedAccess) != BindFlags.BindNone;
    }

    /// <summary>
    /// Check if given level of MSAA is supported on hardware.
    /// </summary>
    public bool IsMultiSampleSupported(TextureFormat format, int multiSample)
    {
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(format);
        return ((int)formatInfo.SampleCounts & multiSample) != 0;
    }

    /// <summary>
    /// Select supported multi-sample level for given format.
    /// </summary>
    public int GetSupportedMultiSample(TextureFormat format, int multiSample)
    {
        multiSample = (int)MathDefs.NextPowerOfTwo((uint)SystemMath.Clamp(multiSample, 1, 16));
        using var formatInfo = GetRenderDevice()!.GetTextureFormatInfoExt(format);
        while (multiSample > 1 && ((int)formatInfo.SampleCounts & multiSample) == 0)
            multiSample >>= 1;
        return SystemMath.Max(1, multiSample);
    }

    public RenderBackend GetBackend() => GetDeviceSettings().Backend;

    public RenderDeviceSettings GetDeviceSettings() => _deviceSettings;

    public WindowSettings GetWindowSettings() => _windowSettings;

    private void InitializeCaps()
    {
        using var adapterInfo = _renderDevice!.GetAdapterInfo();
        _caps = new RenderDeviceCaps
        {
            ComputeShaders = adapterInfo.FeaturesComputeShaders == DeviceFeatureState.DeviceFeatureStateEnabled,
            DrawBaseVertex = (adapterInfo.DrawCommandCapFlags & DrawCommandCapFlags.DrawCommandCapFlagBaseVertex) != 0,
            DrawBaseInstance = !RenderAPIUtils.IsOpenGLESBackend(GetBackend()),
#if REDBOLT_PLATFORM_BROWSER
            ReadOnlyDepth = false,
#else
            ReadOnlyDepth = true,
#endif
            SrgbOutput = IsRenderTargetFormatSupported(TextureFormat.TexFormatRgba8UnormSrgb)
                || IsRenderTargetFormatSupported(TextureFormat.TexFormatBgra8UnormSrgb),
            HdrOutput = IsRenderTargetFormatSupported(TextureFormat.TexFormatRgba16Float),
            ConstantBufferOffsetAlignment = checked((int)adapterInfo.BufferConstantBufferOffsetAlignment),
            MaxTextureSize = checked((int)adapterInfo.TextureMaxTexture2DDimension),
            MaxRenderTargetSize = checked((int)adapterInfo.TextureMaxTexture2DDimension)
        };

        _caps.ClipDistance = !RenderAPIUtils.IsOpenGLESBackend(GetBackend());
    }

    public RenderDeviceCaps GetCaps() => _caps;

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
        return _defaultDepthStencilFormat;
    }

    public TextureFormat GetDefaultDepthFormat()
    {
        return _defaultDepthFormat;
    }

    public RenderContext GetRenderContext() => _renderContext;

    public RenderPool GetRenderPool() => _renderPool;

    public SdlWindow? GetSDLWindow() => _window;

    public nint GetMetalView() => nint.Zero;

    public IEngineFactory? GetFactory() => _factory;

    public IRenderDevice? GetRenderDevice() => _renderDevice;

    public IDeviceContext? GetImmediateContext() => _deviceContext;

    public ISwapChain? GetSwapChain() => _swapChain;

    public Vector2i GetSwapChainSize()
    {
        var graphics = GetSubsystem<Graphics>();
        if (graphics is null || !graphics.IsInitialized)
            return Vector2i.Zero;
        return new Vector2i(graphics.Width, graphics.Height);
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

    public RawTexture? GetDefaultAlbedoTexture() => _defaultMaterialTextures[0];
    public RawTexture? GetDefaultNormalTexture() => _defaultMaterialTextures[1];
    public RawTexture? GetDefaultPropertiesTexture() => _defaultMaterialTextures[2];
    public RawTexture? GetDefaultEmissionTexture() => _defaultMaterialTextures[3];

    public DrawCommandQueue GetDefaultQueue() => _defaultQueue;

    public static List<FullscreenMode> GetFullscreenModes(int monitor)
    {
#if REDBOLT_PLATFORM_BROWSER
        return [];
#else
        var result = new List<FullscreenMode>();
        var numModes = Sdl.SdlGetNumDisplayModes(monitor);
        for (var i = 0; i < numModes; ++i)
        {
            using var mode = new SdlDisplayMode();
            Sdl.SdlGetDisplayMode(monitor, i, mode);
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
#endif
    }

    public static int GetClosestFullscreenModeIndex(IReadOnlyList<FullscreenMode> modes, FullscreenMode desiredMode)
    {
        System.Diagnostics.Debug.Assert(modes.Count != 0);

        for (var i = 0; i < modes.Count; ++i)
        {
            if (modes[i] == desiredMode)
                return i;
        }

        var upperBound = 0;
        while (upperBound < modes.Count && CompareFullscreenMode(modes[upperBound], desiredMode.Size, int.MaxValue) <= 0)
            ++upperBound;

        if (upperBound > 0 && modes[upperBound - 1].Size == desiredMode.Size)
            return upperBound - 1;

        if (upperBound < modes.Count)
        {
            for (var i = upperBound; i < modes.Count; ++i)
            {
                if (modes[i].RefreshRate >= desiredMode.RefreshRate)
                    return i;
            }

            return upperBound;
        }

        return modes.Count - 1;
    }

    public static FullscreenMode GetClosestFullscreenMode(IReadOnlyList<FullscreenMode> modes, FullscreenMode desiredMode)
    {
        var index = GetClosestFullscreenModeIndex(modes, desiredMode);
        return modes[index];
    }

    internal ITextureView ResolveRenderTargetView(RenderTargetView view)
    {
        var graphics = GetSubsystem<Graphics>() ?? throw new InvalidOperationException("Graphics subsystem is not available.");
        return graphics.ResolveRenderTargetView(view);
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

    public void AddDeviceObject(DeviceObject deviceObject)
    {
        ArgumentNullException.ThrowIfNull(deviceObject);

        lock (_deviceObjectsLock)
        {
            _deviceObjects.Add(deviceObject);
        }
    }

    public void RemoveDeviceObject(DeviceObject deviceObject)
    {
        ArgumentNullException.ThrowIfNull(deviceObject);

        lock (_deviceObjectsLock)
            _deviceObjects.Remove(deviceObject);
    }

    private void SendDeviceObjectEvent(DeviceObjectEvent deviceObjectEvent)
    {
        lock (_deviceObjectsLock)
        {
            foreach (var deviceObject in _deviceObjects)
                deviceObject.ProcessDeviceObjectEvent(deviceObjectEvent);
        }
    }

    private void InitializeDefaultObjects()
    {
        void CreateDefaultTexture(TextureType type, ResourceDimensionSupport flag)
        {
            const TextureFormat format = TextureFormat.TexFormatRgba8Unorm;
            using var formatInfo = _renderDevice!.GetTextureFormatInfoExt(format);
            if ((formatInfo.Dimensions & flag) == 0)
                return;

            var parameters = new RawTextureParams(type, format, TextureFlag.None, Vector3i.One);
            var texture = new RawTexture(parameters);
            _defaultTextures[(int)type] = texture;

            ReadOnlySpan<byte> data = stackalloc byte[4];
            for (var slice = 0; slice < texture.GetParams().ArraySize; ++slice)
                texture.Update(0, Vector3i.Zero, Vector3i.One, slice, data);
        }

        CreateDefaultTexture(TextureType.Texture2D, ResourceDimensionSupport.ResourceDimensionSupportTex2d);
        CreateDefaultTexture(TextureType.TextureCube, ResourceDimensionSupport.ResourceDimensionSupportTexCube);
        CreateDefaultTexture(TextureType.Texture3D, ResourceDimensionSupport.ResourceDimensionSupportTex3d);
        CreateDefaultTexture(TextureType.Texture2DArray, ResourceDimensionSupport.ResourceDimensionSupportTex2dArray);

        RawTexture CreateDefaultMaterialTexture(ReadOnlySpan<byte> data)
        {
            var parameters = new RawTextureParams(
                TextureType.Texture2D, TextureFormat.TexFormatRgba8Unorm, TextureFlag.None, Vector3i.One);
            var texture = new RawTexture(parameters);
            texture.Update(0, Vector3i.Zero, Vector3i.One, 0, data);
            return texture;
        }

        _defaultMaterialTextures[0] = CreateDefaultMaterialTexture([255, 255, 255, 255]);
        // PACKEDNORMAL stores tangent-space X/Y in alpha/green, respectively.
        _defaultMaterialTextures[1] = CreateDefaultMaterialTexture([0, 128, 0, 128]);
        _defaultMaterialTextures[2] = CreateDefaultMaterialTexture([255, 255, 255, 255]);
        _defaultMaterialTextures[3] = CreateDefaultMaterialTexture([0, 0, 0, 255]);
        _renderPool.Restore();
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
        Log.GetLogger().LogInformation("OpenGL context is lost");
        InvalidateDeviceState();

        using var deviceContextGL = IDeviceContextGL.QueryManagedWrapper(GetImmediateContext()!)
            ?? throw new InvalidOperationException("OpenGL device context interface is unavailable.");
        deviceContextGL.InvalidateState();

        using var renderDeviceGLES = IRenderDeviceGLES.QueryManagedWrapper(GetRenderDevice()!)
            ?? throw new InvalidOperationException("OpenGL ES render device interface is unavailable.");
        renderDeviceGLES.Invalidate();

        GetSubsystem<Graphics>()!.ReleaseGLContext();
        _glContext = IntPtr.Zero;
#else
        Log.GetLogger().LogWarning("RenderDevice::InvalidateGLESContext is supported only for Android platform");
#endif
    }

    private bool RestoreGLESContext()
    {
#if REDBOLT_PLATFORM_ANDROID && REDBOLT_GLES_SUPPORTED
        var graphics = GetSubsystem<Graphics>()!;
        if (!graphics.RestoreGLContext())
        {
            var error = System.Text.Encoding.UTF8.GetString(Sdl.SdlGetError()).TrimEnd('\0');
            Log.GetLogger().LogError("Could not restore OpenGL context: {Error}", error);
            return false;
        }
        _glContext = graphics.GetGLContext();

        using var renderDeviceGLES = IRenderDeviceGLES.QueryManagedWrapper(GetRenderDevice()!)
            ?? throw new InvalidOperationException("OpenGL ES render device interface is unavailable.");
        renderDeviceGLES.Resume(IntPtr.Zero);
        RestoreDeviceState();
        Log.GetLogger().LogInformation("OpenGL context is restored");
        return true;
#else
        Log.GetLogger().LogWarning("RenderDevice::RestoreGLESContext is supported only for Android platform");
        return true;
#endif
    }

    private void InvalidateVulkanContext()
    {
#if REDBOLT_VULKAN_SUPPORTED
        Log.GetLogger().LogInformation("Vulkan context is lost");
        InvalidateDeviceState();

        _oldNativeSwapChainDesc?.Dispose();
        _oldNativeSwapChainDesc = GetSubsystem<Graphics>()!.ReleaseVulkanSwapChain();
        _swapChain = null;
        _swapChainVulkan = null;
#else
        // The managed Vulkan backend does not currently expose native context
        // loss/recreation on this platform. Still exercise the device-object
        // lifecycle so callers can validate resource recovery semantics.
        InvalidateDeviceState();
#endif
    }

    private bool RestoreVulkanContext()
    {
#if REDBOLT_VULKAN_SUPPORTED
        System.Diagnostics.Debug.Assert(_oldNativeSwapChainDesc is not null);
        using var swapChainDesc = new SwapChainDesc
        {
            ColorBufferFormat = _oldNativeSwapChainDesc!.ColorBufferFormat,
            DepthBufferFormat = _oldNativeSwapChainDesc.DepthBufferFormat,
            Width = _oldNativeSwapChainDesc.Width,
            Height = _oldNativeSwapChainDesc.Height,
            PreTransform = _oldNativeSwapChainDesc.PreTransform
        };

        var graphics = GetSubsystem<Graphics>()!;
        var depthFormat = SelectDefaultDepthFormat(GetRenderDevice()!, true);
        var colorFormat = swapChainDesc.ColorBufferFormat;
        var multiSample = GetSupportedMultiSample(colorFormat, GetWindowSettings().MultiSample);
        if (!graphics.RestoreVulkanSwapChain(swapChainDesc, depthFormat, multiSample))
        {
            Log.GetLogger().LogError("Failed to restore Vulkan context");
            return false;
        }

        _swapChain = graphics.GetNativeSwapChain();
        _swapChainVulkan = graphics.GetNativeVulkanSwapChain();

        _oldNativeSwapChainDesc.Dispose();
        _oldNativeSwapChainDesc = null;
        RestoreDeviceState();
        Log.GetLogger().LogInformation("Vulkan context is restored");
        return true;
#else
        RestoreDeviceState();
        return true;
#endif
    }

    /// <summary>
    /// C++ SelectDefaultDepthFormat — pick first format with BIND_DEPTH_STENCIL support.
    /// </summary>
    internal static global::RedBolt.Bindings.Diligent.TextureFormat SelectDefaultDepthFormat(
        IRenderDevice device, bool needStencil)
    {
        global::RedBolt.Bindings.Diligent.TextureFormat[] depthStencilFormats =
        [
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD24UnormS8Uint,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32FloatS8x24Uint,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32Float,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD16Unorm
        ];
        global::RedBolt.Bindings.Diligent.TextureFormat[] depthOnlyFormats =
        [
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD24UnormS8Uint,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32Float,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32FloatS8x24Uint,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD16Unorm
        ];

        foreach (var format in needStencil ? depthStencilFormats : depthOnlyFormats)
        {
            using var formatInfo = device.GetTextureFormatInfoExt(format);
            if ((formatInfo.BindFlags & BindFlags.BindDepthStencil) != BindFlags.BindNone)
                return format;
        }

        System.Diagnostics.Debug.Assert(false);
        return global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatUnknown;
    }

    private void InitializeDefaultDepthFormats()
    {
        // C++ RenderDevice::InitializeDevice (GL and non-GL paths):
        //   defaultDepthStencilFormat_ = SelectDefaultDepthFormat(device, true) or swap-chain depth on GL
        //   defaultDepthFormat_ = SelectDefaultDepthFormat(device, false)
        var device = GetRenderDevice();
        if (device is null)
            return;

        var backend = GetDeviceSettings().Backend;
        if (backend == RenderBackend.OpenGL)
        {
            var graphics = GetSubsystem<Graphics>();
            if (graphics is { IsInitialized: true })
                _defaultDepthStencilFormat = graphics.GetSwapChainDepthTextureFormat();
            else
                _defaultDepthStencilFormat = SelectDefaultDepthFormat(device, needStencil: true);
        }
        else
        {
            _defaultDepthStencilFormat = SelectDefaultDepthFormat(device, needStencil: true);
        }

        _defaultDepthFormat = SelectDefaultDepthFormat(device, needStencil: false);
    }

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }

    private static void ValidateWindowSettings(ref WindowSettings settings)
    {
#if REDBOLT_PLATFORM_BROWSER
        if (settings.Mode == WindowMode.Fullscreen)
            settings.Mode = WindowMode.Borderless;
#endif

        var numMonitors = Sdl.SdlGetNumVideoDisplays();
        if (settings.Monitor < 0 || settings.Monitor >= numMonitors)
            settings.Monitor = 0;

        settings.MultiSample = (int)SystemMath.Clamp(
            MathDefs.ClosestPowerOfTwo((uint)SystemMath.Max(settings.MultiSample, 1)), 1u, 16u);

        if (settings.Mode != WindowMode.Windowed)
            settings.Resizable = false;

        const int defaultWindowWidth = 1024;
        const int defaultWindowHeight = 768;
        using var desktopMode = new SdlDisplayMode();
        if (Sdl.SdlGetDesktopDisplayMode(settings.Monitor, desktopMode) != 0)
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

    private static int CompareFullscreenMode(FullscreenMode mode, Vector2i size, int refreshRate)
    {
        var comparison = mode.Size.X.CompareTo(size.X);
        if (comparison != 0)
            return comparison;

        comparison = mode.Size.Y.CompareTo(size.Y);
        return comparison != 0 ? comparison : mode.RefreshRate.CompareTo(refreshRate);
    }

}
