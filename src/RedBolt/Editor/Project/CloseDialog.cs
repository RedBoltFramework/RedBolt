// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Utility;

/// <summary>Request to gracefully close resource(s) with prompt.</summary>
public class CloseResourceRequest : ICloneable
{
    public List<string> ResourceNames = [];
    public Action OnSave = static () => { };
    public Action OnDiscard = static () => { };
    public Action OnCancel = static () => { };

    public CloseResourceRequest Clone()
    {
        var clone = new CloseResourceRequest
        {
            OnSave = OnSave,
            OnDiscard = OnDiscard,
            OnCancel = OnCancel,
        };
        clone.ResourceNames.AddRange(ResourceNames);
        return clone;
    }

    object ICloneable.Clone() => Clone();
}

/// <summary>Wrapper for close dialog widget.</summary>
public class CloseDialog : RedObject
{
    public CloseDialog()
    {
    }

    public override void Dispose()
    {
        for (var index = _items.Count - 1; index >= 0; --index)
            _items[index].Dispose();
        _items.Clear();
        _popupName.Dispose();
        _requests.Clear();
        base.Dispose();
    }

    /// <summary>Set whether "Save &amp; Close" option is enabled.</summary>
    public void SetSaveEnabled(bool enabled) => _saveEnabled = enabled;

    /// <summary>Process close request.</summary>
    public void RequestClose(CloseResourceRequest request)
    {
        _requests.Add(request.Clone());
    }

    /// <summary>Return whether the dialog is currently open or will be open on this frame.</summary>
    public bool IsActive => _isOpen || _requests.Count != 0;

    private static readonly CString _saveAndCloseLabel = new($"{Icons.FloppyDisk} Save & Close");
    private static readonly CString _discardAndCloseLabel = new($"{Icons.TriangleExclamation} Discard & Close");
    private static readonly CString _cancelLabel = new($"{Icons.Ban} Cancel");

    /// <summary>Update and render contents if necessary.</summary>
    public void Render()
    {
        if (!_isOpen && _requests.Count != 0)
        {
            _isOpen = true;

            for (var index = _items.Count - 1; index >= 0; --index)
                _items[index].Dispose();
            _items.Clear();
            foreach (var request in _requests)
            {
                foreach (var resourceName in request.ResourceNames)
                    _items.Add(new CString(resourceName));
            }
            _items.Sort(static (lhs, rhs) => lhs.ContentBytes.SequenceCompareTo(rhs.ContentBytes));

            ImGui.OpenPopup(_popupName.ContentBytes);
        }

        if (ImGui.BeginPopupModal(
                _popupName.ContentBytes, flags: ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize))
        {
            ImGui.Text("The following items are unsaved:"u8);

            if (ImGui.BeginChild("Items"u8, new Vector2f(0.0f, 100.0f)))
            {
                foreach (var item in _items)
                    ImGui.MenuItem(item.ContentBytes);
            }
            ImGui.EndChild();

            ImGui.BeginDisabled(!_saveEnabled);
            if (ImGui.Button(_saveAndCloseLabel))
            {
                System.Diagnostics.Debug.Assert(_saveEnabled);

                CloseDialogSave();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndDisabled();

            ImGui.SameLine();

            if (ImGui.Button(_discardAndCloseLabel))
            {
                CloseDialogDiscard();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            var escapeKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
                (int)Key.Escape, (int)Input.GetScancodeFromKey(Key.Escape));
            if (ImGui.Button(_cancelLabel) || ImGui.IsKeyPressed(escapeKey))
            {
                CloseDialogCancel();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        else if (_isOpen)
        {
            CloseDialogDiscard();
        }
    }

    private void CloseDialogSave()
    {
        foreach (var request in _requests)
            request.OnSave();
        _requests.Clear();

        _isOpen = false;
    }

    private void CloseDialogDiscard()
    {
        foreach (var request in _requests)
            request.OnDiscard();
        _requests.Clear();

        _isOpen = false;
    }

    private void CloseDialogCancel()
    {
        foreach (var request in _requests)
            request.OnCancel();
        _requests.Clear();

        _isOpen = false;
    }

    private bool _saveEnabled = true;
    private readonly List<CloseResourceRequest> _requests = [];

    private bool _isOpen;
    private readonly CString _popupName = new("Close?");

    private readonly List<CString> _items = [];
}
