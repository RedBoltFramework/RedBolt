// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;

/// <summary>Sink to reset inspector on failed inspector request.</summary>
public class EmptyInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public EmptyInspector(Project project)
    {
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        GetSubsystem<Project>().OnRequest -= OnProjectRequest;
        OnActivated = null;
        base.Dispose();
    }

    /// <summary>Implement InspectorSource.</summary>
    public EditorTab? GetOwnerTab() => null;

    public void RenderContent()
    {
    }

    public void RenderContextMenuItems()
    {
    }

    public void RenderMenu()
    {
    }

    public void ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
    }

    private void OnProjectRequest(EditorTab? sender, ProjectRequest request)
    {
        if (request is not BaseInspectRequest)
            return;

        request.QueueProcessCallback(() =>
        {
            OnActivated?.Invoke();
        }, int.MinValue);
    }
}
