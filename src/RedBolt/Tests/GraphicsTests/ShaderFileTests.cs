using RedBolt;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;
using RedBolt.Resource;

namespace RedBolt.Tests;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class ShaderFileTests
{
    private readonly GraphicsEngineFixture _fixture;

    public ShaderFileTests(GraphicsEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Shader_registers_empty_named_factory()
    {
        var context = new Context();
        Shader.RegisterObject(context);

        using var shader = Assert.IsType<Shader>(context.CreateObject(ObjectReflection<Shader>.TypeId));
        Assert.Equal(string.Empty, shader.Name);
    }

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
            Assert.True(shader.TimeStamp > DateTime.MinValue);
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

            Assert.Contains("#define COMPILEVS", prepared, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_OPENGL", prepared, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_AMBIENT_PASS", prepared, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_GEOMETRY_STATIC", prepared, StringComparison.Ordinal);
            Assert.Contains("#define DIFFMAP", prepared, StringComparison.Ordinal);
            Assert.Contains("layout(location = 0) in vec4 iPos;", prepared, StringComparison.Ordinal);
            Assert.Contains("layout(location = 3) in vec2 iTexCoord;", prepared, StringComparison.Ordinal);
            Assert.Contains("layout(location = 6) in vec3 iNormal;", prepared, StringComparison.Ordinal);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }

    [Fact]
    public void Normal_buffer_shader_loads_and_prepares_alpha_mask_support()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var shader = Resources.GetResource<Shader>("M_NormalBuffer");
            var prepared = shader.PrepareSource(
                ShaderStage.Pixel,
                "URHO3D_GEOMETRY_STATIC URHO3D_VERTEX_HAS_NORMAL URHO3D_VERTEX_NORMAL_AVAILABLE URHO3D_TEXTURE_ALBEDO=1 ALPHAMASK");

            Assert.Equal("M_NormalBuffer", shader.GetShaderName());
            Assert.Contains("Surface_SetNormal", shader.SourceCode, StringComparison.Ordinal);
            Assert.Contains("Surface_SetBaseAlbedo", shader.SourceCode, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_SURFACE_NEED_NORMAL", prepared, StringComparison.Ordinal);
            Assert.Contains("#define URHO3D_PIXEL_NEED_TEXCOORD", prepared, StringComparison.Ordinal);
            Assert.Contains("#define ALPHAMASK", prepared, StringComparison.Ordinal);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }

    [Fact]
    public void Shader_reload_caches_normalized_variations_and_indexes_files()
    {
        RbfxTestData.AddResourceDirs(Resources);
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.ShaderFileTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        _fixture.InitializeGraphics(parameters);
        try
        {
            var shader = Resources.GetResource<Shader>("LitSolid");
            var reloadCount = 0;
            shader.OnReloaded += () => ++reloadCount;

            var first = shader.GetVariation(ShaderType.VS, "A B");
            var variationReloadCount = 0;
            first.OnReloaded += () => ++variationReloadCount;

            Assert.True(Resources.ReloadResource(shader));
            Assert.Equal(1, reloadCount);
            Assert.True(variationReloadCount > 0);

            var second = shader.GetVariation(ShaderType.VS, "B A");
            var pixel = shader.GetVariation(ShaderType.PS, "A B");

            Assert.Same(first, second);
            Assert.NotSame(first, pixel);
            Assert.Equal("A B", first.Defines);
            Assert.Equal(shader.GetShaderName(), first.Name);
            Assert.True(shader.TimeStamp > DateTime.MinValue);
            Assert.Contains("Shaders/GLSL/v2/LitSolid.glsl", Shader.GetShaderFileList(), StringComparison.Ordinal);
            Assert.True(shader.GetMemoryUse() > 0);
        }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }
}
