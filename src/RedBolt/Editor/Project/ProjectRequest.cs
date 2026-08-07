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

using RedBolt.Core;
using RedBolt.Scene;

/// <summary>Base class for project-wide requests. Should be used from main thread only!</summary>
public class ProjectRequest : RedObject
{
    public ProjectRequest()
    {
    }

    public override void Dispose()
    {
        _callbacks.Clear();
        base.Dispose();
    }

    /// <summary>Queue callback with priority that can be used to process request.</summary>
    public void QueueProcessCallback(Action callback, int priority = 0)
    {
        var desc = new CallbackDesc
        {
            Callback = callback,
            Priority = priority,
        };
        _callbacks.Enqueue(desc, priority);
    }

    /// <summary>Invoke callback with highest priority.</summary>
    public void InvokeProcessCallback()
    {
        if (_callbacks.TryPeek(out var desc, out _))
            desc.Callback!();
    }

    private struct CallbackDesc
    {
        public Action? Callback;
        public int Priority;

        public static bool operator <(CallbackDesc lhs, CallbackDesc rhs) => lhs.Priority < rhs.Priority;

        public static bool operator >(CallbackDesc lhs, CallbackDesc rhs) => lhs.Priority > rhs.Priority;
    }

    private readonly PriorityQueue<CallbackDesc, int> _callbacks = new(
        Comparer<int>.Create(static (lhs, rhs) => rhs.CompareTo(lhs)));
}

/// <summary>Base class for all inspector requests.</summary>
public class BaseInspectRequest : ProjectRequest
{
    public BaseInspectRequest()
    {
    }
}

/// <summary>Request to inspect one or more nodes or components.</summary>
public class InspectNodeComponentRequest : BaseInspectRequest
{
    public InspectNodeComponentRequest(IEnumerable<Node?> nodes, IEnumerable<Component?> components)
    {
        foreach (var node in nodes)
        {
            if (node is not null)
                _nodes.Add(new WeakReference<Node>(node));
        }
        foreach (var component in components)
        {
            if (component is not null)
                _components.Add(new WeakReference<Component>(component));
        }

        _nodes.Sort(static (lhs, rhs) =>
        {
            lhs.TryGetTarget(out var lhsNode);
            rhs.TryGetTarget(out var rhsNode);
            return (lhsNode?.Id ?? 0).CompareTo(rhsNode?.Id ?? 0);
        });
        _components.Sort(static (lhs, rhs) =>
        {
            lhs.TryGetTarget(out var lhsComponent);
            rhs.TryGetTarget(out var rhsComponent);
            return (lhsComponent?.Id ?? 0).CompareTo(rhsComponent?.Id ?? 0);
        });
    }

    public override void Dispose()
    {
        _components.Clear();
        _nodes.Clear();
        base.Dispose();
    }

    public IReadOnlyList<WeakReference<Node>> Nodes => _nodes;

    public IReadOnlyList<WeakReference<Component>> Components => _components;

    /// <summary>Return scene if all nodes and components belong to the same scene, null otherwise.</summary>
    public Scene? CommonScene
    {
        get
        {
            Scene? scene = null;

            foreach (var nodeReference in _nodes)
            {
                if (!nodeReference.TryGetTarget(out var node))
                    continue;

                var nodeScene = node.Scene;
                if (nodeScene is null || (scene is not null && !ReferenceEquals(scene, nodeScene)))
                    return null;

                scene = nodeScene;
            }

            foreach (var componentReference in _components)
            {
                if (!componentReference.TryGetTarget(out var component))
                    continue;

                var componentScene = component.Scene;
                if (componentScene is null || (scene is not null && !ReferenceEquals(scene, componentScene)))
                    return null;

                scene = componentScene;
            }

            return scene;
        }
    }

    public bool IsEmpty => _nodes.Count == 0 && _components.Count == 0;

    public bool HasNodes => _nodes.Count != 0;

    public bool HasComponents => _components.Count != 0;

    private readonly List<WeakReference<Node>> _nodes = [];
    private readonly List<WeakReference<Component>> _components = [];
}

/// <summary>Request to create resource.</summary>
public class CreateResourceRequest : ProjectRequest
{
    public CreateResourceRequest(ResourceFactory factory)
    {
        _factory = factory;
    }

    public override void Dispose()
    {
        _factory = null;
        base.Dispose();
    }

    public ResourceFactory Factory => _factory!;

    private ResourceFactory? _factory;
}
