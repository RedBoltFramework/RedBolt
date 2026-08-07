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

// TODO: port Foundation_MaterialInspector when editor plugins are ported.
// void Foundation_MaterialInspector(Context* context, InspectorTab* inspectorTab)
// {
//     inspectorTab->RegisterAddon<MaterialInspector>(inspectorTab->GetProject());
// }

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
        _widget = new MaterialInspectorWidget(materials);
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
