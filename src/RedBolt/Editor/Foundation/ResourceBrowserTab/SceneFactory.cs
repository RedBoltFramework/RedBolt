// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Project;

/// <summary>Camera controller used by Scene View.</summary>
public class SceneFactory : BaseResourceFactory
{
    public SceneFactory(bool isPrefab)
        : base(0, isPrefab ? "Prefab" : "Scene")
    {
        _isPrefab = isPrefab;
    }

    /// <summary>Implement BaseResourceFactory.</summary>
    public override string DefaultFileName => _isPrefab ? "Prefab.prefab" : "Scene.scene";

    public override void RenderAuxilary()
    {
        if (!_isPrefab)
        {
            ImGui.Separator();

            ImGui.Checkbox("High Quality"u8, ref _highQuality);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Use renderer settings for high picture quality"u8);

            ImGui.Checkbox("Default Objects"u8, ref _defaultObjects);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add default light, environment and teapot to the scene."u8);
        }

        ImGui.Separator();
    }

    public override void CommitAndClose()
    {
        var parameters = new DefaultSceneParameters();
        parameters.HighQuality = _highQuality;
        parameters.CreateObjects = _defaultObjects;
        parameters.IsPrefab = _isPrefab;

        DefaultSceneFunctions.CreateDefaultScene(FinalFileName, in parameters);
    }

    private readonly bool _isPrefab;
    private bool _highQuality = true;
    private bool _defaultObjects = true;
}
