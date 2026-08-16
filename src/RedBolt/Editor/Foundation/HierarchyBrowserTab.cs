// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

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
