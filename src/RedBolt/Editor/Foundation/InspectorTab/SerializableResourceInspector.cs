// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class SerializableResourceInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public SerializableResourceInspector(Project project)
    {
        _project = new WeakReference<Project>(project);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        _pendingAction = null;
        _widget?.Dispose();
        _widget = null;
        _resources.Clear();
        _resourceNames.Clear();
        if (_project.TryGetTarget(out var project))
            project.OnRequest -= OnProjectRequest;
        OnActivated = null;
        base.Dispose();
    }

    /// <summary>Implement InspectorSource.</summary>
    public bool IsUndoSupported() => true;

    public void RenderContent()
    {
        if (_widget is null)
            return;

        _widget.RenderTitle();
        ImGui.Separator();
        _widget.RenderContent();
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

        var resources = inspectResourceRequest.Resources;

        var areAllSerializableResources = resources.All(
            static description => description.HasObjectType<SerializableResource>());
        if (!areAllSerializableResources)
            return;

        request.QueueProcessCallback(() =>
        {
            var resourceNames = inspectResourceRequest.GetSortedResourceNames();
            if (!_resourceNames.SequenceEqual(resourceNames))
            {
                _resourceNames = resourceNames;
                InspectResources();
            }
            OnActivated?.Invoke();
        });
    }

    private void InspectResources()
    {
        var cache = GetSubsystem<ResourceCache>();

        var serializableResources = new List<WeakReference<Serializable>?>();
        _resources.Clear();
        foreach (var resourceName in _resourceNames)
        {
            if (cache.GetResourceOrNull<SerializableResource>(resourceName) is { } serializableResource
                && serializableResource.GetValue() is { } value)
            {
                serializableResources.Add(new WeakReference<Serializable>(value));
                _resources.Add(new WeakReference<SerializableResource>(serializableResource));
            }
        }

        _resourceNames.Clear();
        foreach (var serializableResource in _resources)
        {
            var resourceFound = serializableResource.TryGetTarget(out var resource);
            Debug.Assert(resourceFound);
            _resourceNames.Add(resource!.GetName());
        }

        if (serializableResources.Count == 0)
        {
            _widget?.Dispose();
            _widget = null;
            return;
        }

        _widget?.Dispose();
        _widget = new SerializableInspectorWidget(serializableResources);
        _widget.OnEditAttributeBegin += OnEditAttributeBegin;
        _widget.OnEditAttributeEnd += OnEditAttributeEnd;
        _widget.OnActionBegin += OnActionBegin;
        _widget.OnActionEnd += OnActionEnd;
    }

    private void OnEditAttributeBegin(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
        CreateModifyResourceAction();
    }

    private void OnEditAttributeEnd(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
        SaveModifiedResources();
    }

    private void OnActionBegin(IReadOnlyList<WeakReference<Serializable>?> objects)
    {
        CreateModifyResourceAction();
    }

    private void OnActionEnd(IReadOnlyList<WeakReference<Serializable>?> objects)
    {
        SaveModifiedResources();
    }

    private void CreateModifyResourceAction()
    {
        // Incomplete action will include all the changes automatically
        if (_pendingAction is not null && !_pendingAction.IsComplete())
            return;

        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        var undoManager = project!.UndoManager;

        _pendingAction = new ModifyResourceAction(project);
        foreach (var serializable in _resources)
        {
            var resourceFound = serializable.TryGetTarget(out var resource);
            Debug.Assert(resourceFound);
            _pendingAction.AddResource(resource!);
        }

        // Initialization of "redo" state is delayed so it's okay to push the action here
        undoManager.PushAction(_pendingAction);
    }

    private void SaveModifiedResources()
    {
        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        foreach (var serializableResource in _resources)
        {
            var resourceFound = serializableResource.TryGetTarget(out var resource);
            Debug.Assert(resourceFound);
            project!.SaveFileDelayed(resource!);
        }
    }

    private readonly WeakReference<Project> _project;

    private List<string> _resourceNames = [];
    private readonly List<WeakReference<SerializableResource>> _resources = [];
    private SerializableInspectorWidget? _widget;

    private ModifyResourceAction? _pendingAction;
}
