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

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;

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
            if (ImGui.Button("\uf0c7 Save & Close"u8))
            {
                System.Diagnostics.Debug.Assert(_saveEnabled);

                CloseDialogSave();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndDisabled();

            ImGui.SameLine();

            if (ImGui.Button("\uf071 Discard & Close"u8))
            {
                CloseDialogDiscard();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            var escapeKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
                (int)Key.Escape, (int)Input.GetScancodeFromKey(Key.Escape));
            if (ImGui.Button("\uf05e Cancel"u8) || ImGui.IsKeyPressed(escapeKey))
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
