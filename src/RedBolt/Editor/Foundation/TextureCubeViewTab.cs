// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.SystemUI;

/// <summary>Tab that renders Scene and enables Scene manipulation.</summary>
public class TextureCubeViewTab : CustomSceneViewTab
{
    public TextureCubeViewTab()
        : base("Cubemap", "d66bcf6d-9fe3-4e7c-a519-4b1ad5a0f89c",
            EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault,
            EditorTabPlacement.DockCenter)
    {
    }

    public override void Dispose()
    {
        _textureCube = null;
        base.Dispose();
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    public override void RenderContent()
    {
        if (_textureCube is not null)
            base.RenderContent();
    }

    public override string GetResourceTitle() => "Cubemap";

    public override bool SupportMultipleResources() => false;

    public override bool CanOpenResource(ResourceFileDescriptor descriptor)
    {
        return descriptor.HasObjectType<TextureCube>();
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    protected override void OnResourceLoaded(string resourceName)
    {
        var cache = GetSubsystem<ResourceCache>();
        _textureCube = cache.GetResource<TextureCube>(resourceName);
        _preview.SetSkyboxTexture(_textureCube);
    }

    protected override void OnResourceUnloaded(string resourceName)
    {
        _textureCube = null;
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

    protected void RenderTextureCube(TextureCube? texture)
    {
        base.RenderContent();
    }

    private TextureCube? _textureCube;
}
