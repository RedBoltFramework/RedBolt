using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShaderTests
{
    public ShaderTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Shader_type_matches_cpp_reference_category()
    {
        Assert.False(typeof(Shader).IsValueType);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Shader)));
    }

    [Fact]
    public void Dispose_releases_aliased_variations_once()
    {
        var shader = new Shader("Shaders/GLSL/v2/UnitTest.glsl");
        var variation = shader.GetVariation(ShaderType.VS, "BAR FOO");
        var alias = shader.GetVariation(ShaderType.VS, "foo bar");

        Assert.Same(variation, alias);

        shader.Dispose();
        shader.Dispose();
    }
}
