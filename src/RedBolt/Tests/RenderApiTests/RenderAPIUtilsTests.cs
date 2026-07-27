using RedBolt.Core;
using RedBolt.IO;
using RedBolt.RenderAPI;
namespace RedBolt.Tests;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;


public sealed class RenderAPIUtilsTests
{
    [Fact]
    public void Backend_and_shader_names_match_cpp_tables()
    {
        Assert.Equal("D3D11", RenderAPIUtils.ToString(RenderBackend.D3D11));
        Assert.Equal("D3D12", RenderAPIUtils.ToString(RenderBackend.D3D12));
        Assert.Equal("OpenGL", RenderAPIUtils.ToString(RenderBackend.OpenGL));
        Assert.Equal("Vulkan", RenderAPIUtils.ToString(RenderBackend.Vulkan));
        Assert.Equal("Vertex", RenderAPIUtils.ToString(ShaderType.VS));
        Assert.Equal("Pixel", RenderAPIUtils.ToString(ShaderType.PS));
        Assert.Equal("Compute", RenderAPIUtils.ToString(ShaderType.CS));
    }

    [Fact]
    public void Backend_selection_and_texture_internal_formats_match_managed_surface()
    {
        Assert.Equal(RenderBackend.Vulkan, RenderAPIUtils.SelectRenderBackend(RenderBackend.Vulkan));
        Assert.Equal(RenderBackend.OpenGL, RenderAPIUtils.SelectRenderBackend());
        Assert.Equal(ShaderTranslationPolicy.Verbatim, RenderAPIUtils.SelectShaderTranslationPolicy(RenderBackend.OpenGL));
        Assert.Equal(TextureFormat.TexFormatRgba8Unorm, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0x8058u));
        Assert.Equal(TextureFormat.TexFormatD24UnormS8Uint, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0x88F0u));
#if REDBOLT_VULKAN_SUPPORTED
        Assert.Equal(TextureFormat.TexFormatD32Float, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Vulkan, 126u));
        Assert.Equal(TextureFormat.TexFormatD16Unorm, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Vulkan, 124u));
        Assert.Equal(TextureFormat.TexFormatD32FloatS8x24Uint, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Vulkan, 130u));
        Assert.Equal(RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb,
            RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Vulkan, 43u));
#endif
#if REDBOLT_D3D11_SUPPORTED || REDBOLT_D3D12_SUPPORTED
        Assert.Equal(TextureFormat.TexFormatD32FloatS8x24Uint, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.D3D11, 20u));
        Assert.Equal(RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb,
            RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.D3D11, 29u));
#endif
        Assert.Equal(RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb,
            RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0x8C43u));
        Assert.Equal(TextureFormat.TexFormatUnknown, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0u));
    }

    [Fact]
    public void Texture_mip_helpers_and_format_categories_match_cpp_rules()
    {
        Assert.Equal(4, RenderAPIUtils.GetMipLevelCount(new Vector3i(8, 4, 2)));
        Assert.Equal(new Vector3i(2, 1, 1), RenderAPIUtils.GetMipLevelSize(new Vector3i(8, 4, 2), 2));
        Assert.Equal(8, RenderAPIUtils.GetMipLevelSizeInBytes(TextureFormat.TexFormatRgba8Unorm, new Vector3i(8, 4, 2), 2));
        Assert.Equal(48, RenderAPIUtils.GetMipLevelSizeInBytes(
            RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1Unorm, new Vector3i(9, 5, 1), 0));

        Assert.True(RenderAPIUtils.IsDepthTextureFormat(TextureFormat.TexFormatD32Float));
        Assert.True(RenderAPIUtils.IsDepthStencilTextureFormat(TextureFormat.TexFormatD24UnormS8Uint));
        Assert.True(RenderAPIUtils.IsDepthTextureFormat(TextureFormat.TexFormatD16Unorm));
        Assert.True(RenderAPIUtils.IsDepthStencilTextureFormat(TextureFormat.TexFormatD32FloatS8x24Uint));
        Assert.False(RenderAPIUtils.IsColorTextureFormat(TextureFormat.TexFormatD24UnormS8Uint));
        Assert.True(RenderAPIUtils.IsColorTextureFormat(TextureFormat.TexFormatRgba16Float));
        Assert.True(RenderAPIUtils.IsColorTextureFormat(
            RedBolt.Bindings.Diligent.TextureFormat.TexFormatBc1Unorm));
        Assert.False(RenderAPIUtils.IsColorTextureFormat(TextureFormat.TexFormatUnknown));
    }

    [Fact]
    public void Shader_input_helpers_match_cpp_tables()
    {
        Assert.Equal("iTexCoord", RenderAPIUtils.ToShaderInputName(VertexElementSemantic.TexCoord));
        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.TexCoord, 3, 0),
            RenderAPIUtils.ParseVertexAttribute("input_iTexCoord3"));
        Assert.Equal(RedBolt.Bindings.Diligent.ShaderType.ShaderTypeCompute,
            RenderAPIUtils.ToInternalShaderType(ShaderType.CS));
        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.ObjectIndex, 12, 0),
            RenderAPIUtils.ParseVertexAttribute("12custom"));
    }

    [Fact]
    public void Native_srgb_texture_format_helpers_match_cpp_mapping()
    {
        var linear = RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm;
        var srgb = RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb;

        Assert.False(RenderAPIUtils.IsTextureFormatSRGB(linear));
        Assert.True(RenderAPIUtils.IsTextureFormatSRGB(srgb));
        Assert.Equal(srgb, RenderAPIUtils.SetTextureFormatSRGB(linear));
        Assert.Equal(linear, RenderAPIUtils.SetTextureFormatSRGB(srgb, false));
    }

    [Fact]
    public void Texture_format_info_matches_native_and_emulated_attributes()
    {
        Assert.True(typeof(TextureFormatInfo).IsValueType);

        var native = RenderAPIUtils.GetTextureFormatInfo(
            RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm);
        Assert.Equal("TEX_FORMAT_RGBA8_UNORM", native.Name);
        Assert.Equal((byte)1, native.ComponentSize);
        Assert.Equal((byte)4, native.NumComponents);
        Assert.Equal((byte)1, native.BlockWidth);
        Assert.Equal((byte)1, native.BlockHeight);

        var emulated = RenderAPIUtils.GetTextureFormatInfo(
            (RedBolt.Bindings.Diligent.TextureFormat)(int)EmulatedTextureFormat.PvrtcRgba2Bpp);
        Assert.Equal("TEX_FORMAT_PVRTC_RGBA_2BPP", emulated.Name);
        Assert.Equal((byte)8, emulated.ComponentSize);
        Assert.Equal((byte)4, emulated.NumComponents);
        Assert.Equal((byte)8, emulated.BlockWidth);
        Assert.Equal((byte)4, emulated.BlockHeight);
        Assert.Equal(RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed, emulated.ComponentType);
    }

    [Fact]
    public void Render_device_settings_optional_values_round_trip_through_binary_archive()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var settings = new RenderDeviceSettingsD3D12();
        settings.CpuDescriptorHeapAllocationSize[0] = 128;
        settings.CpuDescriptorHeapAllocationSize[1] = null;
        settings.GpuDescriptorHeapSize[1] = 256;
        settings.DynamicHeapPageSize = 4096;
        settings.QueryPoolSizes[(int)RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion] = 4;
        settings.QueryPoolSizes[(int)RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp] = 8;

        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(output, "settings", ref settings);
        output.Flush();

        buffer.Seek(0);
        var loadedSettings = new RenderDeviceSettingsD3D12();
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(input, "settings", ref loadedSettings);
        input.Flush();

        Assert.Equal(128, loadedSettings.CpuDescriptorHeapAllocationSize[0]);
        Assert.Null(loadedSettings.CpuDescriptorHeapAllocationSize[1]);
        Assert.Equal(256, loadedSettings.GpuDescriptorHeapSize[1]);
        Assert.Equal(4096, loadedSettings.DynamicHeapPageSize);
        Assert.Null(loadedSettings.QueryPoolSizes[(int)RedBolt.Bindings.Diligent.QueryType.QueryTypeUndefined]);
        Assert.Equal(4, loadedSettings.QueryPoolSizes[(int)RedBolt.Bindings.Diligent.QueryType.QueryTypeOcclusion]);
        Assert.Equal(8, loadedSettings.QueryPoolSizes[(int)RedBolt.Bindings.Diligent.QueryType.QueryTypeTimestamp]);
    }

    [Fact]
    public void Vulkan_descriptor_pool_size_round_trips_through_binary_archive()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var value = new VulkanDescriptorPoolSize
        {
            MaxDescriptorSets = 1,
            NumSeparateSamplerDescriptors = 2,
            NumCombinedSamplerDescriptors = 3,
            NumSampledImageDescriptors = 4,
            NumStorageImageDescriptors = 5,
            NumUniformBufferDescriptors = 6,
            NumStorageBufferDescriptors = 7,
            NumUniformTexelBufferDescriptors = 8,
            NumStorageTexelBufferDescriptors = 9,
            NumInputAttachmentDescriptors = 10,
            NumAccelStructDescriptors = 11
        };

        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(output, "pool", ref value);
        output.Flush();

        buffer.Seek(0);
        var loadedValue = new VulkanDescriptorPoolSize();
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(input, "pool", ref loadedValue);
        input.Flush();

        Assert.Equal(value.MaxDescriptorSets, loadedValue.MaxDescriptorSets);
        Assert.Equal(value.NumSeparateSamplerDescriptors, loadedValue.NumSeparateSamplerDescriptors);
        Assert.Equal(value.NumCombinedSamplerDescriptors, loadedValue.NumCombinedSamplerDescriptors);
        Assert.Equal(value.NumSampledImageDescriptors, loadedValue.NumSampledImageDescriptors);
        Assert.Equal(value.NumStorageImageDescriptors, loadedValue.NumStorageImageDescriptors);
        Assert.Equal(value.NumUniformBufferDescriptors, loadedValue.NumUniformBufferDescriptors);
        Assert.Equal(value.NumStorageBufferDescriptors, loadedValue.NumStorageBufferDescriptors);
        Assert.Equal(value.NumUniformTexelBufferDescriptors, loadedValue.NumUniformTexelBufferDescriptors);
        Assert.Equal(value.NumStorageTexelBufferDescriptors, loadedValue.NumStorageTexelBufferDescriptors);
        Assert.Equal(value.NumInputAttachmentDescriptors, loadedValue.NumInputAttachmentDescriptors);
        Assert.Equal(value.NumAccelStructDescriptors, loadedValue.NumAccelStructDescriptors);
    }
}
