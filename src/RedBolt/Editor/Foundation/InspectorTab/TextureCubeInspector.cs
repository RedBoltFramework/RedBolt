// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System.Collections.Generic;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.SystemUI;

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class TextureCubeInspector : InspectorWithPreview
{
    public TextureCubeInspector(Project project)
        : base(project)
    {
    }

    protected override StringHash GetResourceType() => typeof(TextureCube).GetTypeId();

    protected override ResourceInspectorWidget? MakeInspectorWidget(IReadOnlyList<Resource> resources)
    {
        return new TextureCubeInspectorWidget(resources);
    }

    protected override BaseWidget? MakePreviewWidget(Resource resource)
    {
        var sceneWidget = new SceneWidget();
        sceneWidget.CreateDefaultScene();
        sceneWidget.SetSkyboxTexture((Texture)resource);
        return sceneWidget;
    }
}
