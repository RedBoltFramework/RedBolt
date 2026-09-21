// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

/// <summary>SystemUI widget used to edit materials.</summary>
public class MaterialInspectorWidget : RedObject
{
    public event Action? OnEditBegin;
    public event Action? OnEditEnd;

    public MaterialInspectorWidget(IReadOnlyList<Material> materials)
    {
        _previewScene = new Scene();
        _previewWidget = new SceneRendererToTexture(_previewScene);
        _materials = new List<Material>(materials);

        System.Diagnostics.Debug.Assert(_materials.Count != 0);
        InitializePreviewScene();
        ApplyPreviewSettings();
    }

    public override void Dispose()
    {
        _tooltipBuffer.Dispose();
        _textBuffer.Dispose();
        _textureReference.Dispose();
        ClearPendingShaderParameters();
        _shaderParameterNames.Clear();
        _customTextureUnits.Clear();
        _pendingSetTextures.Clear();
        ClearPendingProperties();
        _sortedTechniqueEntries.Clear();
        _techniqueEntries.Clear();
        _materials.Clear();
        _defaultTechnique = null;
        _sortedTechniques.Clear();
        _techniques.Clear();
        _previewWidget.Dispose();
        _previewScene.Dispose();
        _previewSkyboxMaterial?.Dispose();
        _previewSkyboxMaterial = null;
        OnEditBegin = null;
        OnEditEnd = null;
        base.Dispose();
    }

    public void UpdateTechniques(string path)
    {
        var cache = GetSubsystem<ResourceCache>();
        var techniques = new List<string>();
        cache.Scan(techniques, path, "*.xml", ScanFlags.ScanFiles | ScanFlags.ScanRecursive);

        _techniques.Clear();
        _sortedTechniques.Clear();

        foreach (var relativeName in techniques)
        {
            var resourceName = (path.EndsWith('/') ? path : path + "/") + relativeName;
            var description = new TechniqueDesc
            {
                ResourceName = resourceName,
                DisplayName = relativeName[..^4],
                Technique = cache.GetResourceOrNull<Technique>(resourceName),
            };
            description.Deprecated = IsTechniqueDeprecated(description.ResourceName);
            if (description.Technique is not null)
            {
                _techniques[description.ResourceName] = description;
                _sortedTechniques.Add(description);
            }
        }

        _sortedTechniques.Sort();

        _defaultTechnique = null;
        if (_techniques.TryGetValue(_defaultTechniqueName, out var defaultTechnique))
            _defaultTechnique = defaultTechnique;
        else if (_sortedTechniques.Count != 0)
        {
            Log.GetLogger().LogWarning(
                "Could not find default technique '{DefaultTechniqueName}'", _defaultTechniqueName);
            _defaultTechnique = _sortedTechniques[0];
        }
    }

    public void RenderTitle()
    {
        if (_materials.Count == 1)
            _textBuffer.Set(_materials[0].GetName());
        else
            _textBuffer.FormatInPlace($"{_materials.Count} materials");
        ImGui.TextUnformatted(_textBuffer.ContentBytes);
    }

    private static readonly CString _forceSaveLabel = new($"{Icons.FloppyDisk} Force Save");

    public void RenderContent()
    {
        _pendingSetTechniques = false;
        _pendingSetTextures.Clear();
        ClearPendingShaderParameters();
        ClearPendingProperties();

        var hasPreview = _materials.Count == 1;

        if (hasPreview)
        {
            RenderPreview();
            ImGui.BeginChild("Content"u8);
        }

        RenderTechniques();
        RenderProperties();
        RenderTextures();
        RenderShaderParameters();

        ImGui.Separator();
        var forceSave = ImGui.Button(_forceSaveLabel);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Materials are always saved on edit. You can force save even if there are no changes."u8);
        }

        if (hasPreview)
            ImGui.EndChild();

        if (_pendingSetTechniques)
        {
            OnEditBegin?.Invoke();
            foreach (var material in _materials)
                material.SetTechniques(_techniqueEntries);
            OnEditEnd?.Invoke();
        }

        if (_pendingSetTextures.Count != 0)
        {
            OnEditBegin?.Invoke();
            foreach (var material in _materials)
            {
                foreach (var (name, texture) in _pendingSetTextures)
                    material.SetTexture(name, texture);
            }
            OnEditEnd?.Invoke();
        }

        if (_pendingSetShaderParameters.Count != 0)
        {
            OnEditBegin?.Invoke();
            foreach (var material in _materials)
            {
                foreach (var parameter in _pendingSetShaderParameters)
                {
                    if (!parameter.Value.IsEmpty)
                    {
                        var value = parameter.Value;
                        material.SetShaderParameter(parameter.Name, in value);
                    }
                    else
                        material.RemoveShaderParameter(parameter.Name);
                }
            }
            OnEditEnd?.Invoke();
        }

        if (_pendingSetProperties.Count != 0)
        {
            OnEditBegin?.Invoke();
            foreach (var material in _materials)
            {
                foreach (var property in _pendingSetProperties)
                {
                    var value = property.Value;
                    property.Description.Setter(material, in value);
                }
            }
            OnEditEnd?.Invoke();
        }

        if (forceSave)
        {
            OnEditBegin?.Invoke();
            OnEditEnd?.Invoke();
        }
    }

    public IReadOnlyList<Material> GetMaterials() => _materials;

    private sealed class TechniqueDesc : IComparable<TechniqueDesc>
    {
        public string DisplayName = string.Empty;
        public string ResourceName = string.Empty;
        public Technique? Technique;
        public bool Deprecated;

        public int CompareTo(TechniqueDesc? other)
        {
            if (other is null)
                return 1;
            var deprecatedComparison = Deprecated.CompareTo(other.Deprecated);
            return deprecatedComparison != 0
                ? deprecatedComparison
                : string.Compare(DisplayName, other.DisplayName, StringComparison.Ordinal);
        }
    }

    private delegate Variant PropertyGetter(Material material);
    private delegate void PropertySetter(Material material, in Variant value);

    private sealed class PropertyDesc
    {
        public string Name = string.Empty;
        public Variant DefaultValue;
        public PropertyGetter Getter = null!;
        public PropertySetter Setter = null!;
        public string Hint = string.Empty;
        public Widgets.EditVariantOptions Options;
    }

    private static readonly PropertyDesc VertexDefinesProperty = new()
    {
        Name = "Vertex Defines",
        DefaultValue = new Variant(string.Empty),
        Getter = static material => new Variant(material.GetVertexShaderDefines()),
        Setter = static (Material material, in Variant value) => material.SetVertexShaderDefines(value.GetString()),
        Hint = "Additional shader defines applied to vertex shader. Should be space-separated list of DEFINES. "
            + "Example: VOLUMETRIC SOFTPARTICLES",
    };

    private static readonly PropertyDesc PixelDefinesProperty = new()
    {
        Name = "Pixel Defines",
        DefaultValue = new Variant(string.Empty),
        Getter = static material => new Variant(material.GetPixelShaderDefines()),
        Setter = static (Material material, in Variant value) => material.SetPixelShaderDefines(value.GetString()),
        Hint = "Additional shader defines applied to pixel shader. Should be space-separated list of DEFINES. "
            + "Example: VOLUMETRIC SOFTPARTICLES",
    };

    private static readonly StringVector CullModes =
        ["Cull None", "Cull Back Faces", "Cull Front Faces"];

    private static readonly StringVector FillModes = ["Solid", "Wireframe", "Points"];

    private static readonly PropertyDesc[] Properties =
    [
        new()
        {
            Name = "Cull Mode",
            DefaultValue = new Variant((int)CullMode.CounterClockwise),
            Getter = static material => new Variant((int)material.GetCullMode()),
            Setter = static (Material material, in Variant value) => material.SetCullMode((CullMode)value.GetInt()),
            Hint = "Cull mode used to render primary geometry with this material",
            Options = new Widgets.EditVariantOptions().Enum(CullModes),
        },
        new()
        {
            Name = "Shadow Cull Mode",
            DefaultValue = new Variant((int)CullMode.CounterClockwise),
            Getter = static material => new Variant((int)material.GetShadowCullMode()),
            Setter = static (Material material, in Variant value) => material.SetShadowCullMode((CullMode)value.GetInt()),
            Hint = "Cull mode used to render shadow geometry with this material",
            Options = new Widgets.EditVariantOptions().Enum(CullModes),
        },
        new()
        {
            Name = "Fill Mode",
            DefaultValue = new Variant((int)FillMode.Solid),
            Getter = static material => new Variant((int)material.GetFillMode()),
            Setter = static (Material material, in Variant value) => material.SetFillMode((FillMode)value.GetInt()),
            Hint = "Geometry fill mode. Mobiles support only Solid fill mode!",
            Options = new Widgets.EditVariantOptions().Enum(FillModes),
        },

        new()
        {
            Name = "Alpha To Coverage",
            DefaultValue = new Variant(false),
            Getter = static material => new Variant(material.GetAlphaToCoverage()),
            Setter = static (Material material, in Variant value) => material.SetAlphaToCoverage(value.GetBool()),
            Hint = "Whether to treat output alpha as MSAA coverage. It can be used by custom shaders for antialiased alpha cutout.",
        },
        new()
        {
            Name = "Line Anti Alias",
            DefaultValue = new Variant(false),
            Getter = static material => new Variant(material.GetLineAntiAlias()),
            Setter = static (Material material, in Variant value) => material.SetLineAntiAlias(value.GetBool()),
            Hint = "Whether to enable alpha-based line anti-aliasing for materials applied to line geometry",
        },
        new()
        {
            Name = "Render Order",
            DefaultValue = new Variant(128),
            Getter = static material => new Variant((int)material.GetRenderOrder()),
            Setter = static (Material material, in Variant value) => material.SetRenderOrder(unchecked((byte)value.GetInt())),
            Hint = "Global render order of the material. Materials with lower order are rendered first.",
            Options = new Widgets.EditVariantOptions().Range(0, 255),
        },
        new()
        {
            Name = "Occlusion",
            DefaultValue = new Variant(true),
            Getter = static material => new Variant(material.GetOcclusion()),
            Setter = static (Material material, in Variant value) => material.SetOcclusion(value.GetBool()),
            Hint = "Whether to render geometry with this material to occlusion buffer",
        },

        new()
        {
            Name = "Constant Bias",
            DefaultValue = new Variant(0.0f),
            Getter = static material => new Variant(material.GetDepthBias().ConstantBias),
            Setter = static (Material material, in Variant value) =>
            {
                var temporary = material.GetDepthBias();
                temporary.ConstantBias = value.GetFloat();
                material.SetDepthBias(temporary);
            },
            Hint = "Constant value added to pixel depth affecting geometry visibility behind or in front of obstacles",
            Options = new Widgets.EditVariantOptions().Step(0.000001).Range(-1.0f, 1.0f),
        },
        new()
        {
            Name = "Slope Scaled Bias",
            DefaultValue = new Variant(0.0f),
            Getter = static material => new Variant(material.GetDepthBias().SlopeScaledBias),
            Setter = static (Material material, in Variant value) =>
            {
                var temporary = material.GetDepthBias();
                temporary.SlopeScaledBias = value.GetFloat();
                material.SetDepthBias(temporary);
            },
            Hint = "You probably don't want to change this",
        },
        new()
        {
            Name = "Normal Offset",
            DefaultValue = new Variant(0.0f),
            Getter = static material => new Variant(material.GetDepthBias().NormalOffset),
            Setter = static (Material material, in Variant value) =>
            {
                var temporary = material.GetDepthBias();
                temporary.NormalOffset = value.GetFloat();
                material.SetDepthBias(temporary);
            },
            Hint = "You probably don't want to change this",
        },
    ];

    private sealed class TextureUnitDesc
    {
        public string Name = string.Empty;
        public string Hint = string.Empty;
    }

    private static readonly TextureUnitDesc[] TextureUnits =
    [
        new() { Name = ShaderResources.Albedo, Hint = "Albedo map or Diffuse texture with optional alpha channel." },
        new() { Name = ShaderResources.Normal, Hint = "Normal map, ignored unless normal mapping is enabled." },
        new() { Name = ShaderResources.Properties, Hint = "Roughness-Metalness-Occlusion map or Specular map." },
        new() { Name = ShaderResources.Emission, Hint = "Emission map. May also be used as AO map for legacy materials." },
        new() { Name = ShaderResources.Reflection0, Hint = "Reflection map override." },
    ];

    // Keep those global so that they are not reset on material change.
    // TODO: Better solution?
    [ThreadStatic] private static bool _previewSkyboxEnabled = true;
    [ThreadStatic] private static bool _previewLightEnabled = true;

    private static readonly Vector3f DefaultPreviewCameraPosition = Vector3f.Back * 2.0f;

    private static readonly StringVector AllowedTextureTypes =
        [nameof(Texture2D), nameof(Texture2DArray), nameof(TextureCube), nameof(Texture3D)];

    private static Dictionary<string, Variant>? _defaultShaderParameterValues;

    private static readonly (string Label, Variant DefaultValue)[] ShaderParameterTypes =
    [
        ("vec4 or rgba", new Variant(Color.White.ToVector4())),
        ("vec3 or rgb", new Variant(Vector3f.Zero)),
        ("vec2", new Variant(Vector2f.Zero)),
        ("float", new Variant(0.0f)),
    ];

    private static readonly string[] QualityLevels = ["Q Low", "Q Medium", "Q High", "Q Max"];

    private static string _newUnit = string.Empty;

    private static IReadOnlyDictionary<string, Variant> GetDefaultShaderParameterValues()
    {
        if (_defaultShaderParameterValues is not null)
            return _defaultShaderParameterValues;

        using var material = new Material();
        var result = new Dictionary<string, Variant>(StringComparer.Ordinal);
        foreach (var description in material.GetShaderParameters().Values)
            result[description.Name] = description.VariantValue.Clone();
        _defaultShaderParameterValues = result;
        return _defaultShaderParameterValues;
    }

    private static bool IsDefaultValue(string name, in Variant value)
    {
        var defaultValues = GetDefaultShaderParameterValues();
        return defaultValues.TryGetValue(name, out var defaultValue) && defaultValue == value;
    }

    private void InitializePreviewScene()
    {
        var cache = GetSubsystem<ResourceCache>();

        _previewScene.CreateComponent<Octree>();

        var skyboxNode = _previewScene.CreateChild("Skybox");
        var skybox = skyboxNode.CreateComponent<Skybox>();
        skybox.SetModel(cache.Get<Model>("Models/Box.mdl"));
        _previewSkyboxMaterial = cache.Get<Material>("Materials/DefaultSkybox.xml").Clone();
        skybox.SetMaterial(_previewSkyboxMaterial);

        var zoneNode = _previewScene.CreateChild("Global Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(Color.Black);
        zone.SetBackgroundBrightness(1.0f);
        zone.SetZoneTexture(cache.Get<TextureCube>("Textures/DefaultSkybox.xml"));

        var lightNode = _previewScene.CreateChild("Light");
        lightNode.SetDirection(new Vector3f(1.0f, -3.0f, 1.0f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);

        var cameraNode = _previewWidget.CameraNode!;
        cameraNode.SetPosition(DefaultPreviewCameraPosition);
        cameraNode.CreateComponent<MoveAndOrbitComponent>();

        var modelNode = _previewScene.CreateChild("Model");
        var model = modelNode.CreateComponent<StaticModel>();
        model.SetModel(cache.Get<Model>("Models/Sphere.mdl"));
    }

    private void ApplyPreviewSettings()
    {
        var cache = GetSubsystem<ResourceCache>();
        var renderer = GetSubsystem<Renderer>();

        var skybox = _previewScene.FindComponent<Skybox>()!;
        var zone = _previewScene.FindComponent<Zone>()!;

        Texture? defaultTexture = cache.Get<TextureCube>("Textures/DefaultSkybox.xml");
        Texture? emptyTexture = renderer.GetBlackCubeMap();

        var skyboxMaterial = skybox.GetMaterial();
        skyboxMaterial!.SetTexture(
            ShaderResources.Albedo, _previewSkyboxEnabled ? defaultTexture : emptyTexture);
        zone.SetZoneTexture(_previewSkyboxEnabled ? defaultTexture : null);

        var light = _previewScene.FindComponent<Light>()!;
        light.SetBrightness(_previewSkyboxEnabled ? 0.5f : 1.0f);
        light.SetEnabled(_previewLightEnabled);
    }

    // TODO: port UpdateCachedTechniques when its rbfx implementation exists.
    // void UpdateCachedTechniques();

    private static readonly CString _toggleSkyboxLabel = new($"{Icons.Cloud}##ToggleSkybox");
    private static readonly CString _toggleLightLabel = new($"{Icons.Lightbulb}##ToggleLight");
    private static readonly CString _resetCameraLabel = new($"{Icons.ArrowsLeftRightToLine}##ResetCamera");

    private void RenderPreview()
    {
        if (_materials.Count != 1)
            return;

        var workQueue = GetSubsystem<WorkQueue>();

        _previewWidget.SetActive(true);
        workQueue.PostDelayedTaskForMainThread(
            () => _previewWidget.SetActive(false));

        var camera = _previewWidget.Camera!;
        var cameraNode = camera.Node;
        var moveAndOrbit = cameraNode.GetComponent<MoveAndOrbitComponent>()!;

        var availableWidth = ImGui.GetContentRegionAvail().X;
        var textureHeight = MathF.Min(ImGui.GetContentRegionAvail().X, 250.0f);
        var textureSize = Vector2f.RoundToInt(new Vector2f(availableWidth, textureHeight));

        _previewWidget.SetTextureSize(textureSize);
        _previewWidget.Update();

        var sceneTexture = _previewWidget.Texture;
        var imageBegin = ImGui.GetCursorPos();
        Widgets.ImageItem(
            sceneTexture,
            _textureReference,
            new Vector2f(sceneTexture.Width, sceneTexture.Height));
        var imageEnd = ImGui.GetCursorPos();

        var distance = cameraNode.GetPosition().Length;
        if (ImGui.IsItemHovered())
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.ImGuiMouseButtonRight))
            {
                var mouseDelta = ImGui.GetIO().MouseDelta;
                moveAndOrbit.SetYaw(moveAndOrbit.GetYaw() + mouseDelta.X * 0.9f);
                moveAndOrbit.SetPitch(moveAndOrbit.GetPitch() + mouseDelta.Y * 0.9f);
            }

            if (MathF.Abs(ImGui.GetIO().MouseWheel) > 0.05f)
            {
                if (ImGui.GetIO().MouseWheel > 0.0f)
                    distance *= 0.8f;
                else
                    distance *= 1.3f;
            }
        }
        distance = MathDefs.Clamp(distance, 1.01f, 3.0f);

        cameraNode.SetRotation(moveAndOrbit.GetYawPitchRotation());
        cameraNode.SetPosition(distance * (cameraNode.GetRotation() * Vector3f.Back));

        var previewSettingsDirty = false;
        var style = ImGui.GetStyle();
        ImGui.SetCursorPos(imageBegin + style.FramePadding);

        if (Widgets.ToolbarButton(_toggleSkyboxLabel, "Toggle Skybox"u8, _previewSkyboxEnabled))
        {
            _previewSkyboxEnabled = !_previewSkyboxEnabled;
            previewSettingsDirty = true;
        }

        if (Widgets.ToolbarButton(_toggleLightLabel, "Toggle Light"u8, _previewLightEnabled))
        {
            _previewLightEnabled = !_previewLightEnabled;
            previewSettingsDirty = true;
        }

        if (Widgets.ToolbarButton(_resetCameraLabel, "Reset Camera"u8))
        {
            cameraNode.SetPosition(DefaultPreviewCameraPosition);
            moveAndOrbit.SetYaw(0.0f);
            moveAndOrbit.SetPitch(0.0f);
        }

        ImGui.SetCursorPos(imageEnd);

        var model = _previewScene.FindComponent<StaticModel>()!;
        model.SetMaterial(_materials[0]);

        if (previewSettingsDirty)
            ApplyPreviewSettings();
    }

    private void RenderTechniques()
    {
        using var guard = new IdScopeGuard("RenderTechniques"u8);

        var currentTechniqueEntries = _materials[0].GetTechniques();
        if (!TechniqueEntriesEqual(currentTechniqueEntries, _sortedTechniqueEntries))
        {
            CopyTechniqueEntries(currentTechniqueEntries, _techniqueEntries);
            CopyTechniqueEntries(currentTechniqueEntries, _sortedTechniqueEntries);
            _sortedTechniqueEntries.Sort();
        }

        var isUndefined = false;
        for (var index = 1; index < _materials.Count; ++index)
        {
            if (TechniqueEntriesEqual(_sortedTechniqueEntries, _materials[index].GetTechniques()))
                continue;
            isUndefined = true;
            break;
        }

        var title = !isUndefined
            ? "Techniques"u8
            : "Techniques (different for selected materials)"u8;
        if (!ImGui.CollapsingHeader(
                title, ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
        {
            return;
        }

        ImGui.BeginDisabled(isUndefined);
        if (RenderTechniqueEntries())
            _pendingSetTechniques = true;
        ImGui.EndDisabled();

        if (isUndefined)
        {
            ImGui.SameLine();
            if (ImGui.Button(Icons.CodeMergeU8))
                _pendingSetTechniques = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Override all materials' techniques and enable editing"u8);
        }

        ImGui.Separator();
    }

    private bool RenderTechniqueEntries()
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;

        int? pendingDelete = null;
        var modified = false;
        for (var entryIndex = 0; entryIndex < _techniqueEntries.Count; ++entryIndex)
        {
            using var guard = new IdScopeGuard(entryIndex);

            var entry = _techniqueEntries[entryIndex];

            if (EditTechniqueInEntry(ref entry, availableWidth))
                modified = true;

            if (ImGui.Button(Icons.TrashCanU8))
                pendingDelete = entryIndex;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Remove technique from material(s)"u8);
            ImGui.SameLine();

            if (EditDistanceInEntry(ref entry, availableWidth * 0.5f))
                modified = true;
            ImGui.SameLine();

            if (EditQualityInEntry(ref entry))
                modified = true;

            _techniqueEntries[entryIndex] = entry;
        }

        // Remove entry
        if (pendingDelete.HasValue && pendingDelete.Value < _techniqueEntries.Count)
        {
            _techniqueEntries.RemoveAt(pendingDelete.Value);

            modified = true;
        }

        // Add new entry
        if (_defaultTechnique is not null && ImGui.Button(Icons.SquarePlusU8))
        {
            var entry = new TechniqueEntry(_defaultTechnique.Technique);
            _techniqueEntries.Add(entry);

            modified = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add new technique to the material(s)"u8);

        CopyTechniqueEntries(_techniqueEntries, _sortedTechniqueEntries);
        _sortedTechniqueEntries.Sort();
        return modified;
    }

    private bool EditTechniqueInEntry(ref TechniqueEntry entry, float itemWidth)
    {
        var modified = false;

        _textBuffer.Set(GetTechniqueDisplayName(entry.Technique!.GetName()));

        ImGui.SetNextItemWidth(itemWidth);
        if (ImGui.BeginCombo(
                "##Technique"u8,
                _textBuffer.ContentBytes,
                ImGuiComboFlags.ImGuiComboFlagsHeightLarge))
        {
            var wasDeprecated = false;
            for (var techniqueIndex = 0; techniqueIndex < _sortedTechniques.Count; ++techniqueIndex)
            {
                var description = _sortedTechniques[techniqueIndex];

                using var guard = new IdScopeGuard(techniqueIndex);

                if (description.Deprecated && !wasDeprecated)
                {
                    ImGui.Separator();
                    wasDeprecated = true;
                }

                using var guardTextColor = new ColorScopeGuard(
                    ImGuiCol.ImGuiColText,
                    new Vector4f(0.3f, 1.0f, 0.0f, 1.0f),
                    !description.Deprecated);

                _textBuffer.Set(description.DisplayName);
                if (ImGui.Selectable(
                        _textBuffer.ContentBytes,
                        ReferenceEquals(entry.Technique, description.Technique)))
                {
                    entry.Technique = description.Technique;
                    entry.Original = description.Technique;
                    modified = true;
                }
            }
            ImGui.EndCombo();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Technique description from \"Techniques/*.xml\""u8);

        return modified;
    }

    private static bool EditDistanceInEntry(ref TechniqueEntry entry, float itemWidth)
    {
        var modified = false;

        ImGui.SetNextItemWidth(itemWidth);
        var lodDistance = entry.LodDistance;
        if (ImGui.DragFloat("##Distance"u8, ref lodDistance, 1.0f, 0.0f, 1000.0f, "%.1f"u8))
        {
            entry.LodDistance = lodDistance;
            modified = true;
        }
        ImGui.SameLine();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Minimum distance to the object at which the technique is used. Lower distances have higher priority."u8);
        }

        return modified;
    }

    private bool EditQualityInEntry(ref TechniqueEntry entry)
    {
        var modified = false;

        var qualityLevel = Math.Min((int)entry.QualityLevel, QualityLevels.Length - 1);
        _textBuffer.Set(QualityLevels[qualityLevel]);
        if (ImGui.BeginCombo("##Quality"u8, _textBuffer.ContentBytes))
        {
            for (var qualityLevelIndex = 0;
                qualityLevelIndex < QualityLevels.Length;
                ++qualityLevelIndex)
            {
                using var guard = new IdScopeGuard(qualityLevelIndex);
                _textBuffer.Set(QualityLevels[qualityLevelIndex]);
                if (ImGui.Selectable(
                        _textBuffer.ContentBytes, qualityLevel == qualityLevelIndex))
                {
                    entry.QualityLevel = (MaterialQuality)qualityLevelIndex;
                    if (entry.QualityLevel > MaterialQuality.High)
                        entry.QualityLevel = MaterialQuality.Max;
                    modified = true;
                }
            }
            ImGui.EndCombo();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Techniques with higher quality will not be used if lower quality is selected in the RenderPipeline settings"u8);
        }

        return modified;
    }

    private string GetTechniqueDisplayName(string resourceName)
    {
        return _techniques.TryGetValue(resourceName, out var description)
            ? description.DisplayName
            : string.Empty;
    }

    private bool IsTechniqueDeprecated(string resourceName)
    {
        return resourceName.StartsWith("Techniques/PBR/", StringComparison.Ordinal)
            || resourceName.StartsWith("Techniques/Diff", StringComparison.Ordinal)
            || resourceName.StartsWith("Techniques/NoTexture", StringComparison.Ordinal)
            || resourceName == "Techniques/BasicVColUnlitAlpha.xml"
            || resourceName == "Techniques/TerrainBlend.xml"
            || resourceName == "Techniques/VegetationDiff.xml"
            || resourceName == "Techniques/VegetationDiffUnlit.xml"
            || resourceName == "Techniques/Water.xml";
    }

    private void RenderProperties()
    {
        using var guard = new IdScopeGuard("RenderProperties"u8);

        if (!ImGui.CollapsingHeader(
                "Properties"u8,
                ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
        {
            return;
        }

        RenderShaderDefines();
        foreach (var property in Properties)
            RenderProperty(property);

        ImGui.Separator();
    }

    private void RenderShaderDefines()
    {
        var vertexDefines = new Variant(_materials[0].GetVertexShaderDefines());
        var pixelDefines = new Variant(_materials[0].GetPixelShaderDefines());
        try
        {
            var vertexDefinesUndefined = false;
            for (var index = 1; index < _materials.Count; ++index)
            {
                using var otherValue = new Variant(_materials[index].GetVertexShaderDefines());
                if (vertexDefines == otherValue)
                    continue;
                vertexDefinesUndefined = true;
                break;
            }

            var pixelDefinesUndefined = false;
            for (var index = 1; index < _materials.Count; ++index)
            {
                using var otherValue = new Variant(_materials[index].GetPixelShaderDefines());
                if (pixelDefines == otherValue)
                    continue;
                pixelDefinesUndefined = true;
                break;
            }

            if (!_separateShaderDefines.HasValue)
            {
                _separateShaderDefines = vertexDefinesUndefined
                    || pixelDefinesUndefined
                    || vertexDefines != pixelDefines;
            }
            if (!_separateShaderDefines.Value && vertexDefines != pixelDefines)
                _separateShaderDefines = false;

            // Render widget for vertex defines
            {
                using var guard = new IdScopeGuard("Vertex Defines"u8);

                _textBuffer.Set(VertexDefinesProperty.Name);
                Widgets.ItemLabel(
                    _textBuffer.ContentBytes,
                    Widgets.GetItemLabelColor(
                        vertexDefinesUndefined, vertexDefines.GetString().Length == 0));
                if (ImGui.IsItemHovered())
                {
                    _tooltipBuffer.Set(VertexDefinesProperty.Hint);
                    ImGui.SetTooltip(_tooltipBuffer.ContentBytes);
                }

                using var guardBackgroundColor = new ColorScopeGuard(
                    ImGuiCol.ImGuiColFrameBg,
                    Widgets.GetItemBackgroundColor(vertexDefinesUndefined),
                    vertexDefinesUndefined);

                if (Widgets.EditVariant(ref vertexDefines, in VertexDefinesProperty.Options))
                {
                    _pendingSetProperties.Add(
                        new PendingSetProperty(VertexDefinesProperty, vertexDefines.Clone()));
                    if (!_separateShaderDefines.Value)
                    {
                        _pendingSetProperties.Add(
                            new PendingSetProperty(PixelDefinesProperty, vertexDefines.Clone()));
                    }
                }
            }

            // Update whether the defines are synchronized
            var separateShaderDefines = _separateShaderDefines.Value;
            var pixelDefinesModeChanged = ImGui.Checkbox(
                "##SeparateShaderDefines"u8, ref separateShaderDefines);
            _separateShaderDefines = separateShaderDefines;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Enable separate editing for vertex and pixel defines"u8);
            ImGui.SameLine();

            // Render widget for pixel defines
            {
                using var guard = new IdScopeGuard("Pixel Defines"u8);

                ImGui.BeginDisabled(!_separateShaderDefines.Value);

                _textBuffer.Set(PixelDefinesProperty.Name);
                Widgets.ItemLabel(
                    _textBuffer.ContentBytes,
                    Widgets.GetItemLabelColor(
                        pixelDefinesUndefined, pixelDefines.GetString().Length == 0));
                if (ImGui.IsItemHovered())
                {
                    _tooltipBuffer.Set(PixelDefinesProperty.Hint);
                    ImGui.SetTooltip(_tooltipBuffer.ContentBytes);
                }

                using (var guardBackgroundColor = new ColorScopeGuard(
                    ImGuiCol.ImGuiColFrameBg,
                    Widgets.GetItemBackgroundColor(pixelDefinesUndefined),
                    pixelDefinesUndefined))
                {
                    if (Widgets.EditVariant(ref pixelDefines, in PixelDefinesProperty.Options))
                    {
                        _pendingSetProperties.Add(
                            new PendingSetProperty(PixelDefinesProperty, pixelDefines.Clone()));
                    }
                }

                ImGui.EndDisabled();
            }

            // Update pixel defines when separate defines are disabled
            if (pixelDefinesModeChanged && !_separateShaderDefines.Value)
            {
                _pendingSetProperties.Add(
                    new PendingSetProperty(PixelDefinesProperty, vertexDefines.Clone()));
            }
        }
        finally
        {
            pixelDefines.Dispose();
            vertexDefines.Dispose();
        }
    }

    private void RenderProperty(PropertyDesc description)
    {
        _textBuffer.Set(description.Name);
        using var guard = new IdScopeGuard(_textBuffer.ContentBytes);

        var value = description.Getter(_materials[0]);
        try
        {
            var isUndefined = false;
            for (var index = 1; index < _materials.Count; ++index)
            {
                using var otherValue = description.Getter(_materials[index]);
                if (value == otherValue)
                    continue;
                isUndefined = true;
                break;
            }

            _textBuffer.Set(description.Name);
            Widgets.ItemLabel(
                _textBuffer.ContentBytes,
                Widgets.GetItemLabelColor(isUndefined, value == description.DefaultValue));
            if (description.Hint.Length != 0 && ImGui.IsItemHovered())
            {
                _tooltipBuffer.Set(description.Hint);
                ImGui.SetTooltip(_tooltipBuffer.ContentBytes);
            }

            using var guardBackgroundColor = new ColorScopeGuard(
                ImGuiCol.ImGuiColFrameBg,
                Widgets.GetItemBackgroundColor(isUndefined),
                isUndefined);

            if (Widgets.EditVariant(ref value, in description.Options))
            {
                _pendingSetProperties.Add(
                    new PendingSetProperty(description, value.Clone()));
            }
        }
        finally
        {
            value.Dispose();
        }
    }

    private void RenderTextures()
    {
        using var guard = new IdScopeGuard("RenderTextures"u8);

        if (!ImGui.CollapsingHeader(
                "Textures"u8,
                ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
        {
            return;
        }

        foreach (var description in TextureUnits)
            RenderTextureUnit(description);

        ImGui.Separator();

        var customTextureUnits = GetCustomTextureUnits();
        foreach (var unit in customTextureUnits)
            RenderCustomTextureUnit(unit);
        RenderAddCustomTextureUnit(customTextureUnits);
    }

    private void RenderTextureUnit(TextureUnitDesc description)
    {
        _textBuffer.Set(description.Name);
        using var guard = new IdScopeGuard(_textBuffer.ContentBytes);

        var cache = GetSubsystem<ResourceCache>();

        var texture = _materials[0].GetTexture(description.Name);
        var isUndefined = false;
        for (var index = 1; index < _materials.Count; ++index)
        {
            if (ReferenceEquals(_materials[index].GetTexture(description.Name), texture))
                continue;
            isUndefined = true;
            break;
        }

        _textBuffer.Set(description.Name);
        Widgets.ItemLabel(
            _textBuffer.ContentBytes,
            Widgets.GetItemLabelColor(isUndefined, texture is null));
        if (ImGui.IsItemHovered())
        {
            _tooltipBuffer.Set(description.Hint);
            ImGui.SetTooltip(_tooltipBuffer.ContentBytes);
        }

        if (ImGui.Button(Icons.TrashCanU8))
            _pendingSetTextures.Add((description.Name, null));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Remove texture from this unit"u8);
        ImGui.SameLine();

        using var guardBackgroundColor = new ColorScopeGuard(
            ImGuiCol.ImGuiColFrameBg,
            Widgets.GetItemBackgroundColor(isUndefined),
            isUndefined);

        var textureType = new StringHash(texture?.GetTypeName() ?? nameof(Texture2D));
        var textureName = texture?.GetName() ?? string.Empty;
        if (Widgets.EditResourceRef(ref textureType, ref textureName, AllowedTextureTypes))
        {
            if (cache.GetResource(textureType, textureName) is Texture newTexture)
                _pendingSetTextures.Add((description.Name, newTexture));
            else
                _pendingSetTextures.Add((description.Name, null));
        }
    }

    private bool IsDefaultTextureUnit(string unit)
    {
        foreach (var description in TextureUnits)
        {
            if (description.Name == unit)
                return true;
        }
        return false;
    }

    private IReadOnlyList<string> GetCustomTextureUnits()
    {
        _customTextureUnits.Clear();

        foreach (var material in _materials)
        {
            foreach (var texture in material.GetTextures().Values)
            {
                if (!_customTextureUnits.Contains(texture.Name))
                    _customTextureUnits.Add(texture.Name);
            }
        }

        for (var index = _customTextureUnits.Count - 1; index >= 0; --index)
        {
            if (IsDefaultTextureUnit(_customTextureUnits[index]))
                _customTextureUnits.RemoveAt(index);
        }

        _customTextureUnits.Sort(StringComparer.Ordinal);
        return _customTextureUnits;
    }

    private void RenderCustomTextureUnit(string unit)
    {
        _textBuffer.Set(unit);
        using var guardKey = new IdScopeGuard(_textBuffer.ContentBytes);

        var cache = GetSubsystem<ResourceCache>();

        var texture = _materials[0].GetTexture(unit);
        var isUndefined = false;
        for (var index = 1; index < _materials.Count; ++index)
        {
            if (ReferenceEquals(_materials[index].GetTexture(unit), texture))
                continue;
            isUndefined = true;
            break;
        }

        _textBuffer.Set(unit);
        Widgets.ItemLabel(
            _textBuffer.ContentBytes,
            Widgets.GetItemLabelColor(isUndefined, texture is null));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Custom texture unit"u8);

        if (ImGui.Button(Icons.TrashCanU8))
            _pendingSetTextures.Add((unit, null));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Remove texture and unit"u8);
        ImGui.SameLine();

        using var guardBackgroundColor = new ColorScopeGuard(
            ImGuiCol.ImGuiColFrameBg,
            Widgets.GetItemBackgroundColor(isUndefined),
            isUndefined);

        var textureType = new StringHash(texture?.GetTypeName() ?? nameof(Texture2D));
        var textureName = texture?.GetName() ?? string.Empty;
        if (Widgets.EditResourceRef(ref textureType, ref textureName, AllowedTextureTypes))
        {
            if (cache.GetResource(textureType, textureName) is Texture newTexture)
                _pendingSetTextures.Add((unit, newTexture));
            else
                _pendingSetTextures.Add((unit, null));
        }
    }

    private static readonly CString _addTextureLabel = new($"{Icons.SquarePlus} Add new texture");
    private static readonly CString _textureNameUsedLabel = new($"{Icons.TriangleExclamation} This texture unit name is already used");

    private void RenderAddCustomTextureUnit(IReadOnlyCollection<string> customTextureUnits)
    {
        using var guardAddElement = new IdScopeGuard("##AddElement"u8);

        var cache = GetSubsystem<ResourceCache>();
        var defaultTexture = cache.Get<Texture2D>("Textures/Black.png");

        var isButtonClicked = ImGui.Button(_addTextureLabel);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add new item to the map"u8);
        ImGui.SameLine();

        // TODO(editor): this "static" is bad in theory
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var isTextClicked = Widgets.InputText(
            ReadOnlySpan<byte>.Empty,
            ref _newUnit,
            ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue);
        var isNameAvailable = !customTextureUnits.Contains(_newUnit)
            && !IsDefaultTextureUnit(_newUnit);

        if ((isButtonClicked || isTextClicked) && _newUnit.Length != 0 && isNameAvailable)
            _pendingSetTextures.Add((_newUnit, defaultTexture));

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Item name"u8);

        if (!isNameAvailable)
            ImGui.Text(_textureNameUsedLabel);
    }

    private void RenderShaderParameters()
    {
        using var guard = new IdScopeGuard("RenderShaderParameters"u8);

        if (!ImGui.CollapsingHeader(
                "Shader Parameters"u8,
                ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
        {
            return;
        }

        _shaderParameterNames = GetShaderParameterNames();
        foreach (var name in _shaderParameterNames)
            RenderShaderParameter(name);
        ImGui.Separator();

        RenderNewShaderParameter();
        ImGui.Separator();
    }

    private List<string> GetShaderParameterNames()
    {
        _shaderParameterNames.Clear();
        foreach (var material in _materials)
        {
            foreach (var description in material.GetShaderParameters().Values)
            {
                if (!_shaderParameterNames.Contains(description.Name))
                    _shaderParameterNames.Add(description.Name);
            }
        }
        _shaderParameterNames.Sort(StringComparer.Ordinal);
        return _shaderParameterNames;
    }

    private void RenderShaderParameter(string name)
    {
        _textBuffer.Set(name);
        using var guard = new IdScopeGuard(_textBuffer.ContentBytes);

        MaterialShaderParameter? firstParameter = null;
        foreach (var material in _materials)
        {
            if (!material.GetShaderParameters().TryGetValue(name, out var parameter))
                continue;
            firstParameter = parameter;
            break;
        }
        if (firstParameter is null)
            return;

        var value = firstParameter.VariantValue.Clone();
        try
        {
            var isUndefined = false;
            foreach (var material in _materials)
            {
                if (material.GetShaderParameters().TryGetValue(name, out var parameter)
                    && parameter.VariantValue == value)
                {
                    continue;
                }
                isUndefined = true;
                break;
            }

            _textBuffer.Set(name);
            Widgets.ItemLabel(
                _textBuffer.ContentBytes,
                Widgets.GetItemLabelColor(isUndefined, IsDefaultValue(name, in value)));

            if (ImGui.Button(Icons.TrashCanU8))
                _pendingSetShaderParameters.Add(new PendingSetShaderParameter(name, default));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Remove this parameter"u8);
            ImGui.SameLine();

            if (ImGui.Button(Icons.ListU8))
                ImGui.OpenPopup("##ShaderParameterPopup"u8);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    "Shader parameter type which should strictly match the type in shader"u8);
            }

            if (ImGui.BeginPopup("##ShaderParameterPopup"u8))
            {
                foreach (var (label, defaultValue) in ShaderParameterTypes)
                {
                    _textBuffer.Set(label);
                    if (ImGui.MenuItem(_textBuffer.ContentBytes))
                    {
                        _pendingSetShaderParameters.Add(
                            new PendingSetShaderParameter(name, defaultValue.Clone()));
                    }
                }
                ImGui.EndPopup();
            }
            ImGui.SameLine();

            using var guardBackgroundColor = new ColorScopeGuard(
                ImGuiCol.ImGuiColFrameBg,
                Widgets.GetItemBackgroundColor(isUndefined),
                isUndefined);

            var options = new Widgets.EditVariantOptions();
            if (name.Contains("Color", StringComparison.OrdinalIgnoreCase))
                options = options.AsColor();
            if (Widgets.EditVariant(ref value, in options))
            {
                _pendingSetShaderParameters.Add(
                    new PendingSetShaderParameter(name, value.Clone()));
            }
        }
        finally
        {
            value.Dispose();
        }
    }

    private void RenderNewShaderParameter()
    {
        ImGui.Text("Add parameter:"u8);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add new parameter for all selected materials"u8);
        ImGui.SameLine();

        var width = ImGui.GetContentRegionAvail().X;
        var addNewParameter = false;
        ImGui.SetNextItemWidth(width * 0.5f);
        if (Widgets.InputText(
                "##Name"u8,
                ref _newParameterName,
                ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue
                    | ImGuiInputTextFlags.ImGuiInputTextFlagsCharsNoBlank))
        {
            addNewParameter = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Unique parameter name, should be valid GLSL identifier"u8);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(width * 0.3f);
        _textBuffer.Set(ShaderParameterTypes[_newParameterType].Label);
        if (ImGui.BeginCombo(
                "##Type"u8,
                _textBuffer.ContentBytes,
                ImGuiComboFlags.ImGuiComboFlagsHeightSmall))
        {
            var index = 0;
            foreach (var (label, _) in ShaderParameterTypes)
            {
                _textBuffer.Set(label);
                if (ImGui.Selectable(
                        _textBuffer.ContentBytes, _newParameterType == index))
                {
                    _newParameterType = index;
                }
                ++index;
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Shader parameter type which should strictly match the type in shader"u8);
        }

        ImGui.SameLine();
        var canAddParameter = _newParameterName.Length != 0
            && !_shaderParameterNames.Contains(_newParameterName);
        ImGui.BeginDisabled(!canAddParameter);
        if (ImGui.Button(Icons.SquarePlusU8))
            addNewParameter = true;
        if (ImGui.IsItemHovered())
        {
            _tooltipBuffer.FormatInPlace(
                $"Add parameter '{_newParameterName}' of type '{ShaderParameterTypes[_newParameterType].Label}'");
            ImGui.SetTooltip(_tooltipBuffer.ContentBytes);
        }
        ImGui.EndDisabled();

        if (addNewParameter && canAddParameter)
        {
            _pendingSetShaderParameters.Add(
                new PendingSetShaderParameter(
                    _newParameterName,
                    ShaderParameterTypes[_newParameterType].DefaultValue.Clone()));
        }
    }

    private static void CopyTechniqueEntries(
        IReadOnlyList<TechniqueEntry> source, List<TechniqueEntry> destination)
    {
        destination.Clear();
        for (var index = 0; index < source.Count; ++index)
            destination.Add(source[index]);
    }

    private static bool TechniqueEntriesEqual(
        IReadOnlyList<TechniqueEntry> lhs, IReadOnlyList<TechniqueEntry> rhs)
    {
        if (lhs.Count != rhs.Count)
            return false;
        for (var index = 0; index < lhs.Count; ++index)
        {
            if (lhs[index] != rhs[index])
                return false;
        }
        return true;
    }

    private void ClearPendingProperties()
    {
        foreach (var property in _pendingSetProperties)
            property.Value.Dispose();
        _pendingSetProperties.Clear();
    }

    private void ClearPendingShaderParameters()
    {
        foreach (var parameter in _pendingSetShaderParameters)
            parameter.Value.Dispose();
        _pendingSetShaderParameters.Clear();
    }

    private readonly struct PendingSetProperty(PropertyDesc description, Variant value)
    {
        public PropertyDesc Description { get; } = description;
        public Variant Value { get; } = value;
    }

    private readonly struct PendingSetShaderParameter(string name, Variant value)
    {
        public string Name { get; } = name;
        public Variant Value { get; } = value;
    }

    private readonly string _defaultTechniqueName = "Techniques/LitOpaque.xml";

    private Material? _previewSkyboxMaterial;
    private readonly Scene _previewScene;
    private readonly SceneRendererToTexture _previewWidget;

    private readonly Dictionary<string, TechniqueDesc> _techniques = new(StringComparer.Ordinal);
    private readonly List<TechniqueDesc> _sortedTechniques = [];
    private TechniqueDesc? _defaultTechnique;

    private readonly List<Material> _materials;
    private readonly List<TechniqueEntry> _techniqueEntries = [];
    private readonly List<TechniqueEntry> _sortedTechniqueEntries = [];

    private bool _pendingSetTechniques;

    private readonly List<PendingSetProperty> _pendingSetProperties = [];

    private readonly List<(string Name, Texture? Texture)> _pendingSetTextures = [];
    private readonly List<string> _customTextureUnits = [];

    private List<string> _shaderParameterNames = new(128);
    private readonly List<PendingSetShaderParameter> _pendingSetShaderParameters = [];

    private string _newParameterName = string.Empty;
    private int _newParameterType;
    private bool? _separateShaderDefines;

    private readonly ImTextureRef _textureReference = new();
    private readonly CString _textBuffer = new();
    private readonly CString _tooltipBuffer = new();
}
