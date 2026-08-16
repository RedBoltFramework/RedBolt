// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#if REDBOLT_PLATFORM_LINUX || REDBOLT_PLATFORM_WINDOWS
namespace RedBolt.RenderAPI.Backends;

using Microsoft.Extensions.Logging;
using System;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using RedBolt.Engine;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;

/// <summary>Vulkan graphics backend.</summary>
public sealed class VulkanGraphicsBackend : IGraphicsBackend
{
    private static readonly object SharedDeviceSync = new();
    private static IRenderDevice? SharedRenderDevice;
    private static IDeviceContext? SharedImmediateContext;

    public RenderBackend Backend => RenderBackend.Vulkan;

    public bool UsesOpenGLWindow => false;

    public void ConfigureWindow()
    {
    }

    public GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
        WindowSettings windowSettings, SdlWindow window)
    {
        Sdl.SdlGetWindowSize(window, out var width, out var height);

        using var createInfo = new EngineVkCreateInfo();
        CopyBackendDeviceSettings(createInfo, deviceSettings.Vulkan);
        EnableOptionalFeatures(createInfo);
        if (deviceSettings.AdapterId.HasValue)
            createInfo.AdapterId = checked((uint)deviceSettings.AdapterId.Value);

        var factory = Diligent.LoadAndGetEngineFactoryVk();
        if (factory.IsNull)
            throw new InvalidOperationException("Vulkan engine factory could not be loaded.");

        IRenderDevice device;
        IDeviceContext immediateContext;
        lock (SharedDeviceSync)
        {
            if (SharedRenderDevice is null || SharedRenderDevice.IsNull
                || SharedImmediateContext is null || SharedImmediateContext.IsNull)
            {
                factory.CreateDeviceAndContextsVk(createInfo, out device, out immediateContext);
                if (device.IsNull || immediateContext.IsNull)
                {
                    if (!device.IsNull)
                        _ = device.Release();
                    if (!immediateContext.IsNull)
                        _ = immediateContext.Release();
                    throw new InvalidOperationException(
                        "Generated CreateDeviceAndContextsVk binding returned a null device or context.");
                }

                // Keep the first returned reference as the process-wide shared
                // context. Each Graphics/RenderDevice pair receives one extra
                // reference below and releases it during normal disposal.
                SharedRenderDevice = device;
                SharedImmediateContext = immediateContext;
            }
            else
            {
                device = SharedRenderDevice;
                immediateContext = SharedImmediateContext;
            }

            _ = device.AddRef();
            _ = immediateContext.AddRef();
        }

        ISwapChain? nativeSwapChain = null;
        ISwapChainVk? swapChainVulkan = null;
        ISwapChain? swapChain = null;
        try
        {
            using var swapChainDesc = new SwapChainDesc
            {
                ColorBufferFormat = windowSettings.SRgb
                    ? TextureFormat.TexFormatRgba8UnormSrgb
                    : TextureFormat.TexFormatRgba8Unorm,
                DepthBufferFormat = TextureFormat.TexFormatUnknown
            };
            var nativeWindowInfo = NativeWindow.GetInfo(window);
#if REDBOLT_PLATFORM_LINUX
            using var nativeWindow = new LinuxNativeWindow
            {
                PDisplay = nativeWindowInfo.Display,
                WindowId = checked((uint)nativeWindowInfo.Window)
            };
#elif REDBOLT_PLATFORM_WINDOWS
            using var nativeWindow = new Win32NativeWindow
            {
                HWnd = nativeWindowInfo.Window
            };
#endif
            factory.CreateSwapChainVk(device, immediateContext, swapChainDesc, nativeWindow,
                out nativeSwapChain);
            if (nativeSwapChain.IsNull)
                throw new InvalidOperationException("Generated CreateSwapChainVk binding returned a null swap chain.");

            swapChainVulkan = ISwapChainVk.QueryManagedWrapper(nativeSwapChain)
                ?? throw new InvalidOperationException("Vulkan swap chain interface is unavailable.");
            var depthFormat = RenderDevice.SelectDefaultDepthFormat(device, true);
            var multiSample = (int)MathDefs.NextPowerOfTwo(
                (uint)System.Math.Clamp(parameters.MultiSample, 1, 16));
            using (var formatInfo = device.GetTextureFormatInfoExt(swapChainDesc.ColorBufferFormat))
            {
                while (multiSample > 1 && ((int)formatInfo.SampleCounts & multiSample) == 0)
                    multiSample >>= 1;
            }
            multiSample = System.Math.Max(1, multiSample);

            swapChain = Diligent.CreateProxySwapChainMS(
                device, immediateContext, nativeSwapChain, (uint)depthFormat, (uint)multiSample);
            _ = nativeSwapChain.Release();
            nativeSwapChain = null;
            if (swapChain.IsNull)
                throw new InvalidOperationException("Generated CreateProxySwapChainMS binding returned a null swap chain.");

            return new GraphicsBackendResources
            {
                Factory = factory,
                Device = device,
                ImmediateContext = immediateContext,
                SwapChain = swapChain,
                Lifetime = new VulkanLifetime(swapChainVulkan, window),
                Width = width,
                Height = height,
                MultiSample = multiSample
            };
        }
        catch
        {
            if (nativeSwapChain is { IsNull: false })
                _ = nativeSwapChain.Release();
            if (swapChain is { IsNull: false })
                _ = swapChain.Release();
            if (swapChainVulkan is { IsNull: false })
                swapChainVulkan.Dispose();
            _ = immediateContext.Release();
            _ = device.Release();
            throw;
        }
    }

    private static void EnableOptionalFeatures(EngineVkCreateInfo createInfo)
    {
        createInfo.FeaturesAsyncShaderCompilation = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesBinaryOcclusionQueries = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesBindlessResources = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesComputeShaders = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesDepthBiasClamp = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesDepthClamp = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesDualSourceBlend = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesDurationQueries = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesFormattedBuffers = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesGeometryShaders = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesIndependentBlend = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesInstanceDataStepRate = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesMeshShaders = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesMultiViewport = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesMultithreadedResourceCreation = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesNativeFence = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesNativeMultiDraw = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesOcclusionQueries = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesPipelineStatisticsQueries = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesPixelUAVWritesAndAtomics = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesRayTracing = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesResourceBuffer16BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesResourceBuffer8BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesSeparablePrograms = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesShaderFloat16 = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesShaderInputOutput16 = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesShaderInt8 = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesShaderResourceQueries = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesShaderResourceRuntimeArrays = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesShaderResourceStaticArrays = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesSparseResources = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesSubpassFramebufferFetch = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTessellation = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTextureComponentSwizzle = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTextureCompressionBC = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTextureCompressionETC2 = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTextureSubresourceViews = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTextureUAVExtendedFormats = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTileShaders = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTimestampQueries = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesTransferQueueTimestampQueries = DeviceFeatureState.DeviceFeatureStateDisabled;
        createInfo.FeaturesUniformBuffer16BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesUniformBuffer8BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesVariableRateShading = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesVertexPipelineUAVWritesAndAtomics = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesWaveOp = DeviceFeatureState.DeviceFeatureStateOptional;
        createInfo.FeaturesWireframeFill = DeviceFeatureState.DeviceFeatureStateOptional;
    }

    private static void CopyBackendDeviceSettings(EngineVkCreateInfo createInfo, RenderDeviceSettingsVulkan settings)
    {
        if (settings.MainDescriptorPoolSize is { } main)
        {
            createInfo.MainDescriptorPoolSizeMaxDescriptorSets = checked((uint)main.MaxDescriptorSets);
            createInfo.MainDescriptorPoolSizeNumSeparateSamplerDescriptors = checked((uint)main.NumSeparateSamplerDescriptors);
            createInfo.MainDescriptorPoolSizeNumCombinedSamplerDescriptors = checked((uint)main.NumCombinedSamplerDescriptors);
            createInfo.MainDescriptorPoolSizeNumSampledImageDescriptors = checked((uint)main.NumSampledImageDescriptors);
            createInfo.MainDescriptorPoolSizeNumStorageImageDescriptors = checked((uint)main.NumStorageImageDescriptors);
            createInfo.MainDescriptorPoolSizeNumUniformBufferDescriptors = checked((uint)main.NumUniformBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumStorageBufferDescriptors = checked((uint)main.NumStorageBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumUniformTexelBufferDescriptors = checked((uint)main.NumUniformTexelBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumStorageTexelBufferDescriptors = checked((uint)main.NumStorageTexelBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumInputAttachmentDescriptors = checked((uint)main.NumInputAttachmentDescriptors);
            createInfo.MainDescriptorPoolSizeNumAccelStructDescriptors = checked((uint)main.NumAccelStructDescriptors);
        }
        if (settings.DynamicDescriptorPoolSize is { } dynamic)
        {
            createInfo.DynamicDescriptorPoolSizeMaxDescriptorSets = checked((uint)dynamic.MaxDescriptorSets);
            createInfo.DynamicDescriptorPoolSizeNumSeparateSamplerDescriptors = checked((uint)dynamic.NumSeparateSamplerDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumCombinedSamplerDescriptors = checked((uint)dynamic.NumCombinedSamplerDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumSampledImageDescriptors = checked((uint)dynamic.NumSampledImageDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumStorageImageDescriptors = checked((uint)dynamic.NumStorageImageDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumUniformBufferDescriptors = checked((uint)dynamic.NumUniformBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumStorageBufferDescriptors = checked((uint)dynamic.NumStorageBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumUniformTexelBufferDescriptors = checked((uint)dynamic.NumUniformTexelBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumStorageTexelBufferDescriptors = checked((uint)dynamic.NumStorageTexelBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumInputAttachmentDescriptors = checked((uint)dynamic.NumInputAttachmentDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumAccelStructDescriptors = checked((uint)dynamic.NumAccelStructDescriptors);
        }
        if (settings.DeviceLocalMemoryPageSize.HasValue)
            createInfo.DeviceLocalMemoryPageSize = checked((uint)settings.DeviceLocalMemoryPageSize.Value);
        if (settings.HostVisibleMemoryPageSize.HasValue)
            createInfo.HostVisibleMemoryPageSize = checked((uint)settings.HostVisibleMemoryPageSize.Value);
        if (settings.DeviceLocalMemoryReserveSize.HasValue)
            createInfo.DeviceLocalMemoryReserveSize = checked((uint)settings.DeviceLocalMemoryReserveSize.Value);
        if (settings.HostVisibleMemoryReserveSize.HasValue)
            createInfo.HostVisibleMemoryReserveSize = checked((uint)settings.HostVisibleMemoryReserveSize.Value);
        if (settings.UploadHeapPageSize.HasValue)
            createInfo.UploadHeapPageSize = checked((uint)settings.UploadHeapPageSize.Value);
        if (settings.DynamicHeapSize.HasValue)
            createInfo.DynamicHeapSize = checked((uint)settings.DynamicHeapSize.Value);
        if (settings.DynamicHeapPageSize.HasValue)
            createInfo.DynamicHeapPageSize = checked((uint)settings.DynamicHeapPageSize.Value);

        var queryPoolSizes = createInfo.QueryPoolSizes;
        for (var i = 1; i < (int)QueryType.QueryTypeNumTypes; ++i)
        {
            var size = settings.QueryPoolSizes[i];
            if (size.HasValue)
                queryPoolSizes[i] = checked((uint)size.Value);
        }
    }

    private sealed class VulkanLifetime(ISwapChainVk swapChainVulkan, SdlWindow window)
        : IGraphicsBackendLifetime, IVulkanGraphicsApi
    {
        public nint NativeContext => IntPtr.Zero;

        public ISwapChainVk SwapChain => swapChainVulkan;

        public void SetVSync(bool enable)
        {
        }

        public void Resize(IRenderDevice renderDevice, ISwapChain swapChain)
        {
            Sdl.SdlGetWindowSize(window, out var width, out var height);
            swapChain.Resize((uint)System.Math.Max(width, 0), (uint)System.Math.Max(height, 0));
        }

        public void ReleaseBeforeCommonResources()
        {
            if (!swapChainVulkan.IsNull)
                swapChainVulkan.Dispose();
        }

        public void ReleaseAfterCommonResources(SdlWindow? window)
        {
        }
    }
}
#endif
