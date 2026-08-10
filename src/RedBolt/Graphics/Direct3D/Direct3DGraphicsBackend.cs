namespace RedBolt;

using System;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using RedBolt.Engine;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.RenderAPI;

public abstract class Direct3DGraphicsBackend : IGraphicsBackend
{
    public abstract RenderBackend Backend { get; }

    public bool UsesOpenGLWindow => false;

    public void ConfigureWindow()
    {
    }

    public GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
        WindowSettings windowSettings, SdlWindow window, IPlatformRuntime platform)
    {
        SdlNativeBindings.SdlGetWindowSize(window, out var width, out var height);
        // Wine's D3D11 sRGB render-target path omits the gamma encode. A linear
        // swap chain makes RedBolt's final-copy shader perform it explicitly.
        var useSrgbSwapChain = windowSettings.SRgb && !(Backend == RenderBackend.D3D11 && IsWine());
        using var swapChainDesc = new SwapChainDesc
        {
            ColorBufferFormat = useSrgbSwapChain
                ? TextureFormat.TexFormatRgba8UnormSrgb
                : TextureFormat.TexFormatRgba8Unorm,
            DepthBufferFormat = TextureFormat.TexFormatUnknown
        };
        using var fullScreenDesc = new FullScreenModeDesc
        {
            Fullscreen = windowSettings.Mode == WindowMode.Fullscreen,
            RefreshRateNumerator = checked((uint)System.Math.Max(windowSettings.RefreshRate, 0)),
            RefreshRateDenominator = 1
        };
        var nativeWindowInfo = platform.GetNativeWindowInfo(window);
        using var nativeWindow = new Win32NativeWindow { HWnd = nativeWindowInfo.Window };

        var resources = CreateNativeResources(deviceSettings, swapChainDesc, fullScreenDesc, nativeWindow);
        ISwapChain? swapChain = null;
        try
        {
            var depthFormat = RenderDevice.SelectDefaultDepthFormat(resources.Device, true);
            var multiSample = (int)MathDefs.NextPowerOfTwo(
                (uint)System.Math.Clamp(parameters.MultiSample, 1, 16));
            using (var formatInfo = resources.Device.GetTextureFormatInfoExt(swapChainDesc.ColorBufferFormat))
            {
                while (multiSample > 1 && ((int)formatInfo.SampleCounts & multiSample) == 0)
                    multiSample >>= 1;
            }
            multiSample = System.Math.Max(1, multiSample);

            swapChain = DiligentNativeBindings.CreateProxySwapChainMS(resources.Device,
                resources.ImmediateContext, resources.NativeSwapChain, (uint)depthFormat, (uint)multiSample);
            _ = resources.NativeSwapChain.Release();
            resources.NativeSwapChain = null!;
            if (swapChain.IsNull)
                throw new InvalidOperationException("Generated CreateProxySwapChainMS binding returned a null swap chain.");

            return new GraphicsBackendResources
            {
                Factory = resources.Factory,
                Device = resources.Device,
                ImmediateContext = resources.ImmediateContext,
                SwapChain = swapChain,
                Lifetime = new Direct3DLifetime(window),
                Width = width,
                Height = height,
                MultiSample = multiSample
            };
        }
        catch
        {
            if (resources.NativeSwapChain is { IsNull: false })
                _ = resources.NativeSwapChain.Release();
            if (swapChain is { IsNull: false })
                _ = swapChain.Release();
            _ = resources.ImmediateContext.Release();
            _ = resources.Device.Release();
            throw;
        }
    }

    protected abstract NativeResources CreateNativeResources(RenderDeviceSettings deviceSettings,
        SwapChainDesc swapChainDesc, FullScreenModeDesc fullScreenDesc, Win32NativeWindow nativeWindow);

    protected static void ConfigureCreateInfo(EngineCreateInfo createInfo, RenderDeviceSettings settings)
    {
        createInfo.GraphicsAPIVersionMajor = 11;
        createInfo.GraphicsAPIVersionMinor = 0;
        createInfo.EnableValidation = settings.GpuDebug;
        if (settings.AdapterId.HasValue)
            createInfo.AdapterId = checked((uint)settings.AdapterId.Value);
    }

    private static bool IsWine()
    {
        if (!NativeLibrary.TryLoad("ntdll.dll", out var module))
            return false;

        try
        {
            return NativeLibrary.TryGetExport(module, "wine_get_version", out _);
        }
        finally
        {
            NativeLibrary.Free(module);
        }
    }

    protected sealed class NativeResources
    {
        public required IEngineFactory Factory { get; init; }
        public required IRenderDevice Device { get; init; }
        public required IDeviceContext ImmediateContext { get; init; }
        public required ISwapChain NativeSwapChain { get; set; }
    }

    private sealed class Direct3DLifetime(SdlWindow window) : IGraphicsBackendLifetime
    {
        public nint NativeContext => IntPtr.Zero;

        public void SetVSync(bool enable)
        {
        }

        public void Resize(IRenderDevice renderDevice, ISwapChain swapChain)
        {
            SdlNativeBindings.SdlGetWindowSize(window, out var width, out var height);
            swapChain.Resize((uint)System.Math.Max(width, 0), (uint)System.Math.Max(height, 0));
        }

        public void ReleaseBeforeCommonResources()
        {
        }

        public void ReleaseAfterCommonResources(SdlWindow? window)
        {
        }
    }
}

public sealed class D3D11GraphicsBackend : Direct3DGraphicsBackend
{
    public override RenderBackend Backend => RenderBackend.D3D11;

    protected override NativeResources CreateNativeResources(RenderDeviceSettings deviceSettings,
        SwapChainDesc swapChainDesc, FullScreenModeDesc fullScreenDesc, Win32NativeWindow nativeWindow)
    {
        using var createInfo = new EngineD3D11CreateInfo();
        ConfigureCreateInfo(createInfo, deviceSettings);
        var factory = DiligentNativeBindings.LoadAndGetEngineFactoryD3D11();
        if (factory.IsNull)
            throw new InvalidOperationException("D3D11 engine factory could not be loaded.");

        factory.CreateDeviceAndContextsD3D11(createInfo, out var device, out var immediateContext);
        if (device.IsNull || immediateContext.IsNull)
        {
            if (!device.IsNull)
                _ = device.Release();
            if (!immediateContext.IsNull)
                _ = immediateContext.Release();
            throw new InvalidOperationException(
                "Generated CreateDeviceAndContextsD3D11 binding returned a null device or context.");
        }

        try
        {
            factory.CreateSwapChainD3D11(device, immediateContext, swapChainDesc, fullScreenDesc,
                nativeWindow, out var swapChain);
            if (swapChain.IsNull)
                throw new InvalidOperationException("Generated CreateSwapChainD3D11 binding returned a null swap chain.");
            return new NativeResources
            {
                Factory = factory,
                Device = device,
                ImmediateContext = immediateContext,
                NativeSwapChain = swapChain
            };
        }
        catch
        {
            _ = immediateContext.Release();
            _ = device.Release();
            throw;
        }
    }
}

public sealed class D3D12GraphicsBackend : Direct3DGraphicsBackend
{
    public override RenderBackend Backend => RenderBackend.D3D12;

    protected override NativeResources CreateNativeResources(RenderDeviceSettings deviceSettings,
        SwapChainDesc swapChainDesc, FullScreenModeDesc fullScreenDesc, Win32NativeWindow nativeWindow)
    {
        using var createInfo = new EngineD3D12CreateInfo();
        ConfigureCreateInfo(createInfo, deviceSettings);
        CopyBackendDeviceSettings(createInfo, deviceSettings.D3D12);
        var factory = DiligentNativeBindings.LoadAndGetEngineFactoryD3D12();
        if (factory.IsNull)
            throw new InvalidOperationException("D3D12 engine factory could not be loaded.");

        factory.CreateDeviceAndContextsD3D12(createInfo, out var device, out var immediateContext);
        if (device.IsNull || immediateContext.IsNull)
        {
            if (!device.IsNull)
                _ = device.Release();
            if (!immediateContext.IsNull)
                _ = immediateContext.Release();
            throw new InvalidOperationException(
                "Generated CreateDeviceAndContextsD3D12 binding returned a null device or context.");
        }

        try
        {
            factory.CreateSwapChainD3D12(device, immediateContext, swapChainDesc, fullScreenDesc,
                nativeWindow, out var swapChain);
            if (swapChain.IsNull)
                throw new InvalidOperationException("Generated CreateSwapChainD3D12 binding returned a null swap chain.");
            return new NativeResources
            {
                Factory = factory,
                Device = device,
                ImmediateContext = immediateContext,
                NativeSwapChain = swapChain
            };
        }
        catch
        {
            _ = immediateContext.Release();
            _ = device.Release();
            throw;
        }
    }

    private static void CopyBackendDeviceSettings(EngineD3D12CreateInfo createInfo,
        RenderDeviceSettingsD3D12 settings)
    {
        var cpuDescriptorHeapAllocationSize = createInfo.CPUDescriptorHeapAllocationSize;
        for (var i = 0; i < 4; ++i)
        {
            if (settings.CpuDescriptorHeapAllocationSize[i].HasValue)
                cpuDescriptorHeapAllocationSize[i] = checked((uint)settings.CpuDescriptorHeapAllocationSize[i]!.Value);
        }

        var gpuDescriptorHeapSize = createInfo.GPUDescriptorHeapSize;
        var gpuDescriptorHeapDynamicSize = createInfo.GPUDescriptorHeapDynamicSize;
        var dynamicDescriptorAllocationChunkSize = createInfo.DynamicDescriptorAllocationChunkSize;
        for (var i = 0; i < 2; ++i)
        {
            if (settings.GpuDescriptorHeapSize[i].HasValue)
                gpuDescriptorHeapSize[i] = checked((uint)settings.GpuDescriptorHeapSize[i]!.Value);
            if (settings.GpuDescriptorHeapDynamicSize[i].HasValue)
                gpuDescriptorHeapDynamicSize[i] = checked((uint)settings.GpuDescriptorHeapDynamicSize[i]!.Value);
            if (settings.DynamicDescriptorAllocationChunkSize[i].HasValue)
                dynamicDescriptorAllocationChunkSize[i] = checked((uint)settings.DynamicDescriptorAllocationChunkSize[i]!.Value);
        }

        if (settings.DynamicHeapPageSize.HasValue)
            createInfo.DynamicHeapPageSize = checked((uint)settings.DynamicHeapPageSize.Value);
        if (settings.NumDynamicHeapPagesToReserve.HasValue)
            createInfo.NumDynamicHeapPagesToReserve = checked((uint)settings.NumDynamicHeapPagesToReserve.Value);

        var queryPoolSizes = createInfo.QueryPoolSizes;
        for (var i = 1; i < (int)QueryType.QueryTypeNumTypes; ++i)
        {
            if (settings.QueryPoolSizes[i].HasValue)
                queryPoolSizes[i] = checked((uint)settings.QueryPoolSizes[i]!.Value);
        }
    }
}
