// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation.Shared;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.SystemUI;

/// <summary>Tab that renders custom Scene.</summary>
public abstract class CustomSceneViewTab : ResourceEditorTab
{
    public CustomSceneViewTab(
        string title, string guid, EditorTabFlag flags, EditorTabPlacement placement)
        : base(title, guid, flags, placement)
    {
        _preview = new SceneWidget();
        _cameraController = new CameraController(GetHotkeyManager());
        _state = new CameraController.PageState();
        _activeResourceText = new CString();

        _preview.CreateDefaultScene();
    }

    public override void Dispose()
    {
        _activeResourceText.Dispose();
        _cameraController.Dispose();
        _preview.Dispose();
        base.Dispose();
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    public override void RenderContent()
    {
        var basePosition = ImGui.GetCursorPos();

        RenderTitle();

        if (_preview is not null)
        {
            var contentPosition = ImGui.GetCursorPos();

            var tabContentSize = GetContentSize();
            var contentSize = new Vector2f(tabContentSize.X, tabContentSize.Y)
                - new Vector2f(0.0f, contentPosition.Y - basePosition.Y);
            if (ImGui.BeginChild("scene_preview"u8, contentSize))
                _preview.RenderContent();
            ImGui.EndChild();

            _cameraController.ProcessInput(_preview.GetCamera()!, ref _state);
        }
    }

    public virtual void ResetCamera()
    {
        if (_preview is not null)
            _preview.LookAt(new BoundingBox(-0.5f, 0.5f));
    }

    public Scene? GetScene() => _preview is not null ? _preview.GetScene() : null;

    public Camera? GetCamera() => _preview is not null ? _preview.GetCamera() : null;

    protected virtual void RenderTitle()
    {
        _activeResourceText.Set(ActiveResourceName);
        ImGui.TextUnformatted(_activeResourceText.ContentBytes);
        ImGui.SameLine(GetContentSize().X - 100.0f);
        if (ImGui.Button("Reset camera"u8, new Vector2f(100.0f, 0.0f)))
            ResetCamera();
    }

    protected readonly SceneWidget _preview;
    protected CameraController _cameraController;
    protected CameraController.PageState _state;

    private readonly CString _activeResourceText;
}
