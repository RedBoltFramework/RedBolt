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
    Point,
    Max
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

/// <summary>
/// Material's shader parameter animation instance.
/// </summary>
public sealed class ShaderParameterAnimationInfo : ValueAnimationInfo
{
    private readonly Material _material;
    private readonly string _name;

    public ShaderParameterAnimationInfo(Material material, string name, ValueAnimation attributeAnimation, WrapMode wrapMode, float speed)
        : base(material, attributeAnimation, wrapMode, speed)
    {
        _material = material;
        _name = name;
    }

    public string GetName() => _name;

    protected override void ApplyValue(object? newValue)
    {
        _material.SetShaderParameter(_name, newValue);
    }
}

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

/// <summary>Describes how to render 3D geometries.</summary>
/// <remarks>
/// </remarks>
public sealed class Material : ResourceWithMetadata
{
    private readonly IDFamily<Material> _idFamily = new();
    private readonly Dictionary<string, MaterialShaderParameter> _shaderParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<StringHash, ShaderParameterAnimationInfo> _shaderParameterAnimationInfos = new();
    private readonly Dictionary<StringHash, MaterialTexture> _textures = new();
    private readonly List<TechniqueEntry> _techniques = [];
    private uint _shaderParameterHash;
    private bool _batchedParameterUpdate;
    private uint _auxViewFrameNumber;
    private bool _specular;
    private Scene? _scene;
    private ContextEventSubscription? _sceneAttributeAnimationSubscription;

    public Material(string name)
        : base(name)
    {
        ResetToDefaults();
    }

    public override void Dispose()
    {
        _idFamily.Dispose();
        base.Dispose();
    }

    public uint GetObjectID() => _idFamily.GetObjectID();

    public uint GetPipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, new StringHash(VertexShaderDefines).Value);
        CombineHash(ref hash, new StringHash(PixelShaderDefines).Value);
        CombineHash(ref hash, (uint)CullMode);
        CombineHash(ref hash, (uint)ShadowCullMode);
        CombineHash(ref hash, (uint)FillMode);
        CombineHash(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(DepthBias.ConstantBias)));
        CombineHash(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(DepthBias.SlopeScaledBias)));
        CombineHash(ref hash, AlphaToCoverage ? 1u : 0u);
        CombineHash(ref hash, _specular ? 1u : 0u);
        foreach (var (nameHash, info) in _textures)
        {
            CombineHash(ref hash, nameHash.Value);
            CombineHash(ref hash, info.Texture.GetSRGB() ? 1u : 0u);
            CombineHash(ref hash, info.Texture.GetLinear() ? 1u : 0u);
            CombineHash(ref hash, unchecked((uint)info.Texture.GetSamplerStateDesc().GetHashCode()));
        }
        return Math.Max(1u, hash);
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }

    public static uint GetNextFreeObjectID() => IDFamily<Material>.GetNextFreeObjectID();

    public void AcquireObjectID() => _idFamily.AcquireObjectID();

    public void ReleaseObjectID() => _idFamily.ReleaseObjectID();

    public bool AlphaToCoverage { get; set; }

    public BiasParameters DepthBias { get; set; }

    public FillMode FillMode { get; set; } = FillMode.Solid;

    public CullMode CullMode { get; set; } = CullMode.CounterClockwise;

    public bool LineAntiAlias { get; set; }

    public bool Occlusion { get; set; } = true;

    public string PixelShaderDefines { get; set; } = string.Empty;

    public byte RenderOrder { get; set; } = 128;

    public CullMode ShadowCullMode { get; set; } = CullMode.CounterClockwise;

    public IReadOnlyDictionary<string, MaterialShaderParameter> ShaderParameters => _shaderParameters;

    public IReadOnlyDictionary<StringHash, MaterialTexture> Textures => _textures;

    public IReadOnlyList<TechniqueEntry> Techniques => _techniques;
    internal Dictionary<string, MaterialShaderParameter>.Enumerator EnumerateShaderParameters() => _shaderParameters.GetEnumerator();
    internal Dictionary<StringHash, MaterialTexture>.Enumerator EnumerateTextures() => _textures.GetEnumerator();
    internal List<TechniqueEntry>.Enumerator EnumerateTechniques() => _techniques.GetEnumerator();

    public uint ShaderParameterHash => _shaderParameterHash;

    public Vector2f UVOffset { get; private set; }

    public float UVRotation { get; private set; }

    public Vector2f UVRepeat { get; private set; } = Vector2f.One;

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

        OwnerCache = material.OwnerCache;
        AbsoluteFileName = material.AbsoluteFileName;
        MemoryUse = material.MemoryUse;
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
        _specular = material._specular;

        _techniques.Clear();
        foreach (var technique in material._techniques)
            _techniques.Add(technique);

        _textures.Clear();
        foreach (var texture in material._textures)
            _textures.Add(texture.Key, texture.Value);

        _shaderParameters.Clear();
        foreach (var parameter in material._shaderParameters)
            _shaderParameters.Add(parameter.Key, parameter.Value);
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

    public uint GetAuxViewFrameNumber() => _auxViewFrameNumber;

    public bool GetSpecular() => _specular;

    public Scene? GetScene() => _scene;

    public string GetPixelShaderDefines() => PixelShaderDefines;

    public byte GetRenderOrder() => RenderOrder;

    public CullMode GetShadowCullMode() => ShadowCullMode;

    public object? GetShaderParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _shaderParameters.TryGetValue(name, out var parameter) ? parameter.Value : null;
    }

    public ValueAnimation? GetShaderParameterAnimation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetShaderParameterAnimationInfo(name)?.GetAnimation();
    }

    public WrapMode GetShaderParameterAnimationWrapMode(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetShaderParameterAnimationInfo(name)?.GetWrapMode() ?? WrapMode.Loop;
    }

    public float GetShaderParameterAnimationSpeed(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetShaderParameterAnimationInfo(name)?.GetSpeed() ?? 0.0f;
    }

    public ShaderParameterAnimationInfo? GetShaderParameterAnimationInfo(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _shaderParameterAnimationInfos.GetValueOrDefault(new StringHash(name));
    }

    public IReadOnlyDictionary<string, MaterialShaderParameter> GetShaderParameters() => _shaderParameters;

    public uint GetShaderParameterHash() => _shaderParameterHash;

    public string GetVertexShaderDefines() => VertexShaderDefines;

    public uint GetNumTechniques() => (uint)_techniques.Count;

    public IReadOnlyList<TechniqueEntry> GetTechniques() => _techniques;

    public TechniqueEntry GetTechniqueEntry(uint index) => index < _techniques.Count ? _techniques[(int)index] : default;

    public Technique? GetTechnique(uint index) => GetTechniqueEntry(index).Technique;

    /// <summary>
    /// Find the best technique for the specified material quality and drawable LOD distance.
    /// </summary>
    public Technique? FindTechnique(MaterialQuality materialQuality, float lodDistance = 0.0f)
    {
        if (_techniques.Count == 1)
            return _techniques[0].Technique;

        foreach (var entry in _techniques)
        {
            var technique = entry.Technique;
            if (technique is null || materialQuality < entry.QualityLevel)
                continue;
            if (lodDistance >= entry.LodDistance)
                return technique;
        }

        return _techniques.Count > 0 ? _techniques[^1].Technique : null;
    }

    public Pass? GetPass(uint index, string passName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passName);
        return GetTechnique(index)?.GetPass(passName);
    }

    public Pass? GetDefaultPass() => GetPass(0, "base");

    public Texture? GetTexture(StringHash nameHash) => _textures.TryGetValue(nameHash, out var texture) ? texture.Texture : null;

    public Texture? GetTexture(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetTexture(new StringHash(NormalizeTextureSlotName(name)));
    }

    public IReadOnlyDictionary<StringHash, MaterialTexture> GetTextures() => _textures;

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

    public void SetScene(Scene? scene)
    {
        UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
        _sceneAttributeAnimationSubscription?.Dispose();
        _sceneAttributeAnimationSubscription = null;
        _scene = scene;
        UpdateEventSubscription();
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
            _textures.Remove(hash);
        else
            _textures[hash] = new MaterialTexture(normalized, texture);
    }

    public void SetNumTechniques(uint num)
    {
        while (_techniques.Count < num)
            _techniques.Add(default);
        while (_techniques.Count > num)
            _techniques.RemoveAt(_techniques.Count - 1);
    }

    public void SetTechnique(uint index, Technique? tech, MaterialQuality qualityLevel = MaterialQuality.Low, float lodDistance = 0.0f)
    {
        SetNumTechniques(index + 1);
        _techniques[(int)index] = new TechniqueEntry(tech, qualityLevel, lodDistance);
        SortTechniques();
        ApplyShaderDefinesToTechniques();
    }

    public void SetTechniques(IEnumerable<TechniqueEntry> techniques)
    {
        ArgumentNullException.ThrowIfNull(techniques);
        this._techniques.Clear();
        this._techniques.AddRange(techniques);
        SortTechniques();
        ApplyShaderDefinesToTechniques();
    }

    public void SetShaderParameter(string name, object? value, bool isCustom = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _shaderParameters[name] = new MaterialShaderParameter(value, isCustom);
        UpdateSpecularState(name, value);
        if (!_batchedParameterUpdate)
            RefreshShaderParameterHash();
    }

    public void SetShaderParameterAnimation(string name, ValueAnimation? animation, WrapMode wrapMode = WrapMode.Loop, float speed = 1.0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var info = GetShaderParameterAnimationInfo(name);

        if (animation is not null)
        {
            if (info is not null && ReferenceEquals(info.GetAnimation(), animation))
            {
                info.SetWrapMode(wrapMode);
                info.SetSpeed(speed);
                return;
            }

            if (!_shaderParameters.ContainsKey(name))
                return;

            _shaderParameterAnimationInfos[new StringHash(name)] =
                new ShaderParameterAnimationInfo(this, name, animation, wrapMode, speed);
            UpdateEventSubscription();
        }
        else if (info is not null)
        {
            _shaderParameterAnimationInfos.Remove(new StringHash(name));
            UpdateEventSubscription();
        }
    }

    public void SetShaderParameterAnimationWrapMode(string name, WrapMode wrapMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        GetShaderParameterAnimationInfo(name)?.SetWrapMode(wrapMode);
    }

    public void SetShaderParameterAnimationSpeed(string name, float speed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        GetShaderParameterAnimationInfo(name)?.SetSpeed(speed);
    }

    public void SetUVTransform(Vector2f offset, float rotation, float repeat)
    {
        SetUVTransform(offset, rotation, new Vector2f(repeat, repeat));
    }

    public void SetUVTransform(Vector2f offset, float rotation, Vector2f repeat)
    {
        UVOffset = offset;
        UVRotation = rotation;
        UVRepeat = repeat;

        var angle = rotation * MathDefs.DegToRad;
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        SetShaderParameter("UOffset", new Vector4f(repeat.X * cos, repeat.Y * -sin, offset.X, 0.0f));
        SetShaderParameter("VOffset", new Vector4f(repeat.X * sin, repeat.Y * cos, offset.Y, 0.0f));
    }

    public void SetVertexShaderDefines(string defines)
    {
        VertexShaderDefines = defines ?? string.Empty;
        ApplyShaderDefinesToTechniques();
    }

    public void SortTechniques()
    {
        _techniques.Sort();
    }

    public bool RemoveShaderParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var removed = _shaderParameters.Remove(name);
        if (removed)
        {
            if (name == "MatSpecColor")
                _specular = false;
            RefreshShaderParameterHash();
        }
        return removed;
    }

    public void ReleaseShaders()
    {
        for (var i = 0; i < _techniques.Count; ++i)
            _techniques[i].Technique?.ReleaseShaders();
    }

    public void MarkForAuxView(uint frameNumber)
    {
        _auxViewFrameNumber = frameNumber;
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
            2 => new Vector2f(parsed[0], parsed[1]),
            3 => new Vector3f(parsed[0], parsed[1], parsed[2]),
            4 => new Vector4f(parsed[0], parsed[1], parsed[2], parsed[3]),
            _ => value
        };
    }

    public bool TryGetShaderParameter(string name, out object? value)
    {
        if (_shaderParameters.TryGetValue(name, out var parameter))
        {
            value = parameter.Value;
            return true;
        }

        value = null;
        return false;
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var xml = new XMLFile(Name);
        return xml.Load(source) && LoadXML(xml.GetRoot());
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var xml = new XMLFile(Name);
        var root = xml.CreateRoot("material");
        return SaveXML(root) && xml.Save(destination);
    }

    public bool LoadXML(XMLElement source)
    {
        if (source.IsNull)
            return false;

        var previousBatchedParameterUpdate = _batchedParameterUpdate;
        _batchedParameterUpdate = true;
        try
        {
        ResetToDefaults();

        var cache = OwnerCache;
        var shaderElement = source.GetChild("shader");
        if (shaderElement.NotNull)
        {
            SetVertexShaderDefines(shaderElement.GetAttribute("vsdefines"));
            SetPixelShaderDefines(shaderElement.GetAttribute("psdefines"));
        }

        _techniques.Clear();
        for (var techniqueElement = source.GetChild("technique"); techniqueElement.NotNull; techniqueElement = techniqueElement.GetNext("technique"))
        {
            var techniqueName = techniqueElement.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(techniqueName) || cache is null)
                continue;

            var technique = cache.GetResourceOrNull<Technique>(techniqueName);
            if (technique is null)
                continue;

            var quality = techniqueElement.HasAttribute("quality")
                ? (MaterialQuality)techniqueElement.GetInt("quality")
                : MaterialQuality.Low;
            var lodDistance = techniqueElement.HasAttribute("loddistance")
                ? techniqueElement.GetFloat("loddistance")
                : 0.0f;
            _techniques.Add(new TechniqueEntry(technique, quality, lodDistance));
        }

        SortTechniques();
        ApplyShaderDefinesToTechniques();

        for (var textureElement = source.GetChild("texture"); textureElement.NotNull; textureElement = textureElement.GetNext("texture"))
        {
            var slotName = textureElement.GetAttribute("slot");
            if (string.IsNullOrWhiteSpace(slotName))
                slotName = textureElement.GetAttribute("unit");
            if (string.IsNullOrWhiteSpace(slotName))
                continue;

            var resourceName = textureElement.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(resourceName) || cache is null)
                continue;

            var texture = cache.GetResourceOrNull<Texture2D>(resourceName);
            if (texture is not null)
                SetTexture(slotName, texture);
        }

        for (var parameterElement = source.GetChild("parameter"); parameterElement.NotNull; parameterElement = parameterElement.GetNext("parameter"))
        {
            var name = parameterElement.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            SetShaderParameter(name, ParseShaderParameterValue(parameterElement.GetAttribute("value")));
        }

        var cullElement = source.GetChild("cull");
        if (cullElement.NotNull)
            SetCullMode(ParseCullMode(cullElement.GetAttribute("value"), CullMode.CounterClockwise));

        var shadowCullElement = source.GetChild("shadowcull");
        if (shadowCullElement.NotNull)
            SetShadowCullMode(ParseCullMode(shadowCullElement.GetAttribute("value"), CullMode.CounterClockwise));

        var fillElement = source.GetChild("fill");
        if (fillElement.NotNull)
            SetFillMode(ParseFillMode(fillElement.GetAttribute("value")));

        var depthBiasElement = source.GetChild("depthbias");
        if (depthBiasElement.NotNull)
        {
            SetDepthBias(new BiasParameters(
                depthBiasElement.GetFloat("constant"),
                depthBiasElement.GetFloat("slopescaled"),
                depthBiasElement.GetFloat("normaloffset")));
        }

        var alphaToCoverageElement = source.GetChild("alphatocoverage");
        if (alphaToCoverageElement.NotNull)
            SetAlphaToCoverage(alphaToCoverageElement.GetBool("enable"));

        var lineAntiAliasElement = source.GetChild("lineantialias");
        if (lineAntiAliasElement.NotNull)
            SetLineAntiAlias(lineAntiAliasElement.GetBool("enable"));

        var renderOrderElement = source.GetChild("renderorder");
        if (renderOrderElement.NotNull)
            SetRenderOrder(renderOrderElement.GetUInt("value"));

        var occlusionElement = source.GetChild("occlusion");
        if (occlusionElement.NotNull)
            SetOcclusion(occlusionElement.GetBool("enable"));

        return true;
        }
        finally
        {
            _batchedParameterUpdate = previousBatchedParameterUpdate;
            if (!previousBatchedParameterUpdate)
                RefreshShaderParameterHash();
        }
    }

    public bool SaveXML(XMLElement dest)
    {
        if (dest.IsNull)
            return false;

        for (var i = 0; i < _techniques.Count; ++i)
        {
            var entry = _techniques[i];
            if (entry.Original is null)
                continue;

            var techniqueElement = dest.CreateChild("technique");
            techniqueElement.SetString("name", entry.Original.Name);
            techniqueElement.SetInt("quality", (int)entry.QualityLevel);
            techniqueElement.SetFloat("loddistance", entry.LodDistance);
        }

        foreach (var texture in _textures.Values.OrderBy(static texture => texture.Name, StringComparer.Ordinal))
        {
            var textureElement = dest.CreateChild("texture");
            textureElement.SetString("slot", texture.Name);
            textureElement.SetString("name", texture.Texture.Name);
        }

        if (!string.IsNullOrWhiteSpace(VertexShaderDefines) || !string.IsNullOrWhiteSpace(PixelShaderDefines))
        {
            var shaderElement = dest.CreateChild("shader");
            if (!string.IsNullOrWhiteSpace(VertexShaderDefines))
                shaderElement.SetString("vsdefines", VertexShaderDefines);
            if (!string.IsNullOrWhiteSpace(PixelShaderDefines))
                shaderElement.SetString("psdefines", PixelShaderDefines);
        }

        foreach (var parameter in _shaderParameters.OrderBy(static parameter => parameter.Key, StringComparer.Ordinal))
        {
            var parameterElement = dest.CreateChild("parameter");
            parameterElement.SetString("name", parameter.Key);
            parameterElement.SetString("value", SerializeShaderParameterValue(parameter.Value.Value));
        }

        dest.CreateChild("cull").SetString("value", ToUrhoCullMode(CullMode));
        dest.CreateChild("shadowcull").SetString("value", ToUrhoCullMode(ShadowCullMode));
        dest.CreateChild("fill").SetString("value", ToUrhoFillMode(FillMode));

        var depthBiasElement = dest.CreateChild("depthbias");
        depthBiasElement.SetFloat("constant", DepthBias.ConstantBias);
        depthBiasElement.SetFloat("slopescaled", DepthBias.SlopeScaledBias);
        depthBiasElement.SetFloat("normaloffset", DepthBias.NormalOffset);

        dest.CreateChild("alphatocoverage").SetBool("enable", AlphaToCoverage);
        dest.CreateChild("lineantialias").SetBool("enable", LineAntiAlias);
        dest.CreateChild("renderorder").SetUInt("value", RenderOrder);
        dest.CreateChild("occlusion").SetBool("enable", Occlusion);
        return true;
    }

    private void RefreshShaderParameterHash()
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var parameter in _shaderParameters.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                hash = (hash ^ StringHash.Calculate(parameter.Key)) * 16777619u;
                hash = (hash ^ (uint)(parameter.Value.Value?.GetHashCode() ?? 0)) * 16777619u;
                hash = (hash ^ (parameter.Value.IsCustom ? 1u : 0u)) * 16777619u;
            }

            _shaderParameterHash = hash;
        }
    }

    private void ApplyShaderDefinesToTechniques()
    {
        for (var i = 0; i < _techniques.Count; ++i)
        {
            var entry = _techniques[i];
            if (entry.Original is null)
                continue;

            entry.Technique = string.IsNullOrWhiteSpace(VertexShaderDefines) && string.IsNullOrWhiteSpace(PixelShaderDefines)
                ? entry.Original
                : entry.Original.CloneWithDefines(VertexShaderDefines, PixelShaderDefines);
            _techniques[i] = entry;
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

    private void ResetToDefaults()
    {
        _shaderParameterAnimationInfos.Clear();
        UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
        _sceneAttributeAnimationSubscription?.Dispose();
        _sceneAttributeAnimationSubscription = null;
        AlphaToCoverage = false;
        DepthBias = default;
        FillMode = FillMode.Solid;
        CullMode = CullMode.CounterClockwise;
        LineAntiAlias = false;
        Occlusion = true;
        PixelShaderDefines = string.Empty;
        RenderOrder = 128;
        ShadowCullMode = CullMode.CounterClockwise;
        VertexShaderDefines = string.Empty;
        UVOffset = Vector2f.Zero;
        UVRotation = 0.0f;
        UVRepeat = Vector2f.One;

        _shaderParameters.Clear();
        _textures.Clear();
        _techniques.Clear();

        SetNumTechniques(1);
        var renderer = GetSubsystem<Renderer>();
        SetTechnique(0, renderer?.GetDefaultTechnique()
            ?? GetSubsystem<ResourceCache>()?.GetResourceOrNull<Technique>("Techniques/NoTexture.xml"));

        SetUVTransform(Vector2f.Zero, 0.0f, Vector2f.One);
        SetShaderParameter("MatDiffColor", Vector4f.One);
        SetShaderParameter("MatEmissiveColor", Vector3f.Zero);
        SetShaderParameter("MatEnvMapColor", Vector3f.One);
        SetShaderParameter("MatSpecColor", new Vector4f(0.0f, 0.0f, 0.0f, 1.0f));
        SetShaderParameter("Roughness", 1.0f);
        SetShaderParameter("Metallic", 1.0f);
        SetShaderParameter("DielectricReflectance", 0.5f);
        SetShaderParameter("NormalScale", 1.0f);
        SetShaderParameter("AlphaCutoff", 0.5f);
        RefreshShaderParameterHash();
    }

    private void UpdateEventSubscription()
    {
        if (_shaderParameterAnimationInfos.Count != 0)
        {
            if (_scene is not null)
                _sceneAttributeAnimationSubscription ??= SubscribeToEvent<SceneEvents.AttributeAnimationUpdateEvent>(_scene, HandleAttributeAnimationUpdate);
            else if (!HasSubscribedToEvent<CoreEvents.UpdateEvent>())
                SubscribeToEvent<CoreEvents.UpdateEvent>(HandleAttributeAnimationUpdate);
        }
        else
        {
            UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
            _sceneAttributeAnimationSubscription?.Dispose();
            _sceneAttributeAnimationSubscription = null;
        }
    }

    private void HandleAttributeAnimationUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        UpdateShaderParameterAnimations(eventData.TimeStep);
    }

    private void HandleAttributeAnimationUpdate(ref SceneEvents.AttributeAnimationUpdateEvent eventData)
    {
        UpdateShaderParameterAnimations(eventData.TimeStep);
    }

    private void UpdateShaderParameterAnimations(float timeStep)
    {
        var finishedNames = new List<string>();
        foreach (var info in _shaderParameterAnimationInfos.Values)
        {
            if (info.Update(timeStep))
                finishedNames.Add(info.GetName());
        }

        foreach (var name in finishedNames)
            SetShaderParameterAnimation(name, null);
    }

    private void UpdateSpecularState(string name, object? value)
    {
        if (name != "MatSpecColor")
            return;

        _specular = value switch
        {
            Vector3f vector => vector.X > 0.0f || vector.Y > 0.0f || vector.Z > 0.0f,
            Vector4f vector => vector.X > 0.0f || vector.Y > 0.0f || vector.Z > 0.0f,
            _ => _specular
        };
    }

    private static CullMode ParseCullMode(string value, CullMode defaultValue)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "none" => CullMode.None,
            "cw" => CullMode.Clockwise,
            "ccw" => CullMode.CounterClockwise,
            _ => defaultValue
        };
    }

    private static FillMode ParseFillMode(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "wireframe" => FillMode.Wireframe,
            "point" => FillMode.Point,
            _ => FillMode.Solid
        };
    }

    private static string ToUrhoCullMode(CullMode mode) => mode switch
    {
        CullMode.None => "none",
        CullMode.Clockwise => "cw",
        _ => "ccw"
    };

    private static string ToUrhoFillMode(FillMode mode) => mode switch
    {
        FillMode.Wireframe => "wireframe",
        FillMode.Point => "point",
        _ => "solid"
    };

    private static string SerializeShaderParameterValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            bool boolean => boolean ? "true" : "false",
            float scalar => scalar.ToString(CultureInfo.InvariantCulture),
            double scalar => scalar.ToString(CultureInfo.InvariantCulture),
            int integer => integer.ToString(CultureInfo.InvariantCulture),
            uint integer => integer.ToString(CultureInfo.InvariantCulture),
            Vector2f vector => vector.ToString(),
            Vector3f vector => vector.ToString(),
            Vector4f vector => vector.ToString(),
            Quaternion quaternion => quaternion.ToString(),
            Color color => color.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}
