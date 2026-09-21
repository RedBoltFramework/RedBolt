// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation.Shared;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.SystemUI;

using EditorProject = global::RedBolt.Editor.Project.Project;
using ResourceType = global::RedBolt.Resource.Resource;

/// <summary>Simple default inspector for selected resources.</summary>
public class InspectorWithPreview : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public InspectorWithPreview(EditorProject project)
    {
        _project = new WeakReference<EditorProject>(project);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        if (_project.TryGetTarget(out var project))
            project.OnRequest -= OnProjectRequest;

        if (_inspector is not null)
        {
            _inspector.OnEditBegin -= BeginEdit;
            _inspector.OnEditEnd -= EndEdit;
        }
        _preview?.Dispose();
        _preview = null;
        _inspector?.Dispose();
        _inspector = null;
        _pendingAction = null;
        _resourceNames.Clear();
        OnActivated = null;
        base.Dispose();
    }

    /// <summary>Implement InspectorSource.</summary>
    public EditorTab? GetOwnerTab() => null;

    public void RenderContent()
    {
        if (_inspector is null)
            return;

        var resources = _inspector.GetResources();

        var basePosition = ImGui.GetCursorPos();

        _inspector.RenderTitle();
        ImGui.Separator();

        var contentPosition = ImGui.GetCursorPos();
        var window = ImGui.GetCurrentWindow();
        var rect = window.ContentRegionRect;
        rect.Min = new Vector2f(MathDefs.Round(rect.Min.X), MathDefs.Round(rect.Min.Y));
        rect.Max = new Vector2f(MathDefs.Round(rect.Max.X), MathDefs.Round(rect.Max.Y));
        var contentSize = rect.Size - new Vector2f(
            0.0f, contentPosition.Y - basePosition.Y + 5.0f);

        if (_preview is not null)
        {
            var previewSize = new Vector2f(contentSize.X, contentSize.X);
            var inspectorSize = new Vector2f(contentSize.X, contentSize.Y - previewSize.Y);
            if (inspectorSize.Y > previewSize.Y)
            {
                if (ImGui.BeginChild("inspector"u8, inspectorSize))
                {
                    var singleResource = resources[0];
                    if (ImGui.Button("Open"u8)
                        && _project.TryGetTarget(out var project))
                    {
                        project.ProcessRequest(new OpenResourceRequest(singleResource.GetName()));
                    }
                    _inspector.RenderContent();
                }
                ImGui.EndChild();
                if (ImGui.BeginChild("preview"u8, previewSize))
                    _preview.RenderContent();
                ImGui.EndChild();
                return;
            }
        }

        if (ImGui.BeginChild("inspector"u8, contentSize))
        {
            if (resources.Count == 1)
            {
                var singleResource = resources[0];
                if (ImGui.Button("Open"u8)
                    && _project.TryGetTarget(out var project))
                {
                    project.ProcessRequest(new OpenResourceRequest(singleResource.GetName()));
                }
            }
            _inspector.RenderContent();
            _preview?.RenderContent();
        }
        ImGui.EndChild();
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

    protected virtual StringHash GetResourceType() => default;

    protected virtual ResourceInspectorWidget? MakeInspectorWidget(IReadOnlyList<ResourceType> resources)
    {
        return null;
    }

    protected virtual BaseWidget? MakePreviewWidget(ResourceType resource)
    {
        return null;
    }

    private void OnProjectRequest(EditorTab? sender, ProjectRequest request)
    {
        if (request is not InspectResourceRequest inspectResourceRequest)
            return;

        var resources = inspectResourceRequest.Resources;
        if (resources.Count == 0)
            return;

        var resourceType = GetResourceType();
        var areAllSameType = true;
        foreach (var description in resources)
        {
            if (description.HasObjectType(resourceType))
                continue;
            areAllSameType = false;
            break;
        }
        if (!areAllSameType)
            return;

        request.QueueProcessCallback(() =>
        {
            var resourceNames = inspectResourceRequest.GetSortedResourceNames();
            var areResourceNamesEqual = _resourceNames.Count == resourceNames.Count;
            for (var index = 0; areResourceNamesEqual && index < resourceNames.Count; ++index)
                areResourceNamesEqual = _resourceNames[index] == resourceNames[index];
            if (!areResourceNamesEqual)
            {
                _resourceNames = resourceNames;
                InspectResources();
            }
            OnActivated?.Invoke();
        }, int.MinValue + 1);
    }

    private void InspectResources()
    {
        var cache = GetSubsystem<ResourceCache>();

        var resources = new List<ResourceType>();
        foreach (var resourceName in _resourceNames)
        {
            if (cache.GetResource(GetResourceType(), resourceName) is { } resource)
                resources.Add(resource);
        }

        if (resources.Count == 0)
        {
            if (_inspector is not null)
            {
                _inspector.OnEditBegin -= BeginEdit;
                _inspector.OnEditEnd -= EndEdit;
                _inspector.Dispose();
                _inspector = null;
            }
            _preview?.Dispose();
            _preview = null;
            return;
        }

        var inspector = MakeInspectorWidget(resources);
        if (!ReferenceEquals(_inspector, inspector))
        {
            if (_inspector is not null)
            {
                _inspector.OnEditBegin -= BeginEdit;
                _inspector.OnEditEnd -= EndEdit;
                _inspector.Dispose();
            }
            _inspector = inspector;
        }
        if (_inspector is not null)
        {
            _inspector.OnEditBegin += BeginEdit;
            _inspector.OnEditEnd += EndEdit;
        }

        BaseWidget? preview = null;
        if (resources.Count == 1 && resources[0] is not null)
            preview = MakePreviewWidget(resources[0]);
        if (!ReferenceEquals(_preview, preview))
        {
            _preview?.Dispose();
            _preview = preview;
        }
    }

    private void BeginEdit()
    {
        // Incomplete action will include all the changes automatically
        if (_pendingAction is not null && !_pendingAction.IsComplete())
            return;

        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        var undoManager = project!.UndoManager;

        _pendingAction = new ModifyResourceAction(project);
        Debug.Assert(_inspector is not null);
        foreach (var material in _inspector.GetResources())
            _pendingAction.AddResource(material);

        // Initialization of "redo" state is delayed so it's okay to push the action here
        undoManager.PushAction(_pendingAction);
    }

    private void EndEdit()
    {
        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        Debug.Assert(_inspector is not null);
        foreach (var material in _inspector.GetResources())
            project!.SaveFileDelayed(material);
    }

    private readonly WeakReference<EditorProject> _project;

    private List<string> _resourceNames = [];

    private ResourceInspectorWidget? _inspector;
    private BaseWidget? _preview;
    private ModifyResourceAction? _pendingAction;
}
