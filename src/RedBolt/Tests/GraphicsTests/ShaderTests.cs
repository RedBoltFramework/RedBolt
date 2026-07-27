using RedBolt;

using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
namespace RedBolt.Tests;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;


[Collection(nameof(GraphicsEngineCollection))]
public sealed class ShaderTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ShaderTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
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
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.ShaderTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        try
        {
            var shader = new Shader { Name = "Shaders/GLSL/v2/UnitTest.glsl" };
            var variation = shader.GetVariation(ShaderType.VS, "BAR FOO");
            var alias = shader.GetVariation(ShaderType.VS, "foo bar");

            Assert.Same(variation, alias);

            shader.Dispose();
            shader.Dispose();
        }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
