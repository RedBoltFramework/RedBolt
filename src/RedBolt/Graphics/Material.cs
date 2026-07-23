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

public sealed class MaterialShaderParameter : ICloneable, IDisposable
{
    private Variant _value;

    public MaterialShaderParameter(string name, object? value, bool isCustom = false)
    {
        Name = name;
        _value = Variant.FromObject(value);
        IsCustom = isCustom;
    }

    internal MaterialShaderParameter(string name, in Variant value, bool isCustom = false)
    {
        Name = name;
        _value = value.Clone();
        IsCustom = isCustom;
    }

    private MaterialShaderParameter(MaterialShaderParameter rhs)
    {
        Name = rhs.Name;
        _value = rhs._value.Clone();
        IsCustom = rhs.IsCustom;
    }

    public string Name { get; }

    public object? Value => _value.GetObject();

    public ref readonly Variant VariantValue => ref _value;

    public bool IsCustom { get; private set; }

    internal void SetValue(in Variant value, bool isCustom)
    {
        _value.Dispose();
        _value = value.Clone();
        IsCustom = isCustom;
    }

    public object Clone() => new MaterialShaderParameter(this);

    public void Dispose() => _value.Dispose();
}

public readonly record struct MaterialTexture(string Name, Texture Texture);

/// <summary>
/// Material's shader parameter animation instance.
/// </summary>
public class ShaderParameterAnimationInfo : ValueAnimationInfo
{
    /// <summary>Construct.</summary>
    public ShaderParameterAnimationInfo(Material material, string name, ValueAnimation attributeAnimation, WrapMode wrapMode, float speed)
        : base(material, attributeAnimation, wrapMode, speed)
    {
        Name = name;
    }

    /// <summary>Copy construct.</summary>
    public ShaderParameterAnimationInfo(ShaderParameterAnimationInfo rhs)
        : base(rhs)
    {
        Name = rhs.Name;
    }

    public override object Clone() => new ShaderParameterAnimationInfo(this);

    /// <summary>Return shader parameter name.</summary>
    public string Name { get; }

    /// <summary>Apply new animation value to the target object. Called by Update().</summary>
    protected override void ApplyValue(in Variant newValue)
    {
        ((Material)Target!).SetShaderParameter(Name, newValue);
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
        if (LodDistance != other.LodDistance)
            return other.LodDistance.CompareTo(LodDistance);

        return other.QualityLevel.CompareTo(QualityLevel);
    }

    public bool Equals(TechniqueEntry other)
    {
        return ReferenceEquals(Technique, other.Technique)
            && QualityLevel == other.QualityLevel
            && LodDistance == other.LodDistance;
    }

    public override bool Equals(object? obj) => obj is TechniqueEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Technique, QualityLevel, LodDistance);

    public static bool operator ==(TechniqueEntry left, TechniqueEntry right) => left.Equals(right);

    public static bool operator !=(TechniqueEntry left, TechniqueEntry right) => !left.Equals(right);
}

/// <summary>Describes how to render 3D geometries.</summary>
/// <remarks>
/// </remarks>
public sealed class Material : Resource
{
    private sealed class MaterialPipelineStateTracker : PipelineStateTracker
    {
        private readonly Material _owner;

        public MaterialPipelineStateTracker(Material owner)
        {
            _owner = owner;
        }

        protected override uint RecalculatePipelineStateHash() => _owner.RecalculatePipelineStateHash();
    }

    private readonly MaterialPipelineStateTracker _pipelineStateTracker;
    private readonly IDFamily<Material> _idFamily;
    private readonly Dictionary<string, MaterialShaderParameter> _shaderParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<StringHash, ShaderParameterAnimationInfo> _shaderParameterAnimationInfos = new();
    private readonly Dictionary<StringHash, MaterialTexture> _textures = new();
    private readonly List<TechniqueEntry> _techniques = [];
    private readonly List<string> _finishedShaderParameterAnimationNames = [];
    private XMLFile? _loadXmlFile;
    private uint _shaderParameterHash;
    private bool _batchedParameterUpdate;
    private uint _auxViewFrameNumber;
    private bool _specular;
    private Scene? _scene;
    private ContextEventSubscription? _sceneAttributeAnimationSubscription;

    public Material()
    {
        _pipelineStateTracker = new MaterialPipelineStateTracker(this);
        _idFamily = new IDFamily<Material>();
        ResetToDefaults();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<Material>();
    }

    public override void Dispose()
    {
        SetScene(null);
        _loadXmlFile?.Dispose();
        _loadXmlFile = null;
        ClearShaderParameterAnimationInfos();
        foreach (var parameter in _shaderParameters.Values)
            parameter.Dispose();
        _shaderParameters.Clear();
        _textures.Clear();
        _techniques.Clear();
        _idFamily.Dispose();
        _pipelineStateTracker.Dispose();
        base.Dispose();
    }

    public uint GetObjectID() => _idFamily.GetObjectID();

    public uint GetPipelineStateHash() => _pipelineStateTracker.GetPipelineStateHash();

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }

    public static uint GetNextFreeObjectID() => IDFamily<Material>.GetNextFreeObjectID();

    public void AcquireObjectID() => _idFamily.AcquireObjectID();

    public void ReleaseObjectID() => _idFamily.ReleaseObjectID();

    public bool AlphaToCoverage { get; private set; }

    public BiasParameters DepthBias { get; private set; }

    public FillMode FillMode { get; private set; } = FillMode.Solid;

    public CullMode CullMode { get; private set; } = CullMode.CounterClockwise;

    public bool LineAntiAlias { get; private set; }

    public bool Occlusion { get; private set; } = true;

    public string PixelShaderDefines { get; private set; } = string.Empty;

    public byte RenderOrder { get; private set; } = 128;

    public CullMode ShadowCullMode { get; private set; } = CullMode.CounterClockwise;

    public IReadOnlyDictionary<string, MaterialShaderParameter> ShaderParameters => _shaderParameters;

    public IReadOnlyDictionary<StringHash, MaterialTexture> Textures => _textures;

    public IReadOnlyList<TechniqueEntry> Techniques => _techniques;
    internal Dictionary<string, MaterialShaderParameter>.Enumerator EnumerateShaderParameters() => _shaderParameters.GetEnumerator();
    internal Dictionary<StringHash, MaterialTexture>.Enumerator EnumerateTextures() => _textures.GetEnumerator();
    internal List<TechniqueEntry>.Enumerator EnumerateTechniques() => _techniques.GetEnumerator();

    public uint ShaderParameterHash => _shaderParameterHash;

    public string VertexShaderDefines { get; private set; } = string.Empty;

    public Material Clone(string cloneName = "")
    {
        var clone = new Material();
        clone.CopyFrom(this);
        clone.SetName(cloneName);
        return clone;
    }

    public void CopyFrom(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        SetName(material.Name);
        AlphaToCoverage = material.AlphaToCoverage;
        CullMode = material.CullMode;
        DepthBias = material.DepthBias;
        FillMode = material.FillMode;
        LineAntiAlias = material.LineAntiAlias;
        Occlusion = material.Occlusion;
        PixelShaderDefines = material.PixelShaderDefines;
        RenderOrder = material.RenderOrder;
        ShadowCullMode = material.ShadowCullMode;
        VertexShaderDefines = material.VertexShaderDefines;
        _specular = material._specular;

        _techniques.Clear();
        foreach (var technique in material._techniques)
            _techniques.Add(technique);

        _textures.Clear();
        foreach (var texture in material._textures)
            _textures.Add(texture.Key, texture.Value);

        foreach (var parameter in _shaderParameters.Values)
            parameter.Dispose();
        _shaderParameters.Clear();
        foreach (var parameter in material._shaderParameters)
            _shaderParameters.Add(parameter.Key, (MaterialShaderParameter)parameter.Value.Clone());
        _shaderParameterHash = material._shaderParameterHash;
        RefreshMemoryUse();
        RefreshTextureEventSubscriptions();
        _pipelineStateTracker.MarkPipelineStateHashDirty();
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
        return GetShaderParameterAnimationInfo(name)?.Animation;
    }

    public WrapMode GetShaderParameterAnimationWrapMode(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetShaderParameterAnimationInfo(name)?.WrapMode ?? WrapMode.Loop;
    }

    public float GetShaderParameterAnimationSpeed(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetShaderParameterAnimationInfo(name)?.Speed ?? 0.0f;
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
        // If only one technique, no choice
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

        // If no suitable technique found, fallback to the last
        return _techniques.Count > 0 ? _techniques[^1].Technique : null;
    }

    /// <summary>
    /// Find the best technique for the specified drawable and material quality.
    /// </summary>
    public Technique? FindTechnique(Drawable drawable, MaterialQuality materialQuality)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return FindTechnique(materialQuality, drawable.GetLodDistance());
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
        return GetTexture(new StringHash(name));
    }

    public IReadOnlyDictionary<StringHash, MaterialTexture> GetTextures() => _textures;

    public void SetAlphaToCoverage(bool enable)
    {
        AlphaToCoverage = enable;
        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    public void SetCullMode(CullMode mode)
    {
        CullMode = mode;
        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    public void SetDepthBias(BiasParameters parameters)
    {
        parameters.Validate();
        DepthBias = parameters;
        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    public void SetFillMode(FillMode mode)
    {
        FillMode = mode;
        _pipelineStateTracker.MarkPipelineStateHashDirty();
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
        defines ??= string.Empty;
        if (PixelShaderDefines == defines)
            return;
        PixelShaderDefines = defines;
        ApplyShaderDefinesToTechniques();
        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    public void SetRenderOrder(byte order)
    {
        RenderOrder = order;
    }

    public void SetRenderOrder(uint order)
    {
        SetRenderOrder(unchecked((byte)order));
    }

    public void SetShadowCullMode(CullMode mode)
    {
        ShadowCullMode = mode;
        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    public void SetTexture(string name, Texture? texture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        SetTextureInternal(name, texture);
        RefreshTextureEventSubscriptions();
    }

    private void SetTextureInternal(string name, Texture? texture)
    {
        var hash = new StringHash(name);
        if (texture is null)
            _textures.Remove(hash);
        else
            _textures[hash] = new MaterialTexture(name, texture);
    }

    public void SetNumTechniques(uint num)
    {
        if (num == 0)
            return;
        while (_techniques.Count < num)
            _techniques.Add(default);
        while (_techniques.Count > num)
            _techniques.RemoveAt(_techniques.Count - 1);
    }

    public void SetTechnique(uint index, Technique? tech, MaterialQuality qualityLevel = MaterialQuality.Low, float lodDistance = 0.0f)
    {
        if (index >= _techniques.Count)
            return;
        _techniques[(int)index] = new TechniqueEntry(tech, qualityLevel, lodDistance);
        ApplyShaderDefinesToTechnique(index);
    }

    public void SetTechniques(IEnumerable<TechniqueEntry> techniques)
    {
        ArgumentNullException.ThrowIfNull(techniques);
        var copiedTechniques = techniques.ToArray();
        this._techniques.Clear();
        this._techniques.AddRange(copiedTechniques);
        SortTechniques();
        ApplyShaderDefinesToTechniques();
    }

    public void SetShaderParameter(string name, object? value, bool isCustom = false)
    {
        using var variant = Variant.FromObject(value);
        SetShaderParameter(name, variant, isCustom);
    }

    public void SetShaderParameter(string name, in Variant value, bool isCustom = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_shaderParameters.TryGetValue(name, out var parameter))
            parameter.SetValue(value, isCustom);
        else
            _shaderParameters[name] = new MaterialShaderParameter(name, value, isCustom);
        var oldSpecular = _specular;
        UpdateSpecularState(name, value);
        if (oldSpecular != _specular)
            _pipelineStateTracker.MarkPipelineStateHashDirty();
        if (!_batchedParameterUpdate)
        {
            RefreshShaderParameterHash();
            RefreshMemoryUse();
        }
    }

    public void SetShaderParameterAnimation(string name, ValueAnimation? animation, WrapMode wrapMode = WrapMode.Loop, float speed = 1.0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var info = GetShaderParameterAnimationInfo(name);

        if (animation is not null)
        {
            if (info is not null && ReferenceEquals(info.Animation, animation))
            {
                info.WrapMode = wrapMode;
                info.Speed = speed;
                return;
            }

            if (!_shaderParameters.ContainsKey(name))
                return;

            var nameHash = new StringHash(name);
            var animationInfo = new ShaderParameterAnimationInfo(this, name, animation, wrapMode, speed);
            if (_shaderParameterAnimationInfos.TryGetValue(nameHash, out var previousInfo))
            {
                _shaderParameterAnimationInfos[nameHash] = animationInfo;
                previousInfo.Dispose();
            }
            else
                _shaderParameterAnimationInfos[nameHash] = animationInfo;
            UpdateEventSubscription();
        }
        else if (info is not null)
        {
            _shaderParameterAnimationInfos.Remove(new StringHash(name));
            info.Dispose();
            UpdateEventSubscription();
        }
    }

    public void SetShaderParameterAnimationWrapMode(string name, WrapMode wrapMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var info = GetShaderParameterAnimationInfo(name);
        if (info is not null)
            info.WrapMode = wrapMode;
    }

    public void SetShaderParameterAnimationSpeed(string name, float speed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var info = GetShaderParameterAnimationInfo(name);
        if (info is not null)
            info.Speed = speed;
    }

    public void SetUVTransform(Vector2f offset, float rotation, float repeat)
    {
        SetUVTransform(offset, rotation, new Vector2f(repeat, repeat));
    }

    public void SetUVTransform(Vector2f offset, float rotation, Vector2f repeat)
    {
        var transform = Matrix3x4.Identity;
        transform = Matrix3x4.FromTranslation(new Vector3f(-0.5f, -0.5f, 0.0f)) * transform;
        transform = Matrix3x4.FromScale(new Vector3f(repeat.X, repeat.Y, 1.0f)) * transform;
        transform = new Matrix3x4(Vector3f.Zero, new Quaternion(rotation, Vector3f.Forward), Vector3f.One) * transform;
        transform = Matrix3x4.FromTranslation(new Vector3f(0.5f + offset.X, 0.5f + offset.Y, 0.0f)) * transform;
        SetShaderParameter("UOffset", new Vector4f(transform.M00, transform.M01, transform.M03, 0.0f));
        SetShaderParameter("VOffset", new Vector4f(transform.M10, transform.M11, transform.M13, 0.0f));
    }

    public void SetVertexShaderDefines(string defines)
    {
        defines ??= string.Empty;
        if (VertexShaderDefines == defines)
            return;
        VertexShaderDefines = defines;
        ApplyShaderDefinesToTechniques();
        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    public void SortTechniques()
    {
        _techniques.Sort();
    }

    public void RemoveShaderParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var oldSpecular = _specular;
        if (_shaderParameters.Remove(name, out var parameter))
        {
            parameter.Dispose();
            if (name == "MatSpecColor")
                _specular = false;
        }
        if (oldSpecular != _specular)
            _pipelineStateTracker.MarkPipelineStateHashDirty();
        RefreshShaderParameterHash();
        RefreshMemoryUse();
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

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (GetSubsystem<Graphics>() is null)
            return true;

        _loadXmlFile?.Dispose();
        _loadXmlFile = new XMLFile();
        if (_loadXmlFile.Load(source))
            return true;

        _loadXmlFile.Dispose();
        _loadXmlFile = null;
        ResetToDefaults();
        return false;
    }

    public override bool EndLoad()
    {
        if (GetSubsystem<Graphics>() is null)
            return true;

        if (_loadXmlFile is null)
            return false;

        try
        {
            return LoadXML(_loadXmlFile.GetRoot());
        }
        finally
        {
            _loadXmlFile.Dispose();
            _loadXmlFile = null;
        }
    }

    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        using var xml = new XMLFile();
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

        var cache = GetSubsystem<ResourceCache>()!;
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
            if (string.IsNullOrWhiteSpace(techniqueName))
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
            if (string.IsNullOrEmpty(slotName) && textureElement.HasAttribute("unit"))
                slotName = NormalizeTextureSlotName(textureElement.GetAttribute("unit"));

            var resourceName = textureElement.GetAttribute("name");
            if (Path.GetExtension(resourceName).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                var type = ParseTextureTypeXml(cache, resourceName);
                if (type == StringHash.Empty && textureElement.HasAttribute("type"))
                    type = textureElement.GetAttribute("type");

                if (type == ObjectReflection<Texture3D>.TypeId)
                    SetTextureInternal(slotName, cache.GetResourceOrNull<Texture3D>(resourceName));
                else if (type == ObjectReflection<Texture2DArray>.TypeId)
                    SetTextureInternal(slotName, cache.GetResourceOrNull<Texture2DArray>(resourceName));
                else
                    SetTextureInternal(slotName, cache.GetResourceOrNull<TextureCube>(resourceName));
            }
            else
                SetTextureInternal(slotName, cache.GetResourceOrNull<Texture2D>(resourceName));
        }
        RefreshTextureEventSubscriptions();

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
            foreach (var parameter in _shaderParameters)
            {
                hash = (hash ^ StringHash.Calculate(parameter.Key)) * 16777619u;
                hash = (hash ^ (uint)parameter.Value.VariantValue.GetHashCode()) * 16777619u;
                hash = (hash ^ (parameter.Value.IsCustom ? 1u : 0u)) * 16777619u;
            }

            _shaderParameterHash = hash;
        }
    }

    private void ApplyShaderDefinesToTechniques()
    {
        for (var i = 0; i < _techniques.Count; ++i)
            ApplyShaderDefinesToTechnique((uint)i);
    }

    private void ApplyShaderDefinesToTechnique(uint index)
    {
        var entry = _techniques[(int)index];
        if (entry.Original is null)
            return;

        entry.Technique = string.IsNullOrWhiteSpace(VertexShaderDefines) && string.IsNullOrWhiteSpace(PixelShaderDefines)
            ? entry.Original
            : entry.Original.CloneWithDefines(VertexShaderDefines, PixelShaderDefines);
        _techniques[(int)index] = entry;
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
            "5" or "volume" => "Volume",
            "6" or "custom1" => "Custom1",
            "7" or "custom2" => "Custom2",
            "8" or "lightramp" => "LightRamp",
            "9" or "lightshape" => "LightShape",
            "10" or "shadowmap" => "ShadowMap",
            "11" or "faceselect" => "FaceSelect",
            "12" or "indirection" => "Indirection",
            "13" or "depth" => "DepthBuffer",
            "14" or "light" => "LightBuffer",
            "15" or "zone" => "Reflection1",
            _ => name.Trim()
        };
    }

    private static StringHash ParseTextureTypeName(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "texture" => ObjectReflection<Texture2D>.TypeId,
            "cubemap" => ObjectReflection<TextureCube>.TypeId,
            "texture3d" => ObjectReflection<Texture3D>.TypeId,
            "texturearray" => ObjectReflection<Texture2DArray>.TypeId,
            _ => StringHash.Empty
        };
    }

    private static StringHash ParseTextureTypeXml(ResourceCache cache, string fileName)
    {
        using var textureXml = cache.GetTempResource<XMLFile>(fileName, false);
        return textureXml is not null ? ParseTextureTypeName(textureXml.GetRoot().Name) : StringHash.Empty;
    }

    private void ResetToDefaults()
    {
        ClearShaderParameterAnimationInfos();
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
        foreach (var parameter in _shaderParameters.Values)
            parameter.Dispose();
        _shaderParameters.Clear();
        _textures.Clear();
        RefreshTextureEventSubscriptions();
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
        RefreshMemoryUse();
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
        _finishedShaderParameterAnimationNames.Clear();
        foreach (var info in _shaderParameterAnimationInfos.Values)
        {
            if (info.Update(timeStep))
                _finishedShaderParameterAnimationNames.Add(info.Name);
        }

        for (var i = 0; i < _finishedShaderParameterAnimationNames.Count; ++i)
            SetShaderParameterAnimation(_finishedShaderParameterAnimationNames[i], null);
    }

    private void UpdateSpecularState(string name, in Variant value)
    {
        if (name != "MatSpecColor")
            return;

        switch (value.Type)
        {
            case VariantType.Vector3:
            {
                var vector = value.GetVector3();
                _specular = vector.X > 0.0f || vector.Y > 0.0f || vector.Z > 0.0f;
                break;
            }
            case VariantType.Vector4:
            {
                var vector = value.GetVector4();
                _specular = vector.X > 0.0f || vector.Y > 0.0f || vector.Z > 0.0f;
                break;
            }
        }
    }

    private void RefreshTextureEventSubscriptions()
    {
        UnsubscribeFromEvent<ResourceEvents.ReloadFinished>();
        foreach (var item in _textures.Values)
            SubscribeToEvent<ResourceEvents.ReloadFinished>(item.Texture, HandleTextureReloadFinished);

        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    private void HandleTextureReloadFinished(ref ResourceEvents.ReloadFinished eventData)
    {
        _pipelineStateTracker.MarkPipelineStateHashDirty();
    }

    private void RefreshMemoryUse()
    {
        MemoryUse = _techniques.Count * 32
            + _textures.Count * IntPtr.Size
            + _shaderParameters.Count * 48;
    }

    private void ClearShaderParameterAnimationInfos()
    {
        foreach (var info in _shaderParameterAnimationInfos.Values)
            info.Dispose();
        _shaderParameterAnimationInfos.Clear();
    }

    private uint RecalculatePipelineStateHash()
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
            CombineHash(ref hash, info.Texture.GetSamplerStateDesc().ToHash());
        }
        return Math.Max(1u, hash);
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
