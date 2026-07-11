namespace RedBolt;

/// <summary>
/// Shader translated to the target language.
/// </summary>
public struct TargetShader
{
    public TargetShaderLanguage Language;
    public string SourceCode = string.Empty;
    public string CompilerOutput = string.Empty;

    public TargetShader()
    {
    }

    public bool IsValid() => SourceCode.Length != 0;

    public static implicit operator bool(TargetShader value) => value.IsValid();
}

public static class ShaderTranslator
{
    public static (uint Start, uint End)? FindVersionTag(string shaderCode)
    {
        var start = shaderCode.IndexOf("#version", StringComparison.Ordinal);
        if (start < 0)
            return null;

        var end = shaderCode.IndexOf('\n', start);
        return end < 0 ? ((uint)start, (uint)shaderCode.Length) : ((uint)start, (uint)end);
    }

    // TODO: port: the managed API has no glslang translator or SPIR-V-Cross binding.
    // URHO3D_API void ParseUniversalShader(SpirVShader& output, ShaderType shaderType, ea::string_view sourceCode,
    //     const ShaderDefineArray& shaderDefines, TargetShaderLanguage targetLanguage);
    // void ParseUniversalShader(SpirVShader& output, ShaderType shaderType, ea::string_view sourceCode,
    //     const ShaderDefineArray& shaderDefines, TargetShaderLanguage targetLanguage)
    // {
    // #ifdef URHO3D_SHADER_TRANSLATOR
    //     CompileSpirV(output, ConvertShaderType(shaderType), sourceCode, shaderDefines, targetLanguage);
    // #else
    //     URHO3D_ASSERTLOG(0, "URHO3D_SHADER_TRANSLATOR should be enabled to use ParseUniversalShader");
    //
    //     output = {};
    // #endif
    // }

    // TODO: port: the managed API has no glslang translator or SPIR-V-Cross binding.
    // URHO3D_API void TranslateSpirVShader(
    //     TargetShader& output, const SpirVShader& shader, TargetShaderLanguage targetLanguage);
    // void TranslateSpirVShader(TargetShader& output, const SpirVShader& shader, TargetShaderLanguage targetLanguage)
    // {
    // #ifdef URHO3D_SHADER_TRANSLATOR
    //     output.language_ = targetLanguage;
    //     switch (targetLanguage)
    //     {
    //     case TargetShaderLanguage::HLSL_5_0:
    //         ConvertToHLSL5(output, shader);
    //         break;
    //
    //     case TargetShaderLanguage::GLSL_4_1:
    //         ConvertToGLSL(output, shader, 410, false);
    //         break;
    //
    //     case TargetShaderLanguage::GLSL_ES_3_0:
    //         ConvertToGLSL(output, shader, 300, true);
    //         break;
    //
    //     default:
    //         URHO3D_ASSERTLOG(0, "TODO: Implement");
    //     }
    // #else
    //     URHO3D_ASSERTLOG(0, "URHO3D_SHADER_TRANSLATOR should be enabled to use TranslateSpirVShader");
    //
    //     output = {};
    // #endif
    // }

    // TODO: port: the managed API has no SPIR-V-Cross reflection binding.
    // URHO3D_API VertexShaderAttributeVector GetVertexAttributesFromSpirV(const SpirVShader& shader);
    // VertexShaderAttributeVector GetVertexAttributesFromSpirV(const SpirVShader& shader)
    // {
    // #ifdef URHO3D_SHADER_TRANSLATOR
    //     VertexShaderAttributeVector result;
    //
    //     // https://github.com/KhronosGroup/SPIRV-Cross/wiki/Reflection-API-user-guide
    //     spirv_cross::Parser parser{shader.bytecode_};
    //     parser.parse();
    //     spirv_cross::Compiler compiler{ea::move(parser.get_parsed_ir())};
    //
    //     spirv_cross::ShaderResources resources = compiler.get_shader_resources();
    //     for (const auto& input : resources.stage_inputs)
    //     {
    //         if (compiler.has_decoration(input.id, spv::DecorationLocation))
    //         {
    //             const auto location = compiler.get_decoration(input.id, spv::DecorationLocation);
    //             if (const auto vertexInput = ParseVertexAttribute(input.name.c_str()))
    //             {
    //                 result.push_back(*vertexInput);
    //                 result.back().inputIndex_ = location;
    //             }
    //         }
    //     }
    //     return result;
    // #else
    //     URHO3D_ASSERTLOG(0, "URHO3D_SHADER_TRANSLATOR should be enabled to use GetVertexAttributesFromSpirV");
    //     return {};
    // #endif
    // }
}
