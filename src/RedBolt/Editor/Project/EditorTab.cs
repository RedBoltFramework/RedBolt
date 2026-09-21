// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;
using EditorActionFrame = System.UInt64;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Input;
using RedBolt.Math;

[Flags]
public enum EditorTabFlag
{
    None = 0,
    NoContentPadding = 1 << 0,
    OpenByDefault = 1 << 1,
    FocusOnStart = 1 << 2,
}

public enum EditorTabPlacement
{
    Floating,
    DockCenter,
    DockLeft,
    DockRight,
    DockBottom,
}

/// <summary>Interface for entity configurable via INI file.</summary>
public abstract class EditorConfigurable : RedObject
{
    public EditorConfigurable()
    {
    }

    /// <summary>Write all UI settings to text INI file.</summary>
    public abstract void WriteIniSettings(ImGuiTextBuffer output);

    /// <summary>Read one line of text INI file. May be called several times.</summary>
    public abstract void ReadIniSettings(string line);

    /// <summary>Return entry that should be read.</summary>
    public abstract string IniEntry { get; }
}

/// <summary>Helper class to spawn separators only once.</summary>
public class SeparatorHelper
{
    public void Add()
    {
        if (!_added)
            ImGui.Separator();
        _added = true;
    }

    public void Reset()
    {
        _added = false;
    }

    private bool _added;
}

/// <summary>Base class for any Editor tab.</summary>
/// <remarks>It's recommended to create exactly one instance of the tab for the project lifetime.</remarks>
public class EditorTab : EditorConfigurable
{
    private static readonly EditorHotkey HotkeyUndo = new EditorHotkey("Global.Undo").Ctrl().Press(Key.Z);
    private static readonly EditorHotkey HotkeyRedo = new EditorHotkey("Global.Redo").Ctrl().Press(Key.Y);

    public event Action<EditorTab>? OnRenderContextMenu;
    public event Action<EditorTab>? OnFocused;

    public EditorTab(string title, string guid, EditorTabFlag flags, EditorTabPlacement placement)
    {
        _title = title;
        _guid = guid;
        _uniqueId = $"{_title}###{_guid}";
        _titleCString = new CString(_title);
        _uniqueIdCString = new CString(_uniqueId);
        _undoLabel = new CString();
        _redoLabel = new CString();
        _flags = flags;
        _placement = placement;

        BindHotkey(HotkeyUndo, Undo);
        BindHotkey(HotkeyRedo, Redo);
    }

    public override void Dispose()
    {
        _redoLabel.Dispose();
        _undoLabel.Dispose();
        _uniqueIdCString.Dispose();
        _titleCString.Dispose();
        base.Dispose();
    }

    /// <summary>Render contents of the tab. Returns false if closed.</summary>
    public void Render()
    {
        _wasOpen = _open;

        if (_focusPending || _openPending)
            _open = true;

        if (_open)
            RenderWindow();

        _focusPending = false;
        _openPending = false;
    }

    /// <summary>Open tab without focusing.</summary>
    public void Open() => _openPending = true;

    /// <summary>Open tab if it's closed and focus on it unless owned tab is already focused.</summary>
    public void Focus(bool force = false)
    {
        var project = GetProject();
        if (force || !ReferenceEquals(project.RootFocusedTab, this))
            _focusPending = true;
    }

    /// <summary>Close tab.</summary>
    public void Close() => _open = false;

    /// <summary>Pre-render update tab. May be called even for closed tabs.</summary>
    public virtual void PreRenderUpdate()
    {
    }

    /// <summary>Apply hotkeys for this tab.</summary>
    public virtual void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        hotkeyManager.InvokeFor(this);
    }

    /// <summary>Post-render update tab. May be called even for closed tabs.</summary>
    public virtual void PostRenderUpdate()
    {
    }

    /// <summary>Render main menu of the tab.</summary>
    public virtual void RenderMenu()
    {
    }

    /// <summary>Render contents of the tab.</summary>
    public virtual void RenderContent()
    {
    }

    /// <summary>Render context menu of the tab.</summary>
    public virtual void RenderContextMenuItems()
    {
    }

    /// <summary>Render toolbar of the tab.</summary>
    public virtual void RenderToolbar()
    {
    }

    /// <summary>Called when all tabs are created and multi-tab plugins can be safely applied.</summary>
    public virtual void ApplyPlugins()
    {
        var editorPluginManager = GetSubsystem<EditorPluginManager>();
        editorPluginManager.Apply(this);
    }

    /// <summary>Called when project is fully loaded.</summary>
    public virtual void OnProjectLoaded()
    {
    }

    /// <summary>Return whether the tab is connected to undo stack.</summary>
    public virtual bool IsUndoSupported() => false;

    /// <summary>Return owner tab or itself.</summary>
    public virtual EditorTab GetOwnerTab() => this;

    /// <summary>Enumerates all unsaved items corresponding to this tab.</summary>
    public virtual void EnumerateUnsavedItems(List<string> items)
    {
    }

    /// <summary>Push undo action from this tab.</summary>
    public virtual EditorActionFrame? PushAction(EditorAction action)
    {
        var undoManager = GetUndoManager();
        return undoManager.PushAction(action);
    }

    /// <summary>Implement EditorConfigurable.</summary>
    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        IniHelpers.WriteIntToIni(output, "IsOpen", _open ? 1 : 0);
    }

    /// <summary>Implement EditorConfigurable.</summary>
    public override void ReadIniSettings(string line)
    {
        var isOpen = IniHelpers.ReadIntFromIni(line, "IsOpen");
        if (isOpen.HasValue)
            _open = isOpen.Value != 0;
    }

    /// <summary>Implement EditorConfigurable.</summary>
    public override string IniEntry => UniqueId;

    public string Title => _title;

    public string UniqueId => _uniqueId;

    public EditorTabFlag Flags => _flags;

    public EditorTabPlacement Placement => _placement;

    public bool IsOpen => _open;

    public Project GetProject() => GetSubsystem<Project>();

    public HotkeyManager GetHotkeyManager() => GetProject().HotkeyManager;

    public UndoManager GetUndoManager() => GetProject().UndoManager;

    public string GetHotkeyLabel(EditorHotkey info) => GetHotkeyManager().GetHotkeyLabel(info);

    public void BindHotkey(EditorHotkey info, Action callback)
    {
        GetHotkeyManager().BindHotkey(this, info, callback);
    }

    public T PushAction<T>(Func<T> createAction)
        where T : EditorAction
    {
        var action = createAction();
        PushAction(action);
        return action;
    }

    /// <summary>Return size of the window content. Should be called only during content rendering.</summary>
    public Vector2i GetContentSize()
    {
        var window = ImGui.GetCurrentWindow();
        var rect = window.ContentRegionRect;
        var min = new Vector2i(MathDefs.RoundToInt(rect.Min.X), MathDefs.RoundToInt(rect.Min.Y));
        var max = new Vector2i(MathDefs.RoundToInt(rect.Max.X), MathDefs.RoundToInt(rect.Max.Y));
        return max - min;
    }

    /// <summary>Return position of the window content in absolute coordinates. Should be called only during content rendering.</summary>
    public Vector2i GetContentPosition()
    {
        var window = ImGui.GetCurrentWindow();
        var rect = window.ContentRegionRect;
        return new Vector2i(MathDefs.RoundToInt(rect.Min.X), MathDefs.RoundToInt(rect.Min.Y));
    }

    /// <summary>Return whether the document is modified and prompt to save should be shown.</summary>
    protected virtual bool IsMarkedUnsaved() => false;

    /// <summary>Render common Edit menu.</summary>
    protected void RenderEditMenuItems()
    {
        var undoManager = GetProject().UndoManager;
        _undoLabel.Set(GetHotkeyLabel(HotkeyUndo));
        if (ImGui.MenuItem("Undo"u8, _undoLabel.ContentBytes, false, undoManager.CanUndo()))
            Undo();
        _redoLabel.Set(GetHotkeyLabel(HotkeyRedo));
        if (ImGui.MenuItem("Redo"u8, _redoLabel.ContentBytes, false, undoManager.CanRedo()))
            Redo();
        ImGui.Separator();
    }

    /// <summary>Helper for building menu.</summary>
    protected readonly SeparatorHelper _contextMenuSeparator = new();

    private void RenderWindow()
    {
        var noContentPadding = (_flags & EditorTabFlag.NoContentPadding) != 0;

        if (noContentPadding)
            ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(0.0f, 0.0f));

        if (IsMarkedUnsaved())
            _windowFlags |= ImGuiWindowFlags.ImGuiWindowFlagsUnsavedDocument;
        else
            _windowFlags &= ~ImGuiWindowFlags.ImGuiWindowFlagsUnsavedDocument;

        if (_focusPending)
            ImGui.SetNextWindowFocus();

        ImGui.Begin(_uniqueIdCString.ContentBytes, ref _open, _windowFlags);

        if (noContentPadding)
            ImGui.PopStyleVar();

        if (ImGui.BeginPopupContextItem("EditorTab_ContextMenu"u8))
        {
            RenderContextMenu();
            ImGui.EndPopup();
        }

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.ImGuiFocusedFlagsChildWindows))
        {
            var project = GetProject();
            project.SetFocusedTab(this);
        }
        else
        {
            // TODO(editor): Why do we need this?
            var input = GetSubsystem<Input>();
            if (input.IsMouseVisible() && ImGui.IsAnyMouseDown())
            {
                if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ImGuiHoveredFlagsChildWindows))
                    ImGui.SetWindowFocus();
            }
        }

        RenderContent();

        if (noContentPadding)
            ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(0.0f, 0.0f));

        ImGui.End();

        if (noContentPadding)
            ImGui.PopStyleVar();
    }

    private void RenderContextMenu()
    {
        _contextMenuSeparator.Reset();
        RenderContextMenuItems();
        _contextMenuSeparator.Add();

        _contextMenuSeparator.Reset();
        var oldY = ImGui.GetCursorPosY();
        OnRenderContextMenu?.Invoke(this);
        if (oldY != ImGui.GetCursorPosY())
            _contextMenuSeparator.Add();

        if (ImGui.MenuItem("Close Tab"u8))
            Close();
    }

    private void Undo()
    {
        if (IsUndoSupported())
        {
            var project = GetProject();
            var undoManager = project.UndoManager;
            undoManager.Undo();
        }
    }

    private void Redo()
    {
        if (IsUndoSupported())
        {
            var project = GetProject();
            var undoManager = project.UndoManager;
            undoManager.Redo();
        }
    }

    internal void RaiseFocused() => OnFocused?.Invoke(this);

    internal ReadOnlySpan<byte> TitleBytes => _titleCString.ContentBytes;

    internal ReadOnlySpan<byte> UniqueIdBytes => _uniqueIdCString.ContentBytes;

    private readonly string _title;
    private readonly string _guid;
    private readonly string _uniqueId;
    private readonly CString _titleCString;
    private readonly CString _uniqueIdCString;
    private readonly CString _undoLabel;
    private readonly CString _redoLabel;
    private readonly EditorTabFlag _flags;
    private readonly EditorTabPlacement _placement;

    private bool _focusPending;
    private bool _openPending;
    private bool _wasOpen;
    private bool _open;

    private ImGuiWindowFlags _windowFlags;
}
