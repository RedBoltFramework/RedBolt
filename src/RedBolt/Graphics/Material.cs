using System.Globalization;

namespace RedBolt;

public enum CullMode
{
    None,
    CounterClockwise,
    Clockwise,
    Max
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

public readonly record struct MaterialTexture(string Name, Texture Texture);

public struct TechniqueEntry : IEquatable<TechniqueEntry>, IComparable<TechniqueEntry>
{
    public TechniqueEntry(Technique? technique, MaterialQuality qualityLevel = MaterialQuality.Low, float lodDistance = 0.0f)
    {
        Technique = technique;
        Original = technique;
        QualityLevel = qualityLevel;
        LodDistance = lodDistance;
    }

    public Technique? Technique { get; set; }

    public Technique? Original { get; set; }

    public MaterialQuality QualityLevel { get; set; }

    public float LodDistance { get; set; }

    public int CompareTo(TechniqueEntry other)
    {
        if (!MathDefs.ApproximatelyEquivalent(LodDistance, other.LodDistance))
            return other.LodDistance.CompareTo(LodDistance);

        return other.QualityLevel.CompareTo(QualityLevel);
    }

    public bool Equals(TechniqueEntry other)
    {
        return ReferenceEquals(Technique, other.Technique)
            && ReferenceEquals(Original, other.Original)
            && QualityLevel == other.QualityLevel
            && MathDefs.ApproximatelyEquivalent(LodDistance, other.LodDistance);
    }

    public override bool Equals(object? obj) => obj is TechniqueEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Technique, Original, QualityLevel, LodDistance);

    public static bool operator ==(TechniqueEntry left, TechniqueEntry right) => left.Equals(right);

    public static bool operator !=(TechniqueEntry left, TechniqueEntry right) => !left.Equals(right);
}

public sealed class Material : ResourceWithMetadata
{
    private readonly Dictionary<string, MaterialShaderParameter> shaderParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<StringHash, MaterialTexture> textures = new();
    private readonly List<TechniqueEntry> techniques = [];
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

    public IReadOnlyDictionary<StringHash, MaterialTexture> Textures => textures;

    public IReadOnlyList<TechniqueEntry> Techniques => techniques;

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

        techniques.Clear();
        foreach (var technique in material.techniques)
            techniques.Add(technique);

        textures.Clear();
        foreach (var texture in material.textures)
            textures.Add(texture.Key, texture.Value);

        shaderParameters.Clear();
        foreach (var parameter in material.shaderParameters)
            shaderParameters.Add(parameter.Key, parameter.Value);
        ApplyShaderDefinesToTechniques();
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

    public uint GetNumTechniques() => (uint)techniques.Count;

    public IReadOnlyList<TechniqueEntry> GetTechniques() => techniques;

    public TechniqueEntry GetTechniqueEntry(uint index) => index < techniques.Count ? techniques[(int)index] : default;

    public Technique? GetTechnique(uint index) => GetTechniqueEntry(index).Technique;

    public Pass? GetPass(uint index, string passName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passName);
        return GetTechnique(index)?.GetPass(passName);
    }

    public Pass? GetDefaultPass() => GetPass(0, "base");

    public Texture? GetTexture(StringHash nameHash) => textures.TryGetValue(nameHash, out var texture) ? texture.Texture : null;

    public Texture? GetTexture(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetTexture(new StringHash(NormalizeTextureSlotName(name)));
    }

    public IReadOnlyDictionary<StringHash, MaterialTexture> GetTextures() => textures;

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
        ApplyShaderDefinesToTechniques();
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

    public void SetTexture(string name, Texture? texture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = NormalizeTextureSlotName(name);
        var hash = new StringHash(normalized);
        if (texture is null)
            textures.Remove(hash);
        else
            textures[hash] = new MaterialTexture(normalized, texture);
    }

    public void SetNumTechniques(uint num)
    {
        while (techniques.Count < num)
            techniques.Add(default);
        while (techniques.Count > num)
            techniques.RemoveAt(techniques.Count - 1);
    }

    public void SetTechnique(uint index, Technique? tech, MaterialQuality qualityLevel = MaterialQuality.Low, float lodDistance = 0.0f)
    {
        SetNumTechniques(index + 1);
        techniques[(int)index] = new TechniqueEntry(tech, qualityLevel, lodDistance);
        SortTechniques();
        ApplyShaderDefinesToTechniques();
    }

    public void SetTechniques(IEnumerable<TechniqueEntry> techniques)
    {
        ArgumentNullException.ThrowIfNull(techniques);
        this.techniques.Clear();
        this.techniques.AddRange(techniques);
        SortTechniques();
        ApplyShaderDefinesToTechniques();
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
        ApplyShaderDefinesToTechniques();
    }

    public void SortTechniques()
    {
        techniques.Sort();
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

    private void ApplyShaderDefinesToTechniques()
    {
        for (var i = 0; i < techniques.Count; ++i)
        {
            var entry = techniques[i];
            if (entry.Original is null)
                continue;

            entry.Technique = string.IsNullOrWhiteSpace(VertexShaderDefines) && string.IsNullOrWhiteSpace(PixelShaderDefines)
                ? entry.Original
                : entry.Original.CloneWithDefines(VertexShaderDefines, PixelShaderDefines);
            techniques[i] = entry;
        }
    }

    private static string NormalizeTextureSlotName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return normalized switch
        {
            "0" or "diffuse" or "diff" or "albedo" => "Albedo",
            "1" or "normal" or "norm" => "Normal",
            "2" or "specular" or "spec" => "Properties",
            "3" or "emissive" => "Emission",
            "4" or "environment" or "env" => "Reflection0",
            "15" or "zone" => "Reflection1",
            _ => name.Trim()
        };
    }
}
