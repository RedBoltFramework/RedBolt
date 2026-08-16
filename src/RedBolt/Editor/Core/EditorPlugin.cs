// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Core;

using System;
using System.Collections.Generic;
using System.Linq;

using RedBolt.Core;

/// <summary>Base class for any Editor plugin.</summary>
public abstract class EditorPlugin : RedObject
{
    public abstract string Name { get; }

    public abstract bool Apply(RedObject target);
}

public sealed class EditorPlugin<T> : EditorPlugin
    where T : RedObject
{
    public EditorPlugin(string name, Action<T> function)
    {
        _name = name;
        _function = function;
    }

    public override string Name => _name;

    public override bool Apply(RedObject target)
    {
        if (target is T derivedTarget)
        {
            _function(derivedTarget);
            return true;
        }
        return false;
    }

    private readonly string _name;
    private readonly Action<T> _function;
}

public sealed class ScopedEditorPlugin<T> : EditorPlugin
    where T : RedObject
{
    public ScopedEditorPlugin(string name, Func<T, IDisposable?> function)
    {
        _name = name;
        _function = function;
    }

    public override string Name => _name;

    public override bool Apply(RedObject target)
    {
        if (target is not T derivedTarget || _targets.Any(item => item.Target.TryGetTarget(out var existing)
            && ReferenceEquals(existing, target)))
        {
            return false;
        }
        _targets.Add((new WeakReference<RedObject>(target), _function(derivedTarget)));
        return true;
    }

    public override void Dispose()
    {
        for (var index = _targets.Count - 1; index >= 0; --index)
            _targets[index].Lease?.Dispose();
        _targets.Clear();
        base.Dispose();
    }

    private readonly string _name;
    private readonly Func<T, IDisposable?> _function;
    private readonly List<(WeakReference<RedObject> Target, IDisposable? Lease)> _targets = [];
}
