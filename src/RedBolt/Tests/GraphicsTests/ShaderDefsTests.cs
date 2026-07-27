using RedBolt.Graphics;
namespace RedBolt.Tests;

public sealed class ShaderDefsTests
{
    [Fact]
    public void Class_kinds_and_copies_match_cpp_value_types()
    {
        Assert.True(typeof(SpirVShader).IsValueType);
        Assert.True(typeof(ShaderDefineArray).IsValueType);

        var shader = new SpirVShader
        {
            Bytecode = [1, 2],
            CompilerOutput = "output"
        };
        var shaderCopy = (SpirVShader)shader.Clone();
        shaderCopy.Bytecode.Add(3);

        Assert.Equal(new uint[] { 1, 2 }, shader.Bytecode);
        Assert.Equal(new uint[] { 1, 2, 3 }, shaderCopy.Bytecode);
        Assert.Equal(shader.CompilerOutput, shaderCopy.CompilerOutput);

        var defines = new ShaderDefineArray("ALPHA=1");
        var definesCopy = (ShaderDefineArray)defines.Clone();
        definesCopy.Append("BETA", "2");

        Assert.Single(defines.Defines);
        Assert.Equal(2u, definesCopy.Size());
    }

    [Fact]
    public void Shader_define_array_parses_and_appends_defines()
    {
        var defines = new ShaderDefineArray("ALPHA BETA=2");

        defines.Append("GAMMA");
        defines.Append("DELTA", "4");

        Assert.Equal(4u, defines.Size());
        Assert.Equal(("ALPHA", "1"), defines.Defines[0]);
        Assert.Equal(("BETA", "2"), defines.Defines[1]);
        Assert.Equal(("GAMMA", "1"), defines.Defines[2]);
        Assert.Equal(("DELTA", "4"), defines.Defines[3]);
    }

    [Fact]
    public void Shader_define_array_finds_unused_names()
    {
        var defines = new ShaderDefineArray("USED UNUSED");

        var unused = defines.FindUnused("shader uses USED");

        Assert.Equal(new[] { "UNUSED" }, unused);
    }
}
