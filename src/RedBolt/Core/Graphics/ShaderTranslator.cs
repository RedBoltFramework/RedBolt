namespace RedBolt.Graphics;

using SystemMath = global::System.Math;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.Glslang;
using RedBolt.Bindings.SpirVCross;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

/// <summary>
/// Shader translated to the target language.
/// </summary>
public struct TargetShader : ICloneable
{
    public TargetShaderLanguage Language;
    public string SourceCode = string.Empty;
    public string CompilerOutput = string.Empty;

    public TargetShader()
    {
    }

    public bool IsValid() => !string.IsNullOrEmpty(SourceCode);

    public static implicit operator bool(TargetShader value) => value.IsValid();

    public readonly object Clone()
    {
        return new TargetShader
        {
            Language = Language,
            SourceCode = SourceCode,
            CompilerOutput = CompilerOutput
        };
    }
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

    /// <summary>Utility to de/initialize glslang library.</summary>
    private readonly struct GlslangGuardian : IDisposable
    {
        public GlslangGuardian()
        {
            GlslangNativeBindings.GlslangInitializeProcess();
        }

        public void Dispose()
        {
            GlslangNativeBindings.GlslangFinalizeProcess();
        }
    }

    private static readonly GlslangGuardian _glslangGuardian = new();

    static ShaderTranslator()
    {
        AppDomain.CurrentDomain.ProcessExit += static (_, _) => _glslangGuardian.Dispose();
    }

    /// <summary>Convert universal GLSL shader to SPIR-V.</summary>
    public static void ParseUniversalShader(ref SpirVShader output, ShaderType shaderType, string sourceCode,
        in ShaderDefineArray shaderDefines, TargetShaderLanguage targetLanguage)
    {
        CompileSpirV(ref output, ConvertShaderType(shaderType), sourceCode, in shaderDefines, targetLanguage);
    }

    private static GlslangStageT ConvertShaderType(ShaderType shaderType)
    {
        return shaderType switch
        {
            ShaderType.VS => GlslangStageT.GlslangStageVertex,
            ShaderType.PS => GlslangStageT.GlslangStageFragment,
            ShaderType.GS => GlslangStageT.GlslangStageGeometry,
            ShaderType.HS => GlslangStageT.GlslangStageTesscontrol,
            ShaderType.DS => GlslangStageT.GlslangStageTessevaluation,
            ShaderType.CS => GlslangStageT.GlslangStageCompute,
            _ => GlslangStageT.GlslangStageVertex
        };
    }

    private static void AppendWithoutVersion(StringBuilder dest, string source)
    {
        var versionTag = FindVersionTag(source);
        if (versionTag is null)
        {
            dest.Append(source);
            return;
        }

        var version = checked((int)versionTag.Value.Start);
        dest.Append(source, 0, version);
        dest.Append("//");
        dest.Append(source, version, source.Length - version);
    }

    /// <summary>Compile SpirV shader.</summary>
    private static void CompileSpirV(ref SpirVShader outputShader, GlslangStageT stage, string sourceCode,
        in ShaderDefineArray shaderDefines, TargetShaderLanguage targetLanguage)
    {
        outputShader.Bytecode.Clear();
        outputShader.CompilerOutput = string.Empty;

        // Prepare defines
        var shaderCode = new StringBuilder("#version 450\n");
        foreach (var define in shaderDefines.Defines)
            shaderCode.Append("#define ").Append(define.Name).Append(' ').Append(define.Value).Append('\n');
        AppendWithoutVersion(shaderCode, sourceCode);

        var source = Marshal.StringToCoTaskMemUTF8(shaderCode.ToString());
        GlslangShaderS? shader = null;
        GlslangProgramS? program = null;
        try
        {
            var envClient = targetLanguage == TargetShaderLanguage.Vulkan10
                ? GlslangClientT.GlslangClientVulkan
                : GlslangClientT.GlslangClientOpengl;
            using var input = new GlslangInputS
            {
                Language = GlslangSourceT.GlslangSourceGlsl,
                Stage = stage,
                Client = envClient,
                ClientVersion = targetLanguage == TargetShaderLanguage.Vulkan10
                    ? GlslangTargetClientVersionT.GlslangTargetVulkan10
                    : GlslangTargetClientVersionT.GlslangTargetOpengl450,
                TargetLanguage = GlslangTargetLanguageT.GlslangTargetSpv,
                TargetLanguageVersion = GlslangTargetLanguageVersionT.GlslangTargetSpv10,
                Code = source,
                DefaultVersion = 450,
                DefaultProfile = GlslangProfileT.GlslangCoreProfile,
                ForceDefaultVersionAndProfile = 0,
                ForwardCompatible = 0,
                Messages = GlslangMessagesT.GlslangMsgDefaultBit,
                Resource = GlslangNativeBindings.GlslangDefaultResource()
            };

            shader = GlslangNativeBindings.GlslangShaderCreate(input);
            GlslangNativeBindings.GlslangShaderSetOptions(shader,
                (int)(GlslangShaderOptionsT.GlslangShaderAutoMapBindings
                    | GlslangShaderOptionsT.GlslangShaderAutoMapLocations));
            if (GlslangNativeBindings.GlslangShaderPreprocess(shader, input) == 0)
            {
                outputShader.CompilerOutput = Marshal.PtrToStringUTF8(
                    GlslangNativeBindings.GlslangShaderGetInfoLog(shader)) ?? string.Empty;
                return;
            }
            input.Code = GlslangNativeBindings.GlslangShaderGetPreprocessedCode(shader);
            if (GlslangNativeBindings.GlslangShaderParse(shader, input) == 0)
            {
                outputShader.CompilerOutput = Marshal.PtrToStringUTF8(
                    GlslangNativeBindings.GlslangShaderGetInfoLog(shader)) ?? string.Empty;
                return;
            }

            program = GlslangNativeBindings.GlslangProgramCreate();
            GlslangNativeBindings.GlslangProgramAddShader(program, shader);
            if (GlslangNativeBindings.GlslangProgramLink(program, (int)GlslangMessagesT.GlslangMsgDefaultBit) == 0
                || GlslangNativeBindings.GlslangProgramMapIo(program) == 0)
            {
                outputShader.CompilerOutput = Marshal.PtrToStringUTF8(
                    GlslangNativeBindings.GlslangProgramGetInfoLog(program)) ?? string.Empty;
                return;
            }

            using var spvOptions = new GlslangSpvOptionsS
            {
                GenerateDebugInfo = true,
                DisableOptimizer = true,
                OptimizeSize = false
            };
            GlslangNativeBindings.GlslangProgramSpirvGenerateWithOptions(program, stage, spvOptions);
            var spirvLog = Marshal.PtrToStringUTF8(
                GlslangNativeBindings.GlslangProgramSpirvGetMessages(program)) ?? string.Empty;
            if (!string.IsNullOrEmpty(spirvLog))
            {
                outputShader.CompilerOutput = spirvLog;
                return;
            }

            var size = checked((int)GlslangNativeBindings.GlslangProgramSpirvGetSize(program));
            var bytecode = GlslangNativeBindings.GlslangProgramSpirvGetPtr(program);
            outputShader.Bytecode.Capacity = SystemMath.Max(outputShader.Bytecode.Capacity, size);
            for (var i = 0; i < size; ++i)
                outputShader.Bytecode.Add(unchecked((uint)Marshal.ReadInt32(bytecode, i * sizeof(uint))));
        }
        finally
        {
            if (program is not null)
                GlslangNativeBindings.GlslangProgramDelete(program);
            if (shader is not null)
                GlslangNativeBindings.GlslangShaderDelete(shader);
            Marshal.FreeCoTaskMem(source);
        }
    }

    /// <summary>Convert SPIR-V shader to target shader language.</summary>
    public static void TranslateSpirVShader(ref TargetShader output, in SpirVShader shader,
        TargetShaderLanguage targetLanguage)
    {
        output.Language = targetLanguage;
        switch (targetLanguage)
        {
            case TargetShaderLanguage.Hlsl50:
                ConvertToHlsl5(ref output, in shader);
                break;

            case TargetShaderLanguage.Glsl41:
                ConvertToGlsl(ref output, in shader, 410, false);
                break;

            case TargetShaderLanguage.GlslEs30:
                ConvertToGlsl(ref output, in shader, 300, true);
                break;

            default:
                System.Diagnostics.Debug.Assert(false, "TODO: Implement");
                break;
        }
    }

    /// <summary>Convert SpirV to HLSL5</summary>
    private static void ConvertToHlsl5(ref TargetShader output, in SpirVShader shader)
    {
        output.SourceCode = string.Empty;
        output.CompilerOutput = string.Empty;

        using var compiler = CreateCompiler(in shader, SpvcBackend.SpvcBackendHlsl, out var context);
        try
        {
            var samplers = new List<string>();
            RemapHlslInputLayout(compiler, context);
            CollectHlslSamplers(compiler, context, samplers);

            CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerCreateCompilerOptions(compiler, out var options), context);
            SetCompilerOption(options, SpvcCompilerOption.SpvcCompilerOptionEmitLineDirectives, true);
            SetCompilerOption(options, SpvcCompilerOption.SpvcCompilerOptionHlslShaderModel, 50u);
            SetCompilerOption(options, SpvcCompilerOption.SpvcCompilerOptionHlslPointSizeCompat, true);
            CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerInstallCompilerOptions(compiler, options), context);
            output.SourceCode = CompileCompiler(compiler, context);
            ReplaceHlslSamplers(ref output.SourceCode, samplers);
            if (string.IsNullOrEmpty(output.SourceCode))
                output.CompilerOutput = "Unknown error";
        }
        finally
        {
            SpirVCrossNativeBindings.SpvcContextDestroy(context);
        }
    }

    private static void RemapHlslInputLayout(SpvcCompilerS compiler, SpvcContextS context)
    {
        if (SpirVCrossNativeBindings.SpvcCompilerGetExecutionModel(compiler)
            != SpvExecutionModel.SpvExecutionModelVertex)
            return;

        CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerCreateShaderResources(compiler, out var resources), context);
        CheckSpvc(SpirVCrossNativeBindings.SpvcResourcesGetResourceListForType(resources,
            SpvcResourceType.SpvcResourceTypeStageInput, out var resourceList, out var resourceCount), context);
        for (nuint i = 0; i < resourceCount; ++i)
        {
            var input = resourceList.GetArrayElement(i);
            System.Diagnostics.Debug.Assert(SpirVCrossNativeBindings.SpvcCompilerHasDecoration(compiler,
                input.Id, SpvDecoration.SpvDecorationLocation) != 0);
            var location = SpirVCrossNativeBindings.SpvcCompilerGetDecoration(compiler,
                input.Id, SpvDecoration.SpvDecorationLocation);
            var semantic = Marshal.StringToCoTaskMemUTF8($"ATTRIB{location}");
            try
            {
                using var remap = new SpvcHlslVertexAttributeRemap
                {
                    Location = location,
                    Semantic = semantic
                };
                CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerHlslAddVertexAttributeRemap(
                    compiler, remap, 1), context);
            }
            finally
            {
                Marshal.FreeCoTaskMem(semantic);
            }
        }
    }

    private static void CollectHlslSamplers(SpvcCompilerS compiler, SpvcContextS context, List<string> samplers)
    {
        CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerCreateShaderResources(compiler, out var resources), context);
        CheckSpvc(SpirVCrossNativeBindings.SpvcResourcesGetResourceListForType(resources,
            SpvcResourceType.SpvcResourceTypeSampledImage, out var resourceList, out var resourceCount), context);
        for (nuint i = 0; i < resourceCount; ++i)
        {
            var resource = resourceList.GetArrayElement(i);
            var type = SpirVCrossNativeBindings.SpvcCompilerGetTypeHandle(compiler, resource.BaseTypeId);
            if (SpirVCrossNativeBindings.SpvcTypeGetBasetype(type) == SpvcBasetype.SpvcBasetypeSampledImage
                && SpirVCrossNativeBindings.SpvcTypeGetImageDimension(type) != SpvDim.SpvDimBuffer)
            {
                var name = Marshal.PtrToStringUTF8(resource.Name) ?? string.Empty;
                samplers.Add("_" + name + "_sampler");
            }
        }
    }

    private static void ReplaceHlslSamplers(ref string hlsl, IReadOnlyList<string> samplers)
    {
        const char placeholder = '\u0001';
        foreach (var name in samplers)
        {
            for (var position = hlsl.IndexOf(name, StringComparison.Ordinal); position >= 0;
                 position = hlsl.IndexOf(name, position, StringComparison.Ordinal))
            {
                hlsl = hlsl.Remove(position, 1).Insert(position, placeholder.ToString());
            }
        }

        hlsl = hlsl.Replace(placeholder.ToString(), string.Empty, StringComparison.Ordinal);
    }

    private static void RemoveClipDistance(ref string shaderCode)
    {
        var index = shaderCode.IndexOf("out float gl_ClipDistance", StringComparison.Ordinal);
        if (index >= 0)
            shaderCode = shaderCode.Insert(index, "// Workaround for GLSL ES:\n// ");
    }

    private static void ConvertToGlsl(ref TargetShader output, in SpirVShader shader, uint version, bool es)
    {
        output.SourceCode = string.Empty;
        output.CompilerOutput = string.Empty;

        using var compiler = CreateCompiler(in shader, SpvcBackend.SpvcBackendGlsl, out var context);
        try
        {
            CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerCreateCompilerOptions(compiler, out var options), context);
            SetCompilerOption(options, SpvcCompilerOption.SpvcCompilerOptionGlslVersion, version);
            SetCompilerOption(options, SpvcCompilerOption.SpvcCompilerOptionGlslEs, es);
            CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerInstallCompilerOptions(compiler, options), context);
            output.SourceCode = CompileCompiler(compiler, context);
            if (string.IsNullOrEmpty(output.SourceCode))
            {
                output.CompilerOutput = "Unknown error";
                return;
            }

            if (es)
                RemoveClipDistance(ref output.SourceCode);
        }
        finally
        {
            SpirVCrossNativeBindings.SpvcContextDestroy(context);
        }
    }

    private static SpvcCompilerS CreateCompiler(in SpirVShader shader, SpvcBackend backend, out SpvcContextS context)
    {
        CheckSpvc(SpirVCrossNativeBindings.SpvcContextCreate(out context), context: null);
        try
        {
            var bytecode = CollectionsMarshal.AsSpan(shader.Bytecode);
            if (bytecode.IsEmpty)
                throw new InvalidOperationException("SPIR-V shader is empty.");
            CheckSpvc(SpirVCrossNativeBindings.SpvcContextParseSpirv(
                context, bytecode, checked((nuint)bytecode.Length), out var parsedIr), context);
            CheckSpvc(SpirVCrossNativeBindings.SpvcContextCreateCompiler(context, backend, parsedIr,
                SpvcCaptureMode.SpvcCaptureModeTakeOwnership, out var compiler), context);
            return compiler;
        }
        catch
        {
            SpirVCrossNativeBindings.SpvcContextDestroy(context);
            throw;
        }
    }

    private static void SetCompilerOption(SpvcCompilerOptionsS options, SpvcCompilerOption option, uint value)
    {
        CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerOptionsSetUint(options, option, value), null);
    }

    private static void SetCompilerOption(SpvcCompilerOptionsS options, SpvcCompilerOption option, bool value)
    {
        CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerOptionsSetBool(options, option, value ? (byte)1 : (byte)0), null);
    }

    private static string CompileCompiler(SpvcCompilerS compiler, SpvcContextS context)
    {
        CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerCompile(compiler, out var source), context);
        return Marshal.PtrToStringUTF8(source) ?? string.Empty;
    }

    private static void CheckSpvc(SpvcResult result, SpvcContextS? context)
    {
        if (result == SpvcResult.SpvcSuccess)
            return;

        var message = context is null
            ? result.ToString()
            : Marshal.PtrToStringUTF8(SpirVCrossNativeBindings.SpvcContextGetLastErrorString(context)) ?? result.ToString();
        throw new InvalidOperationException(message);
    }

    public static List<VertexShaderAttribute> GetVertexAttributesFromSpirV(in SpirVShader shader)
    {
        var result = new List<VertexShaderAttribute>();
        using var compiler = CreateCompiler(in shader, SpvcBackend.SpvcBackendNone, out var context);
        try
        {
            CheckSpvc(SpirVCrossNativeBindings.SpvcCompilerCreateShaderResources(compiler, out var resources), context);
            CheckSpvc(SpirVCrossNativeBindings.SpvcResourcesGetResourceListForType(resources,
                SpvcResourceType.SpvcResourceTypeStageInput, out var resourceList, out var resourceCount), context);
            for (nuint i = 0; i < resourceCount; ++i)
            {
                var input = resourceList.GetArrayElement(i);
                if (SpirVCrossNativeBindings.SpvcCompilerHasDecoration(compiler,
                        input.Id, SpvDecoration.SpvDecorationLocation) == 0)
                    continue;

                var location = SpirVCrossNativeBindings.SpvcCompilerGetDecoration(compiler,
                    input.Id, SpvDecoration.SpvDecorationLocation);
                var name = Marshal.PtrToStringUTF8(input.Name) ?? string.Empty;
                var vertexInput = RenderAPIUtils.ParseVertexAttribute(name);
                if (vertexInput is not null)
                    result.Add(vertexInput.Value with { InputIndex = checked((int)location) });
            }
            return result;
        }
        finally
        {
            SpirVCrossNativeBindings.SpvcContextDestroy(context);
        }
    }
}
