// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation.Shared;

using RedBolt.Editor.Core;
using RedBolt.Editor.Project;

/// <summary>Interface used to provide content for hierarchy browser tab.</summary>
public interface HierarchyBrowserSource
{
    /// <summary>Return owner tab.</summary>
    EditorTab? GetOwnerTab();

    /// <summary>Update and render tab contents.</summary>
    void RenderContent()
    {
    }

    /// <summary>Update and render tab context menu.</summary>
    void RenderContextMenuItems()
    {
    }

    /// <summary>Render main menu when the hierarchy browser tab is focused.</summary>
    void RenderMenu()
    {
    }

    /// <summary>Apply hotkeys when the hierarchy browser tab is focused.</summary>
    void ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
    }
}
