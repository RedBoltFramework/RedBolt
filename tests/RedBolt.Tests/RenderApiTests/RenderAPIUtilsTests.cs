namespace RedBolt.Tests;

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
        Assert.Equal(TextureFormat.RGBA8, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0x8058u));
        Assert.Equal(TextureFormat.Depth24Stencil8, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0x88F0u));
        Assert.Equal(TextureFormat.Depth32, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.Vulkan, 126u));
        Assert.Equal(TextureFormat.Unknown, RenderAPIUtils.GetTextureFormatFromInternal(RenderBackend.OpenGL, 0u));
    }

    [Fact]
    public void Texture_mip_helpers_and_format_categories_match_cpp_rules()
    {
        Assert.Equal(4u, RenderAPIUtils.GetMipLevelCount(new Vector3i(8, 4, 2)));
        Assert.Equal(new Vector3i(2, 1, 1), RenderAPIUtils.GetMipLevelSize(new Vector3i(8, 4, 2), 2));
        Assert.Equal(8u, RenderAPIUtils.GetMipLevelSizeInBytes(TextureFormat.RGBA8, new Vector3i(8, 4, 2), 2));

        Assert.True(RenderAPIUtils.IsDepthTextureFormat(TextureFormat.Depth32));
        Assert.True(RenderAPIUtils.IsDepthStencilTextureFormat(TextureFormat.Depth24Stencil8));
        Assert.False(RenderAPIUtils.IsColorTextureFormat(TextureFormat.Depth24Stencil8));
        Assert.True(RenderAPIUtils.IsColorTextureFormat(TextureFormat.RGBA16F));
    }

    [Fact]
    public void Shader_input_helpers_match_cpp_tables()
    {
        Assert.Equal("iTexCoord", RenderAPIUtils.ToShaderInputName(VertexElementSemantic.TexCoord));
        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.TexCoord, 3, 0),
            RenderAPIUtils.ParseVertexAttribute("input_iTexCoord3"));
        Assert.Equal(RedBolt.Bindings.Diligent.ShaderType.ShaderTypeCompute,
            RenderAPIUtils.ToInternalShaderType(ShaderType.CS));
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
    public void Render_device_settings_optional_values_round_trip_through_binary_archive()
    {
        var context = new Context();
        var buffer = new VectorBuffer();
        var settings = new RenderDeviceSettingsD3D12();
        settings.CpuDescriptorHeapAllocationSize[0] = 128;
        settings.CpuDescriptorHeapAllocationSize[1] = null;
        settings.GpuDescriptorHeapSize[1] = 256;
        settings.DynamicHeapPageSize = 4096;
        settings.QueryPoolSizes[0] = 4;
        settings.QueryPoolSizes[3] = 8;

        var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(output, "settings", settings);
        output.Flush();

        buffer.Seek(0);
        var loadedSettings = new RenderDeviceSettingsD3D12();
        var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            RenderAPIUtils.SerializeValue(input, "settings", loadedSettings);
        input.Flush();

        Assert.Equal((uint?)128, loadedSettings.CpuDescriptorHeapAllocationSize[0]);
        Assert.Null(loadedSettings.CpuDescriptorHeapAllocationSize[1]);
        Assert.Equal((uint?)256, loadedSettings.GpuDescriptorHeapSize[1]);
        Assert.Equal((uint?)4096, loadedSettings.DynamicHeapPageSize);
        Assert.Equal((uint?)4, loadedSettings.QueryPoolSizes[0]);
        Assert.Equal((uint?)8, loadedSettings.QueryPoolSizes[3]);
        Assert.Null(loadedSettings.QueryPoolSizes[1]);
    }
}
