namespace RedBolt;

public sealed class Material : Resource
{
    private readonly Dictionary<string, object?> shaderParameters = new(StringComparer.Ordinal);

    internal Material(string name)
        : base(name)
    {
    }

    public IReadOnlyDictionary<string, object?> ShaderParameters => shaderParameters;

    public string PixelShaderDefines { get; set; } = string.Empty;

    public byte RenderOrder { get; set; } = 128;

    public string VertexShaderDefines { get; set; } = string.Empty;

    public void SetShaderParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        shaderParameters[name] = value;
    }

    public bool RemoveShaderParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return shaderParameters.Remove(name);
    }

    public bool TryGetShaderParameter(string name, out object? value)
    {
        return shaderParameters.TryGetValue(name, out value);
    }
}
