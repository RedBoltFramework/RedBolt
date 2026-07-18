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
    public void Class_kinds_and_borrowed_parameters_match_cpp_types()
    {
        Assert.True(typeof(TargetShader).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(TargetShader)));
        Assert.False(default(TargetShader).IsValid());

        var original = new TargetShader
        {
            Language = TargetShaderLanguage.Glsl41,
            SourceCode = "source",
            CompilerOutput = "output"
        };
        var clone = (TargetShader)((ICloneable)original).Clone();
        clone.SourceCode = "changed";
        Assert.Equal("source", original.SourceCode);
        Assert.Equal(original.Language, clone.Language);
        Assert.Equal(original.CompilerOutput, clone.CompilerOutput);

        var guardian = typeof(ShaderTranslator).GetNestedType(
            "GlslangGuardian", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(guardian);
        Assert.True(guardian.IsValueType);
        Assert.True(typeof(IDisposable).IsAssignableFrom(guardian));

        var parse = typeof(ShaderTranslator).GetMethod(nameof(ShaderTranslator.ParseUniversalShader));
        Assert.NotNull(parse);
        Assert.True(parse.GetParameters()[3].IsIn);
        var translate = typeof(ShaderTranslator).GetMethod(nameof(ShaderTranslator.TranslateSpirVShader));
        Assert.NotNull(translate);
        Assert.True(translate.GetParameters()[1].IsIn);
        var attributes = typeof(ShaderTranslator).GetMethod(nameof(ShaderTranslator.GetVertexAttributesFromSpirV));
        Assert.NotNull(attributes);
        Assert.True(attributes.GetParameters()[0].IsIn);
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

        var optimizerOutput = string.Empty;
        Assert.True(ShaderOptimizer.OptimizeSpirVShader(
            ref spirv, ref optimizerOutput, TargetShaderLanguage.Glsl41), optimizerOutput);

        var attributes = ShaderTranslator.GetVertexAttributesFromSpirV(spirv);

        Assert.Contains(new VertexShaderAttribute(VertexElementSemantic.Position, 0, 2), attributes);
        Assert.Contains(new VertexShaderAttribute(VertexElementSemantic.TexCoord, 1, 5), attributes);
    }

    [Theory]
    [InlineData(TargetShaderLanguage.Glsl41)]
    [InlineData(TargetShaderLanguage.Vulkan10)]
    public void Valid_spirv_shader_is_optimized(TargetShaderLanguage language)
    {
        var shader = new SpirVShader();
        ShaderTranslator.ParseUniversalShader(ref shader, ShaderType.VS,
            "layout(location = 0) in vec3 iPos;\nvoid main() { gl_Position = vec4(iPos, 1.0); }",
            new ShaderDefineArray(), language);
        Assert.True(shader.IsValid(), shader.CompilerOutput);
        var originalBytecode = shader.Bytecode;
        var optimizerOutput = string.Empty;

        var result = ShaderOptimizer.OptimizeSpirVShader(ref shader, ref optimizerOutput, language);

        Assert.True(result, optimizerOutput);
        Assert.True(shader.IsValid());
        Assert.NotSame(originalBytecode, shader.Bytecode);
        Assert.Equal(0x07230203u, shader.Bytecode[0]);
    }

    [Fact]
    public void Non_vulkan_optimizer_failure_preserves_shader()
    {
        var originalBytecode = new List<uint> { 1, 2, 3 };
        var shader = new SpirVShader { Bytecode = new List<uint>(originalBytecode) };
        var optimizerOutput = string.Empty;

        var result = ShaderOptimizer.OptimizeSpirVShader(
            ref shader, ref optimizerOutput, TargetShaderLanguage.Glsl41);

        Assert.True(result);
        Assert.Equal(originalBytecode, shader.Bytecode);
        Assert.Contains("Failed to optimize SPIR-V shader:", optimizerOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Vulkan_optimizer_failure_is_fatal_and_preserves_shader()
    {
        var originalBytecode = new List<uint> { 1, 2, 3 };
        var shader = new SpirVShader { Bytecode = new List<uint>(originalBytecode) };
        var optimizerOutput = string.Empty;

        var result = ShaderOptimizer.OptimizeSpirVShader(
            ref shader, ref optimizerOutput, TargetShaderLanguage.Vulkan10);

        Assert.False(result);
        Assert.Equal(originalBytecode, shader.Bytecode);
        Assert.Contains("Failed to optimize SPIR-V shader:", optimizerOutput, StringComparison.Ordinal);
    }
}
