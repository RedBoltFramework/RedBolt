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
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;

/// <summary>Tab that hosts inspectors of any kind.</summary>
public class InspectorTab : EditorTab
{
    public InspectorTab()
        : base("Inspector", "bd959865-8929-4f92-a20f-97ff867d6ba6",
            EditorTabFlag.OpenByDefault, EditorTabPlacement.DockRight)
    {
    }

    public override void Dispose()
    {
        _sourceInterface = null;
        _source = null;
        for (var i = _addons.Count - 1; i >= 0; --i)
            _addons[i].Dispose();
        _addons.Clear();
        base.Dispose();
    }

    /// <summary>Register new inspector addon.</summary>
    public void RegisterAddon(RedObject addon)
    {
        _addons.Add(addon);
    }

    public T RegisterAddon<T>(T addon)
        where T : RedObject, InspectorSource
    {
        RegisterAddon((RedObject)addon);
        SubscribeOnActivation(addon);
        return addon;
    }

    /// <summary>Connect to source when activated.</summary>
    public void SubscribeOnActivation<T>(T source)
        where T : RedObject, InspectorSource
    {
        var sourceWeak = new WeakReference<T>(source);
        var inspectorTabWeak = new WeakReference<InspectorTab>(this);
        source.OnActivated += () =>
        {
            if (sourceWeak.TryGetTarget(out var sourceTarget)
                && inspectorTabWeak.TryGetTarget(out var inspectorTab))
                inspectorTab.ConnectToSource(sourceTarget);
        };
    }

    /// <summary>Connect to data source.</summary>
    public void ConnectToSource(RedObject? source, InspectorSource? sourceInterface)
    {
        _source = source is not null ? new WeakReference<RedObject>(source) : null;
        _sourceInterface = sourceInterface is not null
            ? new WeakReference<InspectorSource>(sourceInterface)
            : null;
    }

    public void ConnectToSource<T>(T? source)
        where T : RedObject, InspectorSource
    {
        ConnectToSource(source, source);
    }

    /// <summary>Implement EditorTab.</summary>
    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        base.ApplyHotkeys(hotkeyManager);
        if (_source is not null && _source.TryGetTarget(out var source))
        {
            _sourceInterface!.TryGetTarget(out var sourceInterface);
            sourceInterface!.ApplyHotkeys(hotkeyManager);
            GC.KeepAlive(source);
        }
    }

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

    public override bool IsUndoSupported()
    {
        if (_source is not null && _source.TryGetTarget(out var source))
        {
            _sourceInterface!.TryGetTarget(out var sourceInterface);
            var isUndoSupported = sourceInterface!.IsUndoSupported();
            GC.KeepAlive(source);
            return isUndoSupported;
        }
        return false;
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

    private readonly List<RedObject> _addons = [];
    private WeakReference<RedObject>? _source;

    private WeakReference<InspectorSource>? _sourceInterface;
}
