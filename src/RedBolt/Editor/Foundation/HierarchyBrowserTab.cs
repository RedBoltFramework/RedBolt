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

namespace RedBolt.Editor.Foundation;

using System;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;

/// <summary>Tab that hosts hierarchy display of any kind.</summary>
public class HierarchyBrowserTab : EditorTab
{
    public HierarchyBrowserTab()
        : base("Hierarchy", "38ee90af-0a65-4d7d-93e2-d446ae54dffd",
            EditorTabFlag.OpenByDefault, EditorTabPlacement.DockLeft)
    {
    }

    /// <summary>Connect to data source.</summary>
    public void ConnectToSource(RedObject? source, HierarchyBrowserSource? sourceInterface)
    {
        _source = source is not null ? new WeakReference<RedObject>(source) : null;
        _sourceInterface = sourceInterface is not null
            ? new WeakReference<HierarchyBrowserSource>(sourceInterface)
            : null;
    }

    public void ConnectToSource<T>(T? source)
        where T : RedObject, HierarchyBrowserSource
    {
        ConnectToSource(source, source);
    }

    /// <summary>Implement EditorTab.</summary>
    public override void RenderMenu()
    {
        if (_source is not null && _source.TryGetTarget(out var source))
        {
            _sourceInterface!.TryGetTarget(out var sourceInterface);
            sourceInterface!.RenderMenu();
            GC.KeepAlive(source);
        }
    }

    public override void RenderContent()
    {
        if (_source is not null && _source.TryGetTarget(out var source))
        {
            _sourceInterface!.TryGetTarget(out var sourceInterface);
            sourceInterface!.RenderContent();
            GC.KeepAlive(source);
        }
    }

    public override void RenderContextMenuItems()
    {
        if (_source is not null && _source.TryGetTarget(out var source))
        {
            _sourceInterface!.TryGetTarget(out var sourceInterface);
            sourceInterface!.RenderContextMenuItems();
            GC.KeepAlive(source);
        }
    }

    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        if (_source is not null && _source.TryGetTarget(out var source))
        {
            _sourceInterface!.TryGetTarget(out var sourceInterface);
            sourceInterface!.ApplyHotkeys(hotkeyManager);
            GC.KeepAlive(source);
        }
    }

    public override EditorTab GetOwnerTab()
    {
        if (_source is not null && _source.TryGetTarget(out var source))
        {
            _sourceInterface!.TryGetTarget(out var sourceInterface);
            var ownerTab = sourceInterface!.GetOwnerTab();
            GC.KeepAlive(source);
            return ownerTab!;
        }
        return null!;
    }

    private WeakReference<RedObject>? _source;
    private WeakReference<HierarchyBrowserSource>? _sourceInterface;
}
