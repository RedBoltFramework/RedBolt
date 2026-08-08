//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Project;

// TODO: port Foundation_SceneFactory when editor plugins are ported.
// void Foundation_SceneFactory(Context* context, ResourceBrowserTab* resourceBrowserTab)
// {
//     resourceBrowserTab->AddFactory(MakeShared<SceneFactory>(context, true));
//     resourceBrowserTab->AddFactory(MakeShared<SceneFactory>(context, false));
// }

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
