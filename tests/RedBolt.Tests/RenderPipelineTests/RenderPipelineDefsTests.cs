using RedBolt;

namespace RedBolt.Tests;

public sealed class RenderPipelineDefsTests
{
    [Fact]
    public void Shader_resource_description_matches_native_value_shape()
    {
        var texture = new Texture2D("Texture");
        var description = new ShaderResourceDesc
        {
            Name = new StringHash("sDiffMap"),
            Texture = texture
        };

        var copy = description;
        copy.Name = new StringHash("sNormalMap");

        Assert.Equal(new StringHash("sDiffMap"), description.Name);
        Assert.Same(texture, description.Texture);
        Assert.Equal(new StringHash("sNormalMap"), copy.Name);
        Assert.Same(texture, copy.Texture);
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
