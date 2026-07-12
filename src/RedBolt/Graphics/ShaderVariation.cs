using System.Text;
using System.Text.RegularExpressions;

namespace RedBolt;

/// <summary>Vertex or pixel shader on the GPU.</summary>
public sealed partial class ShaderVariation : RawShader
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
    public string PrepareSource(GraphicsBackend backend = GraphicsBackend.OpenGL, string extraCode = "")
    {
        return Shader.PrepareSource(GetStage(ShaderType), Defines, backend, extraCode);
    }

    /// <summary>Compile shader from source.</summary>
    public bool CompileFromSource(RenderBackend? backend = null, string extraCode = "")
    {
        var effectiveBackend = backend ?? GetRenderBackend();
        var originalShaderCode = Shader.SourceCode;
        if (string.IsNullOrWhiteSpace(originalShaderCode))
            return false;

        var preparedSource = string.IsNullOrEmpty(extraCode)
            ? PrepareGLSLShaderCode(originalShaderCode)
            : PrepareSource(MapRenderBackend(effectiveBackend), extraCode);
        var processed = ProcessShaderSource(
            out var translatedSource, out _, out var translatedBytecode, preparedSource);
        var loggedSourceShaderName = GetShaderCacheFileIdentifier(GetCachedVariationName("glsl"));
        ShaderSourceLogger.LogShaderSource(loggedSourceShaderName, Defines, translatedSource);

        if (!processed)
            return false;

        var shaderBytecode = new ShaderBytecode(GetShaderType())
        {
            Mime = GetCompiledShaderMime(effectiveBackend),
            Bytecode = translatedBytecode
        };
        if (GetShaderType() == ShaderType.VS)
            shaderBytecode.VertexAttributes.AddRange(ParseVertexAttributes(translatedSource));

        CreateFromBinary(shaderBytecode);
        return GetHandle() is { IsNull: false };
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
        // TODO: port: enable shader cache in Web. Currently it is disabled because of suboptimal persistent storage support.
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
        out string translatedSource, out TargetShader? translatedSpirv, out byte[] translatedBytecode,
        string originalShaderCode)
    {
        translatedSource = originalShaderCode;
        translatedSpirv = null;
        translatedBytecode = Encoding.UTF8.GetBytes(originalShaderCode);

        // TODO: port: the managed API has no glslang translator or SPIR-V-Cross binding.
        // The native implementation translates and optimizes the source according to the target backend.
        _ = NeedShaderTranslation();
        _ = NeedShaderOptimization();
        return true;
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

        var fileIdentifier = new FileIdentifier(cacheDirectory);
        if (string.IsNullOrEmpty(fileIdentifier.Scheme))
            fileIdentifier = new FileIdentifier("file", Path.GetFullPath(fileIdentifier.FileName));
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

    private static IEnumerable<VertexShaderAttribute> ParseVertexAttributes(string source)
    {
        var layoutMatches = VertexInputRegex().Matches(source);
        foreach (Match match in layoutMatches.Count != 0 ? layoutMatches : VertexInputMacroRegex().Matches(source))
        {
            var name = match.Groups["name"].Value;
            if (TryMapVertexAttribute(name, out var attribute))
                yield return attribute;
        }
    }

    private static bool TryMapVertexAttribute(string name, out VertexShaderAttribute attribute)
    {
        attribute = name switch
        {
            "iPos" => new VertexShaderAttribute(VertexElementSemantic.Position, 0, 0),
            "iBlendWeights" => new VertexShaderAttribute(VertexElementSemantic.BlendWeights, 0, 0),
            "iBlendIndices" => new VertexShaderAttribute(VertexElementSemantic.BlendIndices, 0, 0),
            "iTexCoord" => new VertexShaderAttribute(VertexElementSemantic.TexCoord, 0, 0),
            "iTexCoord1" => new VertexShaderAttribute(VertexElementSemantic.TexCoord, 1, 0),
            "iColor" => new VertexShaderAttribute(VertexElementSemantic.Color, 0, 0),
            "iNormal" => new VertexShaderAttribute(VertexElementSemantic.Normal, 0, 0),
            "iTangent" => new VertexShaderAttribute(VertexElementSemantic.Tangent, 0, 0),
            _ => default
        };

        return name is "iPos" or "iBlendWeights" or "iBlendIndices" or "iTexCoord" or "iTexCoord1" or "iColor" or "iNormal" or "iTangent";
    }

    private static string GetCompiledShaderMime(RenderBackend backend) => backend switch
    {
        RenderBackend.D3D11 or RenderBackend.D3D12 => "application/hlsl-bin",
        RenderBackend.Vulkan => "application/spirv",
        _ => "application/glsl"
    };

    private static GraphicsBackend MapRenderBackend(RenderBackend backend) => backend switch
    {
        RenderBackend.D3D11 => GraphicsBackend.D3D11,
        RenderBackend.D3D12 => GraphicsBackend.D3D12,
        RenderBackend.Vulkan => GraphicsBackend.Vulkan,
        _ => GraphicsBackend.OpenGL
    };

    [GeneratedRegex(@"layout\s*\(\s*location\s*=\s*\d+\s*\)\s*in\s+[A-Za-z0-9_]+\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex VertexInputRegex();

    [GeneratedRegex(@"VERTEX_INPUT(?:_HIGHP)?\s*\(\s*(?:[A-Za-z_][A-Za-z0-9_]*\s+)+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex VertexInputMacroRegex();
}
