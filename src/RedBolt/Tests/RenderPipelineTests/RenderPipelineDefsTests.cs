namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;

public sealed class RenderPipelineDefsTests
{
    [Fact]
    public void Shader_resource_description_matches_native_value_shape()
    {
        using var texture = new Texture2D { Name = "Texture" };
        Assert.True(texture.SetSize(1, 1, TextureFormat.TexFormatRgba8Unorm));
        var rawTexture = Assert.IsAssignableFrom<RawTexture>(texture.GetRawTexture());
        var description = new ShaderResourceDesc
        {
            Name = new StringHash("sDiffMap"),
            Texture = rawTexture
        };

        var copy = description;
        copy.Name = new StringHash("sNormalMap");

        Assert.Equal(new StringHash("sDiffMap"), description.Name);
        Assert.Same(rawTexture, description.Texture);
        Assert.Equal(new StringHash("sNormalMap"), copy.Name);
        Assert.Same(rawTexture, copy.Texture);
    }

    [Fact]
    public void Shader_parameter_description_clones_owned_variant_value()
    {
        var description = new ShaderParameterDesc
        {
            Name = new StringHash("Buffer"),
            Value = new Variant(new byte[] { 1, 2, 3 })
        };
        var copy = description.Clone();

        copy.Value.GetBuffer()[0] = 9;

        Assert.Equal(1, description.Value.GetBuffer()[0]);
        Assert.Equal(9, copy.Value.GetBuffer()[0]);

        copy.Dispose();
        description.Dispose();
    }
}
