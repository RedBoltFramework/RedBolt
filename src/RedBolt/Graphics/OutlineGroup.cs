namespace RedBolt;

/// <summary>
/// Group of selected drawables rendered into the outline buffer.
/// </summary>
public sealed class OutlineGroup : Component
{
    private static readonly string[] OutlinedPassNames = ["deferred", "deferred_decal", "base", "alpha"];
    private readonly HashSet<Drawable> drawables = [];
    private readonly Dictionary<Material, CachedOutlineMaterial> cachedMaterials = [];
    private Color color = Color.White;
    private byte renderOrder = 128;
    private bool isDebug;

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

    public bool HasDrawables() => drawables.Count > 0;

    public bool ContainsDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return drawables.Contains(drawable);
    }

    public void ClearDrawables()
    {
        drawables.Clear();
    }

    public bool HasDrawable(Drawable drawable) => ContainsDrawable(drawable);

    public bool AddDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return drawables.Add(drawable);
    }

    public bool RemoveDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return drawables.Remove(drawable);
    }

    public IReadOnlyCollection<Drawable> GetDrawables() => drawables;

    public void SetColor(Color color)
    {
        if (this.color == color)
            return;

        this.color = color;
        foreach (var cachedMaterial in cachedMaterials.Values)
            cachedMaterial.Material.SetShaderParameter("OutlineColor", color.ToVector4(), isCustom: true);
    }

    public Color GetColor() => color;

    public void SetRenderOrder(uint renderOrder)
    {
        var normalized = checked((byte)renderOrder);
        if (this.renderOrder == normalized)
            return;

        this.renderOrder = normalized;
        foreach (var cachedMaterial in cachedMaterials.Values)
            cachedMaterial.Material.SetRenderOrder(normalized);
    }

    public byte GetRenderOrder() => renderOrder;

    public void SetDebug(bool debug)
    {
        isDebug = debug;
    }

    public bool IsDebug() => isDebug;

    public Material GetOutlineMaterial(Material referenceMaterial)
    {
        ArgumentNullException.ThrowIfNull(referenceMaterial);

        var sourceHash = CalculateMaterialHash(referenceMaterial);
        if (cachedMaterials.TryGetValue(referenceMaterial, out var cached) && cached.SourceHash == sourceHash)
            return cached.Material;

        var outlineMaterial = CreateOutlineMaterial(referenceMaterial);
        cachedMaterials[referenceMaterial] = new CachedOutlineMaterial(sourceHash, outlineMaterial);
        return outlineMaterial;
    }

    private Material CreateOutlineMaterial(Material referenceMaterial)
    {
        var outlineMaterial = new Material($"{referenceMaterial.Name}#outline");
        outlineMaterial.CopyFrom(referenceMaterial);
        outlineMaterial.SetAlphaToCoverage(false);
        outlineMaterial.SetFillMode(FillMode.Solid);
        outlineMaterial.SetRenderOrder(renderOrder);
        outlineMaterial.SetShaderParameter("OutlineColor", color.ToVector4(), isCustom: true);

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
        var hash = new HashCode();
        hash.Add(material.GetVertexShaderDefines(), StringComparer.Ordinal);
        hash.Add(material.GetPixelShaderDefines(), StringComparer.Ordinal);
        hash.Add(material.GetAlphaToCoverage());

        foreach (var parameter in material.GetShaderParameters().OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (string.Equals(parameter.Key, "MatDiffColor", StringComparison.Ordinal))
                continue;

            hash.Add(parameter.Key, StringComparer.Ordinal);
            hash.Add(parameter.Value.Value);
            hash.Add(parameter.Value.IsCustom);
        }

        foreach (var texture in material.GetTextures().OrderBy(static pair => pair.Value.Name, StringComparer.Ordinal))
        {
            hash.Add(texture.Value.Name, StringComparer.Ordinal);
            hash.Add(texture.Value.Texture);
        }

        foreach (var technique in material.GetTechniques())
        {
            hash.Add(technique.QualityLevel);
            hash.Add(technique.LodDistance);
            hash.Add(technique.Technique);
        }

        return unchecked((uint)hash.ToHashCode());
    }

    private sealed record CachedOutlineMaterial(uint SourceHash, Material Material);
}
