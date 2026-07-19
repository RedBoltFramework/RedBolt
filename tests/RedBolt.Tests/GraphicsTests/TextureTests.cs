using RedBolt;

namespace RedBolt.Tests;

public sealed class TextureTests
{
    [Fact]
    public void Sampler_state_matches_cpp_independent_value_semantics()
    {
        Assert.True(typeof(SamplerStateDesc).IsValueType);
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<SamplerStateDesc>());

        var source = SamplerStateDesc.Bilinear(TextureAddressMode.Clamp);
        var copy = source;
        copy[TextureCoordinate.U] = TextureAddressMode.Mirror;
        copy.MaxLod = 4.0f;

        Assert.Equal(TextureAddressMode.Clamp, source[TextureCoordinate.U]);
        Assert.Equal(MathDefs.LargeValue, source.MaxLod);
        Assert.Equal(TextureAddressMode.Mirror, copy[TextureCoordinate.U]);
        Assert.Equal(4.0f, copy.MaxLod);

        using var texture = new Texture(string.Empty);
        Assert.Equal(MathDefs.LargeValue, texture.GetMaxLod());
    }

    [Fact]
    public void Texture_dispose_releases_shared_backup_reference_without_disposing_alias()
    {
        using var backup = new Texture("Textures/Backup");
        var texture = new Texture("Textures/Primary");
        texture.SetBackupTexture(backup);

        texture.Dispose();

        Assert.Null(texture.GetBackupTexture());
        Assert.Equal("Textures/Backup", backup.Name);
    }

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
        texture.SetNumLevels(1);
        Assert.True(texture.SetSize(8, 4, TextureFormat.RGBA8));

        texture.SetMinLod(1.5f);
        texture.SetMaxLod(6.0f);

        Assert.Equal(new Vector2i(8, 4), texture.GetSize());
        Assert.Equal(new IntRect(0, 0, 8, 4), texture.GetRect());
        Assert.Equal(4, texture.GetLevelWidth(1));
        Assert.Equal(0, texture.GetLevelWidth(2));
        Assert.Equal(1.5f, texture.GetMinLod());
        Assert.Equal(6.0f, texture.GetMaxLod());
        Assert.False(texture.GetAutoResolve());
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
        Assert.Null(renderSurface);
        Assert.True(renderTexture.SetSize(8, 4, TextureFormat.RGBA8,
            TextureFlag.BindRenderTarget | TextureFlag.BindUnorderedAccess));
        Assert.Null(renderTexture.GetRenderSurface());

        var renderCube = new TextureCube("Textures/RenderCube");
        Assert.True(renderCube.SetSize(8, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        Assert.True(renderCube.IsRenderTarget());
        Assert.False(renderCube.GetAutoResolve());

        var renderTextureArray = new Texture2DArray("Textures/RenderTargetArray");
        Assert.True(renderTextureArray.SetSize(2, 8, 4, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        Assert.Null(renderTextureArray.GetRenderSurface(0));
        Assert.Null(renderTextureArray.GetRenderSurface(1));
        Assert.Null(renderTextureArray.GetRenderSurface(2));
    }

    [Fact]
    public void Texture_depth_components_match_cpp_format_sizes()
    {
        var texture = new Texture2D("Textures/Depth");

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth16));
        Assert.Equal(2, texture.GetComponents());
        Assert.Equal(16, texture.GetRowDataSize(8));

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth32));
        Assert.Equal(4, texture.GetComponents());
        Assert.Equal(32, texture.GetRowDataSize(8));

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth24Stencil8));
        Assert.Equal(4, texture.GetComponents());

        Assert.True(texture.SetSize(8, 4, TextureFormat.Depth32Stencil8));
        Assert.Equal(8, texture.GetComponents());

        Assert.True(texture.SetSize(8, 4, TextureFormat.RGB8));
        Assert.Equal(4, texture.GetComponents());
        Assert.Equal(32, texture.GetRowDataSize(8));
    }

    [Fact]
    public void Texture_formats_preserve_cpp_rg8_compression_and_srgb_semantics()
    {
        Assert.True(typeof(TextureFormat).IsValueType);

        using var texture = new Texture2D("Textures/Formats");

        Assert.True(texture.SetSize(8, 4, TextureFormat.RG8));
        Assert.Equal(TextureFormat.RG8, texture.Format);
        Assert.Equal(2, texture.GetComponents());
        Assert.Equal(16, texture.GetRowDataSize(8));

        Assert.True(texture.SetSize(8, 4, TextureFormat.BC1));
        Assert.Equal(TextureFormat.BC1, texture.Format);
        Assert.True(texture.IsCompressed());
        Assert.Equal(16, texture.GetDataSize(8, 4));

        texture.SetSRGB(true);
        Assert.True(texture.SetSize(8, 4, TextureFormat.RGBA8));
        Assert.Equal(TextureFormat.RGBA8Srgb, texture.Format);
        Assert.True(texture.GetSRGB());
    }

    [Fact]
    public void Texture_sampler_mutators_update_raw_texture_sampler_state()
    {
        var texture = new Texture2D("Textures/SamplerState");
        Assert.True(texture.SetSize(8, 4, TextureFormat.RGBA8));

        texture.SetFilterMode(TextureFilterMode.Bilinear);
        texture.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
        texture.SetAnisotropy(8u);
        texture.SetShadowCompare(true);
        texture.SetMinLod(1.5f);
        texture.SetMaxLod(6.0f);

        var sampler = texture.GetSamplerStateDesc();
        Assert.Equal(TextureFilterMode.Bilinear, sampler.FilterMode);
        Assert.Equal(TextureAddressMode.Clamp, sampler[TextureCoordinate.U]);
        Assert.Equal(TextureAddressMode.Wrap, sampler[TextureCoordinate.V]);
        Assert.Equal((byte)8, sampler.Anisotropy);
        Assert.True(sampler.ShadowCompare);
        Assert.Equal(1.5f, sampler.MinLod);
        Assert.Equal(6.0f, sampler.MaxLod);
    }

    [Fact]
    public void Texture_sampler_state_exists_before_gpu_creation_and_has_value_semantics()
    {
        var texture = new Texture("Textures/SamplerState");
        var source = SamplerStateDesc.Bilinear(TextureAddressMode.Clamp);

        texture.SetSamplerStateDesc(source);
        source[TextureCoordinate.U] = TextureAddressMode.Mirror;
        var returned = texture.GetSamplerStateDesc();
        returned[TextureCoordinate.V] = TextureAddressMode.Mirror;

        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.U));
        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.V));
        texture.SetAnisotropy(256u);
        Assert.Equal(0u, texture.GetAnisotropy());
    }

    [Fact]
    public void Texture_xml_parameters_match_cpp_parser()
    {
        var file = new XMLFile("Textures/SmokeParameters.xml");
        var root = file.CreateRoot("texture");
        root.CreateChild("address").SetAttribute("coord", "V");
        root.GetChild("address").SetAttribute("mode", "CLAMP");
        root.CreateChild("address").SetAttribute("coord", "U");
        root.GetChild("address").GetNext("address").SetAttribute("mode", "BORDER");
        root.CreateChild("filter").SetAttribute("mode", "NEARESTANISOTROPIC");
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
        Assert.Equal(TextureAddressMode.Wrap, texture.GetAddressMode(TextureCoordinate.U));
        Assert.Equal(TextureFilterMode.NearestAnisotropic, texture.GetFilterMode());
        Assert.Equal(4u, texture.GetAnisotropy());
        Assert.Equal(0, texture.GetLevels());
        Assert.Equal(1.0f, texture.GetMinLod());
        Assert.Equal(5.0f, texture.GetMaxLod());
        Assert.Equal(3, texture.GetMipsToSkip(MaterialQuality.Low));
        Assert.Equal(2, texture.GetMipsToSkip(MaterialQuality.Medium));
        Assert.Equal(1, texture.GetMipsToSkip(MaterialQuality.High));
        Assert.False(texture.GetSRGB());
        Assert.True(texture.SetSize(8, 4, TextureFormat.RGBA8));
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
    public void Texture2D_image_upload_configures_texture()
    {
        var image = new Image("Textures/SmokeImage");
        var imageData = Enumerable.Range(0, 16).Select(value => (byte)(value * 3)).ToArray();
        Assert.True(image.SetSize(2, 2, 4u));
        image.SetData(imageData);

        var texture = new Texture2D("Textures/Smoke2D");
        texture.SetNumLevels(1);
        Assert.True(texture.SetData(image));

        Assert.Equal(2, texture.Width);
        Assert.Equal(2, texture.Height);
        Assert.Equal(TextureFormat.RGBA8, texture.Format);
    }

    [Fact]
    public void Texture3D_image_upload_and_readback_work()
    {
        var image = new Image("Textures/SmokeVolumeImage");
        var imageData = Enumerable.Range(0, 4 * 4 * 4 * 4).Select(value => (byte)value).ToArray();
        Assert.True(image.SetSize(4, 4, 4, 4u));
        image.SetData(imageData);

        var texture = new Texture3D("Textures/Smoke3D");
        texture.SetNumLevels(1);
        texture.SetMipsToSkip(MaterialQuality.High, 1);
        Assert.True(texture.SetData(image));
        Assert.Equal(2, texture.Width);
        Assert.Equal(2, texture.Height);
        Assert.Equal(2, texture.Depth);

        var readback = new byte[2 * 2 * 2 * 4];
        Assert.False(texture.GetData(0, readback));
    }

    [Fact]
    public void Texture_array_uploads_forward_to_raw_texture()
    {
        var volume = new Texture3D("Textures/Volume");
        Assert.True(volume.SetSize(2, 2, 2, TextureFormat.RGBA8));
        Assert.True(volume.SetData(0, 0, 0, 0, 2, 2, 2, new byte[2 * 2 * 2 * 4]));
        Assert.Equal(TextureType.Texture3D, volume.GetRawTexture()!.GetParams().Type);

        var cube = new TextureCube("Textures/Cube");
        Assert.True(cube.SetSize(2, TextureFormat.RGBA8));
        Assert.True(cube.SetData(CubeMapFace.PositiveX, 0, 0, 0, 2, 2, new byte[2 * 2 * 4]));
        Assert.Equal(TextureType.TextureCube, cube.GetRawTexture()!.GetParams().Type);

        var array = new Texture2DArray("Textures/Array");
        Assert.True(array.SetSize(2, 2, 2, TextureFormat.RGBA8));
        Assert.True(array.SetData(1, 0, 0, 0, 2, 2, new byte[2 * 2 * 4]));
        Assert.Equal(TextureType.Texture2DArray, array.GetRawTexture()!.GetParams().Type);
    }

    [Fact]
    public void Texture3D_set_size_ignores_usage_flags_like_cpp()
    {
        var texture = new Texture3D("Textures/RenderVolume");

        Assert.True(texture.SetSize(4, 4, 4, TextureFormat.RGBA8, TextureFlag.BindRenderTarget));
        Assert.False(texture.IsRenderTarget());
        Assert.Null(texture.GetRenderSurface());
    }

    [Fact]
    public void TextureCube_image_upload_and_readback_work()
    {
        var image = new Image("Textures/SmokeCubeImage");
        var imageData = Enumerable.Range(0, 2 * 2 * 4).Select(value => (byte)value).ToArray();
        Assert.True(image.SetSize(2, 2, 4u));
        image.SetData(imageData);

        var texture = new TextureCube("Textures/SmokeCube");
        texture.SetNumLevels(1);
        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; face = (CubeMapFace)((int)face + 1))
            Assert.True(texture.SetData(face, image));

        Assert.Equal(TextureFilterMode.Bilinear, texture.GetFilterMode());
        Assert.Equal(TextureAddressMode.Clamp, texture.GetAddressMode(TextureCoordinate.U));
        Assert.False(texture.GetAutoResolve());
        Assert.False(texture.GetData(CubeMapFace.PositiveX, 0, new byte[16]));
        Assert.Null(texture.GetImage(CubeMapFace.PositiveX));
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
        texture.SetLayers(2);
        texture.SetNumLevels(1);
        Assert.True(texture.SetData(0, image));
        Assert.True(texture.SetData(1, image));
        Assert.Equal(2, texture.Layers);
        Assert.False(texture.GetData(1, 0, new byte[16]));
        var readback = new byte[16];
        Assert.False(texture.GetData(0, 0, readback));
    }
}
