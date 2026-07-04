using System.Globalization;

namespace RedBolt;

public enum CullMode
{
    None,
    CounterClockwise,
    Clockwise
}

public enum FillMode
{
    Solid,
    Wireframe,
    Point
}

public enum MaterialQuality : uint
{
    Low = 0,
    Medium = 1,
    High = 2,
    Max = 15
}

public readonly record struct BiasParameters(float ConstantBias, float SlopeScaledBias, float NormalOffset = 0.0f);

public readonly record struct MaterialShaderParameter(object? Value, bool IsCustom = false);

public sealed class Material : ResourceWithMetadata
{
    private readonly Dictionary<string, MaterialShaderParameter> shaderParameters = new(StringComparer.Ordinal);
    private uint shaderParameterHash;

    internal Material(string name)
        : base(name)
    {
        SetUVTransform(Vector2.Zero, 0.0f, Vector2.One);
        SetShaderParameter("MatDiffColor", Vector4.One);
        SetShaderParameter("MatEmissiveColor", Vector3.Zero);
        SetShaderParameter("MatEnvMapColor", Vector3.One);
        SetShaderParameter("MatSpecColor", new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        SetShaderParameter("Roughness", 1.0f);
        SetShaderParameter("Metallic", 1.0f);
        SetShaderParameter("DielectricReflectance", 0.5f);
        SetShaderParameter("NormalScale", 1.0f);
        SetShaderParameter("AlphaCutoff", 0.5f);
    }

    public bool AlphaToCoverage { get; set; }

    public BiasParameters DepthBias { get; set; }

    public FillMode FillMode { get; set; } = FillMode.Solid;

    public CullMode CullMode { get; set; } = CullMode.CounterClockwise;

    public bool LineAntiAlias { get; set; }

    public bool Occlusion { get; set; } = true;

    public string PixelShaderDefines { get; set; } = string.Empty;

    public byte RenderOrder { get; set; } = 128;

    public CullMode ShadowCullMode { get; set; } = CullMode.CounterClockwise;

    public IReadOnlyDictionary<string, MaterialShaderParameter> ShaderParameters => shaderParameters;

    public uint ShaderParameterHash => shaderParameterHash;

    public Vector2 UVOffset { get; private set; }

    public float UVRotation { get; private set; }

    public Vector2 UVRepeat { get; private set; } = Vector2.One;

    public string VertexShaderDefines { get; set; } = string.Empty;

    public Material Clone(string cloneName = "")
    {
        var clone = new Material(string.IsNullOrEmpty(cloneName) ? Name : cloneName);
        clone.CopyFrom(this);
        return clone;
    }

    public void CopyFrom(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        AlphaToCoverage = material.AlphaToCoverage;
        CullMode = material.CullMode;
        DepthBias = material.DepthBias;
        FillMode = material.FillMode;
        LineAntiAlias = material.LineAntiAlias;
        Occlusion = material.Occlusion;
        PixelShaderDefines = material.PixelShaderDefines;
        RenderOrder = material.RenderOrder;
        ShadowCullMode = material.ShadowCullMode;
        UVOffset = material.UVOffset;
        UVRotation = material.UVRotation;
        UVRepeat = material.UVRepeat;
        VertexShaderDefines = material.VertexShaderDefines;

        shaderParameters.Clear();
        foreach (var parameter in material.shaderParameters)
            shaderParameters.Add(parameter.Key, parameter.Value);
        RefreshShaderParameterHash();
        CopyMetadata(material);
    }

    public bool GetAlphaToCoverage() => AlphaToCoverage;

    public CullMode GetCullMode() => CullMode;

    public BiasParameters GetDepthBias() => DepthBias;

    public FillMode GetFillMode() => FillMode;

    public bool GetLineAntiAlias() => LineAntiAlias;

    public bool GetOcclusion() => Occlusion;

    public string GetPixelShaderDefines() => PixelShaderDefines;

    public byte GetRenderOrder() => RenderOrder;

    public CullMode GetShadowCullMode() => ShadowCullMode;

    public object? GetShaderParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return shaderParameters.TryGetValue(name, out var parameter) ? parameter.Value : null;
    }

    public IReadOnlyDictionary<string, MaterialShaderParameter> GetShaderParameters() => shaderParameters;

    public uint GetShaderParameterHash() => shaderParameterHash;

    public string GetVertexShaderDefines() => VertexShaderDefines;

    public void SetAlphaToCoverage(bool enable)
    {
        AlphaToCoverage = enable;
    }

    public void SetCullMode(CullMode mode)
    {
        CullMode = mode;
    }

    public void SetDepthBias(BiasParameters parameters)
    {
        DepthBias = parameters;
    }

    public void SetFillMode(FillMode mode)
    {
        FillMode = mode;
    }

    public void SetLineAntiAlias(bool enable)
    {
        LineAntiAlias = enable;
    }

    public void SetOcclusion(bool enable)
    {
        Occlusion = enable;
    }

    public void SetPixelShaderDefines(string defines)
    {
        PixelShaderDefines = defines ?? string.Empty;
    }

    public void SetRenderOrder(byte order)
    {
        RenderOrder = order;
    }

    public void SetRenderOrder(uint order)
    {
        RenderOrder = checked((byte)order);
    }

    public void SetShadowCullMode(CullMode mode)
    {
        ShadowCullMode = mode;
    }

    public void SetShaderParameter(string name, object? value, bool isCustom = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        shaderParameters[name] = new MaterialShaderParameter(value, isCustom);
        RefreshShaderParameterHash();
    }

    public void SetUVTransform(Vector2 offset, float rotation, float repeat)
    {
        SetUVTransform(offset, rotation, new Vector2(repeat, repeat));
    }

    public void SetUVTransform(Vector2 offset, float rotation, Vector2 repeat)
    {
        UVOffset = offset;
        UVRotation = rotation;
        UVRepeat = repeat;

        var angle = rotation * MathDefs.DegToRad;
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        SetShaderParameter("UOffset", new Vector4(repeat.X * cos, repeat.Y * -sin, offset.X, 0.0f));
        SetShaderParameter("VOffset", new Vector4(repeat.X * sin, repeat.Y * cos, offset.Y, 0.0f));
    }

    public void SetVertexShaderDefines(string defines)
    {
        VertexShaderDefines = defines ?? string.Empty;
    }

    public bool RemoveShaderParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var removed = shaderParameters.Remove(name);
        if (removed)
            RefreshShaderParameterHash();
        return removed;
    }

    public static object ParseShaderParameterValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var components = value.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 1)
        {
            if (bool.TryParse(components[0], out var boolValue))
                return boolValue;
            if (float.TryParse(components[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                return floatValue;
        }

        var parsed = new float[components.Length];
        for (var i = 0; i < components.Length; ++i)
        {
            if (!float.TryParse(components[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]))
                return value;
        }

        return parsed.Length switch
        {
            2 => new Vector2(parsed[0], parsed[1]),
            3 => new Vector3(parsed[0], parsed[1], parsed[2]),
            4 => new Vector4(parsed[0], parsed[1], parsed[2], parsed[3]),
            _ => value
        };
    }

    public bool TryGetShaderParameter(string name, out object? value)
    {
        if (shaderParameters.TryGetValue(name, out var parameter))
        {
            value = parameter.Value;
            return true;
        }

        value = null;
        return false;
    }

    private void RefreshShaderParameterHash()
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var parameter in shaderParameters.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                hash = (hash ^ StringHash.Calculate(parameter.Key)) * 16777619u;
                hash = (hash ^ (uint)(parameter.Value.Value?.GetHashCode() ?? 0)) * 16777619u;
                hash = (hash ^ (parameter.Value.IsCustom ? 1u : 0u)) * 16777619u;
            }

            shaderParameterHash = hash;
        }
    }
}
