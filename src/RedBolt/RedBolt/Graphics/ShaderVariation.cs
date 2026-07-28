namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;

/// <summary>Vertex or pixel shader on the GPU.</summary>
public sealed class ShaderVariation : RawShader
{
    /// <summary>Cached pointer to Graphics subsystem.</summary>
    private readonly Graphics _graphics;
    /// <summary>Source shader.</summary>
    private readonly Shader _owner;
    /// <summary>Defines to use when compiling the shader.</summary>
    private readonly string _defines;

    /// <summary>Defines to use when compiling the shader.</summary>
    public string Defines => _defines;

    public ShaderVariation(Shader shader, ShaderType shaderType, string defines)
        : base(shaderType)
    {
        _graphics = GetSubsystem<Graphics>()!;
        _owner = shader;
        _defines = defines;
        SetDebugName(GetShaderVariationName());
        Create();
        _owner.OnReloaded += OnReloaded;
    }

    /// <summary>Return shader name (as used in resources).</summary>
    public string Name => _owner.GetShaderName();

    /// <summary>Return full shader variation name with defines.</summary>
    public string GetShaderVariationName() => $"{Name}({Defines})";

    private bool CompileFromSource()
    {
        var originalShaderCode = _owner.SourceCode;
        var preparedSource = PrepareGLSLShaderCode(originalShaderCode);
        var processed = ProcessShaderSource(
            out var translatedSource, out var translatedSpirv, out var translatedBytecode, preparedSource);
        var loggedSourceShaderName = _graphics!.GetSettings().ShaderCacheDir + GetCachedVariationName("glsl");
        ShaderSourceLogger.LogShaderSource(loggedSourceShaderName, Defines, translatedSource);

        if (!processed)
            return false;

        var renderBackend = GetRenderBackend();
        var shaderBytecode = new ShaderBytecode(GetShaderType())
        {
            Mime = GetCompiledShaderMime(renderBackend),
            Bytecode = translatedBytecode
        };
        if (translatedSpirv.HasValue && GetShaderType() == ShaderType.VS)
            shaderBytecode.VertexAttributes.AddRange(
                ShaderTranslator.GetVertexAttributesFromSpirV(translatedSpirv.Value));

        CreateFromBinary(shaderBytecode);
        if (GetHandle() is not { IsNull: false })
        {
            if (renderBackend == RenderBackend.OpenGL)
                Log.GetLogger().LogInformation("Shader files:\n{Files}", global::RedBolt.Graphics.Shader.GetShaderFileList());
            return false;
        }

        return true;
    }

    private new void OnReloaded()
    {
        Create();
    }

    private bool Create()
    {
        Destroy();

        if (_graphics is null)
            return false;

        ref readonly var settings = ref _graphics.GetSettings();
        var binaryShaderName = settings.ShaderCacheDir + GetCachedVariationName("bytecode");
        if (!LoadByteCode(binaryShaderName))
        {
            if (!CompileFromSource())
            {
                CreateFromBinary(new ShaderBytecode(GetShaderType()));
                return false;
            }

            if (settings.CacheShaders && _owner.TimeStamp > DateTime.MinValue)
                SaveByteCode(in binaryShaderName);
        }

        return true;
    }

    private bool LoadByteCode(in FileIdentifier binaryShaderName)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        if (virtualFileSystem is null || binaryShaderName.IsEmpty() || !virtualFileSystem.Exists(binaryShaderName))
            return false;

        var sourceTimeStamp = GetUnixTimeStamp(_owner.TimeStamp);
        if (sourceTimeStamp != 0)
        {
            var bytecodeTimeStamp = virtualFileSystem.GetLastModifiedTime(binaryShaderName, false);
            if (bytecodeTimeStamp != 0 && bytecodeTimeStamp < sourceTimeStamp)
                return false;
        }

        var file = virtualFileSystem.OpenFile(binaryShaderName, FileMode.Read);
        if (file is null)
            return false;

        try
        {
            var bytecode = new ShaderBytecode();
            if (!bytecode.LoadFromFile(file) || bytecode.Mime != GetCompiledShaderMime(GetRenderBackend()))
                return false;

            CreateFromBinary(bytecode);
            return GetHandle() is { IsNull: false };
        }
        finally
        {
            file.Close();
        }
    }

    private void SaveByteCode(in FileIdentifier binaryShaderName)
    {
        // TODO: Enable shader cache in Web. Currently it is disabled because of suboptimal persistent storage support.
        if (ProcessUtils.GetPlatform() == PlatformId.Web)
            return;

        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        if (virtualFileSystem is null || binaryShaderName.IsEmpty())
            return;

        var file = virtualFileSystem.OpenFile(binaryShaderName, FileMode.Write);
        if (file is null)
            return;

        try
        {
            _ = GetBytecode().SaveToFile(file);
        }
        finally
        {
            file.Close();
        }
    }

    private string GetCachedVariationName(string extension)
    {
        var backendName = RenderAPIUtils.ToString(GetRenderBackend()).ToLowerInvariant();
        var shortName = _owner.GetShaderName();
        var shaderTypeName = RenderAPIUtils.ToString(GetShaderType()).ToLowerInvariant();
        var definesHash = new StringHash(Defines);
        return $"{shortName}_{shaderTypeName}_{definesHash}.{backendName}.{extension}";
    }

    private bool NeedShaderTranslation()
    {
        return _graphics!.GetSettings().ShaderTranslationPolicy != ShaderTranslationPolicy.Verbatim;
    }

    private bool NeedShaderOptimization()
    {
        return _graphics!.GetSettings().ShaderTranslationPolicy == ShaderTranslationPolicy.Optimize;
    }

    private string PrepareGLSLShaderCode(string originalShaderCode)
    {
        var shaderCode = new StringBuilder();
        var renderBackend = GetRenderBackend();
        var skipVersionTag = NeedShaderTranslation();
        var versionTag = ShaderTranslator.FindVersionTag(originalShaderCode);
        if (!skipVersionTag)
        {
            if (versionTag.HasValue)
            {
                var versionDefine = originalShaderCode[(int)versionTag.Value.Start..(int)versionTag.Value.End];
                shaderCode.Append(versionDefine).Append('\n');
            }
            else
            {
                var isOpenGLES = RenderAPIUtils.IsOpenGLESBackend(renderBackend);
                var isCompute = GetShaderType() == ShaderType.CS;
                shaderCode.Append(isOpenGLES
                    ? isCompute ? "#version 310 es\n" : "#version 300 es\n"
                    : isCompute ? "#version 430\n" : "#version 410\n");
            }
        }

        shaderCode.Append(GetShaderTypeDefine()).AppendLine();
        shaderCode.Append("#define URHO3D_").Append(RenderAPIUtils.ToString(renderBackend).ToUpperInvariant()).AppendLine();
        foreach (var define in Defines.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            shaderCode.Append("#define ").Append(define.Replace('=', ' ')).Append(" \n");

        if (!versionTag.HasValue)
            shaderCode.Append(originalShaderCode);
        else
        {
            shaderCode.Append(originalShaderCode[..(int)versionTag.Value.Start]);
            shaderCode.Append("//");
            shaderCode.Append(originalShaderCode[(int)versionTag.Value.Start..]);
        }

        return shaderCode.ToString();
    }

    private bool ProcessShaderSource(
        out string translatedSource, out SpirVShader? translatedSpirv, out byte[] translatedBytecode,
        string originalShaderCode)
    {
        translatedSource = originalShaderCode;
        translatedSpirv = null;
        translatedBytecode = Encoding.UTF8.GetBytes(originalShaderCode);

        var renderBackend = GetRenderBackend();
        var targetShaderLanguage = GetTargetShaderLanguage(renderBackend);
        var needShaderTranslation = NeedShaderTranslation();
        var needShaderOptimization = NeedShaderOptimization();

        if (needShaderTranslation)
        {
            var spirvShader = new SpirVShader();
            ShaderTranslator.ParseUniversalShader(ref spirvShader, GetShaderType(), originalShaderCode,
                new ShaderDefineArray(), targetShaderLanguage);
            if (!spirvShader)
            {
                Log.GetLogger().LogError("Failed to convert shader {Shader} from GLSL to SPIR-V:\n{Files}{Output}",
                    GetShaderVariationName(), global::RedBolt.Graphics.Shader.GetShaderFileList(), spirvShader.CompilerOutput);
                return false;
            }

            translatedSpirv = spirvShader;

            if (needShaderOptimization)
            {
                var optimizerOutput = string.Empty;
                if (!ShaderOptimizer.OptimizeSpirVShader(ref spirvShader, ref optimizerOutput, targetShaderLanguage))
                {
                    Log.GetLogger().LogError("Failed to optimize SPIR-V shader {Shader}:\n{Output}",
                        GetShaderVariationName(), optimizerOutput);
                    return false;
                }

                translatedSpirv = spirvShader;
            }

            // Vulkan uses SPIRV directly
            if (targetShaderLanguage == TargetShaderLanguage.Vulkan10)
            {
                translatedBytecode = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(spirvShader.Bytecode)).ToArray();
            }
            else
            {
                // Translate to target language
                var targetShader = new TargetShader();
                ShaderTranslator.TranslateSpirVShader(ref targetShader, spirvShader, targetShaderLanguage);
                if (!targetShader)
                {
                    Log.GetLogger().LogError("Failed to convert shader {Shader} from SPIR-V to HLSL:\n{Files}{Output}",
                        GetShaderVariationName(), global::RedBolt.Graphics.Shader.GetShaderFileList(), targetShader.CompilerOutput);
                    return false;
                }

                translatedSource = targetShader.SourceCode;
                if (renderBackend is RenderBackend.D3D11 or RenderBackend.D3D12)
                {
                    // On D3D backends, compile the translated source code
                    if (!ShaderCompiler.CompileHLSLToBinary(out var hlslBytecode, out var compilerOutput,
                            targetShader.SourceCode, GetShaderType()))
                    {
                        Log.GetLogger().LogError("Failed to compile HLSL shader {Shader}:\n{Files}{Output}",
                            GetShaderVariationName(), global::RedBolt.Graphics.Shader.GetShaderFileList(), compilerOutput);
                        return false;
                    }

                    translatedBytecode = hlslBytecode;
                }
                else
                {
                    // On OpenGL backends, just store the translated source code
                    translatedBytecode = Encoding.UTF8.GetBytes(targetShader.SourceCode);
                }
            }
        }

        return true;
    }

    private static TargetShaderLanguage GetTargetShaderLanguage(RenderBackend renderBackend)
    {
        switch (renderBackend)
        {
            case RenderBackend.D3D11:
            case RenderBackend.D3D12:
                return TargetShaderLanguage.Hlsl50;
            case RenderBackend.Vulkan:
                return TargetShaderLanguage.Vulkan10;
            case RenderBackend.OpenGL:
                return RenderAPIUtils.IsOpenGLESBackend(renderBackend)
                    ? TargetShaderLanguage.GlslEs30
                    : TargetShaderLanguage.Glsl41;
            default:
                System.Diagnostics.Debug.Assert(false);
                return TargetShaderLanguage.Vulkan10;
        }
    }

    private RenderBackend GetRenderBackend()
    {
        return GetSubsystem<RenderDevice>()?.GetBackend() ?? RenderBackend.OpenGL;
    }

    private static uint GetUnixTimeStamp(DateTime timeStamp)
    {
        if (timeStamp <= DateTime.MinValue)
            return 0;

        var seconds = new DateTimeOffset(timeStamp.ToUniversalTime()).ToUnixTimeSeconds();
        return seconds > 0 ? unchecked((uint)seconds) : 0;
    }

    private string GetShaderTypeDefine()
    {
        return GetShaderType() switch
        {
            ShaderType.VS => "#define COMPILEVS",
            ShaderType.PS => "#define COMPILEPS",
            ShaderType.GS => "#define COMPILEGS",
            ShaderType.HS => "#define COMPILEHS",
            ShaderType.DS => "#define COMPILEDS",
            ShaderType.CS => "#define COMPILECS",
            _ => throw new ArgumentOutOfRangeException(nameof(ShaderType))
        };
    }

    private static string GetCompiledShaderMime(RenderBackend backend) => backend switch
    {
        RenderBackend.D3D11 or RenderBackend.D3D12 => "application/hlsl-bin",
        RenderBackend.Vulkan => "application/spirv",
        _ => "application/glsl"
    };

}
