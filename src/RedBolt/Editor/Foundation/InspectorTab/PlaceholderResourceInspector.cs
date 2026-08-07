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
