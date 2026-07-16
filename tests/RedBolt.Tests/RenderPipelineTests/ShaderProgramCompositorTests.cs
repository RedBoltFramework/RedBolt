namespace RedBolt.Tests;

public sealed class ShaderProgramCompositorTests
{
    [Fact]
    public void Shader_program_desc_clear_matches_cpp_defaults()
    {
        var desc = new ShaderProgramDesc();
        desc.ShaderName[(int)ShaderType.VS] = "v2/Test";
        desc.AddShaderDefines(ShaderType.VS, "TEST");
        desc.AddCommonShaderDefines("COMMON");
        desc.IsInstancingUsed = true;

        desc.Clear();

        Assert.All(desc.ShaderName, Assert.Empty);
        Assert.All(desc.ShaderDefines, Assert.Empty);
        Assert.Empty(desc.CommonShaderDefines);
        Assert.False(desc.IsInstancingUsed);
    }

    [Fact]
    public void Shader_program_desc_appends_cpp_define_separators()
    {
        var desc = new ShaderProgramDesc();

        desc.AddCommonShaderDefines("FIRST");
        desc.AddCommonShaderDefines("SECOND ");
        desc.AddShaderDefines(ShaderType.VS, "VERTEX");
        desc.AddShaderDefines(ShaderType.VS, "MORE ");

        Assert.Equal("FIRST SECOND ", desc.CommonShaderDefines);
        Assert.Equal("VERTEX MORE ", desc.ShaderDefines[(int)ShaderType.VS]);
        Assert.Empty(desc.ShaderDefines[(int)ShaderType.PS]);
    }
}
