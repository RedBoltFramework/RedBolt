// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

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
