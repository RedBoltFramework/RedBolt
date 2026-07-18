using System.Runtime.CompilerServices;

namespace RedBolt;

/// <summary>Group of selected drawables.</summary>
public class OutlineGroup : Component
{
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
            typeof(uint),
            self => ((OutlineGroup)self).GetRenderOrder(),
            (self, value) => ((OutlineGroup)self).SetRenderOrder(value is uint typed ? typed : 128u),
            128u);
        RegisterAttribute(
            "Is Debug",
            typeof(bool),
            self => ((OutlineGroup)self).IsDebug(),
            (self, value) => ((OutlineGroup)self).SetDebug(value is bool typed && typed),
            false);
        // TODO: Not resolved on load
        RegisterAttribute(
            "Drawables",
            typeof(VariantVector),
            self => ((OutlineGroup)self).GetDrawablesAttr(),
            (self, value) => ((OutlineGroup)self).SetDrawablesAttr((VariantVector)value!),
            new VariantVector());
    }

    public override void Dispose()
    {
        foreach (var material in _materials.Values)
            material.Dispose();
        _materials.Clear();
        _drawablesAttr.Dispose();
        _drawables.Clear();
        base.Dispose();
    }

    public override void ApplyAttributes()
    {
        var scene = Scene;
        if (_drawablesDirty && scene is not null)
        {
            _drawablesDirty = false;
            _drawables.Clear();
            foreach (var drawableId in _drawablesAttr)
            {
                if (scene.GetComponent(drawableId.GetUInt()) is Component component)
                {
                    if (component is Drawable drawable)
                        AddDrawable(drawable);
                }
            }
        }
    }

    /// <summary>Attributes for serialization.</summary>
    public void SetColor(in Color color)
    {
        if (_color != color)
        {
            _color = color;

            foreach (var material in _materials.Values)
                material.SetShaderParameter(ShaderConsts.Custom_OutlineColor.ToString(), _color.ToVector4(), true);
        }
    }

    public Color GetColor() => _color;

    public void SetRenderOrder(uint renderOrder)
    {
        if (_renderOrder != renderOrder)
        {
            _renderOrder = renderOrder;

            foreach (var material in _materials.Values)
                material.SetRenderOrder(_renderOrder);
        }
    }

    public uint GetRenderOrder() => _renderOrder;

    public void SetDebug(bool isDebug) => _isDebug = isDebug;

    public bool IsDebug() => _isDebug;

    public void SetDrawablesAttr(VariantVector drawables)
    {
        _drawables.Clear();
        if (!ReferenceEquals(drawables, _drawablesAttr))
        {
            _drawablesAttr.Dispose();
            foreach (var drawable in drawables)
                _drawablesAttr.Add((Variant)drawable.Clone());
        }
        _drawablesDirty = _drawablesAttr.Count != 0;
    }

    public VariantVector GetDrawablesAttr()
    {
        _drawablesAttr.Dispose();
        foreach (var drawable in _drawables)
        {
            if (drawable is not null)
                _drawablesAttr.Add(drawable.Id);
        }
        return _drawablesAttr;
    }

    /// <summary>Return cached artificial material with only resources and shader parameters set.</summary>
    public Material GetOutlineMaterial(Material referenceMaterial)
    {
        var key = new MaterialKey(referenceMaterial);
        if (_materials.TryGetValue(key, out var cachedMaterial))
            return cachedMaterial;

        var material = new Material(string.Empty);
        var shaderParameterEnumerator = referenceMaterial.EnumerateShaderParameters();
        while (shaderParameterEnumerator.MoveNext())
        {
            var nameValue = shaderParameterEnumerator.Current.Value;
            material.SetShaderParameter(nameValue.Name, nameValue.Value);
        }
        var textureEnumerator = referenceMaterial.EnumerateTextures();
        while (textureEnumerator.MoveNext())
        {
            var texture = textureEnumerator.Current.Value;
            material.SetTexture(texture.Name, texture.Texture);
        }

        material.SetShaderParameter(ShaderConsts.Custom_OutlineColor.ToString(), _color.ToVector4(), true);
        material.SetRenderOrder(_renderOrder);

        _materials[key] = material;
        return material;
    }

    /// <summary>Manage collection.</summary>
    public bool HasDrawables() => _drawables.Count != 0;

    public bool ContainsDrawable(Drawable drawable) => _drawables.Contains(drawable);

    public void ClearDrawables()
    {
        _drawables.Clear();
    }

    /// <summary>Check if Drawable is present in group.</summary>
    public bool HasDrawable(Drawable drawable)
    {
        return _drawables.Contains(drawable);
    }

    /// <summary>Add drawable. Returns true if drawable added.</summary>
    public bool AddDrawable(Drawable drawable)
    {
        return _drawables.Add(drawable);
    }

    /// <summary>Remove drawable. Returns true if drawable was removed.</summary>
    public bool RemoveDrawable(Drawable drawable)
    {
        return _drawables.Remove(drawable);
    }

    internal HashSet<Drawable>.Enumerator GetDrawableEnumerator() => _drawables.GetEnumerator();

    private struct MaterialKey : IEquatable<MaterialKey>
    {
        public uint ParametersHash;
        public uint ResourcesHash;

        public MaterialKey(Material material)
        {
            ParametersHash = 0;
            ResourcesHash = 0;

            var shaderParameterEnumerator = material.EnumerateShaderParameters();
            while (shaderParameterEnumerator.MoveNext())
            {
                var nameValue = shaderParameterEnumerator.Current;
                var nameHash = new StringHash(nameValue.Key);
                if (nameHash != ShaderConsts.Material_MatDiffColor.GetHash())
                {
                    uint hash = 0;
                    CombineHash(ref hash, nameHash.Value);
                    CombineHash(ref hash, nameValue.Value.VariantValue.ToHash());
                    ParametersHash += hash;
                }
            }

            var textureEnumerator = material.EnumerateTextures();
            while (textureEnumerator.MoveNext())
            {
                var texture = textureEnumerator.Current;
                uint hash = 0;
                CombineHash(ref hash, texture.Key.Value);
                CombineHash(ref hash, unchecked((uint)RuntimeHelpers.GetHashCode(texture.Value.Texture)));
                ResourcesHash += hash;
            }
        }

        public readonly bool Equals(MaterialKey rhs)
        {
            return ParametersHash == rhs.ParametersHash && ResourcesHash == rhs.ResourcesHash;
        }

        public override readonly bool Equals(object? obj) => obj is MaterialKey rhs && Equals(rhs);

        public readonly uint ToHash()
        {
            uint hash = 0;
            CombineHash(ref hash, ParametersHash);
            CombineHash(ref hash, ResourcesHash);
            return hash;
        }

        public override readonly int GetHashCode() => unchecked((int)ToHash());
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }

    private Color _color = Color.White;
    private uint _renderOrder = 128;
    private bool _isDebug;

    /// <summary>Selected drawables.</summary>
    private readonly HashSet<Drawable> _drawables = [];
    private bool _drawablesDirty;
    private readonly VariantVector _drawablesAttr = [];

    /// <summary>Cache of materials.</summary>
    private readonly Dictionary<MaterialKey, Material> _materials = [];
}
