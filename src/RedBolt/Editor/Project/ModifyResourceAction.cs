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
using System.Diagnostics;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.IO;
using RedBolt.Resource;

public class ModifyResourceAction : EditorAction
{
    public ModifyResourceAction(Project project)
    {
        _project = new WeakReference<Project>(project);
    }

    public void AddResource(Resource resource)
    {
        Debug.Assert(_newData.Count == 0);
        _context = resource.Context;

        using var buffer = new VectorBuffer();
        if (resource.Save(buffer))
        {
            var oldData = new ResourceData
            {
                ResourceType = resource.GetTypeNameHash(),
                FileName = resource.AbsoluteFileName,
                Bytes = [.. buffer.Buffer],
            };
            _oldData[resource.Name] = oldData;
        }
    }

    public void DisableAutoComplete()
    {
        _autoComplete = false;
    }

    public void SaveOnComplete()
    {
        _saveOnComplete = true;
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool IsComplete() => _newData.Count != 0;

    public override void Complete(bool force)
    {
        if (!_autoComplete && !force)
            return;

        var cache = _context?.GetSubsystem<ResourceCache>();
        foreach (var pair in _oldData)
        {
            var resourceName = pair.Key;
            var oldData = pair.Value;
            if (cache?.GetResource(oldData.ResourceType, resourceName) is Resource resource)
            {
                using var buffer = new VectorBuffer();
                if (resource.Save(buffer))
                {
                    var newData = new ResourceData
                    {
                        ResourceType = oldData.ResourceType,
                        FileName = oldData.FileName,
                        Bytes = [.. buffer.Buffer],
                    };
                    _newData[resourceName] = newData;
                }
            }
        }

        if (_oldData.Count != _newData.Count)
            throw new UndoException("ModifyResourceAction failed to complete action creation");

        if (_saveOnComplete && Project is { } project)
        {
            foreach (var pair in _newData)
                project.SaveFileDelayed(pair.Value.FileName, pair.Key, pair.Value.Bytes);
        }
    }

    public override void Redo()
    {
        foreach (var pair in _newData)
            ApplyResourceData(pair.Key, pair.Value);
    }

    public override void Undo()
    {
        foreach (var pair in _oldData)
            ApplyResourceData(pair.Key, pair.Value);
    }

    public override bool MergeWith(EditorAction other)
    {
        return false;
    }

    private void ApplyResourceData(string resourceName, ResourceData data)
    {
        var cache = _context!.GetSubsystem<ResourceCache>()!;
        if (cache.GetResource(data.ResourceType, resourceName, false) is Resource resource)
        {
            using var buffer = new MemoryBuffer(data.Bytes);
            buffer.SetName(resourceName);
            resource.Load(buffer);
        }

        Project?.SaveFileDelayed(data.FileName, resourceName, data.Bytes);
    }

    private Project? Project =>
        _project.TryGetTarget(out var project) ? project : null;

    private sealed class ResourceData
    {
        public StringHash ResourceType;
        public string FileName = string.Empty;
        public byte[] Bytes = [];
    }

    private readonly WeakReference<Project> _project;
    private Context? _context;

    private bool _autoComplete = true;
    private bool _saveOnComplete;

    private readonly Dictionary<string, ResourceData> _oldData = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResourceData> _newData = new(StringComparer.Ordinal);

#pragma warning disable CS0169 // Preserved native callback_ member is currently unused in rbfx as well.
    private Action? _callback;
#pragma warning restore CS0169
}
