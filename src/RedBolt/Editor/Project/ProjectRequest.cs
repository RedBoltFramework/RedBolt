// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Scene;
using RedBolt.SystemUI;

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

/// <summary>Request to open resource.</summary>
public class OpenResourceRequest : ProjectRequest
{
    public OpenResourceRequest(string resourceName)
    {
        var project = GetSubsystem<Project>();
        _resourceDesc = project.GetResourceDescriptor(resourceName);
    }

    public ResourceFileDescriptor Resource => _resourceDesc;

    private readonly ResourceFileDescriptor _resourceDesc;
}

/// <summary>Base class for all inspector requests.</summary>
public class BaseInspectRequest : ProjectRequest
{
    public BaseInspectRequest()
    {
    }
}

/// <summary>Request to inspect one or more resources.</summary>
public class InspectResourceRequest : BaseInspectRequest
{
    public InspectResourceRequest(IEnumerable<string> resourceNames)
    {
        var project = GetSubsystem<Project>();
        foreach (var resourceName in resourceNames)
            _resourceDescs.Add(project.GetResourceDescriptor(resourceName));
    }

    public IReadOnlyList<ResourceFileDescriptor> Resources => _resourceDescs;

    public List<string> GetSortedResourceNames()
    {
        var resourceNames = new List<string>(_resourceDescs.Count);
        foreach (var descriptor in _resourceDescs)
            resourceNames.Add(descriptor.ResourceName);
        resourceNames.Sort(StringComparer.Ordinal);
        return resourceNames;
    }

    private readonly List<ResourceFileDescriptor> _resourceDescs = [];
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
