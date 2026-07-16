namespace RedBolt;

/// <summary>
/// Group of selected drawables rendered into the outline buffer.
/// </summary>
public sealed class OutlineGroup : Component
{
    private static readonly string[] OutlinedPassNames = ["deferred", "deferred_decal", "base", "alpha"];
    private readonly HashSet<Drawable> _drawables = [];
    private readonly Dictionary<Material, CachedOutlineMaterial> _cachedMaterials = [];
    private Color _color = Color.White;
    private byte _renderOrder = 128;
    private bool _isDebug;
    private readonly List<uint> _drawablesAttr = [];
    private bool _drawablesDirty;

    public OutlineGroup()
    {
        RegisterAttribute(
            "Color",
            typeof(Color),
            self => ((OutlineGroup)self).GetColor(),
            (self, value) => ((OutlineGroup)self).SetColor(value is Color typed ? typed : Color.White),
            Color.White);
        RegisterAttribute(
            "Render Order",
            typeof(byte),
            self => ((OutlineGroup)self).GetRenderOrder(),
            (self, value) => ((OutlineGroup)self).SetRenderOrder(value switch
            {
                byte typed => typed,
                uint typed => typed,
                int typed when typed >= 0 => (uint)typed,
                _ => 128u
            }),
            (byte)128);
        RegisterAttribute(
            "Is Debug",
            typeof(bool),
            self => ((OutlineGroup)self).IsDebug(),
            (self, value) => ((OutlineGroup)self).SetDebug(value is bool typed && typed),
            false);
    }

    public bool HasDrawables() => _drawables.Count > 0;

    public bool ContainsDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return _drawables.Contains(drawable);
    }

    public void ClearDrawables()
    {
        _drawables.Clear();
    }

    public bool HasDrawable(Drawable drawable) => ContainsDrawable(drawable);

    public bool AddDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return _drawables.Add(drawable);
    }

    public bool RemoveDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return _drawables.Remove(drawable);
    }

    public IReadOnlyCollection<Drawable> GetDrawables() => _drawables;
    internal HashSet<Drawable>.Enumerator GetDrawableEnumerator() => _drawables.GetEnumerator();

    public void SetColor(Color color)
    {
        if (this._color == color)
            return;

        this._color = color;
        foreach (var cachedMaterial in _cachedMaterials.Values)
            cachedMaterial.Material.SetShaderParameter(ShaderConsts.Custom_OutlineColor, color.ToVector4(), isCustom: true);
    }

    public Color GetColor() => _color;

    public void SetRenderOrder(uint renderOrder)
    {
        var normalized = checked((byte)renderOrder);
        if (this._renderOrder == normalized)
            return;

        this._renderOrder = normalized;
        foreach (var cachedMaterial in _cachedMaterials.Values)
            cachedMaterial.Material.SetRenderOrder(normalized);
    }

    public byte GetRenderOrder() => _renderOrder;

    public void SetDebug(bool debug)
    {
        _isDebug = debug;
    }

    public bool IsDebug() => _isDebug;

    public override void ApplyAttributes()
    {
        if (!_drawablesDirty || Scene is not { } scene)
            return;

        _drawablesDirty = false;
        _drawables.Clear();
        foreach (var drawableId in _drawablesAttr)
        {
            if (scene.GetComponent(drawableId) is Drawable drawable)
                AddDrawable(drawable);
        }
    }

    public void SetDrawablesAttr(IEnumerable<uint>? drawables)
    {
        _drawables.Clear();
        _drawablesAttr.Clear();
        if (drawables is not null)
            _drawablesAttr.AddRange(drawables);
        _drawablesDirty = _drawablesAttr.Count != 0;
    }

    public IReadOnlyList<uint> GetDrawablesAttr()
    {
        _drawablesAttr.Clear();
        foreach (var drawable in _drawables)
            _drawablesAttr.Add(drawable.Id);
        return _drawablesAttr.ToArray();
    }

    public Material GetOutlineMaterial(Material referenceMaterial)
    {
        ArgumentNullException.ThrowIfNull(referenceMaterial);

        var sourceHash = CalculateMaterialHash(referenceMaterial);
        if (_cachedMaterials.TryGetValue(referenceMaterial, out var cached) && cached.SourceHash == sourceHash)
            return cached.Material;

        var outlineMaterial = CreateOutlineMaterial(referenceMaterial);
        _cachedMaterials[referenceMaterial] = new CachedOutlineMaterial(sourceHash, outlineMaterial);
        return outlineMaterial;
    }

    private Material CreateOutlineMaterial(Material referenceMaterial)
    {
        var outlineMaterial = new Material($"{referenceMaterial.Name}#outline");
        outlineMaterial.CopyFrom(referenceMaterial);
        outlineMaterial.SetAlphaToCoverage(false);
        outlineMaterial.SetFillMode(FillMode.Solid);
        outlineMaterial.SetRenderOrder(_renderOrder);
        outlineMaterial.SetShaderParameter(ShaderConsts.Custom_OutlineColor, _color.ToVector4(), isCustom: true);

        var outlineTechniques = new List<TechniqueEntry>(referenceMaterial.GetTechniques().Count);
        foreach (var entry in referenceMaterial.GetTechniques())
        {
            var clonedTechnique = entry.Technique?.Clone();
            if (clonedTechnique is not null)
                ConfigureOutlineTechnique(clonedTechnique, referenceMaterial);
            outlineTechniques.Add(new TechniqueEntry(clonedTechnique, entry.QualityLevel, entry.LodDistance));
        }
        outlineMaterial.SetTechniques(outlineTechniques);
        return outlineMaterial;
    }

    private static void ConfigureOutlineTechnique(Technique technique, Material referenceMaterial)
    {
        foreach (var passName in OutlinedPassNames)
        {
            var pass = technique.GetPass(passName);
            if (pass is null)
                continue;

            var needAlphaMask = (pass.IsAlphaMask || pass.BlendMode != BlendMode.Replace) && referenceMaterial.GetTexture("Albedo") is not null;
            pass.SetDepthWrite(false);
            pass.SetDepthTestMode(CompareMode.Always);
            pass.SetColorWrite(true);
            pass.SetBlendMode(BlendMode.Replace);
            pass.SetAlphaToCoverage(false);
            pass.SetCullMode(CullMode.None);
            pass.SetPixelShader("M_OutlinePixel");
            pass.SetPixelShaderDefines(needAlphaMask ? "ALPHAMASK" : string.Empty);
        }
    }

    private static uint CalculateMaterialHash(Material material)
    {
        uint hash = 0;
        CombineHash(ref hash, material.GetVertexShaderDefines());
        CombineHash(ref hash, material.GetPixelShaderDefines());
        CombineHash(ref hash, material.GetAlphaToCoverage());

        uint parametersHash = 0;
        var parameterEnumerator = material.EnumerateShaderParameters();
        while (parameterEnumerator.MoveNext())
        {
            var parameter = parameterEnumerator.Current;
            if (string.Equals(parameter.Key, "MatDiffColor", StringComparison.Ordinal))
                continue;

            uint entryHash = 0;
            CombineHash(ref entryHash, parameter.Key);
            CombineHash(ref entryHash, parameter.Value.Value);
            CombineHash(ref entryHash, parameter.Value.IsCustom);
            parametersHash += entryHash;
        }
        CombineHash(ref hash, parametersHash);

        uint texturesHash = 0;
        var textureEnumerator = material.EnumerateTextures();
        while (textureEnumerator.MoveNext())
        {
            var texture = textureEnumerator.Current;
            uint entryHash = 0;
            CombineHash(ref entryHash, texture.Key);
            CombineHash(ref entryHash, texture.Value.Texture);
            texturesHash += entryHash;
        }
        CombineHash(ref hash, texturesHash);

        var techniqueEnumerator = material.EnumerateTechniques();
        while (techniqueEnumerator.MoveNext())
        {
            var technique = techniqueEnumerator.Current;
            CombineHash(ref hash, technique.QualityLevel);
            CombineHash(ref hash, technique.LodDistance);
            CombineHash(ref hash, technique.Technique);
        }

        return hash;
    }

    private sealed record CachedOutlineMaterial(uint SourceHash, Material Material);

    private static void CombineHash<T>(ref uint hash, T value)
    {
        hash = unchecked(hash * 37u + (uint)(value?.GetHashCode() ?? 0));
    }
}
