//
// Copyright (c) 2022-2022 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

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
