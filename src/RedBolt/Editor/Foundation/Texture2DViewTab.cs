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

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

// TODO: port Foundation_Texture2DViewTab when editor plugins are ported.
// void Foundation_Texture2DViewTab(Context* context, Project* project)
// {
//     project->AddTab(MakeShared<Texture2DViewTab>(context));
// }

/// <summary>Tab that renders Scene and enables Scene manipulation.</summary>
public class Texture2DViewTab : CustomSceneViewTab
{
    public Texture2DViewTab()
        : base("Texture", "2a3032e6-541a-42fe-94c3-8baf96604690",
            EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault,
            EditorTabPlacement.DockCenter)
    {
    }

    public override void Dispose()
    {
        _preview?.Dispose();
        _preview = null;
        base.Dispose();
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    public override void RenderContent()
    {
        if (_preview is not null)
        {
            RenderTitle();
            if (ImGui.BeginChild("content"u8, new Vector2f(0.0f, 0.0f)))
                _preview.RenderContent();
            ImGui.EndChild();
        }
    }

    public override string GetResourceTitle() => "Texture2D";

    public override bool SupportMultipleResources() => false;

    public override bool CanOpenResource(ResourceFileDescriptor descriptor)
    {
        return descriptor.HasObjectType<Texture2D>();
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    protected override void OnResourceLoaded(string resourceName)
    {
        var cache = GetSubsystem<ResourceCache>();
        var preview = new Texture2DWidget(cache.GetResource<Texture2D>(resourceName));
        _preview?.Dispose();
        _preview = preview;
    }

    protected override void OnResourceUnloaded(string resourceName)
    {
        _preview?.Dispose();
        _preview = null;
    }

    protected override void OnActiveResourceChanged(string oldResourceName, string newResourceName)
    {
    }

    protected override void OnResourceSaved(string resourceName)
    {
    }

    protected override void OnResourceShallowSaved(string resourceName)
    {
    }

    private new Texture2DWidget? _preview;
}
