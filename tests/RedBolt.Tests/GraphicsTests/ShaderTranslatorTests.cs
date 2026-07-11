namespace RedBolt.Tests;

public sealed class ShaderTranslatorTests
{
    [Fact]
    public void Shader_version_tag_returns_line_range()
    {
        Assert.Equal((3u, 15u), ShaderTranslator.FindVersionTag("//\n#version 410\nvoid main()"));
        Assert.Equal((0u, 12u), ShaderTranslator.FindVersionTag("#version 410"));
        Assert.Null(ShaderTranslator.FindVersionTag("void main()"));
    }

    [Fact]
    public void Shader_data_types_report_validity()
    {
        var spirVShader = new SpirVShader();
        var targetShader = new TargetShader();

        Assert.False(spirVShader.IsValid());
        Assert.False(targetShader.IsValid());

        spirVShader.Bytecode.Add(1);
        targetShader.SourceCode = "void main() {}";

        Assert.True(spirVShader.IsValid());
        Assert.True(targetShader.IsValid());
    }
}
