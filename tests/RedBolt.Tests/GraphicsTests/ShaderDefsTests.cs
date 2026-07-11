namespace RedBolt.Tests;

public sealed class ShaderDefsTests
{
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
