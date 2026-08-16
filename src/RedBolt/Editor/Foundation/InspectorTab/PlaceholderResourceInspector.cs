// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.SystemUI;

/// <summary>Simple default inspector for selected resources.</summary>
public class PlaceholderResourceInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public PlaceholderResourceInspector(Project project)
    {
        _project = new WeakReference<Project>(project);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        if (_project.TryGetTarget(out var project))
            project.OnRequest -= OnProjectRequest;
        OnActivated = null;
        base.Dispose();
    }

    /// <summary>Implement InspectorSource.</summary>
    public EditorTab? GetOwnerTab() => null;

    public void RenderContent()
    {
        if (_singleResource is { } singleResource)
        {
            if (ImGui.Button(Encoding.UTF8.GetBytes($"Open {singleResource.ResourceType}"))
                && _project.TryGetTarget(out var project))
            {
                project.ProcessRequest(new OpenResourceRequest(singleResource.ResourceName));
            }

            ImGui.TextWrapped(Encoding.UTF8.GetBytes(singleResource.ResourceName));
        }
        else if (_multipleResources is { } multipleResources)
        {
            ImGui.Text(Encoding.UTF8.GetBytes($"{multipleResources.NumFiles} files selected"));
            ImGui.Text(Encoding.UTF8.GetBytes($"{multipleResources.NumFolders} folders selected"));
        }
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
        if (request is not InspectResourceRequest inspectResourceRequest
            || inspectResourceRequest.Resources.Count == 0)
        {
            return;
        }

        request.QueueProcessCallback(() =>
        {
            InspectResources(inspectResourceRequest.Resources);
            OnActivated?.Invoke();
        }, int.MinValue + 1);
    }

    private void InspectResources(IReadOnlyList<ResourceFileDescriptor> resources)
    {
        if (resources.Count == 1)
        {
            var descriptor = resources[0];
            var resourceType = !descriptor.IsDirectory ? "File" : "Folder";
            _singleResource = new SingleResource(resourceType, descriptor.ResourceName);
            _multipleResources = null;
        }
        else
        {
            var numFolders = (uint)resources.Count(static descriptor => descriptor.IsDirectory);
            var numFiles = (uint)resources.Count - numFolders;
            _multipleResources = new MultipleResources(numFiles, numFolders);
            _singleResource = null;
        }
    }

    private readonly WeakReference<Project> _project;

    private readonly record struct SingleResource(string ResourceType, string ResourceName);

    private SingleResource? _singleResource;

    private readonly record struct MultipleResources(uint NumFiles, uint NumFolders);

    private MultipleResources? _multipleResources;
}
