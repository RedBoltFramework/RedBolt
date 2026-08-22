// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Logging;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class RenderPathInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public RenderPathInspector(Project project)
    {
        _project = new WeakReference<Project>(project);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        _textBuffer.Dispose();
        _pendingAdds.Clear();
        _pendingReorders.Clear();
        _pendingRemoves.Clear();
        _pendingAction = null;
        ClearInspectors();
        if (_resource is not null)
            UnsubscribeFromEvent<ResourceEvents.ReloadFinished>(_resource);
        _resource = null;
        _resourceName = string.Empty;
        if (_project.TryGetTarget(out var project))
            project.OnRequest -= OnProjectRequest;
        OnActivated = null;
        base.Dispose();
    }

    /// <summary>Implement InspectorSource.</summary>
    public EditorTab? GetOwnerTab() => null;

    public bool IsUndoSupported() => true;

    public void RenderContent()
    {
        if (_resource is null)
            return;

        EnsureInitialized();

        var assetManager = GetProject().AssetManager;

        _textBuffer.Set(_resourceName);
        ImGui.TextUnformatted(_textBuffer.ContentBytes);

        _pendingRemoves.Clear();
        _pendingAdds.Clear();
        _pendingReorders.Clear();

        for (var index = 0; index < _inspectorWidgets.Count; ++index)
            RenderInspector(index, _inspectorWidgets[index]);
        RenderAddPass();

        foreach (var (passReference, newIndex) in _pendingReorders)
        {
            if (!passReference.TryGetTarget(out var pass))
                continue;

            BeginChange();
            _resource.ReorderPass(pass, newIndex);
            OnResourceReloaded();
        }

        foreach (var passReference in _pendingRemoves)
        {
            if (!passReference.TryGetTarget(out var pass))
                continue;

            BeginChange();
            _resource.RemovePass(pass);
            OnResourceReloaded();
        }

        foreach (var type in _pendingAdds)
        {
            var redObject = Context.Instance.CreateObject(type);
            if (redObject is not RenderPass pass)
            {
                redObject?.Dispose();
                Log.GetLogger().LogError("Failed to create RenderPass");
                continue;
            }

            BeginChange();
            _resource.AddPass(pass);
            OnResourceReloaded();
        }

        if (HasPendingChanges())
        {
            _suppressReloadCallback = true;
            _resource.SendEvent<ResourceEvents.ReloadFinished>();
            _suppressReloadCallback = false;
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

    private static StringHash? RenderCreatePass(CString textBuffer)
    {
        var typesByCategory = Context.Instance.GetObjectCategories();
        if (!typesByCategory.TryGetValue(ObjectCategory.Category_RenderPass, out var types))
            return null;

        StringHash? result = null;
        foreach (var type in types)
        {
            var reflection = Context.Instance.GetReflection(type);
            if (!reflection.HasObjectFactory())
                continue;
            textBuffer.Set(reflection.ClassName);
            if (ImGui.MenuItem(textBuffer.ContentBytes))
                result = type;
        }
        return result;
    }

    private void OnProjectRequest(EditorTab? senderTab, ProjectRequest request)
    {
        if (senderTab is null || request is not InspectResourceRequest inspectResourceRequest)
            return;

        var resources = inspectResourceRequest.Resources;
        if (resources.Count != 1 || !resources[0].HasObjectType<RenderPath>())
            return;

        request.QueueProcessCallback(() =>
        {
            if (!string.Equals(_resourceName, resources[0].ResourceName, StringComparison.Ordinal))
            {
                _resourceName = resources[0].ResourceName;
                InspectObjects();
            }
            OnActivated?.Invoke();
        });
    }

    private void OnResourceReloaded()
    {
        if (_suppressReloadCallback)
            return;

        ClearInspectors();
    }

    private void OnResourceReloaded(ref ResourceEvents.ReloadFinished eventData)
    {
        OnResourceReloaded();
    }

    private void InspectObjects()
    {
        var cache = GetSubsystem<ResourceCache>();
        _resource = cache.GetResource<RenderPath>(_resourceName);

        UnsubscribeFromEvent<ResourceEvents.ReloadFinished>();
        if (_resource is not null)
            SubscribeToEvent<ResourceEvents.ReloadFinished>(_resource, OnResourceReloaded);

        OnResourceReloaded();
    }

    private bool HasPendingChanges()
    {
        return _pendingAction is not null && !_pendingAction.IsComplete();
    }

    private void BeginChange()
    {
        if (_pendingAction is null || _pendingAction.IsComplete())
        {
            _pendingAction = new ModifyResourceAction(GetProject());
            _pendingAction.AddResource(_resource!);
            _pendingAction.SaveOnComplete();

            var undoManager = GetProject().UndoManager;
            undoManager.PushAction(_pendingAction);
        }
    }

    private void BeginEditAttribute(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
        BeginChange();
    }

    private void EndEditAttribute(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
    }

    private void EnsureInitialized()
    {
        if (_resource is null)
            return;

        var passes = _resource.Passes;
        if (ArePassesEqual(_passes, passes))
            return;

        ClearInspectors();
        foreach (var pass in passes)
        {
            _passes.Add(new WeakReference<RenderPass>(pass));
            var widget = new SerializableInspectorWidget(
                [new WeakReference<Serializable>(pass)]);
            widget.OnEditAttributeBegin += BeginEditAttribute;
            widget.OnEditAttributeEnd += EndEditAttribute;
            _inspectorWidgets.Add(widget);
        }
    }

    private static readonly CString _removePassLabel = new($"{Icons.TrashCan}##RemovePass");
    private static readonly CString _movePassUpLabel = new($"{Icons.ArrowUp}##MovePassUp");
    private static readonly CString _movePassDownLabel = new($"{Icons.ArrowDown}##MovePassDown");

    private void RenderInspector(int index, SerializableInspectorWidget inspector)
    {
        using var guard = new IdScopeGuard(RuntimeHelpers.GetHashCode(inspector));

        if (ImGui.Button(_removePassLabel))
        {
            foreach (var reference in inspector.GetObjects())
            {
                if (reference is not null
                    && reference.TryGetTarget(out var serializable)
                    && serializable is RenderPass pass)
                {
                    _pendingRemoves.Add(new WeakReference<RenderPass>(pass));
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Remove this pass"u8);
        ImGui.SameLine();

        ImGui.BeginDisabled(index == 0);
        if (ImGui.Button(_movePassUpLabel))
        {
            foreach (var reference in inspector.GetObjects())
            {
                if (reference is not null
                    && reference.TryGetTarget(out var serializable)
                    && serializable is RenderPass pass)
                {
                    _pendingReorders.Add((new WeakReference<RenderPass>(pass), index - 1));
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Move this pass up"u8);
        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.BeginDisabled(index + 1 == _inspectorWidgets.Count);
        if (ImGui.Button(_movePassDownLabel))
        {
            foreach (var reference in inspector.GetObjects())
            {
                if (reference is not null
                    && reference.TryGetTarget(out var serializable)
                    && serializable is RenderPass pass)
                {
                    _pendingReorders.Add((new WeakReference<RenderPass>(pass), index + 1));
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Move this pass down"u8);
        ImGui.EndDisabled();
        ImGui.SameLine();

        var firstReference = inspector.GetObjects()[0];
        Serializable? firstSerializable = null;
        var passFound = firstReference is not null
            && firstReference.TryGetTarget(out firstSerializable)
            && firstSerializable is RenderPass;
        Debug.Assert(passFound);
        var firstPass = (RenderPass)firstSerializable!;
        _textBuffer.Set(firstPass.GetPassName());
        if (ImGui.CollapsingHeader(_textBuffer.ContentBytes))
            inspector.RenderContent();
    }

    private static readonly CString _addPassLabel = new($"{Icons.SquarePlus} Add Pass");

    private void RenderAddPass()
    {
        if (ImGui.Button(_addPassLabel))
            ImGui.OpenPopup("##AddPass"u8);
        if (ImGui.BeginPopup("##AddPass"u8))
        {
            var passType = RenderCreatePass(_textBuffer);
            if (passType.HasValue)
            {
                _pendingAdds.Add(passType.Value);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private static bool ArePassesEqual(
        IReadOnlyList<WeakReference<RenderPass>> lhs, IReadOnlyList<RenderPass> rhs)
    {
        if (lhs.Count != rhs.Count)
            return false;

        for (var index = 0; index < lhs.Count; ++index)
        {
            if (!lhs[index].TryGetTarget(out var pass) || !ReferenceEquals(pass, rhs[index]))
                return false;
        }
        return true;
    }

    private void ClearInspectors()
    {
        for (var index = _inspectorWidgets.Count - 1; index >= 0; --index)
            _inspectorWidgets[index].Dispose();
        _inspectorWidgets.Clear();
        _passes.Clear();
    }

    private Project GetProject()
    {
        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        return project!;
    }

    private readonly WeakReference<Project> _project;

    private string _resourceName = string.Empty;
    private RenderPath? _resource;

    private readonly List<WeakReference<RenderPass>> _passes = [];
    private readonly List<SerializableInspectorWidget> _inspectorWidgets = [];

    private ModifyResourceAction? _pendingAction;
    private readonly List<WeakReference<RenderPass>> _pendingRemoves = [];
    private readonly List<(WeakReference<RenderPass> Pass, int Index)> _pendingReorders = [];
    private readonly List<StringHash> _pendingAdds = [];
    private bool _suppressReloadCallback;

    private readonly CString _textBuffer = new();
}
