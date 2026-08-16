// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Resource;

/// <summary>Camera controller used by Scene View.</summary>
public class MaterialFactory : BaseResourceFactory
{
    public MaterialFactory()
        : base(0, "Material")
    {
    }

    /// <summary>Implement BaseResourceFactory.</summary>
    public override string DefaultFileName => "Material.material";

    public override void RenderAuxilary()
    {
        ImGui.Separator();

        ImGui.RadioButton("Opaque"u8, ref _type, (int)Type.Opaque);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Opaque material with solid surface."u8);

        ImGui.RadioButton("Alpha Mask"u8, ref _type, (int)Type.AlphaMask);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Opaque material with pixels discarded based on alpha channel in diffuse (albedo) texture."u8);
        }

        ImGui.RadioButton("Transparent"u8, ref _type, (int)Type.Transparent);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Realistic transparent material like glass or plastic with specular highlights and reflections not affected by alpha value."u8);
        }

        ImGui.RadioButton("Transparent Fade"u8, ref _type, (int)Type.TransparentFade);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Transparent material with specular highlights and reflections faded out by alpha value."u8);

        ImGui.Checkbox("Lit"u8, ref _lit);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Enable lighting for this material."u8);

        ImGui.BeginDisabled(!_lit);

        ImGui.Checkbox("PBR"u8, ref _pbr);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Use physically based rendering for this material."u8);

        ImGui.Checkbox("Normal Mapping"u8, ref _normal);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Use normal mapping for this material, if normal texture is provided."u8);

        ImGui.EndDisabled();

        ImGui.Separator();
    }

    public override void CommitAndClose()
    {
        var cache = GetSubsystem<ResourceCache>();

        var techniqueName = GetTechniqueName();
        var vertexDefines = string.Empty;
        var pixelDefines = string.Empty;
        if (_lit && _pbr)
        {
            vertexDefines += "PBR ";
            pixelDefines += "PBR ";
        }
        if (_type == (int)Type.AlphaMask)
            pixelDefines += "ALPHAMASK ";

        using var material = new Material();

        material.SetTechnique(0, cache.GetResource<Technique>(techniqueName));
        material.SetVertexShaderDefines(pixelDefines);
        material.SetPixelShaderDefines(pixelDefines);

        material.SaveFile(new FileIdentifier(FinalFileName));
    }

    private string GetTechniqueName()
    {
        if (!_lit)
        {
            if (_type == (int)Type.Opaque || _type == (int)Type.AlphaMask)
                return "Techniques/UnlitOpaque.xml";
            else
                return "Techniques/UnlitTransparent.xml";
        }
        else
        {
            if (_normal)
            {
                if (_type == (int)Type.Opaque || _type == (int)Type.AlphaMask)
                    return "Techniques/LitOpaqueNormalMap.xml";
                else if (_type == (int)Type.Transparent)
                    return "Techniques/LitTransparentNormalMap.xml";
                else
                    return "Techniques/LitTransparentFadeNormalMap.xml";
            }
            else
            {
                if (_type == (int)Type.Opaque || _type == (int)Type.AlphaMask)
                    return "Techniques/LitOpaque.xml";
                else if (_type == (int)Type.Transparent)
                    return "Techniques/LitTransparent.xml";
                else
                    return "Techniques/LitTransparentFade.xml";
            }
        }
    }

    private enum Type
    {
        Opaque,
        AlphaMask,
        Transparent,
        TransparentFade
    }

    private int _type;
    private bool _lit = true;
    private bool _pbr = true;
    private bool _normal = true;
}
