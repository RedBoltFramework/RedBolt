using System.Text;
using System.Text.RegularExpressions;

namespace RedBolt;

public sealed partial class ShaderVariation : RawShader
{
    public ShaderVariation(Shader shader, ShaderType shaderType, string defines = "")
        : base(shaderType)
    {
        Shader = shader ?? throw new ArgumentNullException(nameof(shader));
        Defines = defines ?? string.Empty;
        SetDebugName(GetShaderVariationName());

        if (!string.IsNullOrWhiteSpace(shader.SourceCode))
            CompileFromSource();
    }

    public Shader Shader { get; }

    public ShaderType ShaderType => GetShaderType();

    public string Defines { get; }

    public string Name => Shader.GetShaderName();

    public string GetShaderVariationName() => $"{Name}({Defines})";

    public string PrepareSource(GraphicsBackend backend = GraphicsBackend.OpenGL, string extraCode = "")
    {
        return Shader.PrepareSource(GetStage(ShaderType), Defines, backend, extraCode);
    }

    public bool CompileFromSource(RenderBackend? backend = null, string extraCode = "")
    {
        var effectiveBackend = backend ?? GetSubsystem<RenderDevice>()?.GetBackend() ?? RenderBackend.OpenGL;
        var source = PrepareSource(MapRenderBackend(effectiveBackend), extraCode);
        if (string.IsNullOrWhiteSpace(source))
            return false;

        var shaderBytecode = new ShaderBytecode(GetShaderType())
        {
            Mime = GetCompiledShaderMime(effectiveBackend),
            Bytecode = Encoding.UTF8.GetBytes(source)
        };
        if (GetShaderType() == ShaderType.VS)
            shaderBytecode.VertexAttributes.AddRange(ParseVertexAttributes(source));

        CreateFromBinary(shaderBytecode);
        return GetHandle() is { IsNull: false };
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

    private static IEnumerable<VertexShaderAttribute> ParseVertexAttributes(string source)
    {
        foreach (Match match in VertexInputRegex().Matches(source))
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
}
