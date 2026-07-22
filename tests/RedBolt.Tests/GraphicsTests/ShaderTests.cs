using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShaderTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ShaderTests(HeadlessEngineFixture fixture)
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
        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        _fixture.RenderDevice.RestoreDeviceState();
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
            _fixture.RenderDevice.InvalidateDeviceState();
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
