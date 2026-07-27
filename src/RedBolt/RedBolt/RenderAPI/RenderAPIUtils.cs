using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Platform;

namespace RedBolt.RenderAPI;

/// <summary>
/// Render API utility functions.
/// </summary>
public static class RenderAPIUtils
{
    private static readonly string[] BackendNames =
    [
        "D3D11",
        "D3D12",
        "OpenGL",
        "Vulkan"
    ];

    private static readonly string[] ShaderTypeNames =
    [
        "Vertex",
        "Pixel",
        "Geometry",
        "Hull",
        "Domain",
        "Compute"
    ];

    private static readonly global::RedBolt.Bindings.Diligent.ShaderType[] ShaderTypes =
    [
        global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
        global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
        global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeGeometry,
        global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeHull,
        global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeDomain,
        global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeCompute
    ];

    private static readonly ShaderTranslationPolicy[] SupportedShaderPolicies =
    [
        ShaderTranslationPolicy.Translate,
        ShaderTranslationPolicy.Optimize
    ];

    private static readonly ShaderTranslationPolicy[] SupportedOpenGLShaderPolicies =
    [
        ShaderTranslationPolicy.Verbatim,
        ShaderTranslationPolicy.Translate,
        ShaderTranslationPolicy.Optimize
    ];

    private static readonly string[] ShaderInputNames =
    [
        "iPos", // SEM_POSITION
        "iNormal", // SEM_NORMAL
        "iBinormal", // SEM_BINORMAL
        "iTangent", // SEM_TANGENT
        "iTexCoord", // SEM_TEXCOORD
        "iColor", // SEM_COLOR
        "iBlendWeights", // SEM_BLENDWEIGHTS
        "iBlendIndices", // SEM_BLENDINDICES
        "" // SEM_OBJECTINDEX
    ];

    private static readonly TextureFormatInfo[] NativeTextureFormatInfos =
        CreateNativeTextureFormatInfos();

    private static readonly Dictionary<global::RedBolt.Bindings.Diligent.TextureFormat,
        TextureFormatInfo> EmulatedTextureFormatInfos =
        CreateEmulatedTextureFormatInfos();

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
        => BackendNames[(int)backend];

    public static string ToString(ShaderType type)
        => ShaderTypeNames[(int)type];

    public static VertexShaderAttribute? ParseVertexAttribute(string name)
    {
        for (var index = 0; index < ShaderInputNames.Length; ++index)
        {
            var semanticName = ShaderInputNames[index];
            var semanticValue = (VertexElementSemantic)index;

            var semanticPosition = name.IndexOf(semanticName, StringComparison.Ordinal);
            if (semanticPosition < 0)
                continue;

            var semanticIndexPosition = semanticPosition + semanticName.Length;
            var semanticIndex = 0;
            while (semanticIndexPosition < name.Length)
            {
                var digit = name[semanticIndexPosition] - '0';
                if (digit is < 0 or > 9)
                    break;

                semanticIndex = semanticIndex * 10 + digit;
                ++semanticIndexPosition;
            }

            return new VertexShaderAttribute(semanticValue, semanticIndex, 0);
        }

        return null;
    }

    public static string ToShaderInputName(VertexElementSemantic semantic) => ShaderInputNames[(int)semantic];

    public static global::RedBolt.Bindings.Diligent.ShaderType ToInternalShaderType(ShaderType type) =>
        ShaderTypes[(int)type];

    public static int GetMipLevelCount(Vector3i size)
    {
        var maxLevels = 1;
        var dim = size;
        while (dim.X > 1 || dim.Y > 1 || dim.Z > 1)
        {
            ++maxLevels;
            dim.X = dim.X > 1 ? dim.X >> 1 : 1;
            dim.Y = dim.Y > 1 ? dim.Y >> 1 : 1;
            dim.Z = dim.Z > 1 ? dim.Z >> 1 : 1;
        }

        return maxLevels;
    }

    public static Vector3i GetMipLevelSize(Vector3i size, int level)
    {
        var dim = size;
        dim.X >>= level;
        dim.Y >>= level;
        dim.Z >>= level;
        return Vector3i.Max(dim, Vector3i.One);
    }

    public static int GetMipLevelSizeInBytes(TextureFormat textureFormat, Vector3i size, int level)
    {
        var formatAttribs = GetTextureFormatInfo(textureFormat);
        var levelSize = GetMipLevelSize(size, level);
        var widthInBlocks = (levelSize.X + formatAttribs.BlockWidth - 1) / formatAttribs.BlockWidth;
        var heightInBlocks = (levelSize.Y + formatAttribs.BlockHeight - 1) / formatAttribs.BlockHeight;
        var elementSize = formatAttribs.ComponentSize *
            (formatAttribs.ComponentType != global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed
                ? formatAttribs.NumComponents
                : 1);
        return elementSize * widthInBlocks * heightInBlocks * levelSize.Z;
    }

    public static bool IsDepthTextureFormat(TextureFormat format)
    {
        var componentType = NativeTextureFormatInfos[(int)format].ComponentType;
        return componentType is global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeDepth
            or global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeDepthStencil;
    }

    public static bool IsDepthStencilTextureFormat(TextureFormat format) =>
        NativeTextureFormatInfos[(int)format].ComponentType ==
        global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeDepthStencil;

    public static bool IsColorTextureFormat(TextureFormat format) =>
        NativeTextureFormatInfos[(int)format].ComponentType !=
        global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeUndefined && !IsDepthTextureFormat(format);

    public static TextureFormatInfo GetTextureFormatInfo(
        global::RedBolt.Bindings.Diligent.TextureFormat textureFormat)
    {
        var formatValue = (int)textureFormat;
        if (formatValue < NativeTextureFormatInfos.Length)
            return NativeTextureFormatInfos[formatValue];
        if (EmulatedTextureFormatInfos.TryGetValue(textureFormat, out var formatInfo))
            return formatInfo;
        return NativeTextureFormatInfos[(int)global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatUnknown];
    }

    public static bool IsTextureFormatSRGB(global::RedBolt.Bindings.Diligent.TextureFormat format) => format is
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgrx8UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc2UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc3UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc7UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8a1UnormSrgb
        or global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgba8UnormSrgb;

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
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8a1Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8a1UnormSrgb,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgba8Unorm => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgba8UnormSrgb,
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
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8a1UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgb8a1Unorm,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgba8UnormSrgb => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatEtc2Rgba8Unorm,
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

        var backend = requestedBackend ?? defaultBackend;
        var supportedBackends = Context.Instance.RuntimeServices.GraphicsBackends;
        foreach (var supportedBackend in supportedBackends)
        {
            if (supportedBackend.Backend == backend)
                return backend;
        }

        return supportedBackends.First().Backend;
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

        var supportedPolicies = backend == RenderBackend.OpenGL
            ? SupportedOpenGLShaderPolicies
            : SupportedShaderPolicies;
        var policy = requestedPolicy ?? defaultPolicy;
        foreach (var supportedPolicy in supportedPolicies)
        {
            if (supportedPolicy == policy)
                return policy;
        }

        return supportedPolicies[0];
    }

    public static void SerializeValue(Archive archive, string name, ref RenderDeviceSettingsVulkan value)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(name);

        using var block = archive.OpenUnorderedBlock(name);
        var mainDescriptorPoolSize = value.MainDescriptorPoolSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "mainDescriptorPoolSize",
            ref mainDescriptorPoolSize, null, SerializeOptionalVulkanDescriptorPoolSize);
        value.MainDescriptorPoolSize = mainDescriptorPoolSize;
        var dynamicDescriptorPoolSize = value.DynamicDescriptorPoolSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "dynamicDescriptorPoolSize",
            ref dynamicDescriptorPoolSize, null, SerializeOptionalVulkanDescriptorPoolSize);
        value.DynamicDescriptorPoolSize = dynamicDescriptorPoolSize;
        var deviceLocalMemoryPageSize = value.DeviceLocalMemoryPageSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "deviceLocalMemoryPageSize",
            ref deviceLocalMemoryPageSize, null, SerializeOptionalInt);
        value.DeviceLocalMemoryPageSize = deviceLocalMemoryPageSize;
        var hostVisibleMemoryPageSize = value.HostVisibleMemoryPageSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "hostVisibleMemoryPageSize",
            ref hostVisibleMemoryPageSize, null, SerializeOptionalInt);
        value.HostVisibleMemoryPageSize = hostVisibleMemoryPageSize;
        var deviceLocalMemoryReserveSize = value.DeviceLocalMemoryReserveSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "deviceLocalMemoryReserveSize",
            ref deviceLocalMemoryReserveSize, null, SerializeOptionalInt);
        value.DeviceLocalMemoryReserveSize = deviceLocalMemoryReserveSize;
        var hostVisibleMemoryReserveSize = value.HostVisibleMemoryReserveSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "hostVisibleMemoryReserveSize",
            ref hostVisibleMemoryReserveSize, null, SerializeOptionalInt);
        value.HostVisibleMemoryReserveSize = hostVisibleMemoryReserveSize;
        var uploadHeapPageSize = value.UploadHeapPageSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "uploadHeapPageSize",
            ref uploadHeapPageSize, null, SerializeOptionalInt);
        value.UploadHeapPageSize = uploadHeapPageSize;
        var dynamicHeapSize = value.DynamicHeapSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "dynamicHeapSize",
            ref dynamicHeapSize, null, SerializeOptionalInt);
        value.DynamicHeapSize = dynamicHeapSize;
        var dynamicHeapPageSize = value.DynamicHeapPageSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "dynamicHeapPageSize",
            ref dynamicHeapPageSize, null, SerializeOptionalInt);
        value.DynamicHeapPageSize = dynamicHeapPageSize;
        SerializeQueryTypes(archive, ref value);
    }

    public static void SerializeValue(Archive archive, string name, ref RenderDeviceSettingsD3D12 value)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(name);

        using var block = archive.OpenUnorderedBlock(name);
        SerializeCpuDescriptorHeapAllocationSize(archive, "mainDescriptorPoolSize_cvb_srv_uav", ref value, 0);
        SerializeCpuDescriptorHeapAllocationSize(archive, "mainDescriptorPoolSize_sampler", ref value, 1);
        SerializeCpuDescriptorHeapAllocationSize(archive, "mainDescriptorPoolSize_rtv", ref value, 2);
        SerializeCpuDescriptorHeapAllocationSize(archive, "mainDescriptorPoolSize_dsv", ref value, 3);
        SerializeGpuDescriptorHeapSize(archive, "gpuDescriptorHeapSize_cvb_srv_uav", ref value, 0);
        SerializeGpuDescriptorHeapSize(archive, "gpuDescriptorHeapSize_sampler", ref value, 1);
        SerializeGpuDescriptorHeapDynamicSize(archive, "gpuDescriptorHeapDynamicSize_cvb_srv_uav", ref value, 0);
        SerializeGpuDescriptorHeapDynamicSize(archive, "gpuDescriptorHeapDynamicSize_sampler", ref value, 1);
        SerializeDynamicDescriptorAllocationChunkSize(archive,
            "dynamicDescriptorAllocationChunkSize_cvb_srv_uav", ref value, 0);
        SerializeDynamicDescriptorAllocationChunkSize(archive,
            "dynamicDescriptorAllocationChunkSize_sampler", ref value, 1);
        var dynamicHeapPageSize = value.DynamicHeapPageSize;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "dynamicHeapPageSize",
            ref dynamicHeapPageSize, null, SerializeOptionalInt);
        value.DynamicHeapPageSize = dynamicHeapPageSize;
        var numDynamicHeapPagesToReserve = value.NumDynamicHeapPagesToReserve;
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, "numDynamicHeapPagesToReserve",
            ref numDynamicHeapPagesToReserve, null, SerializeOptionalInt);
        value.NumDynamicHeapPagesToReserve = numDynamicHeapPagesToReserve;
        SerializeQueryTypes(archive, ref value);
    }

    private static void SerializeQueryTypes(Archive archive, ref RenderDeviceSettingsVulkan value)
    {
        var occlusion = (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion;
        var binaryOcclusion = (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeBinaryOcclusion;
        var timestamp = (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp;
        var pipelineStatistics = (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypePipelineStatistics;
        var duration = (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeDuration;
        SerializeQueryType(archive, "queryPoolSize_occlusion", ref value, occlusion);
        SerializeQueryType(archive, "queryPoolSize_binaryOcclusion", ref value, binaryOcclusion);
        SerializeQueryType(archive, "queryPoolSize_timestamp", ref value, timestamp);
        SerializeQueryType(archive, "queryPoolSize_pipelineStatistics", ref value, pipelineStatistics);
        SerializeQueryType(archive, "queryPoolSize_duration", ref value, duration);
    }

    private static void SerializeQueryTypes(Archive archive, ref RenderDeviceSettingsD3D12 value)
    {
        SerializeQueryType(archive, "queryPoolSize_occlusion", ref value,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion);
        SerializeQueryType(archive, "queryPoolSize_binaryOcclusion", ref value,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeBinaryOcclusion);
        SerializeQueryType(archive, "queryPoolSize_timestamp", ref value,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp);
        SerializeQueryType(archive, "queryPoolSize_pipelineStatistics", ref value,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypePipelineStatistics);
        SerializeQueryType(archive, "queryPoolSize_duration", ref value,
            (int)global::RedBolt.Bindings.Diligent.QueryType.QueryTypeDuration);
    }

    public static void SerializeValue(Archive archive, string name, ref VulkanDescriptorPoolSize value)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(name);
        using var block = archive.OpenUnorderedBlock(name);
        SerializeVulkanDescriptorPoolSizeFields(archive, ref value);
    }

    private static void SerializeCpuDescriptorHeapAllocationSize(Archive archive, string name,
        ref RenderDeviceSettingsD3D12 value, int index)
    {
        var size = value.CpuDescriptorHeapAllocationSize[index];
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, name, ref size, null, SerializeOptionalInt);
        value.CpuDescriptorHeapAllocationSize[index] = size;
    }

    private static void SerializeGpuDescriptorHeapSize(Archive archive, string name,
        ref RenderDeviceSettingsD3D12 value, int index)
    {
        var size = value.GpuDescriptorHeapSize[index];
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, name, ref size, null, SerializeOptionalInt);
        value.GpuDescriptorHeapSize[index] = size;
    }

    private static void SerializeGpuDescriptorHeapDynamicSize(Archive archive, string name,
        ref RenderDeviceSettingsD3D12 value, int index)
    {
        var size = value.GpuDescriptorHeapDynamicSize[index];
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, name, ref size, null, SerializeOptionalInt);
        value.GpuDescriptorHeapDynamicSize[index] = size;
    }

    private static void SerializeDynamicDescriptorAllocationChunkSize(Archive archive, string name,
        ref RenderDeviceSettingsD3D12 value, int index)
    {
        var size = value.DynamicDescriptorAllocationChunkSize[index];
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, name, ref size, null, SerializeOptionalInt);
        value.DynamicDescriptorAllocationChunkSize[index] = size;
    }

    private static void SerializeQueryType(Archive archive, string name,
        ref RenderDeviceSettingsVulkan value, int index)
    {
        var size = value.QueryPoolSizes[index];
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, name, ref size, null, SerializeOptionalInt);
        value.QueryPoolSizes[index] = size;
    }

    private static void SerializeQueryType(Archive archive, string name,
        ref RenderDeviceSettingsD3D12 value, int index)
    {
        var size = value.QueryPoolSizes[index];
        ArchiveSerialization.SerializeStrictlyOptionalValue(archive, name, ref size, null, SerializeOptionalInt);
        value.QueryPoolSizes[index] = size;
    }

    private static void SerializeOptionalInt(Archive archive, string name, ref int? value)
    {
        if (!value.HasValue)
        {
            System.Diagnostics.Debug.Assert(archive.IsInput);
            value = 0;
        }
        var serializedValue = value.Value;
        ArchiveSerialization.SerializeValue(archive, name, ref serializedValue);
        value = serializedValue;
    }

    private static void SerializeOptionalVulkanDescriptorPoolSize(Archive archive, string name,
        ref VulkanDescriptorPoolSize? value)
    {
        if (!value.HasValue)
        {
            System.Diagnostics.Debug.Assert(archive.IsInput);
            value = new VulkanDescriptorPoolSize();
        }
        var serializedValue = value.Value;
        SerializeValue(archive, name, ref serializedValue);
        value = serializedValue;
    }

    private static void SerializeVulkanDescriptorPoolSizeFields(
        Archive archive, ref VulkanDescriptorPoolSize value)
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
                    28u => TextureFormat.TexFormatRgba8Unorm,
                    29u => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb,
                    55u => TextureFormat.TexFormatD16Unorm,
                    45u => TextureFormat.TexFormatD24UnormS8Uint,
                    40u => TextureFormat.TexFormatD32Float,
                    20u => TextureFormat.TexFormatD32FloatS8x24Uint,
                    _ => TextureFormat.TexFormatUnknown
                };
            case RenderBackend.Vulkan:
                return internalFormat switch
                {
                    37u => TextureFormat.TexFormatRgba8Unorm,
                    43u => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb,
                    124u => TextureFormat.TexFormatD16Unorm,
                    129u => TextureFormat.TexFormatD24UnormS8Uint,
                    126u => TextureFormat.TexFormatD32Float,
                    130u => TextureFormat.TexFormatD32FloatS8x24Uint,
                    _ => TextureFormat.TexFormatUnknown
                };
            case RenderBackend.OpenGL:
                return internalFormat switch
                {
                    0x8051u or 0x8058u => TextureFormat.TexFormatRgba8Unorm,
                    0x8C41u or 0x8C43u => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb,
                    0x81A5u => TextureFormat.TexFormatD16Unorm,
                    0x81A6u or 0x88F0u => TextureFormat.TexFormatD24UnormS8Uint,
                    0x8CACu => TextureFormat.TexFormatD32Float,
                    0x8CADu => TextureFormat.TexFormatD32FloatS8x24Uint,
                    _ => TextureFormat.TexFormatUnknown
                };
            default:
                return TextureFormat.TexFormatUnknown;
        }
    }

    private static TextureFormatInfo[] CreateNativeTextureFormatInfos()
    {
        var infos = new TextureFormatInfo[
            (int)global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatNumFormats];
        for (var i = 0; i < infos.Length; ++i)
        {
            var format = (global::RedBolt.Bindings.Diligent.TextureFormat)i;
            var handle = global::RedBolt.Bindings.Diligent.DiligentNativeBindings.GetTextureFormatAttribs(format);
            using var info = global::RedBolt.Bindings.Diligent.TextureFormatAttribs.CreateManagedWrapper(handle, false);
            infos[i] = new TextureFormatInfo(
                global::System.Runtime.InteropServices.Marshal.PtrToStringUTF8(info.Name) ?? string.Empty,
                info.Format, info.ComponentSize, info.NumComponents,
                info.ComponentType, info.IsTypeless, info.BlockWidth, info.BlockHeight);
        }
        return infos;
    }

    private static Dictionary<global::RedBolt.Bindings.Diligent.TextureFormat,
        TextureFormatInfo> CreateEmulatedTextureFormatInfos()
    {
        return new Dictionary<global::RedBolt.Bindings.Diligent.TextureFormat,
            TextureFormatInfo>
        {
            [EmulatedTextureFormat.PvrtcRgb2Bpp] = new("TEX_FORMAT_PVRTC_RGB_2BPP",
                EmulatedTextureFormat.PvrtcRgb2Bpp, 8, 3,
                global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed, false, 8, 4),
            [EmulatedTextureFormat.PvrtcRgba2Bpp] = new("TEX_FORMAT_PVRTC_RGBA_2BPP",
                EmulatedTextureFormat.PvrtcRgba2Bpp, 8, 4,
                global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed, false, 8, 4),
            [EmulatedTextureFormat.PvrtcRgb4Bpp] = new("TEX_FORMAT_PVRTC_RGB_4BPP",
                EmulatedTextureFormat.PvrtcRgb4Bpp, 8, 3,
                global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed, false, 4, 4),
            [EmulatedTextureFormat.PvrtcRgba4Bpp] = new("TEX_FORMAT_PVRTC_RGBA_4BPP",
                EmulatedTextureFormat.PvrtcRgba4Bpp, 8, 4,
                global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed, false, 4, 4)
        };
    }

}
