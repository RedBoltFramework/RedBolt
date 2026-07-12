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
}
