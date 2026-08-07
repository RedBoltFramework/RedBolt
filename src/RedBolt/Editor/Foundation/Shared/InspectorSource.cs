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
