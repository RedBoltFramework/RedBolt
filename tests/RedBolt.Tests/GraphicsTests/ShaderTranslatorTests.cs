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

    [Fact]
    public void Universal_shader_is_compiled_to_spirv()
    {
        var shader = new SpirVShader();
        var defines = new ShaderDefineArray();
        defines.Append("TEST_OFFSET", "1.0");

        ShaderTranslator.ParseUniversalShader(ref shader, ShaderType.VS,
            "layout(location = 0) in vec3 iPos;\nvoid main() { gl_Position = vec4(iPos + TEST_OFFSET, 1.0); }",
            defines, TargetShaderLanguage.Glsl41);

        Assert.True(shader.IsValid(), shader.CompilerOutput);
        Assert.Equal(0x07230203u, shader.Bytecode[0]);
        Assert.Empty(shader.CompilerOutput);
    }

    [Fact]
    public void Invalid_universal_shader_reports_compiler_output()
    {
        var shader = new SpirVShader();

        ShaderTranslator.ParseUniversalShader(ref shader, ShaderType.PS,
            "void main() { this is not valid GLSL; }", new ShaderDefineArray(), TargetShaderLanguage.Glsl41);

        Assert.False(shader.IsValid());
        Assert.NotEmpty(shader.CompilerOutput);
    }

    [Theory]
    [InlineData(TargetShaderLanguage.Glsl41, "#version 410")]
    [InlineData(TargetShaderLanguage.GlslEs30, "#version 300 es")]
    [InlineData(TargetShaderLanguage.Hlsl50, "void vert_main")]
    public void Spirv_shader_is_translated_to_target_language(TargetShaderLanguage language, string expectedText)
    {
        var spirv = new SpirVShader();
        ShaderTranslator.ParseUniversalShader(ref spirv, ShaderType.VS,
            "layout(location = 0) in vec3 iPos;\nvoid main() { gl_Position = vec4(iPos, 1.0); }",
            new ShaderDefineArray(), language);
        Assert.True(spirv.IsValid(), spirv.CompilerOutput);

        var target = new TargetShader();
        ShaderTranslator.TranslateSpirVShader(ref target, spirv, language);

        Assert.True(target.IsValid(), target.CompilerOutput);
        Assert.Equal(language, target.Language);
        Assert.Contains(expectedText, target.SourceCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Hlsl_translation_remaps_vertex_inputs_and_sampler_names()
    {
        var spirv = new SpirVShader();
        ShaderTranslator.ParseUniversalShader(ref spirv, ShaderType.VS,
            "layout(location = 0) in vec3 iPos;\nlayout(binding = 0) uniform sampler2D sDiffMap;\n" +
            "void main() { gl_Position = vec4(iPos + texture(sDiffMap, vec2(0.0)).xyz, 1.0); }",
            new ShaderDefineArray(), TargetShaderLanguage.Hlsl50);
        Assert.True(spirv.IsValid(), spirv.CompilerOutput);

        var target = new TargetShader();
        ShaderTranslator.TranslateSpirVShader(ref target, spirv, TargetShaderLanguage.Hlsl50);

        Assert.True(target.IsValid(), target.CompilerOutput);
        Assert.Contains("ATTRIB0", target.SourceCode, StringComparison.Ordinal);
        Assert.Contains("sDiffMap_sampler", target.SourceCode, StringComparison.Ordinal);
        Assert.DoesNotContain("_sDiffMap_sampler", target.SourceCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Spirv_vertex_attributes_preserve_semantics_indices_and_locations()
    {
        var spirv = new SpirVShader();
        ShaderTranslator.ParseUniversalShader(ref spirv, ShaderType.VS,
            "layout(location = 2) in vec3 iPos;\nlayout(location = 5) in vec2 iTexCoord1;\n" +
            "void main() { gl_Position = vec4(iPos + vec3(iTexCoord1, 0.0), 1.0); }",
            new ShaderDefineArray(), TargetShaderLanguage.Glsl41);
        Assert.True(spirv.IsValid(), spirv.CompilerOutput);

        var attributes = ShaderTranslator.GetVertexAttributesFromSpirV(spirv);

        Assert.Contains(new VertexShaderAttribute(VertexElementSemantic.Position, 0, 2), attributes);
        Assert.Contains(new VertexShaderAttribute(VertexElementSemantic.TexCoord, 1, 5), attributes);
    }
}
