// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EditorActionFrame = System.UInt64;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Declare Editor-only type to avoid interference with user code.</summary>
[Reflected]
public partial class SceneResourceForEditor : SceneResource
{
}

public sealed class SceneViewAddonData
{
    public WeakReference<SceneViewAddon>? Addon;
    public object? Value;
}

/// <summary>Single page of SceneViewTab.</summary>
public class SceneViewPage : RedObject
{
    public SceneViewPage(SceneResource resource)
    {
        Resource = resource;
        Scene = resource.Scene;
        Renderer = new SceneRendererToTexture(Scene);
        CfgFileName = Resource.AbsoluteFileName + ".user.json";

        Scene.SetFileName(Resource.AbsoluteFileName);
        Scene.SetUpdateEnabled(false);
    }

    public override void Dispose()
    {
        if (_reloadBeginCallback is not null)
            Resource.OnReloadBegin -= _reloadBeginCallback;
        if (_reloadEndCallback is not null)
            Resource.OnReloadEnd -= _reloadEndCallback;
        if (_selectionChangedCallback is not null)
            Selection.OnChanged -= _selectionChangedCallback;
        _reloadBeginCallback = null;
        _reloadEndCallback = null;
        _selectionChangedCallback = null;

        CurrentSimulationAction = null;
        LoadingSelection = null;
        foreach (var data in AddonData.Values)
        {
            data.Value = null;
            data.Addon = null;
        }
        AddonData.Clear();
        Renderer.Dispose();
        base.Dispose();
    }

    public ref object? GetAddonData(SceneViewAddon addon)
    {
        var uniqueName = addon.GetUniqueName();
        if (!AddonData.TryGetValue(uniqueName, out var data))
        {
            data = new SceneViewAddonData();
            AddonData.Add(uniqueName, data);
        }

        if (data.Addon is null
            || !data.Addon.TryGetTarget(out var existingAddon)
            || !ReferenceEquals(existingAddon, addon))
        {
            data.Addon = new WeakReference<SceneViewAddon>(addon);
            data.Value = null;
        }
        return ref data.Value;
    }

    public readonly SceneResource Resource;
    public readonly Scene Scene;
    public readonly SceneRendererToTexture Renderer;
    public readonly string CfgFileName;

    public readonly Dictionary<string, SceneViewAddonData> AddonData = new(StringComparer.Ordinal);

    public readonly SceneSelection Selection = new();
    public readonly PackedSceneSelection OldSelection = new();
    public readonly PackedSceneSelection NewSelection = new();

    public bool IgnoreNextReload;
    public PackedSceneSelection? LoadingSelection;

    public SimulateSceneAction? CurrentSimulationAction;

    public Ray CameraRay;

    public PackedSceneData ArchivedScene = new();
    public PackedSceneSelection ArchivedSelection = new();

    /// <summary>UI state.</summary>
    public Rect ContentArea;

    public bool IsSimulationActive()
    {
        return CurrentSimulationAction is not null && !CurrentSimulationAction.IsComplete();
    }

    public void StartSimulation(SceneViewTab owner)
    {
        CurrentSimulationAction = owner.PushAction(() => new SimulateSceneAction(this));
    }

    public void BeginSelection()
    {
        Selection.Update();
        Selection.Save(OldSelection);
    }

    public void EndSelection(SceneViewTab owner)
    {
        Selection.Save(NewSelection);
        if (OldSelection != NewSelection)
            owner.PushAction(new ChangeSceneSelectionAction(this, OldSelection, NewSelection));
    }

    public void SetReloadCallbacks(
        SceneResource.ReloadBeginHandler reloadBeginCallback,
        SceneResource.ReloadEndHandler reloadEndCallback,
        Action<SceneSelection> selectionChangedCallback)
    {
        _reloadBeginCallback = reloadBeginCallback;
        _reloadEndCallback = reloadEndCallback;
        _selectionChangedCallback = selectionChangedCallback;
        Resource.OnReloadBegin += _reloadBeginCallback;
        Resource.OnReloadEnd += _reloadEndCallback;
        Selection.OnChanged += _selectionChangedCallback;
    }

    private SceneResource.ReloadBeginHandler? _reloadBeginCallback;
    private SceneResource.ReloadEndHandler? _reloadEndCallback;
    private Action<SceneSelection>? _selectionChangedCallback;
}

/// <summary>Interface of SceneViewTab addon.</summary>
public abstract class SceneViewAddon : RedObject
{
    protected SceneViewAddon(SceneViewTab owner)
    {
        _owner = new WeakReference<SceneViewTab>(owner);
    }

    public override void Dispose()
    {
        _owner.SetTarget(null!);
        base.Dispose();
    }

    /// <summary>Return unique name of the addon for serialization.</summary>
    public abstract string GetUniqueName();

    /// <summary>Return input priority.</summary>
    public virtual int GetInputPriority() => 0;

    /// <summary>Return priority in the toolbar.</summary>
    public virtual int GetToolbarPriority() => 0;

    /// <summary>Initialize addon for the given page.</summary>
    public virtual void Initialize(SceneViewPage page)
    {
    }

    /// <summary>Process input.</summary>
    public virtual void ProcessInput(SceneViewPage page, ref bool mouseConsumed)
    {
    }

    /// <summary>Update and render addon.</summary>
    public virtual void Render(SceneViewPage page)
    {
    }

    /// <summary>Apply hotkeys for given addon.</summary>
    public virtual void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        hotkeyManager.InvokeFor(this);
    }

    /// <summary>Render context menu of the tab.</summary>
    public virtual bool RenderTabContextMenu() => false;

    /// <summary>Render main toolbar.</summary>
    public virtual bool RenderToolbar() => false;

    /// <summary>Serialize per-scene page state of the addon.</summary>
    public virtual void SerializePageState(Archive archive, string name, ref object? stateWrapped)
    {
        // Just open empty block
        using var block = archive.OpenUnorderedBlock(name);
    }

    /// <summary>Check if this type of drag&amp;drop payload is accepted.</summary>
    public virtual bool IsDragDropPayloadSupported(SceneViewPage page, DragDropPayload? payload) => false;

    /// <summary>Begin drag&amp;drop operation, render preview.</summary>
    public virtual void BeginDragDrop(SceneViewPage page, DragDropPayload? payload)
    {
    }

    /// <summary>Update drag&amp;drop state, called continuously while dragging.</summary>
    public virtual void UpdateDragDrop(DragDropPayload? payload)
    {
    }

    /// <summary>End drag&amp;drop operation and commit result.</summary>
    public virtual void CompleteDragDrop(DragDropPayload? payload)
    {
    }

    /// <summary>End drag&amp;drop operation and discard result.</summary>
    public virtual void CancelDragDrop()
    {
    }

    /// <summary>Write INI settings to file. Use as few lines as possible.</summary>
    public virtual void WriteIniSettings(ImGuiTextBuffer output)
    {
    }

    /// <summary>Read INI settings from file. Use as few lines as possible.</summary>
    public virtual void ReadIniSettings(string line)
    {
    }

    protected readonly WeakReference<SceneViewTab> _owner;
}

/// <summary>Tab that renders Scene and enables Scene manipulation.</summary>
public class SceneViewTab : ResourceEditorTab
{
    private static readonly EditorHotkey HotkeyRewindSimulation =
        new EditorHotkey("SceneViewTab.RewindSimulation").Press(Key.Unknown);
    private static readonly EditorHotkey HotkeyTogglePaused =
        new EditorHotkey("SceneViewTab.TogglePaused").Press(Key.Pause).MaybeMouse();

    private static readonly EditorHotkey HotkeyCut =
        new EditorHotkey("SceneViewTab.Cut").Ctrl().Press(Key.X);
    private static readonly EditorHotkey HotkeyCopy =
        new EditorHotkey("SceneViewTab.Copy").Ctrl().Press(Key.C);
    private static readonly EditorHotkey HotkeyPaste =
        new EditorHotkey("SceneViewTab.Paste").Ctrl().Press(Key.V);
    private static readonly EditorHotkey HotkeyPasteInto =
        new EditorHotkey("SceneViewTab.PasteInto").Ctrl().Shift().Press(Key.V);
    private static readonly EditorHotkey HotkeyDelete =
        new EditorHotkey("SceneViewTab.Delete").Press(Key.Delete);
    private static readonly EditorHotkey HotkeyDuplicate =
        new EditorHotkey("SceneViewTab.Duplicate").Ctrl().Press(Key.D);

    private static readonly EditorHotkey HotkeyFocus =
        new EditorHotkey("SceneViewTab.Focus").Press(Key.F);
    private static readonly EditorHotkey HotkeyMoveToLatest =
        new EditorHotkey("SceneViewTab.MoveToLatest");
    private static readonly EditorHotkey HotkeyMovePositionToLatest =
        new EditorHotkey("SceneViewTab.MovePositionToLatest");
    private static readonly EditorHotkey HotkeyMoveRotationToLatest =
        new EditorHotkey("SceneViewTab.MoveRotationToLatest");
    private static readonly EditorHotkey HotkeyMoveScaleToLatest =
        new EditorHotkey("SceneViewTab.MoveScaleToLatest");
    private static readonly EditorHotkey HotkeyMakePersistent =
        new EditorHotkey("SceneViewTab.MakePersistent");

    private static readonly EditorHotkey HotkeyCreateSiblingNode =
        new EditorHotkey("SceneViewTab.CreateSiblingNode").Ctrl().Press(Key.N);
    private static readonly EditorHotkey HotkeyCreateChildNode =
        new EditorHotkey("SceneViewTab.CreateChildNode").Ctrl().Shift().Press(Key.N);

    public event Action<SceneViewPage, Vector3f>? OnLookAt;
    public event Action<SceneViewPage, Scene, SceneSelection>? OnSelectionEditMenu;

    public SceneViewTab()
        : base("Scene", "9f4f7432-dd60-4c83-aecd-2f6cf69d3549",
            EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault | EditorTabFlag.FocusOnStart,
            EditorTabPlacement.DockCenter)
    {
        BindHotkey(HotkeyRewindSimulation, RewindSimulation);
        BindHotkey(HotkeyTogglePaused, ToggleSimulationPaused);
        BindHotkey(HotkeyCut, CutSelection);
        BindHotkey(HotkeyCopy, CopySelection);
        BindHotkey(HotkeyPaste, PasteNextToSelection);
        BindHotkey(HotkeyPasteInto, PasteIntoSelection);
        BindHotkey(HotkeyDelete, DeleteSelection);
        BindHotkey(HotkeyDuplicate, DuplicateSelection);
        BindHotkey(HotkeyFocus, FocusSelection);
        BindHotkey(HotkeyMoveToLatest, MoveSelectionToLatest);
        BindHotkey(HotkeyMovePositionToLatest, MoveSelectionPositionToLatest);
        BindHotkey(HotkeyMoveRotationToLatest, MoveSelectionRotationToLatest);
        BindHotkey(HotkeyMoveScaleToLatest, MoveSelectionScaleToLatest);
        BindHotkey(HotkeyMakePersistent, MakePersistent);
        BindHotkey(HotkeyCreateSiblingNode, CreateNodeNextToSelection);
        BindHotkey(HotkeyCreateChildNode, CreateNodeInSelection);

        SubscribeToEvent<EngineEvents.BeginPluginReloadEvent>(
            (ref EngineEvents.BeginPluginReloadEvent _) => BeginPluginReload());
        SubscribeToEvent<EngineEvents.EndPluginReloadEvent>(
            (ref EngineEvents.EndPluginReloadEvent _) => EndPluginReload());
    }

    public override void Dispose()
    {
        _dragAndDropAddon = null;
        foreach (var page in _scenes.Values)
            page.Dispose();
        _scenes.Clear();
        _addonsByName.Clear();
        _addonsByToolbarPriority.Clear();
        _addonsByInputPriority.Clear();
        for (var index = _addons.Count - 1; index >= 0; --index)
            _addons[index].Dispose();
        _addons.Clear();
        _hotkeyLabel.Dispose();
        OnSelectionEditMenu = null;
        OnLookAt = null;
        base.Dispose();
    }

    /// <summary>Register new scene addon.</summary>
    public void RegisterAddon(SceneViewAddon addon)
    {
        _addons.Add(addon);

        var inputIndex = 0;
        while (inputIndex < _addonsByInputPriority.Count
            && _addonsByInputPriority[inputIndex].GetInputPriority() >= addon.GetInputPriority())
        {
            ++inputIndex;
        }
        _addonsByInputPriority.Insert(inputIndex, addon);

        var toolbarIndex = 0;
        while (toolbarIndex < _addonsByToolbarPriority.Count
            && _addonsByToolbarPriority[toolbarIndex].GetToolbarPriority() <= addon.GetToolbarPriority())
        {
            ++toolbarIndex;
        }
        _addonsByToolbarPriority.Insert(toolbarIndex, addon);

        var nameIndex = 0;
        while (nameIndex < _addonsByName.Count
            && string.CompareOrdinal(_addonsByName[nameIndex].GetUniqueName(), addon.GetUniqueName()) <= 0)
        {
            ++nameIndex;
        }
        _addonsByName.Insert(nameIndex, addon);
    }

    public T RegisterAddon<T>(T addon)
        where T : SceneViewAddon
    {
        RegisterAddon((SceneViewAddon)addon);
        return addon;
    }

    public T? GetAddon<T>()
        where T : SceneViewAddon
    {
        foreach (var addon in _addons)
        {
            if (addon is T casted)
                return casted;
        }
        return null;
    }

    /// <summary>Setup context for plugin application execution.</summary>
    public void SetupPluginContext()
    {
        // TODO: port plugin application execution and its Scene context setup.
        // SceneViewPage* activePage = GetActivePage();
        // auto engine = GetSubsystem<Engine>();
        // if (activePage)
        // {
        //     engine->SetParameter(Param_SceneName, GetActiveResourceName());
        //     engine->SetParameter(Param_ScenePosition, activePage->renderer_->GetCameraPosition());
        //     engine->SetParameter(Param_SceneRotation, activePage->renderer_->GetCameraRotation());
        // }
        // else
        // {
        //     engine->SetParameter(Param_SceneName, Variant::EMPTY);
        //     engine->SetParameter(Param_ScenePosition, Variant::EMPTY);
        //     engine->SetParameter(Param_SceneRotation, Variant::EMPTY);
        // }
    }

    /// <summary>Draw Edit menu for selection in the scene.</summary>
    public void RenderEditMenu(Scene scene, SceneSelection selection)
    {
        var hasNodeSelection = selection.Nodes.Count != 0;
        var hasSelection = hasNodeSelection || selection.Components.Count != 0;
        var hasClipboard = _clipboard.HasNodesOrComponents;

        SetHotkeyLabel(HotkeyCut);
        if (ImGui.MenuItem("Cut"u8, _hotkeyLabel.ContentBytes, false, hasSelection))
            CutSelection(selection);
        SetHotkeyLabel(HotkeyCopy);
        if (ImGui.MenuItem("Copy"u8, _hotkeyLabel.ContentBytes, false, hasSelection))
            CopySelection(selection);
        SetHotkeyLabel(HotkeyPaste);
        if (ImGui.MenuItem("Paste"u8, _hotkeyLabel.ContentBytes, false, hasClipboard))
            PasteNextToSelection(scene, selection);
        SetHotkeyLabel(HotkeyPasteInto);
        if (ImGui.MenuItem("Paste Into"u8, _hotkeyLabel.ContentBytes, false, hasClipboard))
            PasteIntoSelection(scene, selection);
        SetHotkeyLabel(HotkeyDelete);
        if (ImGui.MenuItem("Delete"u8, _hotkeyLabel.ContentBytes, false, hasSelection))
            DeleteSelection(selection);
        SetHotkeyLabel(HotkeyDuplicate);
        if (ImGui.MenuItem("Duplicate"u8, _hotkeyLabel.ContentBytes, false, hasSelection))
            DuplicateSelection(selection);

        ImGui.Separator();

        SetHotkeyLabel(HotkeyFocus);
        if (ImGui.MenuItem("Focus"u8, _hotkeyLabel.ContentBytes, false, hasSelection))
            FocusSelection(selection);

        if (hasNodeSelection)
        {
            SetHotkeyLabel(HotkeyMoveToLatest);
            if (ImGui.MenuItem("Move to Latest"u8, _hotkeyLabel.ContentBytes))
                MoveSelectionToLatest(selection);
            if (ImGui.BeginMenu("Move Attribute to Latest..."u8))
            {
                SetHotkeyLabel(HotkeyMovePositionToLatest);
                if (ImGui.MenuItem("Position"u8, _hotkeyLabel.ContentBytes))
                    MoveSelectionPositionToLatest(selection);
                SetHotkeyLabel(HotkeyMoveRotationToLatest);
                if (ImGui.MenuItem("Rotation"u8, _hotkeyLabel.ContentBytes))
                    MoveSelectionRotationToLatest(selection);
                SetHotkeyLabel(HotkeyMoveScaleToLatest);
                if (ImGui.MenuItem("Scale"u8, _hotkeyLabel.ContentBytes))
                    MoveSelectionScaleToLatest(selection);
                ImGui.EndMenu();
            }
        }

        SetHotkeyLabel(HotkeyMakePersistent);
        if (ImGui.MenuItem("Make Persistent"u8, _hotkeyLabel.ContentBytes, false, hasSelection))
            MakePersistent(selection);

        if (GetActivePage() is { } activePage)
        {
            ImGui.Separator();
            OnSelectionEditMenu?.Invoke(activePage, scene, selection);
        }

        var hasComponentSelection = selection.Components.Count != 0;
        if (hasComponentSelection)
        {
            ImGui.Separator();

            if (ImGui.BeginMenu("Replace Component"u8))
            {
                if (CreateComponentMenuFunctions.RenderCreateComponentMenu() is { } componentType)
                    ReplaceSelectedComponents(selection, componentType.TypeId);

                ImGui.EndMenu();
            }
        }
    }

    /// <summary>Draw Create menu for selection in the scene.</summary>
    public void RenderCreateMenu(Scene scene, SceneSelection selection)
    {
        SetHotkeyLabel(HotkeyCreateSiblingNode);
        if (ImGui.MenuItem("Create Node"u8, _hotkeyLabel.ContentBytes))
            CreateNodeNextToSelection(scene, selection);

        SetHotkeyLabel(HotkeyCreateChildNode);
        if (ImGui.MenuItem("Create Child Node"u8, _hotkeyLabel.ContentBytes))
            CreateNodeInSelection(scene, selection);

        ImGui.MenuItem("Create Component:"u8, ReadOnlySpan<byte>.Empty, false, false);
        ImGui.Indent();
        if (CreateComponentMenuFunctions.RenderCreateComponentMenu() is { } componentType)
            CreateComponentInSelection(scene, selection, componentType.TypeId);
        ImGui.Unindent();
    }

    /// <summary>Set whether component selection is supported.</summary>
    public void SetComponentSelection(bool enabled) => _componentSelection = enabled;

    /// <summary>Commands.</summary>
    public void ResumeSimulation()
    {
        var activePage = GetActivePage();
        if (activePage is null)
            return;

        if (!activePage.IsSimulationActive())
            activePage.StartSimulation(this);
        activePage.Scene.SetUpdateEnabled(true);
    }

    public void PauseSimulation()
    {
        var activePage = GetActivePage();
        if (activePage is null)
            return;

        if (activePage.IsSimulationActive())
            activePage.CurrentSimulationAction!.Complete(true);
        activePage.Scene.SetUpdateEnabled(false);
    }

    public void ToggleSimulationPaused()
    {
        var activePage = GetActivePage();
        if (activePage is null)
            return;

        if (activePage.Scene.IsUpdateEnabled())
            PauseSimulation();
        else
            ResumeSimulation();
    }

    public void RewindSimulation()
    {
        var activePage = GetActivePage();
        if (activePage is null || !activePage.IsSimulationActive())
            return;

        // Simulation is stored as EditorAction, so we can just undo it
        var undoManager = GetUndoManager();
        undoManager.Undo();
    }

    public void CompactObjectIds()
    {
        var activePage = GetActivePage();
        if (activePage is null)
            return;

        // Preserve and clear selection
        var oldSelectionData = new PackedSceneSelection();
        activePage.Selection.Save(oldSelectionData);
        PushAction(new ChangeSceneSelectionAction(
            activePage, oldSelectionData, new PackedSceneSelection()));
        activePage.Selection.Clear();

        // Preserve and remap scene;
        var oldSceneData = PackedSceneData.FromScene(activePage.Scene);

        SetSceneNextIds(activePage.Scene, 0, 0);
        var sceneData = PackedSceneData.FromScene(activePage.Scene);
        sceneData.ToScene(activePage.Scene, PrefabLoadFlag.DiscardIds);

        PushAction(new ChangeSceneAction(activePage.Scene, oldSceneData));
    }

    public void CutSelection(SceneSelection selection)
    {
        CopySelection(selection);
        DeleteSelection(selection);
    }

    public void CopySelection(SceneSelection selection)
    {
        var nodes = CopyWeakTargets(selection.Nodes);
        var components = CopyWeakTargets(selection.Components);

        if (nodes.Count != 0)
            _clipboard = PackedNodeComponentData.FromNodes(nodes);
        else if (components.Count != 0)
            _clipboard = PackedNodeComponentData.FromComponents(components);
    }

    public void PasteNextToSelection(Scene scene, SceneSelection selection)
    {
        if (_clipboard.HasNodes)
        {
            var siblingNode = selection.ActiveNodeOrScene;
            var parentNode = siblingNode?.Parent ?? scene;

            selection.Clear();
            foreach (var packedNode in _clipboard.Nodes)
            {
                var builder = new CreateNodeActionBuilder(scene, packedNode.EffectiveScopeHint);

                var newNode = packedNode.SpawnCopy(parentNode);
                selection.SetSelected(newNode, true);

                PushAction(builder.Build(newNode)!);
            }
        }
        else if (_clipboard.HasComponents)
        {
            PasteIntoSelection(scene, selection);
        }
    }

    public void PasteIntoSelection(Scene scene, SceneSelection selection)
    {
        // Copy because selection changes during paste
        var selectedNodes = CopyWeakTargets(selection.EffectiveNodesAndScenes);
        if (selectedNodes.Count == 0)
            selectedNodes.Add(scene);

        if (_clipboard.HasNodes)
        {
            selection.Clear();
            foreach (var selectedNode in selectedNodes)
            {
                foreach (var packedNode in _clipboard.Nodes)
                {
                    var builder = new CreateNodeActionBuilder(scene, packedNode.EffectiveScopeHint);

                    var newNode = packedNode.SpawnCopy(selectedNode);
                    selection.SetSelected(newNode, true);

                    PushAction(builder.Build(newNode)!);
                }
            }
        }
        else if (_clipboard.HasComponents)
        {
            selection.Clear();
            foreach (var selectedNode in selectedNodes)
            {
                foreach (var packedComponent in _clipboard.Components)
                {
                    var builder = new CreateComponentActionBuilder(selectedNode, packedComponent.Type);
                    var newComponent = packedComponent.SpawnCopy(selectedNode);
                    PushAction(builder.Build(newComponent)!);

                    if (_componentSelection)
                        selection.SetSelected(newComponent, true);
                    else
                        selection.SetSelected(selectedNode, true);
                }
            }
        }
    }

    public void DeleteSelection(SceneSelection selection)
    {
        var nodes = CopyWeakTargets(selection.Nodes);
        var components = CopyWeakTargets(selection.Components);

        foreach (var node in nodes)
        {
            if (node.Parent is not null)
            {
                var builder = new RemoveNodeActionBuilder(node);

                node.Remove();

                PushAction(builder.Build()!);
            }
        }

        foreach (var component in components)
        {
            var builder = new RemoveComponentActionBuilder(component);
            component.Remove();
            PushAction(builder.Build()!);
        }

        selection.Clear();
    }

    public void DuplicateSelection(SceneSelection selection)
    {
        var nodes = CopyWeakTargets(selection.Nodes);
        if (nodes.Count != 0)
        {
            // Copy because selection changes during paste
            selection.Clear();

            foreach (var node in nodes)
            {
                var parent = node.Parent;
                Debug.Assert(parent is not null);
                var data = new PackedNodeData(node);

                var builder = new CreateNodeActionBuilder(
                    parent!.Scene, data.EffectiveScopeHint);

                var newNode = data.SpawnCopy(parent);
                selection.SetSelected(newNode, true);

                PushAction(builder.Build(newNode)!);
            }
        }
        else
        {
            var components = CopyWeakTargets(selection.Components);
            if (components.Count == 0)
                return;

            // Copy because selection changes during paste
            selection.Clear();

            foreach (var component in components)
            {
                var node = component.TryGetNode();
                Debug.Assert(node is not null);

                var data = new PackedComponentData(component);

                var builder = new CreateComponentActionBuilder(node, data.Type);
                var newComponent = data.SpawnCopy(node!);
                PushAction(builder.Build(newComponent)!);

                if (_componentSelection)
                    selection.SetSelected(newComponent, true);
                else
                    selection.SetSelected(node, true);
            }
        }
    }

    public void CreateNodeNextToSelection(Scene scene, SceneSelection selection)
    {
        var siblingNode = selection.ActiveNodeOrScene;
        var parentNode = siblingNode?.Parent ?? scene;

        var builder = new CreateNodeActionBuilder(scene, AttributeScopeHint.Attribute);

        var newNode = parentNode.CreateChild();
        selection.Clear();
        selection.SetSelected(newNode, true);

        PushAction(builder.Build(newNode)!);
    }

    public void CreateNodeInSelection(Scene scene, SceneSelection selection)
    {
        // Copy because selection changes during paste
        var selectedNodes = CopyWeakTargets(selection.EffectiveNodesAndScenes);
        if (selectedNodes.Count == 0)
            selectedNodes.Add(scene);

        selection.Clear();
        foreach (var selectedNode in selectedNodes)
        {
            var builder = new CreateNodeActionBuilder(scene, AttributeScopeHint.Attribute);

            var newNode = selectedNode.CreateChild();
            selection.SetSelected(newNode, true);

            PushAction(builder.Build(newNode)!);
        }
    }

    public void CreateComponentInSelection(
        Scene scene, SceneSelection selection, StringHash componentType)
    {
        // Copy because selection changes during paste
        var selectedNodes = CopyWeakTargets(selection.EffectiveNodesAndScenes);
        if (selectedNodes.Count == 0)
            selectedNodes.Add(scene);

        selection.Clear();
        foreach (var selectedNode in selectedNodes)
        {
            var builder = new CreateComponentActionBuilder(selectedNode, componentType);
            var newComponent = selectedNode.CreateComponent(componentType);
            PushAction(builder.Build(newComponent)!);

            if (_componentSelection)
                selection.SetSelected(newComponent, true);
            else
                selection.SetSelected(selectedNode, true);
        }
    }

    public void FocusSelection(SceneSelection selection)
    {
        if (selection.ActiveNode is { } activeNode
            && activeNode.Scene is { } scene
            && GetPage(scene) is { } page)
        {
            OnLookAt?.Invoke(page, activeNode.WorldPosition);
        }
    }

    public void MoveSelectionToLatest(SceneSelection selection)
    {
        if (selection.ActiveNode is { } activeNode)
        {
            var worldTransform = activeNode.GetWorldTransform();

            foreach (var nodeReference in selection.Nodes)
            {
                if (nodeReference.TryGetTarget(out var node) && !ReferenceEquals(node, activeNode))
                    node.SetWorldTransform(worldTransform);
            }
        }
    }

    public void MoveSelectionPositionToLatest(SceneSelection selection)
    {
        if (selection.ActiveNode is { } activeNode)
        {
            var worldPosition = activeNode.WorldPosition;

            foreach (var nodeReference in selection.Nodes)
            {
                if (nodeReference.TryGetTarget(out var node) && !ReferenceEquals(node, activeNode))
                    node.SetWorldPosition(worldPosition);
            }
        }
    }

    public void MoveSelectionRotationToLatest(SceneSelection selection)
    {
        if (selection.ActiveNode is { } activeNode)
        {
            var worldRotation = activeNode.WorldRotation;

            foreach (var nodeReference in selection.Nodes)
            {
                if (nodeReference.TryGetTarget(out var node) && !ReferenceEquals(node, activeNode))
                    node.SetWorldRotation(worldRotation);
            }
        }
    }

    public void MoveSelectionScaleToLatest(SceneSelection selection)
    {
        if (selection.ActiveNode is { } activeNode)
        {
            var worldScale = activeNode.WorldScale;

            foreach (var nodeReference in selection.Nodes)
            {
                if (nodeReference.TryGetTarget(out var node) && !ReferenceEquals(node, activeNode))
                    node.SetWorldScale(worldScale);
            }
        }
    }

    public void MakePersistent(SceneSelection selection)
    {
        foreach (var nodeReference in selection.Nodes)
        {
            if (nodeReference.TryGetTarget(out var node))
                node.SetTemporary(false);
        }

        foreach (var componentReference in selection.Components)
        {
            if (componentReference.TryGetTarget(out var component))
                component.SetTemporary(false);
        }
    }

    public void ReplaceSelectedComponents(SceneSelection selection, StringHash componentType)
    {
        // Copy selection because it is modified during replacement
        var components = CopyWeakTargets(selection.Components);
        selection.Clear();

        var action = new CompositeEditorAction();
        foreach (var oldComponent in components)
        {
            var node = oldComponent.TryGetNode();
            if (node is null)
                continue;
            var componentIndex = node.GetComponentIndex(oldComponent);

            // Create new component
            {
                var createBuilder = new CreateComponentActionBuilder(node, componentType);
                var newComponent = node.CreateComponent(componentType);
                if (newComponent is null)
                    continue;

                newComponent.CopyAttributes(oldComponent);
                newComponent.ApplyAttributes();
                node.ReorderComponent(newComponent, componentIndex);

                action.AddAction(createBuilder.Build(newComponent)!);

                selection.SetSelected(newComponent, true);
            }

            // Remove old component
            {
                var removeBuilder = new RemoveComponentActionBuilder(oldComponent);
                oldComponent.Remove();
                action.AddAction(removeBuilder.Build()!);
            }
        }

        // Push composite action to undo stack
        PushAction(action);
    }

    public void CutSelection()
    {
        if (GetActivePage() is { } activePage)
            CutSelection(activePage.Selection);
    }

    public void CopySelection()
    {
        if (GetActivePage() is { } activePage)
            CopySelection(activePage.Selection);
    }

    public void PasteNextToSelection()
    {
        if (GetActivePage() is { } activePage)
            PasteNextToSelection(activePage.Scene, activePage.Selection);
    }

    public void PasteIntoSelection()
    {
        if (GetActivePage() is { } activePage)
            PasteIntoSelection(activePage.Scene, activePage.Selection);
    }

    public void DeleteSelection()
    {
        if (GetActivePage() is { } activePage)
            DeleteSelection(activePage.Selection);
    }

    public void DuplicateSelection()
    {
        if (GetActivePage() is { } activePage)
            DuplicateSelection(activePage.Selection);
    }

    public void CreateNodeNextToSelection()
    {
        if (GetActivePage() is { } activePage)
            CreateNodeNextToSelection(activePage.Scene, activePage.Selection);
    }

    public void CreateNodeInSelection()
    {
        if (GetActivePage() is { } activePage)
            CreateNodeInSelection(activePage.Scene, activePage.Selection);
    }

    public void FocusSelection()
    {
        if (GetActivePage() is { } activePage)
            FocusSelection(activePage.Selection);
    }

    public void MoveSelectionToLatest()
    {
        if (GetActivePage() is { } activePage)
            MoveSelectionToLatest(activePage.Selection);
    }

    public void MoveSelectionPositionToLatest()
    {
        if (GetActivePage() is { } activePage)
            MoveSelectionPositionToLatest(activePage.Selection);
    }

    public void MoveSelectionRotationToLatest()
    {
        if (GetActivePage() is { } activePage)
            MoveSelectionRotationToLatest(activePage.Selection);
    }

    public void MoveSelectionScaleToLatest()
    {
        if (GetActivePage() is { } activePage)
            MoveSelectionScaleToLatest(activePage.Selection);
    }

    public void MakePersistent()
    {
        if (GetActivePage() is { } activePage)
            MakePersistent(activePage.Selection);
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    public override void PreRenderUpdate()
    {
        if (GetActivePage() is { } activePage)
        {
            if (!_componentSelection)
                activePage.Selection.ConvertToNodes();
            activePage.BeginSelection();
        }
    }

    public override void PostRenderUpdate()
    {
        if (GetActivePage() is { } activePage)
            activePage.EndSelection(this);
    }

    public override void RenderMenu()
    {
        if (ImGui.BeginMenu("Edit"u8))
        {
            RenderEditMenuItems();

            if (GetActivePage() is { } activePage)
            {
                ImGui.Separator();
                RenderEditMenu(activePage.Scene, activePage.Selection);
            }

            ImGui.EndMenu();
        }

        if (GetActivePage() is { } page && ImGui.BeginMenu("Create"u8))
        {
            RenderCreateMenu(page.Scene, page.Selection);
            ImGui.EndMenu();
        }
    }

    private static readonly CString _rewindToolbarLabel = new($"{Icons.CircleChevronLeft}##RewindSceneSimulation");
    private static readonly CString _pauseToolbarLabel = new($"{Icons.CirclePause}##ToggleSceneSimulation");
    private static readonly CString _playToolbarLabel = new($"{Icons.CirclePlay}##ToggleSceneSimulation");

    public override void RenderToolbar()
    {
        var activePage = GetActivePage();

        {
            var canRewind = activePage is not null && activePage.IsSimulationActive();
            ImGui.BeginDisabled(!canRewind);
            if (Widgets.ToolbarButton(_rewindToolbarLabel, "Rewind Scene Simulation"u8))
                RewindSimulation();
            ImGui.EndDisabled();
        }

        {
            var isStarted = activePage is not null && activePage.IsSimulationActive();
            var isUpdating = activePage is not null && activePage.Scene.IsUpdateEnabled();
            var label = isUpdating
                ? _pauseToolbarLabel
                : _playToolbarLabel;
            ReadOnlySpan<byte> tooltip = isUpdating
                ? "Pause Scene Simulation"u8
                : isStarted ? "Resume Scene Simulation"u8 : "Start Scene Simulation"u8;
            ImGui.BeginDisabled(activePage is null);
            if (Widgets.ToolbarButton(label, tooltip))
                ToggleSimulationPaused();
            ImGui.EndDisabled();
        }

        Widgets.ToolbarSeparator();

        foreach (var addon in _addonsByToolbarPriority)
        {
            if (addon.RenderToolbar())
                Widgets.ToolbarSeparator();
        }
    }

    public override bool IsUndoSupported() => true;

    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        base.ApplyHotkeys(hotkeyManager);

        foreach (var addon in _addons)
            addon.ApplyHotkeys(hotkeyManager);
    }

    private static readonly CString _rewindMenuLabel = new($"{Icons.CircleChevronLeft} Rewind Scene Simulation");
    private static readonly CString _resumeMenuLabel = new($"{Icons.CirclePlay} Resume Scene Simulation");
    private static readonly CString _startMenuLabel = new($"{Icons.CirclePlay} Start Scene Simulation");
    private static readonly CString _pauseMenuLabel = new($"{Icons.CirclePause} Pause Scene Simulation");

    public override void RenderContextMenuItems()
    {
        base.RenderContextMenuItems();

        if (GetActivePage() is { } activePage)
        {
            _contextMenuSeparator.Reset();

            SetHotkeyLabel(HotkeyRewindSimulation);
            if (ImGui.MenuItem(
                    _rewindMenuLabel,
                    _hotkeyLabel.ContentBytes,
                    false,
                    activePage.IsSimulationActive()))
            {
                RewindSimulation();
            }

            var pauseTitle = !activePage.Scene.IsUpdateEnabled()
                ? activePage.IsSimulationActive()
                    ? _resumeMenuLabel
                    : _startMenuLabel
                : _pauseMenuLabel;
            SetHotkeyLabel(HotkeyTogglePaused);
            if (ImGui.MenuItem(pauseTitle, _hotkeyLabel.ContentBytes))
                ToggleSimulationPaused();

            if (ImGui.MenuItem("Compact object IDs"u8))
                CompactObjectIds();
        }

        _contextMenuSeparator.Add();

        foreach (var addon in _addonsByName)
        {
            if (addon.RenderTabContextMenu())
                _contextMenuSeparator.Reset();
        }

        _contextMenuSeparator.Add();
    }

    public override void RenderContent()
    {
        var activePage = GetActivePage();
        if (activePage is null)
            return;

        if (!activePage.Scene.HasComponent<DebugRenderer>())
        {
            var debug = activePage.Scene.GetOrCreateComponent<DebugRenderer>();
            debug.SetTemporary(true);
            debug.SetLineAntiAlias(true);
        }

        var contentSize = GetContentSize();
        if (contentSize.X == 0 || contentSize.Y == 0)
            return;

        activePage.Renderer.SetTextureSize(contentSize);
        activePage.Renderer.Update();

        var basePosition = ImGui.GetCursorPos();

        var sceneTexture = activePage.Renderer.Texture;
        ImGui.SetCursorPos(basePosition);
        Widgets.ImageItem(sceneTexture, _textureReference,
            new Vector2f(sceneTexture.Width, sceneTexture.Height));

        var contentAreaMin = ImGui.GetItemRectMin();
        var contentAreaMax = ImGui.GetItemRectMax();
        activePage.ContentArea = new Rect(contentAreaMin, contentAreaMax);

        UpdateCameraRay();
        UpdateAddons(activePage);
    }

    public override string GetResourceTitle() => "Scene";

    public override bool SupportMultipleResources() => true;

    public override bool CanOpenResource(ResourceFileDescriptor descriptor)
    {
        return descriptor.HasObjectType<Scene>() || descriptor.HasObjectType<PrefabResource>();
    }

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        base.WriteIniSettings(output);
        foreach (var addon in _addons)
            addon.WriteIniSettings(output);
    }

    public override void ReadIniSettings(string line)
    {
        base.ReadIniSettings(line);
        foreach (var addon in _addons)
            addon.ReadIniSettings(line);
    }

    public override EditorActionFrame? PushAction(EditorAction action)
    {
        var activePage = GetActivePage();
        if (activePage is null)
            return null;

        // Ignore all actions while simulating
        if (activePage.IsSimulationActive())
            return null;

        if (action is ChangeSceneSelectionAction)
            return base.PushAction(action);

        var wrappedAction = new PreserveSceneSelectionWrapper(action, activePage);
        return base.PushAction(wrappedAction);
    }

    /// <summary>Return current state.</summary>
    public IReadOnlyList<SceneViewAddon> GetAddonsByName() => _addonsByName;

    public SceneViewPage? GetPage(string resourceName)
    {
        return _scenes.GetValueOrDefault(resourceName);
    }

    public SceneViewPage? GetPage(Scene scene)
    {
        foreach (var page in _scenes.Values)
        {
            if (ReferenceEquals(page.Scene, scene))
                return page;
        }
        return null;
    }

    public SceneViewPage? GetActivePage()
    {
        return GetPage(ActiveResourceName);
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    protected override void OnResourceLoaded(string resourceName)
    {
        var cache = GetSubsystem<ResourceCache>();
        var sceneResource = cache.GetResourceOrNull<SceneResourceForEditor>(resourceName);

        if (sceneResource is null)
        {
            Log.GetLogger().LogError("Cannot load scene file '{ResourceName}'", resourceName);
            return;
        }

        if (resourceName.EndsWith(".prefab", StringComparison.Ordinal))
            sceneResource.SetPrefab(true);

        var isActive = resourceName == ActiveResourceName;
        _scenes[resourceName] = CreatePage(sceneResource, isActive);
    }

    protected override void OnResourceUnloaded(string resourceName)
    {
        if (_scenes.Remove(resourceName, out var page))
            page.Dispose();
    }

    protected override void OnActiveResourceChanged(
        string oldResourceName, string newResourceName)
    {
        if (GetPage(oldResourceName) is { } oldActivePage)
            oldActivePage.Scene.SetUpdateEnabled(false);

        foreach (var (name, data) in _scenes)
            data.Renderer.SetActive(name == newResourceName);

        if (GetPage(newResourceName) is { } newActivePage)
            InspectSelection(newActivePage);
    }

    protected override void OnResourceSaved(string resourceName)
    {
        var page = GetPage(resourceName);
        if (page is null)
            return;

        SavePageConfig(page);
        SavePageScene(page);
    }

    protected override void OnResourceShallowSaved(string resourceName)
    {
        var page = GetPage(resourceName);
        if (page is null)
            return;

        SavePageConfig(page);

        if (ActiveResourceName == resourceName)
            SavePagePreview(page);
    }

    /// <summary>Manage pages.</summary>
    private SceneViewPage CreatePage(SceneResource sceneResource, bool isActive)
    {
        var page = new SceneViewPage(sceneResource);

        void ReloadBegin(SceneResource _, ref bool cancelReload)
        {
            if (page.IgnoreNextReload || IsResourceUnsaved(page.Resource.Name))
            {
                page.IgnoreNextReload = false;
                cancelReload = true;
                return;
            }

            page.LoadingSelection = page.Selection.Pack();
        }

        void ReloadEnd(SceneResource _, bool success)
        {
            if (success && page.LoadingSelection is not null)
            {
                page.Selection.Load(page.Scene, page.LoadingSelection);
                page.LoadingSelection = null;
            }
        }

        void SelectionChanged(SceneSelection _)
        {
            InspectSelection(page);
        }

        page.SetReloadCallbacks(ReloadBegin, ReloadEnd, SelectionChanged);
        page.Renderer.SetActive(isActive);

        LoadPageConfig(page);
        foreach (var addon in _addons)
            addon.Initialize(page);
        return page;
    }

    private void SavePageScene(SceneViewPage page)
    {
        var isLegacyScene = page.Resource.Name.EndsWith(".xml", StringComparison.Ordinal);

        page.Scene.SetUpdateEnabled(false);
        RecalculateSceneNextIds(page.Scene);

        using var buffer = new VectorBuffer();
        if (isLegacyScene)
        {
            using var xmlFile = new XMLFile();
            using var rootElement = xmlFile.GetOrCreateRoot("scene");
            page.Scene.SaveXML(rootElement);
            xmlFile.Save(buffer);
        }
        else
        {
            page.Resource.Save(buffer);
        }

        var sharedBuffer = buffer.GetBuffer();
        var weakPage = new WeakReference<SceneViewPage>(page);

        var project = GetProject();
        project.SaveFileDelayed(
            page.Resource.AbsoluteFileName,
            page.Resource.Name,
            sharedBuffer,
            (string _, string resourceName, ref bool needReload) =>
            {
                if (weakPage.TryGetTarget(out var pageTarget))
                {
                    // Force reload of the scene and/or prefabs, but ignore it in SceneViewTab
                    pageTarget.IgnoreNextReload = true;
                    needReload = true;
                }
            });
    }

    private void SavePageConfig(SceneViewPage page)
    {
        using var jsonFile = new JSONFile();
        if (jsonFile.SaveObjectCallback(archive => SerializePage(archive, "Scene", page, this)))
            jsonFile.SaveFile(new FileIdentifier(page.CfgFileName));
    }

    private void LoadPageConfig(SceneViewPage page)
    {
        if (!System.IO.File.Exists(page.CfgFileName))
            return;

        using var jsonFile = new JSONFile();
        if (jsonFile.LoadFile(new FileIdentifier(page.CfgFileName)))
            jsonFile.LoadObjectCallback(archive => SerializePage(archive, "Scene", page, this));
    }

    private void SavePagePreview(SceneViewPage page)
    {
        if (GetSubsystem<Graphics>()?.IsInitialized != true)
            return;

        var texture = page.Renderer.Texture;
        using var image = texture.GetImage();

        if (image is null)
            return;

        // Crop to square
        var size = new Vector2i(image.Width, image.Height);
        var minSide = Math.Min(size.X, size.Y);
        var excess = size - Vector2i.One * minSide;
        var offsetTopLeft = excess / 2;
        var offsetRightBottom = excess - offsetTopLeft;

        var rect = new IntRect(offsetTopLeft, size - offsetRightBottom);
        using var croppedImage = image.GetSubimage(rect);

        var path = GetProject().PreviewPngPath;
        croppedImage!.SavePNG(path);
    }

    private void UpdateAddons(SceneViewPage page)
    {
        var mouseConsumed = false;

        if (UpdateDropToScene())
            mouseConsumed = true;

        foreach (var addon in _addonsByInputPriority)
            addon.ProcessInput(page, ref mouseConsumed);

        foreach (var addon in _addons)
            addon.Render(page);
    }

    private void UpdateCameraRay()
    {
        var activePage = GetActivePage();
        if (activePage is null)
            return;

        var io = ImGui.GetIO();
        var camera = activePage.Renderer.Camera!;

        var viewportMin = ImGui.GetItemRectMin();
        var viewportMax = ImGui.GetItemRectMax();
        var viewportSize = viewportMax - viewportMin;
        var pos = (io.MousePos - viewportMin) / viewportSize;
        activePage.CameraRay = camera.GetScreenRay(pos.X, pos.Y);
    }

    private bool UpdateDropToScene()
    {
        var activePage = GetActivePage();
        if (activePage is not null && ImGui.BeginDragDropTarget())
        {
            var payload = DragDropPayload.Get();

            if (_dragAndDropAddon is null)
            {
                foreach (var addon in _addonsByInputPriority)
                {
                    if (!addon.IsDragDropPayloadSupported(activePage, payload))
                        continue;
                    _dragAndDropAddon = addon;
                    break;
                }
                _dragAndDropAddon?.BeginDragDrop(activePage, payload);
            }

            if (_dragAndDropAddon is not null)
            {
                var acceptedPayload = ImGui.AcceptDragDropPayload(
                    "DragDropPayload"u8,
                    ImGuiDragDropFlags.ImGuiDragDropFlagsAcceptBeforeDelivery);
                if (!acceptedPayload.IsNull)
                {
                    _dragAndDropAddon.UpdateDragDrop(payload);
                    var nativePayload = ImGui.GetDragDropPayload();
                    if (nativePayload.IsDelivery())
                    {
                        _dragAndDropAddon.CompleteDragDrop(payload);
                        _dragAndDropAddon = null;
                    }
                }
            }

            ImGui.EndDragDropTarget();
        }
        else if (_dragAndDropAddon is not null)
        {
            _dragAndDropAddon.CancelDragDrop();
            _dragAndDropAddon = null;
        }
        return _dragAndDropAddon is not null;
    }

    private void InspectSelection(SceneViewPage page)
    {
        CopyWeakTargets(page.Selection.NodesAndScenes, _nodeBuffer);
        CopyWeakTargets(page.Selection.Components, _componentBuffer);
        var project = GetProject();
        var request = new InspectNodeComponentRequest(_nodeBuffer, _componentBuffer);
        project.ProcessRequest(request, this);
    }

    private void BeginPluginReload()
    {
        foreach (var page in _scenes.Values)
        {
            page.ArchivedScene = PackedSceneData.FromScene(page.Scene);
            page.ArchivedSelection = page.Selection.Pack();
            page.Scene.Clear();
        }
    }

    private void EndPluginReload()
    {
        foreach (var page in _scenes.Values)
        {
            page.Scene.Clear();
            page.ArchivedScene.ToScene(page.Scene);
            page.Selection.Load(page.Scene, page.ArchivedSelection);
        }
    }

    private static void SerializePage(
        Archive archive, string name, SceneViewPage page, SceneViewTab owner)
    {
        using var block = archive.OpenUnorderedBlock(name);

        {
            var selection = archive.IsInput ? new PackedSceneSelection() : page.Selection.Pack();
            using (archive.OpenUnorderedBlock("Selection"))
                selection.SerializeInBlock(archive);

            if (archive.IsInput)
                page.Selection.Load(page.Scene, selection);
        }

        {
            using var addonsBlock = archive.OpenUnorderedBlock("Addons");
            foreach (var addon in owner.GetAddonsByName())
            {
                ref var state = ref page.GetAddonData(addon);
                addon.SerializePageState(archive, addon.GetUniqueName(), ref state);
            }
        }
    }

    private static void SetSceneNextIds(
        Scene scene, uint nextNodeId, uint nextComponentId)
    {
        using var nodeValue = new Variant(nextNodeId);
        scene.SetAttribute("Next Node ID", nodeValue);
        using var componentValue = new Variant(nextComponentId);
        scene.SetAttribute("Next Component ID", componentValue);
    }

    private static void RecalculateSceneNextIds(Scene scene)
    {
        var nextNodeId = 0u;
        var nextComponentId = 0u;

        bool NodeCallback(Node node)
        {
            if (node.IsTemporary)
                return false;

            nextNodeId = Math.Max(nextNodeId, node.Id + 1);
            return true;
        }

        void ComponentCallback(Component component)
        {
            if (!component.IsTemporary)
                nextComponentId = Math.Max(nextComponentId, component.Id + 1);
        }

        scene.TraverseDepthFirst(NodeCallback, ComponentCallback);
        SetSceneNextIds(scene, nextNodeId, nextComponentId);
    }

    private static void CopyWeakTargets<T>(
        IEnumerable<WeakReference<T>> source, List<T> destination)
        where T : class
    {
        destination.Clear();
        foreach (var reference in source)
        {
            if (reference.TryGetTarget(out var target))
                destination.Add(target);
        }
    }

    private static List<T> CopyWeakTargets<T>(IEnumerable<WeakReference<T>> source)
        where T : class
    {
        var result = new List<T>();
        foreach (var reference in source)
        {
            if (reference.TryGetTarget(out var target))
                result.Add(target);
        }
        return result;
    }

    private void SetHotkeyLabel(EditorHotkey hotkey)
    {
        _hotkeyLabel.Set(GetHotkeyLabel(hotkey));
    }

    private readonly List<SceneViewAddon> _addons = [];
    private readonly List<SceneViewAddon> _addonsByInputPriority = [];
    private readonly List<SceneViewAddon> _addonsByToolbarPriority = [];
    private readonly List<SceneViewAddon> _addonsByName = [];

    private SceneViewAddon? _dragAndDropAddon;

    private readonly Dictionary<string, SceneViewPage> _scenes = new(StringComparer.Ordinal);
    private PackedNodeComponentData _clipboard = new();

    private bool _componentSelection = true;

    private readonly List<Node> _nodeBuffer = [];
    private readonly List<Component> _componentBuffer = [];
    private readonly CString _hotkeyLabel = new();
    private readonly ImTextureRef _textureReference = new();
}

/// <summary>Action for scene simulation interval.</summary>
public class SimulateSceneAction : EditorAction
{
    public SimulateSceneAction(SceneViewPage page)
    {
        _page = new WeakReference<SceneViewPage>(page);
        _oldData = PackedSceneData.FromScene(page.Scene);
        page.Selection.Save(_oldSelection);
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool IsComplete() => _isComplete;

    public override void Complete(bool force)
    {
        if (!force)
            return;

        _isComplete = true;
        if (_page.TryGetTarget(out var page))
        {
            page.Scene.SetElapsedTime(0.0f);
            page.Scene.SetUpdateEnabled(false);
            _newData = PackedSceneData.FromScene(page.Scene);
            page.Selection.Save(_newSelection);
        }
    }

    public override bool CanUndoRedo()
    {
        return _page.TryGetTarget(out _) && _newData.HasSceneData;
    }

    public override void Redo()
    {
        SetState(_newData, _newSelection);
    }

    public override void Undo()
    {
        SetState(_oldData, _oldSelection);
    }

    private void SetState(PackedSceneData data, PackedSceneSelection selection)
    {
        _page.TryGetTarget(out var page);
        page!.Scene.SetUpdateEnabled(false);
        data.ToScene(page.Scene);
        page.Selection.Load(page.Scene, selection);
    }

    private readonly WeakReference<SceneViewPage> _page;
    private bool _isComplete;

    private readonly PackedSceneData _oldData;
    private readonly PackedSceneSelection _oldSelection = new();

    private PackedSceneData _newData = new();
    private readonly PackedSceneSelection _newSelection = new();
}

/// <summary>Action for scene selection.</summary>
public class ChangeSceneSelectionAction : EditorAction
{
    public ChangeSceneSelectionAction(SceneViewPage page,
        PackedSceneSelection oldSelection, PackedSceneSelection newSelection)
    {
        _page = new WeakReference<SceneViewPage>(page);
        _oldSelection = oldSelection.Clone();
        _newSelection = newSelection.Clone();
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool IsTransparent() => true;

    public override void Redo()
    {
        SetSelection(_newSelection);
    }

    public override void Undo()
    {
        SetSelection(_oldSelection);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeSceneSelectionAction otherAction)
            return false;

        _page.TryGetTarget(out var page);
        otherAction._page.TryGetTarget(out var otherPage);
        if (!ReferenceEquals(page, otherPage))
            return false;

        _newSelection = otherAction._newSelection.Clone();
        return true;
    }

    private void SetSelection(PackedSceneSelection selection)
    {
        if (_page.TryGetTarget(out var page))
        {
            page.Selection.Load(page.Scene, selection);
            page.BeginSelection();
        }
    }

    private readonly WeakReference<SceneViewPage> _page;
    private readonly PackedSceneSelection _oldSelection;
    private PackedSceneSelection _newSelection;
}

/// <summary>Wrapper to preserve scene selection on undo/redo.</summary>
public class PreserveSceneSelectionWrapper : BaseEditorActionWrapper
{
    public PreserveSceneSelectionWrapper(EditorAction action, SceneViewPage page)
        : base(action)
    {
        _page = new WeakReference<SceneViewPage>(page);
        _selection = page.Selection.Pack();
    }

    /// <summary>Implement BaseEditorActionWrapper.</summary>
    public override bool CanRedo()
    {
        return _page.TryGetTarget(out _) && _action.CanRedo();
    }

    public override void Redo()
    {
        _action.Redo();
        _page.TryGetTarget(out var page);
        page!.Selection.Load(page.Scene, _selection);
    }

    public override bool CanUndo()
    {
        return _page.TryGetTarget(out _) && _action.CanUndo();
    }

    public override void Undo()
    {
        _action.Undo();
        _page.TryGetTarget(out var page);
        page!.Selection.Load(page.Scene, _selection);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not PreserveSceneSelectionWrapper otherWrapper)
            return false;

        _page.TryGetTarget(out var page);
        otherWrapper._page.TryGetTarget(out var otherPage);
        if (!ReferenceEquals(page, otherPage) || _selection != otherWrapper._selection)
            return false;

        return _action.MergeWith(otherWrapper._action);
    }

    private readonly WeakReference<SceneViewPage> _page;
    private readonly PackedSceneSelection _selection;
}

public static class SceneViewFunctions
{
    /// <summary>Helper function to query geometries from a scene.</summary>
    public static List<RayQueryResult> QueryGeometriesFromScene(
        Scene scene,
        Ray ray,
        RayQueryLevel level = RayQueryLevel.RayTriangle,
        float maxDistance = float.PositiveInfinity,
        uint viewMask = Drawable.DefaultViewMask)
    {
        var results = new List<RayQueryResult>();
        using var query = new RayOctreeQuery(
            results, ray, level, maxDistance, DrawableFlags.Geometry, viewMask);
        scene.GetComponent<Octree>()?.Raycast(query);
        return results;
    }
}
