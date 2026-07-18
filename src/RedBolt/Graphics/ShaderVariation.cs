using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Vertex or pixel shader on the GPU.</summary>
public sealed class ShaderVariation : RawShader
{

    private readonly Graphics? _graphics;

    /// <summary>Source shader.</summary>
    public Shader Shader { get; }

    /// <summary>Defines to use when compiling the shader.</summary>
    public string Defines { get; }

    /// <summary>Shader stage.</summary>
    public ShaderType ShaderType => GetShaderType();

    public ShaderVariation(Shader shader, ShaderType shaderType, string defines = "")
        : base(shaderType)
    {
        Shader = shader ?? throw new ArgumentNullException(nameof(shader));
        Defines = defines ?? string.Empty;
        _graphics = GetSubsystem<Graphics>();
        SetDebugName(GetShaderVariationName());
        Shader.OnReloaded += OnReloaded;
        Create();
    }

    /// <summary>Return shader name (as used in resources).</summary>
    public string Name => Shader.GetShaderName();

    /// <summary>Return full shader variation name with defines.</summary>
    public string GetShaderVariationName() => $"{Name}({Defines})";

    /// <summary>Prepare shader source for the managed renderer.</summary>
    public string PrepareSource(RenderBackend backend = RenderBackend.OpenGL, string extraCode = "")
    {
        return Shader.PrepareSource(GetStage(ShaderType), Defines, backend, extraCode);
    }

    private bool CompileFromSource()
    {
        var originalShaderCode = Shader.SourceCode;
        var preparedSource = PrepareGLSLShaderCode(originalShaderCode);
        var processed = ProcessShaderSource(
            out var translatedSource, out var translatedSpirv, out var translatedBytecode, preparedSource);
        var loggedSourceShaderName = GetShaderCacheFileIdentifier(GetCachedVariationName("glsl"));
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
                Log.Logger.LogInformation("Shader files:\n{Files}", global::RedBolt.Shader.GetShaderFileList());
            return false;
        }

        return true;
    }

    private void OnReloaded()
    {
        Create();
    }

    private bool Create()
    {
        Destroy();

        if (_graphics is null)
            return false;

        var binaryShaderName = GetShaderCacheFileIdentifier(GetCachedVariationName("bytecode"));
        if (!LoadByteCode(binaryShaderName))
        {
            if (!CompileFromSource())
            {
                CreateFromBinary(new ShaderBytecode(GetShaderType()));
                return false;
            }

            var engine = GetSubsystem<Engine>();
            if (engine?.Parameters.SaveShaderCache == true && Shader.TimeStamp > DateTime.MinValue)
                SaveByteCode(binaryShaderName);
        }

        return true;
    }

    private bool LoadByteCode(FileIdentifier binaryShaderName)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        if (virtualFileSystem is null || binaryShaderName.IsEmpty() || !virtualFileSystem.Exists(binaryShaderName))
            return false;

        var sourceTimeStamp = GetUnixTimeStamp(Shader.TimeStamp);
        if (sourceTimeStamp != 0)
        {
            var bytecodeTimeStamp = virtualFileSystem.GetLastModifiedTime(binaryShaderName, false);
            if (bytecodeTimeStamp != 0 && bytecodeTimeStamp < sourceTimeStamp)
                return false;
        }

        var file = virtualFileSystem.OpenFile(binaryShaderName, File.FileMode.Read);
        if (file is null)
            return false;

        try
        {
            var data = new byte[checked((int)file.Size)];
            if (file.Read(data) != (uint)data.Length)
                return false;

            using var stream = new MemoryStream(data, writable: false);
            var bytecode = new ShaderBytecode();
            if (!bytecode.LoadFromFile(stream) || bytecode.Mime != GetCompiledShaderMime(GetRenderBackend()))
                return false;

            CreateFromBinary(bytecode);
            return GetHandle() is { IsNull: false };
        }
        finally
        {
            file.Close();
        }
    }

    private void SaveByteCode(FileIdentifier binaryShaderName)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        if (virtualFileSystem is null || binaryShaderName.IsEmpty())
            return;

        var file = virtualFileSystem.OpenFile(binaryShaderName, File.FileMode.Write);
        if (file is null)
            return;

        try
        {
            using var stream = new MemoryStream();
            if (!GetBytecode().SaveToFile(stream))
                return;

            _ = file.Write(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
        }
        finally
        {
            file.Close();
        }
    }

    private string GetCachedVariationName(string extension)
    {
        var backendName = RenderAPIUtils.ToString(GetRenderBackend()).ToLowerInvariant();
        var shortName = Shader.GetShaderName();
        var shaderTypeName = RenderAPIUtils.ToString(GetShaderType()).ToLowerInvariant();
        var definesHash = new StringHash(Defines);
        return $"{shortName}_{shaderTypeName}_{definesHash}.{backendName}.{extension}";
    }

    private bool NeedShaderTranslation()
    {
        return GetShaderTranslationPolicy() != ShaderTranslationPolicy.Verbatim;
    }

    private bool NeedShaderOptimization()
    {
        return GetShaderTranslationPolicy() == ShaderTranslationPolicy.Optimize;
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
                Log.Logger.LogError("Failed to convert shader {Shader} from GLSL to SPIR-V:\n{Files}{Output}",
                    GetShaderVariationName(), global::RedBolt.Shader.GetShaderFileList(), spirvShader.CompilerOutput);
                return false;
            }

            translatedSpirv = spirvShader;

            if (needShaderOptimization)
            {
                var optimizerOutput = string.Empty;
                if (!ShaderOptimizer.OptimizeSpirVShader(ref spirvShader, ref optimizerOutput, targetShaderLanguage))
                {
                    Log.Logger.LogError("Failed to optimize SPIR-V shader {Shader}:\n{Output}",
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
                    Log.Logger.LogError("Failed to convert shader {Shader} from SPIR-V to HLSL:\n{Files}{Output}",
                        GetShaderVariationName(), global::RedBolt.Shader.GetShaderFileList(), targetShader.CompilerOutput);
                    return false;
                }

                translatedSource = targetShader.SourceCode;
                if (renderBackend is RenderBackend.D3D11 or RenderBackend.D3D12)
                {
                    // On D3D backends, compile the translated source code
                    if (!ShaderCompiler.CompileHLSLToBinary(out var hlslBytecode, out var compilerOutput,
                            targetShader.SourceCode, GetShaderType()))
                    {
                        Log.Logger.LogError("Failed to compile HLSL shader {Shader}:\n{Files}{Output}",
                            GetShaderVariationName(), global::RedBolt.Shader.GetShaderFileList(), compilerOutput);
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

    private ShaderTranslationPolicy GetShaderTranslationPolicy()
    {
        var requestedPolicy = GetSubsystem<Engine>()?.Parameters.ShaderPolicy;
        var parsedPolicy = requestedPolicy switch
        {
            nameof(ShaderTranslationPolicy.Translate) => ShaderTranslationPolicy.Translate,
            nameof(ShaderTranslationPolicy.Optimize) => ShaderTranslationPolicy.Optimize,
            nameof(ShaderTranslationPolicy.Verbatim) => ShaderTranslationPolicy.Verbatim,
            _ => (ShaderTranslationPolicy?)null
        };
        return RenderAPIUtils.SelectShaderTranslationPolicy(GetRenderBackend(), parsedPolicy);
    }

    private RenderBackend GetRenderBackend()
    {
        return GetSubsystem<RenderDevice>()?.GetBackend() ?? RenderBackend.OpenGL;
    }

    private string GetShaderCacheDirectory()
    {
        return GetSubsystem<Engine>()?.Parameters.ShaderCacheDir ?? string.Empty;
    }

    private FileIdentifier GetShaderCacheFileIdentifier(string variationName)
    {
        var cacheDirectory = GetShaderCacheDirectory();
        if (string.IsNullOrEmpty(cacheDirectory))
            return FileIdentifier.Empty;

        var fileIdentifier = FileIdentifier.FromUri(cacheDirectory);
        fileIdentifier.AppendPath(variationName);
        return fileIdentifier;
    }

    private static uint GetUnixTimeStamp(DateTime timeStamp)
    {
        if (timeStamp <= DateTime.MinValue)
            return 0;

        var seconds = new DateTimeOffset(timeStamp.ToUniversalTime()).ToUnixTimeSeconds();
        return seconds > 0 ? unchecked((uint)seconds) : 0;
    }

    private static ShaderStage GetStage(ShaderType shaderType)
    {
        return shaderType switch
        {
            ShaderType.VS => ShaderStage.Vertex,
            ShaderType.PS => ShaderStage.Pixel,
            _ => throw new NotSupportedException($"Shader stage '{shaderType}' is not supported by the current managed shader variation pipeline.")
        };
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
