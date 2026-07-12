using RedBolt;

namespace RedBolt.Tests;

public sealed class TextureTests
{
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
}
