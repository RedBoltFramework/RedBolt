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

namespace RedBolt.Editor.Core;

using System;
using System.Collections.Generic;

using RedBolt.Core;

public class EditorPluginManager : RedObject
{
    public EditorPluginManager()
    {
    }

    public override void Dispose()
    {
        for (var i = _plugins.Count - 1; i >= 0; --i)
            _plugins[i].Dispose();
        _plugins.Clear();
        base.Dispose();
    }

    /// <summary>Add new editor plugin. Should be called before any plugin user is initialized.</summary>
    public void AddPlugin(EditorPlugin plugin)
    {
        _plugins.Add(plugin);
    }

    public void AddPlugin<T>(string name, Action<T> function)
        where T : RedObject
    {
        AddPlugin(new EditorPlugin<T>(name, function));
    }

    /// <summary>Apply all plugins to the target.</summary>
    public void Apply(RedObject target)
    {
        foreach (var plugin in _plugins)
            plugin.Apply(target);
    }

    /// <summary>Return all plugins.</summary>
    public IReadOnlyList<EditorPlugin> Plugins => _plugins;

    private readonly List<EditorPlugin> _plugins = [];
}
