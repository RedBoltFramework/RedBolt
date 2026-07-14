using RedBolt;

namespace RedBolt.Tests;

public sealed class TextureTests
{
    [Fact]
    public void Texture2D_registers_empty_named_factory()
    {
        var context = new Context();
        Texture2D.RegisterObject(context);

        using var texture = Assert.IsType<Texture2D>(context.CreateObject(ObjectReflection<Texture2D>.TypeId));
        Assert.Equal(string.Empty, texture.Name);
    }

    [Fact]
    public void Texture3D_registers_empty_named_factory()
    {
        var context = new Context();
        Texture3D.RegisterObject(context);

        using var texture = Assert.IsType<Texture3D>(context.CreateObject(ObjectReflection<Texture3D>.TypeId));
        Assert.Equal(string.Empty, texture.Name);
    }

    [Fact]
    public void TextureCube_registers_empty_named_factory()
    {
        var context = new Context();
        TextureCube.RegisterObject(context);

        using var texture = Assert.IsType<TextureCube>(context.CreateObject(ObjectReflection<TextureCube>.TypeId));
        Assert.Equal(string.Empty, texture.Name);
    }

    [Fact]
    public void Texture2DArray_registers_empty_named_factory()
    {
        var context = new Context();
        Texture2DArray.RegisterObject(context);

        using var texture = Assert.IsType<Texture2DArray>(context.CreateObject(ObjectReflection<Texture2DArray>.TypeId));
        Assert.Equal(string.Empty, texture.Name);
    }

    [Fact]
    public void Texture_sampler_dimensions_and_flags_match_cpp_api()
    {
        var texture = new Texture2D("Textures/Smoke2D");
        texture.SetNumLevels(1u);
        Assert.True(texture.SetSize(8, 4, TextureFormat.RGBA8));

        texture.SetMinLod(1.5f);
        texture.SetMaxLod(6.0f);

        Assert.Equal(new Vector2i(8, 4), texture.GetSize());
        Assert.Equal(new IntRect(0, 0, 8, 4), texture.GetRect());
        Assert.Equal(4, texture.GetLevelWidth(1u));
        Assert.Equal(0, texture.GetLevelWidth(2u));
        Assert.Equal(1.5f, texture.GetMinLod());
        Assert.Equal(6.0f, texture.GetMaxLod());
        Assert.True(texture.GetAutoResolve());
        Assert.False(texture.IsRenderTarget());
        Assert.False(texture.IsDepthStencil());
        Assert.False(texture.IsUnorderedAccess());
        Assert.Null(texture.GetRenderSurface());

        var renderTexture = new Texture2D("Textures/RenderTarget");
        Assert.True(renderTexture.SetSize(8, 4, TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess));
        Assert.True(renderTexture.IsRenderTarget());
        Assert.False(renderTexture.IsDepthStencil());
        Assert.True(renderTexture.IsUnorderedAccess());
        var renderSurface = renderTexture.GetRenderSurface();
        Assert.NotNull(renderSurface);
        Assert.True(renderTexture.SetSize(8, 4, TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess));
        Assert.Same(renderSurface, renderTexture.GetRenderSurface());

        var renderCube = new TextureCube("Textures/RenderCube");
        Assert.True(renderCube.SetSize(8, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        Assert.True(renderCube.IsRenderTarget());
        Assert.True(renderCube.GetAutoResolve());

        var renderTextureArray = new Texture2DArray("Textures/RenderTargetArray");
        Assert.True(renderTextureArray.SetSize(2u, 8, 4, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        Assert.NotNull(renderTextureArray.GetRenderSurface(0));
        Assert.NotNull(renderTextureArray.GetRenderSurface(1));
        Assert.Null(renderTextureArray.GetRenderSurface(2));
    }

    [Fact]
    public void Texture_depth_components_match_cpp_format_sizes()
    {
        var texture = new Texture2D("Textures/Depth");

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth16));
        Assert.Equal(2u, texture.GetComponents());
        Assert.Equal(16u, texture.GetRowDataSize(8));

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth32));
        Assert.Equal(4u, texture.GetComponents());
        Assert.Equal(32u, texture.GetRowDataSize(8));

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth24Stencil8));
        Assert.Equal(4u, texture.GetComponents());

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth32Stencil8));
        Assert.Equal(8u, texture.GetComponents());
    }

    [Fact]
    public void Texture_xml_parameters_match_cpp_parser()
    {
        var file = new XMLFile("Textures/SmokeParameters.xml");
        var root = file.CreateRoot("texture");
        root.CreateChild("address").SetAttribute("coord", "V");
        root.GetChild("address").SetAttribute("mode", "CLAMP");
        root.CreateChild("filter").SetAttribute("mode", "TRILINEAR");
        root.GetChild("filter").SetUInt("anisotropy", 4u);
        root.CreateChild("mipmap").SetBool("enable", true);
        root.GetChild("mipmap").SetFloat("minlod", 1.0f);
        root.GetChild("mipmap").SetFloat("maxlod", 5.0f);
        root.CreateChild("quality").SetInt("low", 3);
        root.GetChild("quality").SetInt("med", 2);
        root.GetChild("quality").SetInt("high", 1);
        root.CreateChild("srgb").SetBool("enable", true);
        root.CreateChild("linear").SetBool("enable", true);

        var texture = new Texture2D("Textures/Smoke2D");
        texture.SetParameters(file);

        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.V));
        Assert.Equal(TextureFilterMode.Trilinear, texture.GetFilterMode());
        Assert.Equal(4u, texture.GetAnisotropy());
        Assert.Equal(0u, texture.GetLevels());
        Assert.Equal(1.0f, texture.GetMinLod());
        Assert.Equal(5.0f, texture.GetMaxLod());
        Assert.Equal(3, texture.GetMipsToSkip(MaterialQuality.Low));
        Assert.Equal(2, texture.GetMipsToSkip(MaterialQuality.Medium));
        Assert.Equal(1, texture.GetMipsToSkip(MaterialQuality.High));
        Assert.True(texture.GetSRGB());
        Assert.True(texture.GetLinear());
    }

    [Fact]
    public void Texture_invalid_quality_is_ignored()
    {
        var texture = new Texture2D("Textures/Smoke2D");

        texture.SetMipsToSkip((MaterialQuality)99u, 4);

        Assert.Equal(0, texture.GetMipsToSkip((MaterialQuality)99u));
        Assert.Equal(2, texture.GetMipsToSkip(MaterialQuality.Low));
        Assert.Equal(1, texture.GetMipsToSkip(MaterialQuality.Medium));
        Assert.Equal(0, texture.GetMipsToSkip(MaterialQuality.High));
    }

    [Fact]
    public void Texture2D_image_upload_and_readback_work()
    {
        var image = new Image("Textures/SmokeImage");
        var imageData = Enumerable.Range(0, 16).Select(value => (byte)(value * 3)).ToArray();
        Assert.True(image.SetSize(2, 2, 4u));
        image.SetData(imageData);

        var texture = new Texture2D("Textures/Smoke2D");
        texture.SetNumLevels(1u);
        Assert.True(texture.SetData(image));

        var readback = new byte[16];
        Assert.True(texture.GetData(0u, readback));
        Assert.Equal(imageData, readback);
        Assert.False(texture.GetData(0u, new byte[15]));

        var output = texture.GetImage();
        Assert.NotNull(output);
        Assert.Equal(2, output.Width);
        Assert.Equal(2, output.Height);
        Assert.Equal(imageData, output.GetData());
    }

    [Fact]
    public void Texture3D_image_upload_and_readback_work()
    {
        var image = new Image("Textures/SmokeVolumeImage");
        var imageData = Enumerable.Range(0, 4 * 4 * 4 * 4).Select(value => (byte)value).ToArray();
        Assert.True(image.SetSize(4, 4, 4, 4u));
        image.SetData(imageData);

        var texture = new Texture3D("Textures/Smoke3D");
        texture.SetNumLevels(1u);
        texture.SetMipsToSkip(MaterialQuality.High, 1);
        Assert.True(texture.SetData(image));
        Assert.Equal(2, texture.Width);
        Assert.Equal(2, texture.Height);
        Assert.Equal(2, texture.Depth);

        var readback = new byte[2 * 2 * 2 * 4];
        Assert.True(texture.GetData(0u, readback));
        Assert.Equal(readback.Length, texture.GetData(0u)?.Length);
    }

    [Fact]
    public void Texture3D_set_size_preserves_usage_flags()
    {
        var texture = new Texture3D("Textures/RenderVolume");

        Assert.True(texture.SetSize(4, 4, 4, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        Assert.True(texture.IsRenderTarget());
        Assert.NotNull(texture.GetRenderSurface());
    }

    [Fact]
    public void TextureCube_image_upload_and_readback_work()
    {
        var image = new Image("Textures/SmokeCubeImage");
        var imageData = Enumerable.Range(0, 2 * 2 * 4).Select(value => (byte)value).ToArray();
        Assert.True(image.SetSize(2, 2, 4u));
        image.SetData(imageData);

        var texture = new TextureCube("Textures/SmokeCube");
        texture.SetNumLevels(1u);
        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; face = (CubeMapFace)((int)face + 1))
            Assert.True(texture.SetData(face, image));

        Assert.Equal(TextureFilterMode.Bilinear, texture.GetFilterMode());
        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.U));
        Assert.True(texture.GetAutoResolve());
        Assert.True(texture.GetData(CubeMapFace.PositiveX, 0u, new byte[16]));
        Assert.Equal(imageData, texture.GetImage(CubeMapFace.PositiveX)?.GetData());
        Assert.Null(texture.GetRenderSurface(CubeMapFace.MaxCubeMapFaces));
    }

    [Fact]
    public void Texture2DArray_image_upload_and_readback_work()
    {
        var image = new Image("Textures/SmokeArrayImage");
        var imageData = Enumerable.Range(0, 2 * 2 * 4).Select(value => (byte)value).ToArray();
        Assert.True(image.SetSize(2, 2, 4u));
        image.SetData(imageData);

        var texture = new Texture2DArray("Textures/SmokeArray");
        texture.SetLayers(2u);
        texture.SetNumLevels(1u);
        Assert.True(texture.SetData(0u, image));
        Assert.True(texture.SetData(1u, image));
        Assert.Equal(2u, texture.Layers);
        Assert.True(texture.GetData(1u, 0u, new byte[16]));
        Assert.Equal(imageData, texture.GetData(0u, 0u));
    }
}
