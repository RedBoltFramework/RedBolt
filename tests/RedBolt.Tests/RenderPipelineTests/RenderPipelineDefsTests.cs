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
}
