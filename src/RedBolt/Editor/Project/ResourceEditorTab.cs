// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using EditorActionFrame = System.UInt64;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Input;
using RedBolt.SystemUI;

/// <summary>Base class for editor tab that represents engine resource.</summary>
public abstract class ResourceEditorTab : EditorTab
{
    private static readonly EditorHotkey HotkeySaveDocument =
        new EditorHotkey("Global.SaveDocument").Ctrl().Press(Key.S);
    private static readonly EditorHotkey HotkeyCloseDocument =
        new EditorHotkey("Global.CloseDocument").Ctrl().Press(Key.W);

    protected ResourceEditorTab(
        string title, string guid, EditorTabFlag flags, EditorTabPlacement placement)
        : base(title, guid, flags, placement)
    {
        var project = GetProject();
        project.OnInitialized += OnProjectInitialized;
        project.OnRequest += OnProjectRequest;

        BindHotkey(HotkeySaveDocument, SaveCurrentResource);
        BindHotkey(HotkeyCloseDocument, CloseCurrentResource);
    }

    public override void Dispose()
    {
        var project = GetProject();
        project.OnRequest -= OnProjectRequest;
        project.OnInitialized -= OnProjectInitialized;

        foreach (var data in _resources.Values)
            data.Dispose();
        _resources.Clear();
        _closeResourceTitle.Dispose();
        _closeResourceHotkey.Dispose();
        _saveResourceTitle.Dispose();
        _saveResourceHotkey.Dispose();
        _saveAllResourcesTitle.Dispose();
        _closeAllResourcesTitle.Dispose();
        base.Dispose();
    }

    /// <summary>Commands.</summary>
    public void SaveCurrentResource()
    {
        SaveResource(_activeResourceName, true);
    }

    public void CloseCurrentResource()
    {
        CloseResourceGracefully(_activeResourceName);
    }

    /// <summary>EditorTab implementation.</summary>
    public override void RenderContextMenuItems()
    {
        var project = GetProject();
        var hotkeyManager = project.HotkeyManager;

        var closeResourcePending = string.Empty;
        var closeAllResourcesPending = false;
        var saveResourcePending = string.Empty;
        var saveAllResourcesPending = false;

        _contextMenuSeparator.Reset();
        if (_resources.Count == 0)
        {
            ImGui.MenuItem("(No Resources)"u8, ReadOnlySpan<byte>.Empty, false, false);
            _contextMenuSeparator.Add();
        }
        else
        {
            ImGui.PushID("ActiveResources"u8);
            foreach (var pair in _resources)
            {
                var resourceName = pair.Key;
                var data = pair.Value;
                ImGui.PushID(data.ResourceName.ContentBytes);

                var selected = string.Equals(resourceName, _activeResourceName, StringComparison.Ordinal);
                if (ImGui.SmallButton("\uf00d"u8))
                    closeResourcePending = resourceName;
                ImGui.SameLine();

                data.MenuTitle.FormatInPlace($"{(data.IsUnsaved ? "* " : string.Empty)}{resourceName}");

                if (ImGui.MenuItem(data.MenuTitle.ContentBytes, ReadOnlySpan<byte>.Empty, selected))
                    SetActiveResource(resourceName);

                ImGui.PopID();
            }
            ImGui.PopID();
            _contextMenuSeparator.Add();
        }

        _contextMenuSeparator.Reset();
        {
            _saveResourceTitle.FormatInPlace($"Save Current [{GetResourceTitle()}]");
            _saveResourceHotkey.Set(hotkeyManager.GetHotkeyLabel(HotkeySaveDocument));
            if (ImGui.MenuItem(
                    _saveResourceTitle.ContentBytes, _saveResourceHotkey.ContentBytes, false, _resources.Count != 0))
            {
                saveResourcePending = _activeResourceName;
            }
        }
        {
            _saveAllResourcesTitle.FormatInPlace($"Save All [{GetResourceTitle()}]s");
            if (ImGui.MenuItem(
                    _saveAllResourcesTitle.ContentBytes, ReadOnlySpan<byte>.Empty, false, _resources.Count != 0))
            {
                saveAllResourcesPending = true;
            }
        }

        _contextMenuSeparator.Add();

        _contextMenuSeparator.Reset();
        {
            _closeResourceTitle.FormatInPlace($"Close Current [{GetResourceTitle()}]");
            _closeResourceHotkey.Set(hotkeyManager.GetHotkeyLabel(HotkeyCloseDocument));
            if (ImGui.MenuItem(
                    _closeResourceTitle.ContentBytes, _closeResourceHotkey.ContentBytes, false, _resources.Count != 0))
            {
                closeResourcePending = _activeResourceName;
            }
        }
        {
            _closeAllResourcesTitle.FormatInPlace($"Close All [{GetResourceTitle()}]s");
            if (ImGui.MenuItem(
                    _closeAllResourcesTitle.ContentBytes, ReadOnlySpan<byte>.Empty, false, _resources.Count != 0))
            {
                closeAllResourcesPending = true;
            }
        }

        _contextMenuSeparator.Add();

        // Apply delayed actions
        if (closeAllResourcesPending)
            CloseAllResourcesGracefully();
        else if (closeResourcePending.Length != 0)
            CloseResourceGracefully(closeResourcePending);
        else if (saveAllResourcesPending)
            SaveAllResources();
        else if (saveResourcePending.Length != 0)
            SaveResource(saveResourcePending);
    }

    public override void EnumerateUnsavedItems(List<string> items)
    {
        foreach (var pair in _resources)
        {
            if (pair.Value.IsUnsaved)
                items.Add(pair.Key);
        }
    }

    public override EditorActionFrame? PushAction(EditorAction action)
    {
        if (_resources.TryGetValue(_activeResourceName, out var data))
        {
            var oldActionFrame = data.CurrentActionFrame;
            var wrappedAction = new ResourceActionWrapper(action, this, _activeResourceName, oldActionFrame);
            var newActionFrame = base.PushAction(wrappedAction);
            if (newActionFrame.HasValue)
            {
                if (!wrappedAction.IsTransparent())
                    data.CurrentActionFrame = newActionFrame.Value;
                return newActionFrame;
            }
        }
        return null;
    }

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        base.WriteIniSettings(output);

        var resourceNames = GetResourceNames();
        IniHelpers.WriteStringToIni(output, "ResourceNames", string.Join('|', resourceNames));
        IniHelpers.WriteStringToIni(output, "ActiveResourceName", _activeResourceName);
    }

    public override void ReadIniSettings(string line)
    {
        base.ReadIniSettings(line);

        var value = IniHelpers.ReadStringFromIni(line, "ResourceNames");
        if (value is not null)
        {
            var resourceNames = value.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var resourceName in resourceNames)
                OpenResource(resourceName);
        }

        value = IniHelpers.ReadStringFromIni(line, "ActiveResourceName");
        if (value is not null)
            SetActiveResource(value);
    }

    /// <summary>Return user-readable name of resource.</summary>
    public virtual string GetResourceTitle() => "Resource";

    /// <summary>Return whether the specified request can be handled by this tab.</summary>
    public abstract bool CanOpenResource(ResourceFileDescriptor descriptor);

    /// <summary>Return whether the several resources can be handled simultaneously.</summary>
    public abstract bool SupportMultipleResources();

    /// <summary>Open resource.</summary>
    public void OpenResource(string resourceName, bool activate = true)
    {
        if (!_resources.ContainsKey(resourceName))
        {
            if (_resources.Count != 0 && !SupportMultipleResources())
            {
                if (CloseAllResourcesGracefully(resourceName))
                    return;
            }

            _resources.Add(resourceName, new ResourceData(resourceName));
            if (_loadResources)
                OnResourceLoaded(resourceName);
        }

        if (activate || _activeResourceName.Length == 0)
            SetActiveResource(resourceName);
    }

    /// <summary>Close resource.</summary>
    public void CloseResource(string resourceName)
    {
        if (_resources.TryGetValue(resourceName, out var data))
        {
            _ = GetProject().UndoManager;
            _ = string.Equals(resourceName, _activeResourceName, StringComparison.Ordinal);

            _resources.Remove(resourceName);
            data.Dispose();
            if (_loadResources)
                OnResourceUnloaded(resourceName);

            if (!_resources.ContainsKey(_activeResourceName))
            {
                string? nextResource = null;
                foreach (var key in _resources.Keys)
                {
                    if (string.CompareOrdinal(key, _activeResourceName) >= 0)
                    {
                        nextResource = key;
                        break;
                    }
                }

                if (nextResource is not null)
                    SetActiveResource(nextResource);
                else if (_resources.Count != 0)
                {
                    foreach (var key in _resources.Keys)
                    {
                        SetActiveResource(key);
                        break;
                    }
                }
                else
                    SetActiveResource(string.Empty);
            }
        }
    }

    /// <summary>Close all opened resources.</summary>
    public void CloseAllResources()
    {
        if (_loadResources)
        {
            foreach (var pair in _resources)
            {
                _ = string.Equals(pair.Key, _activeResourceName, StringComparison.Ordinal);
                OnResourceUnloaded(pair.Key);
            }
        }
        foreach (var data in _resources.Values)
            data.Dispose();
        _resources.Clear();
        _activeResourceName = string.Empty;
    }

    /// <summary>Close resource gracefully.</summary>
    public void CloseResourceGracefully(string resourceName, Action? onClosed = null)
    {
        CloseResourceGracefully(resourceName, onClosed, null);
    }

    private void CloseResourceGracefully(string resourceName, Action? onClosed, Action? onCanceled)
    {
        onClosed ??= static () => { };
        onCanceled ??= static () => { };
        if (!IsResourceUnsaved(resourceName))
        {
            CloseResource(resourceName);
            onClosed();
            return;
        }

        var weakSelf = new WeakReference<ResourceEditorTab>(this);

        var request = new CloseResourceRequest
        {
            ResourceNames = [resourceName],
            OnSave = () =>
            {
                if (weakSelf.TryGetTarget(out var self))
                {
                    self.SaveResource(resourceName);
                    self.CloseResource(resourceName);
                    onClosed();
                }
            },
            OnDiscard = () =>
            {
                if (weakSelf.TryGetTarget(out var self))
                {
                    self.CloseResource(resourceName);
                    onClosed();
                }
            },
            OnCancel = onCanceled,
        };

        GetProject().CloseResourceGracefully(request);
    }

    /// <summary>Close all opened resources gracefully.</summary>
    public bool CloseAllResourcesGracefully(Action? onAllClosed = null)
    {
        onAllClosed ??= static () => { };
        if (!IsAnyResourceUnsaved())
        {
            CloseAllResources();
            return false;
        }

        var resourceNames = GetResourceNames();
        var releaseWhenClosed = new DelayedCallback(resourceNames.Count, onAllClosed);
        foreach (var resourceName in resourceNames)
            CloseResourceGracefully(resourceName, releaseWhenClosed.Release, releaseWhenClosed.Release);
        return true;
    }

    /// <summary>Close all opened resources gracefully. Open other resource if requested.</summary>
    public bool CloseAllResourcesGracefully(string pendingOpenResourceName)
    {
        return CloseAllResourcesGracefully(() =>
        {
            if (pendingOpenResourceName.Length != 0)
                OpenResource(pendingOpenResourceName);
        });
    }

    /// <summary>Save specific opened resource.</summary>
    public void SaveResource(string resourceName, bool forced = false)
    {
        if (!forced && !IsResourceUnsaved(resourceName))
            return;

        if (_resources.TryGetValue(resourceName, out var data))
            DoSaveResource(resourceName, data);
    }

    /// <summary>Save all resources.</summary>
    public void SaveAllResources(bool forced = false)
    {
        foreach (var pair in _resources)
        {
            if (forced || pair.Value.IsUnsaved)
                DoSaveResource(pair.Key, pair.Value);
        }
    }

    /// <summary>Save all shallow data.</summary>
    public void SaveShallow()
    {
        foreach (var resourceName in _resources.Keys)
            OnResourceShallowSaved(resourceName);
    }

    /// <summary>Set currently active resource.</summary>
    public void SetActiveResource(string activeResourceName)
    {
        if (!string.Equals(_activeResourceName, activeResourceName, StringComparison.Ordinal))
        {
            if (_resources.ContainsKey(activeResourceName))
            {
                var oldActiveResourceName = _activeResourceName;
                _activeResourceName = activeResourceName;
                OnActiveResourceChanged(oldActiveResourceName, _activeResourceName);
            }
            else
                _activeResourceName = string.Empty;
        }
    }

    /// <summary>Set current action for resource.</summary>
    public void SetCurrentAction(string resourceName, EditorActionFrame? frame)
    {
        if (_resources.TryGetValue(resourceName, out var data))
            data.CurrentActionFrame = frame;
    }

    public bool IsResourceOpen(string resourceName) => _resources.ContainsKey(resourceName);

    public bool IsResourceUnsaved(string resourceName)
    {
        return _resources.TryGetValue(resourceName, out var data) && data.IsUnsaved;
    }

    public bool IsAnyResourceUnsaved()
    {
        foreach (var data in _resources.Values)
        {
            if (data.IsUnsaved)
                return true;
        }
        return false;
    }

    public string ActiveResourceName => _activeResourceName;

    /// <summary>EditorTab implementation.</summary>
    protected override bool IsMarkedUnsaved()
    {
        return IsResourceUnsaved(_activeResourceName);
    }

    /// <summary>Called when resource should be loaded.</summary>
    protected abstract void OnResourceLoaded(string resourceName);

    /// <summary>Called when resource should be unloaded.</summary>
    protected abstract void OnResourceUnloaded(string resourceName);

    /// <summary>Called when active resource changed.</summary>
    protected abstract void OnActiveResourceChanged(string oldResourceName, string newResourceName);

    /// <summary>Called when resource should be saved.</summary>
    protected abstract void OnResourceSaved(string resourceName);

    /// <summary>Called when shallow data for resource is saved.</summary>
    protected abstract void OnResourceShallowSaved(string resourceName);

    private List<string> GetResourceNames()
    {
        return [.. _resources.Keys];
    }

    private void OnProjectInitialized(Project project)
    {
        _loadResources = true;
        foreach (var resourceName in _resources.Keys)
            OnResourceLoaded(resourceName);
    }

    private void OnProjectRequest(EditorTab? sender, ProjectRequest request)
    {
        if (request is OpenResourceRequest openResourceRequest)
        {
            var descriptor = openResourceRequest.Resource;
            if (!descriptor.IsDirectory && CanOpenResource(descriptor))
            {
                request.QueueProcessCallback(() =>
                {
                    OpenResource(openResourceRequest.Resource.ResourceName);
                    Focus();
                });
            }
        }
    }

    private void DoSaveResource(string resourceName, ResourceData data)
    {
        OnResourceSaved(resourceName);
        data.SavedActionFrame = data.CurrentActionFrame;
    }

    private sealed class ResourceData : IDisposable
    {
        public ResourceData(string resourceName)
        {
            ResourceName = new CString(resourceName);
        }

        public void Dispose()
        {
            MenuTitle.Dispose();
            ResourceName.Dispose();
        }

        public bool IsUnsaved => CurrentActionFrame != SavedActionFrame;

        public EditorActionFrame? CurrentActionFrame;
        public EditorActionFrame? SavedActionFrame;
        public readonly CString ResourceName;
        public readonly CString MenuTitle = new();
    }

    private sealed class DelayedCallback
    {
        public DelayedCallback(int count, Action callback)
        {
            _count = count;
            _callback = callback;
        }

        public void Release()
        {
            --_count;
            if (_count == 0)
                _callback();
        }

        private int _count;
        private readonly Action _callback;
    }

    private bool _loadResources;
    private readonly SortedDictionary<string, ResourceData> _resources = new(StringComparer.Ordinal);
    private string _activeResourceName = string.Empty;

    private readonly CString _closeResourceTitle = new();
    private readonly CString _closeResourceHotkey = new();
    private readonly CString _saveResourceTitle = new();
    private readonly CString _saveResourceHotkey = new();
    private readonly CString _saveAllResourcesTitle = new();
    private readonly CString _closeAllResourcesTitle = new();
}

/// <summary>Action wrapper that focuses resource on undo/redo.</summary>
public class ResourceActionWrapper : BaseEditorActionWrapper
{
    public ResourceActionWrapper(
        EditorAction action, ResourceEditorTab tab, string resourceName, EditorActionFrame? oldFrame)
        : base(action)
    {
        _tab = new WeakReference<ResourceEditorTab>(tab);
        _resourceName = resourceName;
        _oldFrame = oldFrame;
        Debug.Assert(action is not null);
    }

    /// <summary>Implement EditorAction.</summary>
    public override void OnPushed(EditorActionFrame frame)
    {
        _newFrame = frame;
        base.OnPushed(frame);
    }

    public override bool CanRedo()
    {
        return Tab is { } tab && tab.IsResourceOpen(_resourceName) && base.CanRedo();
    }

    public override void Redo()
    {
        base.Redo();
        FocusMe();
        if (!IsTransparent())
            UpdateCurrentAction(_newFrame);
    }

    public override bool CanUndo()
    {
        return Tab is { } tab && tab.IsResourceOpen(_resourceName) && base.CanUndo();
    }

    public override void Undo()
    {
        base.Undo();
        FocusMe();
        if (!IsTransparent())
            UpdateCurrentAction(_oldFrame);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ResourceActionWrapper otherWrapper)
            return false;

        if (!ReferenceEquals(Tab, otherWrapper.Tab)
            || !string.Equals(_resourceName, otherWrapper._resourceName, StringComparison.Ordinal))
        {
            return false;
        }

        if (_action.MergeWith(otherWrapper._action))
        {
            _newFrame = otherWrapper._newFrame;
            return true;
        }
        return false;
    }

    private void FocusMe()
    {
        if (Tab is { } tab)
        {
            tab.Focus();
            tab.SetActiveResource(_resourceName);
        }
    }

    private void UpdateCurrentAction(EditorActionFrame? frame)
    {
        if (Tab is { } tab)
        {
            tab.Focus();
            tab.SetCurrentAction(_resourceName, frame);
        }
    }

    private ResourceEditorTab? Tab => _tab.TryGetTarget(out var tab) ? tab : null;

    private readonly WeakReference<ResourceEditorTab> _tab;
    private readonly string _resourceName;

    private readonly EditorActionFrame? _oldFrame;
    private EditorActionFrame _newFrame;
}
