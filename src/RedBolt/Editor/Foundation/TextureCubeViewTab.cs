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
