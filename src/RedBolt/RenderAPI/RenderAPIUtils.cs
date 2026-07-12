namespace RedBolt;

/// <summary>
/// Render API utility functions.
/// </summary>
public static class RenderAPIUtils
{
    private static readonly string[] ShaderInputNames =
    [
        "iPos",
        "iNormal",
        "iBinormal",
        "iTangent",
        "iTexCoord",
        "iColor",
        "iBlendWeights",
        "iBlendIndices",
        ""
    ];

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

    public static VertexShaderAttribute? ParseVertexAttribute(string name)
    {
        for (var index = 0; index < ShaderInputNames.Length; ++index)
        {
            var semanticName = ShaderInputNames[index];
            var semanticPosition = name.IndexOf(semanticName, StringComparison.Ordinal);
            if (semanticPosition < 0)
                continue;

            var semanticIndexPosition = semanticPosition + semanticName.Length;
            var semanticIndex = 0u;
            while (semanticIndexPosition < name.Length)
            {
                var digit = name[semanticIndexPosition] - '0';
                if (digit is < 0 or > 9)
                    break;

                semanticIndex = semanticIndex * 10u + (uint)digit;
                ++semanticIndexPosition;
            }

            return new VertexShaderAttribute((VertexElementSemantic)index, semanticIndex, 0);
        }

        return null;
    }

    public static string ToShaderInputName(VertexElementSemantic semantic) => ShaderInputNames[(int)semantic];

    public static global::RedBolt.Bindings.Diligent.ShaderType ToInternalShaderType(ShaderType type) => type switch
    {
        ShaderType.VS => global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
        ShaderType.PS => global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
        ShaderType.GS => global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeGeometry,
        ShaderType.HS => global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeHull,
        ShaderType.DS => global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeDomain,
        ShaderType.CS => global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeCompute,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

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

    public static bool IsTextureFormatSRGB(global::RedBolt.Bindings.Diligent.TextureFormat format) => format is
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgrx8UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc2UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc3UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc7UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8UnormSrgb;

    public static global::RedBolt.Bindings.Diligent.TextureFormat SetTextureFormatSRGB(
        global::RedBolt.Bindings.Diligent.TextureFormat format, bool sRgb = true)
    {
        if (sRgb)
        {
            return format switch
            {
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgrx8Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgrx8UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc2Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc2UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc3Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc3UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc7Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc7UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8UnormSrgb,
                _ => format
            };
        }

        return format switch
        {
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgrx8UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgrx8Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc2UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc2Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc3UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc3Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc7UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc7Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8Unorm,
            _ => format
        };
    }

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

        using var block = archive.OpenUnorderedBlock(name);
        value.MainDescriptorPoolSize = SerializeOptionalVulkanDescriptorPoolSize(archive, "mainDescriptorPoolSize", value.MainDescriptorPoolSize);
        value.DynamicDescriptorPoolSize = SerializeOptionalVulkanDescriptorPoolSize(archive, "dynamicDescriptorPoolSize", value.DynamicDescriptorPoolSize);
        value.DeviceLocalMemoryPageSize = SerializeOptionalUInt(archive, "deviceLocalMemoryPageSize", value.DeviceLocalMemoryPageSize);
        value.HostVisibleMemoryPageSize = SerializeOptionalUInt(archive, "hostVisibleMemoryPageSize", value.HostVisibleMemoryPageSize);
        value.DeviceLocalMemoryReserveSize = SerializeOptionalUInt(archive, "deviceLocalMemoryReserveSize", value.DeviceLocalMemoryReserveSize);
        value.HostVisibleMemoryReserveSize = SerializeOptionalUInt(archive, "hostVisibleMemoryReserveSize", value.HostVisibleMemoryReserveSize);
        value.UploadHeapPageSize = SerializeOptionalUInt(archive, "uploadHeapPageSize", value.UploadHeapPageSize);
        value.DynamicHeapSize = SerializeOptionalUInt(archive, "dynamicHeapSize", value.DynamicHeapSize);
        value.DynamicHeapPageSize = SerializeOptionalUInt(archive, "dynamicHeapPageSize", value.DynamicHeapPageSize);
        SerializeQueryTypes(archive, value.QueryPoolSizes);
    }

    public static void SerializeValue(Archive archive, string name, RenderDeviceSettingsD3D12 value)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        using var block = archive.OpenUnorderedBlock(name);
        value.CpuDescriptorHeapAllocationSize[0] = SerializeOptionalUInt(archive, "mainDescriptorPoolSize_cvb_srv_uav", value.CpuDescriptorHeapAllocationSize[0]);
        value.CpuDescriptorHeapAllocationSize[1] = SerializeOptionalUInt(archive, "mainDescriptorPoolSize_sampler", value.CpuDescriptorHeapAllocationSize[1]);
        value.CpuDescriptorHeapAllocationSize[2] = SerializeOptionalUInt(archive, "mainDescriptorPoolSize_rtv", value.CpuDescriptorHeapAllocationSize[2]);
        value.CpuDescriptorHeapAllocationSize[3] = SerializeOptionalUInt(archive, "mainDescriptorPoolSize_dsv", value.CpuDescriptorHeapAllocationSize[3]);
        value.GpuDescriptorHeapSize[0] = SerializeOptionalUInt(archive, "gpuDescriptorHeapSize_cvb_srv_uav", value.GpuDescriptorHeapSize[0]);
        value.GpuDescriptorHeapSize[1] = SerializeOptionalUInt(archive, "gpuDescriptorHeapSize_sampler", value.GpuDescriptorHeapSize[1]);
        value.GpuDescriptorHeapDynamicSize[0] = SerializeOptionalUInt(archive, "gpuDescriptorHeapDynamicSize_cvb_srv_uav", value.GpuDescriptorHeapDynamicSize[0]);
        value.GpuDescriptorHeapDynamicSize[1] = SerializeOptionalUInt(archive, "gpuDescriptorHeapDynamicSize_sampler", value.GpuDescriptorHeapDynamicSize[1]);
        value.DynamicDescriptorAllocationChunkSize[0] = SerializeOptionalUInt(archive, "dynamicDescriptorAllocationChunkSize_cvb_srv_uav", value.DynamicDescriptorAllocationChunkSize[0]);
        value.DynamicDescriptorAllocationChunkSize[1] = SerializeOptionalUInt(archive, "dynamicDescriptorAllocationChunkSize_sampler", value.DynamicDescriptorAllocationChunkSize[1]);
        value.DynamicHeapPageSize = SerializeOptionalUInt(archive, "dynamicHeapPageSize", value.DynamicHeapPageSize);
        value.NumDynamicHeapPagesToReserve = SerializeOptionalUInt(archive, "numDynamicHeapPagesToReserve", value.NumDynamicHeapPagesToReserve);
        SerializeQueryTypes(archive, value.QueryPoolSizes);
    }

    private static void SerializeQueryTypes(Archive archive, uint?[] sizes)
    {
        sizes[0] = SerializeOptionalUInt(archive, "queryPoolSize_occlusion", sizes[0]);
        sizes[1] = SerializeOptionalUInt(archive, "queryPoolSize_binaryOcclusion", sizes[1]);
        sizes[2] = SerializeOptionalUInt(archive, "queryPoolSize_timestamp", sizes[2]);
        sizes[3] = SerializeOptionalUInt(archive, "queryPoolSize_pipelineStatistics", sizes[3]);
        sizes[4] = SerializeOptionalUInt(archive, "queryPoolSize_duration", sizes[4]);
    }

    private static uint? SerializeOptionalUInt(Archive archive, string name, uint? value)
    {
        if (!archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            using var block = archive.OpenUnorderedBlock(name);
            var initialized = archive.IsInput ? false : value.HasValue;
            archive.Serialize("initialized", ref initialized);

            if (initialized)
            {
                var serializedValue = archive.IsInput ? 0u : value!.Value;
                archive.Serialize("value", ref serializedValue);
                if (archive.IsInput)
                    value = serializedValue;
            }
            else if (archive.IsInput)
                value = null;
        }
        else
        {
            var initialized = archive.IsInput ? archive.HasElementOrBlock(name) : value.HasValue;
            if (initialized)
            {
                var serializedValue = archive.IsInput ? 0u : value!.Value;
                archive.Serialize(name, ref serializedValue);
                if (archive.IsInput)
                    value = serializedValue;
            }
            else if (archive.IsInput)
                value = null;
        }

        return value;
    }

    private static global::RedBolt.Bindings.Diligent.VulkanDescriptorPoolSize? SerializeOptionalVulkanDescriptorPoolSize(
        Archive archive, string name, global::RedBolt.Bindings.Diligent.VulkanDescriptorPoolSize? value)
    {
        if (!archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            using var block = archive.OpenUnorderedBlock(name);
            var initialized = archive.IsInput ? false : value is not null;
            archive.Serialize("initialized", ref initialized);

            if (initialized)
            {
                var serializedValue = archive.IsInput ? new global::RedBolt.Bindings.Diligent.VulkanDescriptorPoolSize() : value!;
                SerializeVulkanDescriptorPoolSizeFields(archive, serializedValue);
                if (archive.IsInput)
                    value = serializedValue;
            }
            else if (archive.IsInput)
                value = null;
        }
        else
        {
            var initialized = archive.IsInput ? archive.HasElementOrBlock(name) : value is not null;
            if (initialized)
            {
                using var block = archive.OpenUnorderedBlock(name);
                var serializedValue = archive.IsInput ? new global::RedBolt.Bindings.Diligent.VulkanDescriptorPoolSize() : value!;
                SerializeVulkanDescriptorPoolSizeFields(archive, serializedValue);
                if (archive.IsInput)
                    value = serializedValue;
            }
            else if (archive.IsInput)
                value = null;
        }

        return value;
    }

    private static void SerializeVulkanDescriptorPoolSizeFields(
        Archive archive, global::RedBolt.Bindings.Diligent.VulkanDescriptorPoolSize value)
    {
        var maxDescriptorSets = value.MaxDescriptorSets;
        archive.Serialize("maxDescriptorSets", ref maxDescriptorSets);
        value.MaxDescriptorSets = maxDescriptorSets;
        var numSeparateSamplerDescriptors = value.NumSeparateSamplerDescriptors;
        archive.Serialize("numSeparateSamplerDescriptors", ref numSeparateSamplerDescriptors);
        value.NumSeparateSamplerDescriptors = numSeparateSamplerDescriptors;
        var numCombinedSamplerDescriptors = value.NumCombinedSamplerDescriptors;
        archive.Serialize("numCombinedSamplerDescriptors", ref numCombinedSamplerDescriptors);
        value.NumCombinedSamplerDescriptors = numCombinedSamplerDescriptors;
        var numSampledImageDescriptors = value.NumSampledImageDescriptors;
        archive.Serialize("numSampledImageDescriptors", ref numSampledImageDescriptors);
        value.NumSampledImageDescriptors = numSampledImageDescriptors;
        var numStorageImageDescriptors = value.NumStorageImageDescriptors;
        archive.Serialize("numStorageImageDescriptors", ref numStorageImageDescriptors);
        value.NumStorageImageDescriptors = numStorageImageDescriptors;
        var numUniformBufferDescriptors = value.NumUniformBufferDescriptors;
        archive.Serialize("numUniformBufferDescriptors", ref numUniformBufferDescriptors);
        value.NumUniformBufferDescriptors = numUniformBufferDescriptors;
        var numStorageBufferDescriptors = value.NumStorageBufferDescriptors;
        archive.Serialize("numStorageBufferDescriptors", ref numStorageBufferDescriptors);
        value.NumStorageBufferDescriptors = numStorageBufferDescriptors;
        var numUniformTexelBufferDescriptors = value.NumUniformTexelBufferDescriptors;
        archive.Serialize("numUniformTexelBufferDescriptors", ref numUniformTexelBufferDescriptors);
        value.NumUniformTexelBufferDescriptors = numUniformTexelBufferDescriptors;
        var numStorageTexelBufferDescriptors = value.NumStorageTexelBufferDescriptors;
        archive.Serialize("numStorageTexelBufferDescriptors", ref numStorageTexelBufferDescriptors);
        value.NumStorageTexelBufferDescriptors = numStorageTexelBufferDescriptors;
        var numInputAttachmentDescriptors = value.NumInputAttachmentDescriptors;
        archive.Serialize("numInputAttachmentDescriptors", ref numInputAttachmentDescriptors);
        value.NumInputAttachmentDescriptors = numInputAttachmentDescriptors;
        var numAccelStructDescriptors = value.NumAccelStructDescriptors;
        archive.Serialize("numAccelStructDescriptors", ref numAccelStructDescriptors);
        value.NumAccelStructDescriptors = numAccelStructDescriptors;
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
