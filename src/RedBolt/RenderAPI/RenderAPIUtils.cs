namespace RedBolt;

/// <summary>
/// Render API utility functions.
/// </summary>
public static class RenderAPIUtils
{
    public static bool IsOpenGLESBackend(RenderBackend backend)
    {
        var platform = ProcessUtils.GetPlatform();
        var isMobilePlatform = platform is PlatformId.Android or PlatformId.iOS or PlatformId.TvOS or PlatformId.Web or PlatformId.RaspberryPi;
        return backend == RenderBackend.OpenGL && isMobilePlatform;
    }

    public static bool IsMetalBackend(RenderBackend backend)
    {
        var platform = ProcessUtils.GetPlatform();
        var isApplePlatform = platform is PlatformId.iOS or PlatformId.TvOS or PlatformId.MacOS;
        return backend == RenderBackend.Vulkan && isApplePlatform;
    }

    public static string ToString(RenderBackend backend)
    {
        var backendNames = new[]
        {
            "D3D11",
            "D3D12",
            "OpenGL",
            "Vulkan"
        };
        return backendNames[(int)backend];
    }

    public static string ToString(ShaderType type)
    {
        var shaderTypeNames = new[]
        {
            "Vertex",
            "Pixel",
            "Geometry",
            "Hull",
            "Domain",
            "Compute"
        };
        return shaderTypeNames[(int)type];
    }

    public static uint GetMipLevelCount(Vector3i size)
    {
        var largestDimension = Math.Max(size.X, Math.Max(size.Y, size.Z));
        var levels = 1u;
        while (largestDimension > 1)
        {
            largestDimension >>= 1;
            ++levels;
        }

        return levels;
    }

    public static Vector3i GetMipLevelSize(Vector3i size, uint level) => new(
        Math.Max(size.X >> (int)level, 1),
        Math.Max(size.Y >> (int)level, 1),
        Math.Max(size.Z >> (int)level, 1));

    public static uint GetMipLevelSizeInBytes(TextureFormat textureFormat, Vector3i size, uint level)
    {
        var levelSize = GetMipLevelSize(size, level);
        return checked((uint)levelSize.X * (uint)levelSize.Y * (uint)levelSize.Z * GetTexelSize(textureFormat));
    }

    public static bool IsDepthTextureFormat(TextureFormat format) =>
        format is TextureFormat.Depth32 or TextureFormat.Depth24Stencil8;

    public static bool IsDepthStencilTextureFormat(TextureFormat format) =>
        format == TextureFormat.Depth24Stencil8;

    public static bool IsColorTextureFormat(TextureFormat format) => format is
        TextureFormat.R8 or TextureFormat.RG16F or TextureFormat.RGB8 or TextureFormat.RGBA8 or TextureFormat.RGBA16F;

    public static RenderBackend SelectRenderBackend(RenderBackend? requestedBackend = null)
    {
        var platform = ProcessUtils.GetPlatform();
        var defaultBackend = platform switch
        {
            PlatformId.Windows or PlatformId.UniversalWindowsPlatform => RenderBackend.D3D11,
            _ => RenderBackend.OpenGL
        };

        // Native compile-time backend feature flags are not represented by the managed API.
        // Keep the complete managed backend enum as the supported set.
        var supportedBackends = new[]
        {
            RenderBackend.D3D11,
            RenderBackend.D3D12,
            RenderBackend.OpenGL,
            RenderBackend.Vulkan
        };

        var backend = requestedBackend ?? defaultBackend;
        foreach (var supportedBackend in supportedBackends)
        {
            if (supportedBackend == backend)
                return backend;
        }

        return supportedBackends[0];
    }

    public static ShaderTranslationPolicy SelectShaderTranslationPolicy(
        RenderBackend backend, ShaderTranslationPolicy? requestedPolicy = null)
    {
        var defaultPolicy = backend switch
        {
            RenderBackend.D3D11 or RenderBackend.D3D12 => ShaderTranslationPolicy.Translate,
            RenderBackend.OpenGL => ShaderTranslationPolicy.Verbatim,
            RenderBackend.Vulkan => ShaderTranslationPolicy.Optimize,
            _ => ShaderTranslationPolicy.Verbatim
        };

        // The managed project does not currently expose the native shader translator or optimizer.
        // OpenGL therefore supports only verbatim shaders until those APIs are ported.
        if (backend == RenderBackend.OpenGL)
            return ShaderTranslationPolicy.Verbatim;

        return requestedPolicy ?? defaultPolicy;
    }

    public static void SerializeValue(Archive archive, string name, RenderDeviceSettingsVulkan value)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        // TODO: port: the managed Archive API has no equivalent optional-value serializer.
        // void SerializeValue(Archive& archive, const char* name, RenderDeviceSettingsVulkan& value)
        // {
        //     auto block = archive.OpenUnorderedBlock(name);
        //     // clang-format off
        //     SerializeStrictlyOptionalValue(archive, "mainDescriptorPoolSize", value.mainDescriptorPoolSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "dynamicDescriptorPoolSize", value.dynamicDescriptorPoolSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "deviceLocalMemoryPageSize", value.deviceLocalMemoryPageSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "hostVisibleMemoryPageSize", value.hostVisibleMemoryPageSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "deviceLocalMemoryReserveSize", value.deviceLocalMemoryReserveSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "hostVisibleMemoryReserveSize", value.hostVisibleMemoryReserveSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "uploadHeapPageSize", value.uploadHeapPageSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "dynamicHeapSize", value.dynamicHeapSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "dynamicHeapPageSize", value.dynamicHeapPageSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeQueryTypes(archive, value.queryPoolSizes_);
        //     // clang-format on
        // }
    }

    public static void SerializeValue(Archive archive, string name, RenderDeviceSettingsD3D12 value)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        // TODO: port: the managed Archive API has no equivalent optional-value serializer.
        // void SerializeValue(Archive& archive, const char* name, RenderDeviceSettingsD3D12& value)
        // {
        //     auto block = archive.OpenUnorderedBlock(name);
        //     // clang-format off
        //     SerializeStrictlyOptionalValue(archive, "mainDescriptorPoolSize_cvb_srv_uav", value.cpuDescriptorHeapAllocationSize_[0], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "mainDescriptorPoolSize_sampler", value.cpuDescriptorHeapAllocationSize_[1], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "mainDescriptorPoolSize_rtv", value.cpuDescriptorHeapAllocationSize_[2], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "mainDescriptorPoolSize_dsv", value.cpuDescriptorHeapAllocationSize_[3], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "gpuDescriptorHeapSize_cvb_srv_uav", value.gpuDescriptorHeapSize_[0], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "gpuDescriptorHeapSize_sampler", value.gpuDescriptorHeapSize_[1], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "gpuDescriptorHeapDynamicSize_cvb_srv_uav", value.gpuDescriptorHeapDynamicSize_[0], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "gpuDescriptorHeapDynamicSize_sampler", value.gpuDescriptorHeapDynamicSize_[1], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "dynamicDescriptorAllocationChunkSize_cvb_srv_uav", value.dynamicDescriptorAllocationChunkSize_[0], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "dynamicDescriptorAllocationChunkSize_sampler", value.dynamicDescriptorAllocationChunkSize_[1], ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "dynamicHeapPageSize", value.dynamicHeapPageSize_, ea::nullopt, OptionalSerializer{});
        //     SerializeStrictlyOptionalValue(archive, "numDynamicHeapPagesToReserve", value.numDynamicHeapPagesToReserve_, ea::nullopt, OptionalSerializer{});
        //     SerializeQueryTypes(archive, value.queryPoolSizes_);
        //     // clang-format on
        // }
    }

    /// <summary>
    /// Try to find a suitable texture format for the given internal GAPI format.
    /// </summary>
    public static TextureFormat GetTextureFormatFromInternal(RenderBackend backend, uint internalFormat)
    {
        switch (backend)
        {
            case RenderBackend.D3D11:
            case RenderBackend.D3D12:
                return internalFormat switch
                {
                    28u => TextureFormat.RGBA8,
                    29u => TextureFormat.RGBA8,
                    55u => TextureFormat.Unknown,
                    45u => TextureFormat.Depth24Stencil8,
                    40u => TextureFormat.Depth32,
                    20u => TextureFormat.Unknown,
                    _ => TextureFormat.Unknown
                };
            case RenderBackend.Vulkan:
                return internalFormat switch
                {
                    37u => TextureFormat.RGBA8,
                    43u => TextureFormat.RGBA8,
                    124u => TextureFormat.Unknown,
                    129u => TextureFormat.Depth24Stencil8,
                    126u => TextureFormat.Depth32,
                    130u => TextureFormat.Unknown,
                    _ => TextureFormat.Unknown
                };
            case RenderBackend.OpenGL:
                return internalFormat switch
                {
                    0x8051u or 0x8058u => TextureFormat.RGBA8,
                    0x8C41u or 0x8C43u => TextureFormat.RGBA8,
                    0x81A5u => TextureFormat.Unknown,
                    0x81A6u or 0x88F0u => TextureFormat.Depth24Stencil8,
                    0x8CACu => TextureFormat.Depth32,
                    0x8CADu => TextureFormat.Unknown,
                    _ => TextureFormat.Unknown
                };
            default:
                return TextureFormat.Unknown;
        }
    }

    private static uint GetTexelSize(TextureFormat format) => format switch
    {
        TextureFormat.R8 => 1u,
        TextureFormat.RG16F => 4u,
        TextureFormat.RGB8 => 4u,
        TextureFormat.RGBA8 => 4u,
        TextureFormat.RGBA16F => 8u,
        TextureFormat.Depth32 => 4u,
        TextureFormat.Depth24Stencil8 => 4u,
        _ => throw new NotSupportedException($"Texture format '{format}' is not supported.")
    };
}
