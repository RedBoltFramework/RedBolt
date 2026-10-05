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
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.SystemUI;

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class MaterialInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public MaterialInspector(Project project)
    {
        _project = new WeakReference<Project>(project);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        _pendingAction = null;
        if (_widget is not null)
        {
            _widget.OnEditBegin -= BeginEdit;
            _widget.OnEditEnd -= EndEdit;
            _widget.Dispose();
            _widget = null;
        }
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

        if (_updateTimer.GetMSec(false) > UpdatePeriodMs)
        {
            _widget.UpdateTechniques(TechniquePath);
            _updateTimer.Reset();
        }

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

    private void OnProjectRequest(EditorTab? senderTab, ProjectRequest request)
    {
        if (request is not InspectResourceRequest inspectResourceRequest
            || inspectResourceRequest.Resources.Count == 0)
        {
            return;
        }

        var resources = inspectResourceRequest.Resources;

        var areAllMaterials = resources.All(static description => description.HasObjectType<Material>());
        if (!areAllMaterials)
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

        var materials = new List<Material>();
        foreach (var resourceName in _resourceNames)
        {
            var material = cache.GetResourceOrNull<Material>(resourceName);
            if (material is not null)
                materials.Add(material);
        }

        _resourceNames.Clear();
        foreach (var material in materials)
            _resourceNames.Add(material.GetName());

        if (materials.Count == 0)
        {
            if (_widget is not null)
            {
                _widget.OnEditBegin -= BeginEdit;
                _widget.OnEditEnd -= EndEdit;
                _widget.Dispose();
                _widget = null;
            }
            return;
        }

        if (_widget is not null)
        {
            _widget.OnEditBegin -= BeginEdit;
            _widget.OnEditEnd -= EndEdit;
            _widget.Dispose();
        }
        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        _widget = new MaterialInspectorWidget(materials, project!.CoreDataPath);
        _widget.UpdateTechniques(TechniquePath);
        _widget.OnEditBegin += BeginEdit;
        _widget.OnEditEnd += EndEdit;
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
        Debug.Assert(_widget is not null);
        foreach (var material in _widget.GetMaterials())
            _pendingAction.AddResource(material);

        // Initialization of "redo" state is delayed so it's okay to push the action here
        undoManager.PushAction(_pendingAction);
    }

    private void EndEdit()
    {
        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        Debug.Assert(_widget is not null);
        foreach (var material in _widget.GetMaterials())
            project!.SaveFileDelayed(material);
    }

    private const uint UpdatePeriodMs = 1000;
    private const string TechniquePath = "Techniques/";

    private readonly WeakReference<Project> _project;

    private List<string> _resourceNames = [];
    private MaterialInspectorWidget? _widget;
    private readonly Timer _updateTimer = new();

    private ModifyResourceAction? _pendingAction;
}
