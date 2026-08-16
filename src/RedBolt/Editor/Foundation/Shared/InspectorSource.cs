// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation.Shared;

using System;

using RedBolt.Editor.Core;
using RedBolt.Editor.Project;

/// <summary>Interface used to provide content for inspector tab.</summary>
public interface InspectorSource
{
    event Action? OnActivated;

    /// <summary>Return owner tab.</summary>
    EditorTab? GetOwnerTab() => null;

    /// <summary>Return whether the inspector is connected to undo stack.</summary>
    bool IsUndoSupported() => false;

    /// <summary>Update and render inspector contents.</summary>
    void RenderContent()
    {
    }

    /// <summary>Update and render tab context menu.</summary>
    void RenderContextMenuItems()
    {
    }

    /// <summary>Render main menu when the inspector tab is focused.</summary>
    void RenderMenu()
    {
    }

    /// <summary>Apply hotkeys when the inspector tab is focused.</summary>
    void ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
    }
}
