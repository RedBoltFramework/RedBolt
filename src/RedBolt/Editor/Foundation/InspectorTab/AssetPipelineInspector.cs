// Copyright (c) 2017-2020 the rbfx project.
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
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

// TODO: port Foundation_AssetPipelineInspector when editor plugins are ported.
// void Foundation_AssetPipelineInspector(Context* context, InspectorTab* inspectorTab)
// {
//     inspectorTab->RegisterAddon<AssetPipelineInspector>(inspectorTab->GetProject());
// }

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class AssetPipelineInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public AssetPipelineInspector(Project project)
    {
        _project = new WeakReference<Project>(project);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        _textBuffer.Dispose();
        _pendingAdds.Clear();
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

        if (ImGui.Button("\uf021 Update Assets"u8))
            assetManager.MarkCacheDirty(GetPath(_resource.GetName()));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Re-import all assets potentially affected by this pipeline"u8);

        ImGui.SameLine();
        if (assetManager.IsProcessing)
            ImGui.TextUnformatted("\uf071 Assets are cooking now"u8);
        else
            ImGui.TextUnformatted(ReadOnlySpan<byte>.Empty);

        _pendingRemoves.Clear();
        _pendingAdds.Clear();

        foreach (var inspector in _inspectorWidgets)
            RenderInspector(inspector);
        RenderAddTransformer();

        foreach (var transformerReference in _pendingRemoves)
        {
            if (!transformerReference.TryGetTarget(out var transformer))
                continue;

            BeginChange();
            _resource.RemoveTransformer(transformer);
            OnResourceReloaded();
        }

        foreach (var type in _pendingAdds)
        {
            var redObject = Context.Instance.CreateObject(type);
            if (redObject is not AssetTransformer transformer)
            {
                redObject?.Dispose();
                Log.GetLogger().LogError("Failed to create AssetTransformer");
                continue;
            }

            BeginChange();
            _resource.AddTransformer(transformer);
            OnResourceReloaded();
        }

        RenderFinalButtons();
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

    private static StringHash? RenderCreateTransformer(CString textBuffer)
    {
        var typesByCategory = Context.Instance.GetObjectCategories();
        if (!typesByCategory.TryGetValue(ObjectCategory.Category_Transformer, out var types))
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
        if (resources.Count != 1 || !resources[0].HasObjectType<AssetPipeline>())
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
        ClearInspectors();
    }

    private void OnResourceReloaded(ref ResourceEvents.ReloadFinished eventData)
    {
        OnResourceReloaded();
    }

    private void InspectObjects()
    {
        var cache = GetSubsystem<ResourceCache>();
        _resource = cache.GetResource<AssetPipeline>(_resourceName);

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
            _pendingAction.DisableAutoComplete();
            _pendingAction.SaveOnComplete();

            var undoManager = GetProject().UndoManager;
            undoManager.PushAction(_pendingAction);
        }
    }

    private void Apply()
    {
        if (HasPendingChanges())
            _pendingAction!.Complete(true);
    }

    private void Discard()
    {
        if (HasPendingChanges())
        {
            var undoManager = GetProject().UndoManager;
            undoManager.Undo();
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

        var transformers = _resource.GetTransformers();
        if (AreTransformersEqual(_transformers, transformers))
            return;

        ClearInspectors();
        foreach (var transformer in transformers)
        {
            WeakReference<Serializable>? serializableReference = null;
            if (transformer is not null)
            {
                _transformers.Add(new WeakReference<AssetTransformer>(transformer));
                serializableReference = new WeakReference<Serializable>(transformer);
            }
            else
                _transformers.Add(null);

            var widget = new SerializableInspectorWidget([serializableReference]);
            widget.OnEditAttributeBegin += BeginEditAttribute;
            widget.OnEditAttributeEnd += EndEditAttribute;
            _inspectorWidgets.Add(widget);
        }
    }

    private void RenderInspector(SerializableInspectorWidget inspector)
    {
        using var guard = new IdScopeGuard(RuntimeHelpers.GetHashCode(inspector));

        if (ImGui.Button("\uf2ed##RemoveTransformer"u8))
        {
            foreach (var reference in inspector.GetObjects())
            {
                if (reference is not null
                    && reference.TryGetTarget(out var serializable)
                    && serializable is AssetTransformer transformer)
                {
                    _pendingRemoves.Add(new WeakReference<AssetTransformer>(transformer));
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Remove this transformer"u8);
        ImGui.SameLine();

        _textBuffer.Set(inspector.GetTitle());
        if (ImGui.CollapsingHeader(
            _textBuffer.ContentBytes,
            ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
        {
            inspector.RenderContent();
        }
    }

    private void RenderAddTransformer()
    {
        if (ImGui.Button("\uf0fe Add Transformer"u8))
            ImGui.OpenPopup("##AddTransformer"u8);
        if (ImGui.BeginPopup("##AddTransformer"u8))
        {
            var transformerType = RenderCreateTransformer(_textBuffer);
            if (transformerType.HasValue)
            {
                _pendingAdds.Add(transformerType.Value);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void RenderFinalButtons()
    {
        var hasChanges = HasPendingChanges();

        ImGui.BeginDisabled(!hasChanges);
        if (ImGui.Button("\uf14a Apply"u8))
            Apply();
        ImGui.SameLine();
        if (ImGui.Button("\uf2d3 Discard"u8))
            Discard();
        ImGui.EndDisabled();

        if (hasChanges)
            ImGui.TextUnformatted("\uf071 Some changes are not applied yet!"u8);
        else
            ImGui.NewLine();
    }

    private static bool AreTransformersEqual(
        IReadOnlyList<WeakReference<AssetTransformer>?> lhs,
        IReadOnlyList<AssetTransformer?> rhs)
    {
        if (lhs.Count != rhs.Count)
            return false;

        for (var index = 0; index < lhs.Count; ++index)
        {
            AssetTransformer? lhsTransformer = null;
            lhs[index]?.TryGetTarget(out lhsTransformer);
            if (!ReferenceEquals(lhsTransformer, rhs[index]))
                return false;
        }
        return true;
    }

    private void ClearInspectors()
    {
        for (var index = _inspectorWidgets.Count - 1; index >= 0; --index)
            _inspectorWidgets[index].Dispose();
        _inspectorWidgets.Clear();
        _transformers.Clear();
    }

    private static string GetPath(string fullPath)
    {
        var normalized = fullPath.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash >= 0 ? normalized[..(slash + 1)] : string.Empty;
    }

    private Project GetProject()
    {
        var projectFound = _project.TryGetTarget(out var project);
        Debug.Assert(projectFound);
        return project!;
    }

    private readonly WeakReference<Project> _project;

    private string _resourceName = string.Empty;
    private AssetPipeline? _resource;

    private readonly List<WeakReference<AssetTransformer>?> _transformers = [];
    private readonly List<SerializableInspectorWidget> _inspectorWidgets = [];

    private ModifyResourceAction? _pendingAction;
    private readonly List<WeakReference<AssetTransformer>> _pendingRemoves = [];
    private readonly List<StringHash> _pendingAdds = [];

    private readonly CString _textBuffer = new();
}
