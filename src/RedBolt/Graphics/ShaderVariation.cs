namespace RedBolt;

public sealed class ShaderVariation
{
    public ShaderVariation(Shader shader, ShaderType shaderType, string defines = "")
    {
        Shader = shader ?? throw new ArgumentNullException(nameof(shader));
        ShaderType = shaderType;
        Defines = defines ?? string.Empty;
    }

    public Shader Shader { get; }

    public ShaderType ShaderType { get; }

    public string Defines { get; }

    public string Name => Shader.GetShaderName();

    public string PrepareSource(GraphicsBackend backend = GraphicsBackend.OpenGL, string extraCode = "")
    {
        return Shader.PrepareSource(GetStage(ShaderType), Defines, backend, extraCode);
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
}
