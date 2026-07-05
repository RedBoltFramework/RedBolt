using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShaderFileTests
{
    private readonly HeadlessEngineFixture fixture;

    public ShaderFileTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Repo_backed_rbfx_shader_loads_with_recursive_includes()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var shader = Resources.GetResource<Shader>("LitSolid");

            Assert.Equal("LitSolid", shader.GetShaderName());
            Assert.Contains("#ifndef _MATERIAL_GLSL_", shader.SourceCode, StringComparison.Ordinal);
            Assert.Contains("GetSurfaceColor", shader.SourceCode, StringComparison.Ordinal);
            Assert.True(shader.LastWriteTimeUtc > DateTime.MinValue);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }

    [Fact]
    public void Prepared_shader_source_injects_rbfx_style_engine_macros()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var shader = Resources.GetResource<Shader>("LitSolid");
            var prepared = shader.PrepareSource(
                ShaderStage.Vertex,
                "URHO3D_GEOMETRY_STATIC URHO3D_AMBIENT_PASS DIFFMAP");

            Assert.Contains("#version 410", prepared, StringComparison.Ordinal);
            Assert.Contains("#define COMPILEVS", prepared, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_OPENGL", prepared, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_AMBIENT_PASS", prepared, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_GEOMETRY_STATIC", prepared, StringComparison.Ordinal);
            Assert.Contains("#define DIFFMAP", prepared, StringComparison.Ordinal);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }
}
