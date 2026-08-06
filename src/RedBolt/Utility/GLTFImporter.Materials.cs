// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;

/// <summary>Utility to import materials. Only referenced materials are saved.</summary>
internal sealed class GLTFMaterialImporter
{
    /// <summary>Specifies material variant of specific material.</summary>
    /// <remarks>If specific variant is not supported, highest available variant is selected.</remarks>
    public enum MaterialVariant
    {
        LitNormalMapMaterial,
        LitMaterial,
        UnlitMaterial,
        NumMaterialVariants
    }

    public GLTFMaterialImporter(GLTFImporterBase importerBase, GLTFTextureImporter textureImporter)
    {
        _base = importerBase;
        _model = _base.GetModel();
        _textureImporter = textureImporter;
        InitializeStandardTechniques();
        InitializeMaterials();
        _textureImporter.CookTextures();
    }

    public Material GetMaterial(int materialIndex, MaterialVariant variant)
    {
        _base.CheckMaterial(materialIndex);
        var material = _materials[materialIndex].Variants[(int)variant]!;
        _referencedMaterials.Add(material);
        return material;
    }

    public void SaveResources()
    {
        foreach (var material in _referencedMaterials)
            _base.SaveResource(material);
    }

    private void InitializeStandardTechniques()
    {
        _litOpaqueNormalMapTechnique = LoadTechnique("Techniques/LitOpaqueNormalMap.xml");
        _litOpaqueTechnique = LoadTechnique("Techniques/LitOpaque.xml");
        _unlitOpaqueTechnique = LoadTechnique("Techniques/UnlitOpaque.xml");
        if (_base.GetSettings().FadeTransparency)
        {
            _litTransparentNormalMapTechnique = LoadTechnique("Techniques/LitTransparentFadeNormalMap.xml");
            _litTransparentTechnique = LoadTechnique("Techniques/LitTransparentFade.xml");
        }
        else
        {
            _litTransparentNormalMapTechnique = LoadTechnique("Techniques/LitTransparentNormalMap.xml");
            _litTransparentTechnique = LoadTechnique("Techniques/LitTransparent.xml");
        }
        _unlitTransparentTechnique = LoadTechnique("Techniques/UnlitTransparent.xml");
    }

    private static Technique LoadTechnique(string name)
    {
        var technique = Context.Instance.GetSubsystem<ResourceCache>()!.GetResource<Technique>(name);
        return technique ?? throw new InvalidOperationException($"Cannot find standard technique '{name}'");
    }

    private void InitializeMaterials()
    {
        for (var i = 0; i < _model.Materials.Count; ++i)
        {
            var sourceMaterial = _model.Materials[i];
            var result = new ImportedMaterial();
            _materials.Add(result);
            var isLit = !IsUnlitMaterial(sourceMaterial);
            if (isLit && sourceMaterial.NormalTextureIndex >= 0)
                result.Variants[(int)MaterialVariant.LitNormalMapMaterial] = ImportMaterial(sourceMaterial, MaterialVariant.LitNormalMapMaterial);
            if (isLit)
                result.Variants[(int)MaterialVariant.LitMaterial] = ImportMaterial(sourceMaterial, MaterialVariant.LitMaterial);
            result.Variants[(int)MaterialVariant.UnlitMaterial] = ImportMaterial(sourceMaterial, MaterialVariant.UnlitMaterial);
            result.Variants[(int)MaterialVariant.LitMaterial] ??= result.Variants[(int)MaterialVariant.UnlitMaterial];
            result.Variants[(int)MaterialVariant.LitNormalMapMaterial] ??= result.Variants[(int)MaterialVariant.LitMaterial];
        }
    }

    private Material ImportMaterial(Tg.Material sourceMaterial, MaterialVariant variant)
    {
        var material = new Material();
        InitializeTechnique(material, sourceMaterial, variant);
        InitializeBaseColor(material, sourceMaterial);
        switch (variant)
        {
        case MaterialVariant.UnlitMaterial:
            InitializeMaterialName(material, sourceMaterial, "_Unlit");
            break;
        case MaterialVariant.LitMaterial:
            InitializeMaterialName(material, sourceMaterial, "_Lit");
            InitializeRoughnessMetallicOcclusion(material, sourceMaterial);
            InitializeEmissiveMap(material, sourceMaterial);
            break;
        case MaterialVariant.LitNormalMapMaterial:
            InitializeMaterialName(material, sourceMaterial, "_LitNormalMap");
            InitializeRoughnessMetallicOcclusion(material, sourceMaterial);
            InitializeNormalMap(material, sourceMaterial);
            InitializeEmissiveMap(material, sourceMaterial);
            break;
        }
        _base.AddToResourceCache(material);
        return material;
    }

    private void InitializeTechnique(Material material, Tg.Material sourceMaterial, MaterialVariant variant)
    {
        var isLit = !IsUnlitMaterial(sourceMaterial);
        var alphaMode = sourceMaterial.AlphaMode.ToString();
        var isOpaque = alphaMode == "OPAQUE";
        var isAlphaMask = alphaMode == "MASK";
        var isTransparent = alphaMode == "BLEND";
        if (!isOpaque && !isAlphaMask && !isTransparent)
            throw new InvalidOperationException($"Unknown alpha mode '{sourceMaterial.AlphaMode}'");
        var litNormalMapTechnique = isOpaque || isAlphaMask ? _litOpaqueNormalMapTechnique : _litTransparentNormalMapTechnique;
        var litTechnique = isOpaque || isAlphaMask ? _litOpaqueTechnique : _litTransparentTechnique;
        var unlitTechnique = isOpaque || isAlphaMask ? _unlitOpaqueTechnique : _unlitTransparentTechnique;
        var shaderDefines = string.Empty;
        if (isAlphaMask)
        {
            shaderDefines += "ALPHAMASK ";
            material.SetShaderParameter("AlphaCutoff", (float)sourceMaterial.AlphaCutoff);
        }
        if (isAlphaMask && sourceMaterial.AlphaCutoff != 0.5)
            Log.GetLogger().Warning("Material '{}' has non-standard alpha cutoff", sourceMaterial.Name);
        if (variant == MaterialVariant.LitNormalMapMaterial && sourceMaterial.NormalTextureIndex >= 0 && isLit)
        {
            shaderDefines += "PBR ";
            material.SetTechnique(0, litNormalMapTechnique, MaterialQuality.Medium);
            material.SetTechnique(1, litTechnique, MaterialQuality.Low);
        }
        else if (variant == MaterialVariant.LitMaterial && isLit)
        {
            shaderDefines += "PBR ";
            material.SetTechnique(0, litTechnique, MaterialQuality.Low);
        }
        else material.SetTechnique(0, unlitTechnique, MaterialQuality.Low);
        material.SetVertexShaderDefines(shaderDefines);
        material.SetPixelShaderDefines(shaderDefines);
        if (sourceMaterial.DoubleSided)
        {
            material.SetCullMode(CullMode.None);
            material.SetShadowCullMode(CullMode.None);
        }
    }

    private void InitializeBaseColor(Material material, Tg.Material sourceMaterial)
    {
        var values = sourceMaterial.PbrMetallicRoughnessBaseColorFactor;
        var baseColor = new Vector4f((float)values[0], (float)values[1], (float)values[2], (float)values[3]);
        material.SetShaderParameter(ShaderConsts.Material_MatDiffColor, new Color(baseColor).LinearToGamma().ToVector4());
        var textureIndex = sourceMaterial.PbrMetallicRoughnessBaseColorTextureIndex;
        if (textureIndex < 0)
            return;
        _base.CheckTexture(textureIndex);
        if (sourceMaterial.PbrMetallicRoughnessBaseColorTextureTexCoord != 0)
            Log.GetLogger().Warning("Material '{}' has non-standard UV for diffuse texture #{}", sourceMaterial.Name, textureIndex);
        material.SetTexture(ShaderResources.Albedo, _textureImporter.ReferenceTextureAsIs(textureIndex));
    }

    private void InitializeRoughnessMetallicOcclusion(Material material, Tg.Material sourceMaterial)
    {
        material.SetShaderParameter(ShaderConsts.Material_Metallic, (float)sourceMaterial.PbrMetallicRoughnessMetallicFactor);
        material.SetShaderParameter(ShaderConsts.Material_Roughness, (float)sourceMaterial.PbrMetallicRoughnessRoughnessFactor);
        // Occlusion and metallic-roughness textures are backed together,
        // ignore occlusion if is uses different UV.
        var occlusionTextureIndex = sourceMaterial.OcclusionTextureIndex;
        var metallicRoughnessTextureIndex = sourceMaterial.PbrMetallicRoughnessMetallicRoughnessTextureIndex;
        if (occlusionTextureIndex >= 0 && metallicRoughnessTextureIndex >= 0 &&
            sourceMaterial.OcclusionTextureTexCoord != sourceMaterial.PbrMetallicRoughnessMetallicRoughnessTextureTexCoord)
        {
            Log.GetLogger().Warning("Material '{}' uses different UV for metallic-roughness texture #{} and for occlusion texture #{}. Occlusion texture is ignored.",
                sourceMaterial.Name, metallicRoughnessTextureIndex, occlusionTextureIndex);
            occlusionTextureIndex = -1;
        }
        if (metallicRoughnessTextureIndex < 0 && occlusionTextureIndex < 0)
            return;
        if (metallicRoughnessTextureIndex >= 0 && sourceMaterial.PbrMetallicRoughnessMetallicRoughnessTextureTexCoord != 0)
            Log.GetLogger().Warning("Material '{}' has non-standard UV for metallic-roughness texture #{}", sourceMaterial.Name, metallicRoughnessTextureIndex);
        if (occlusionTextureIndex >= 0)
        {
            if (sourceMaterial.OcclusionTextureTexCoord != 0)
                Log.GetLogger().Warning("Material '{}' has non-standard UV for occlusion texture #{}", sourceMaterial.Name, occlusionTextureIndex);
            if (sourceMaterial.OcclusionTextureStrength != 1.0)
                Log.GetLogger().Warning("Material '{}' has non-default occlusion strength for occlusion texture #{}", sourceMaterial.Name, occlusionTextureIndex);
            // TODO: Add support in standard shader
            material.SetShaderParameter("OcclusionStrength", (float)sourceMaterial.OcclusionTextureStrength);
        }
        material.SetTexture(ShaderResources.Properties,
            _textureImporter.ReferenceRoughnessMetallicOcclusionTexture(metallicRoughnessTextureIndex, occlusionTextureIndex));
    }

    private void InitializeNormalMap(Material material, Tg.Material sourceMaterial)
    {
        var normalTextureIndex = sourceMaterial.NormalTextureIndex;
        if (normalTextureIndex < 0)
            return;
        _base.CheckTexture(normalTextureIndex);
        if (sourceMaterial.NormalTextureTexCoord != 0)
            Log.GetLogger().Warning("Material '{}' has non-standard UV for normal texture #{}", sourceMaterial.Name, normalTextureIndex);
        material.SetShaderParameter(ShaderConsts.Material_NormalScale, (float)sourceMaterial.NormalTextureScale);
        material.SetTexture(ShaderResources.Normal, _textureImporter.ReferenceTextureAsIs(normalTextureIndex));
    }

    private void InitializeEmissiveMap(Material material, Tg.Material sourceMaterial)
    {
        var values = sourceMaterial.EmissiveFactor;
        var emissiveColor = new Vector3f((float)values[0], (float)values[1], (float)values[2]);
        material.SetShaderParameter(ShaderConsts.Material_MatEmissiveColor, new Color(emissiveColor).LinearToGamma().ToVector3());
        var emissiveTextureIndex = sourceMaterial.EmissiveTextureIndex;
        if (emissiveTextureIndex < 0)
            return;
        _base.CheckTexture(emissiveTextureIndex);
        if (sourceMaterial.EmissiveTextureTexCoord != 0)
            Log.GetLogger().Warning("Material '{}' has non-standard UV for emissive texture #{}", sourceMaterial.Name, emissiveTextureIndex);
        material.SetTexture(ShaderResources.Emission, _textureImporter.ReferenceTextureAsIs(emissiveTextureIndex));
    }

    private void InitializeMaterialName(Material material, Tg.Material sourceMaterial, string suffix) =>
        material.SetName(_base.GetResourceName(sourceMaterial.Name.ToString(), "Materials/", "Material", suffix + ".xml"));

    private static bool IsUnlitMaterial(Tg.Material sourceMaterial) =>
        sourceMaterial.Extensions.ContainsKey("KHR_materials_unlit");

    private readonly GLTFImporterBase _base;
    private readonly Tg.Model _model;
    private readonly GLTFTextureImporter _textureImporter;
    private Technique _litOpaqueNormalMapTechnique = null!;
    private Technique _litOpaqueTechnique = null!;
    private Technique _unlitOpaqueTechnique = null!;
    private Technique _litTransparentNormalMapTechnique = null!;
    private Technique _litTransparentTechnique = null!;
    private Technique _unlitTransparentTechnique = null!;

    private sealed class ImportedMaterial
    {
        public Material?[] Variants = new Material?[(int)MaterialVariant.NumMaterialVariants];
    }

    private readonly List<ImportedMaterial> _materials = [];
    private readonly HashSet<Material> _referencedMaterials = [];
}
