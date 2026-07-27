// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System;
using System.Collections.Generic;

using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.IK;

public sealed class IKNodeCache
{
    internal readonly struct Entry
    {
        public Entry(Node node, IKNode solverNode)
        {
            Node = new WeakReference<Node>(node);
            SolverNode = solverNode;
        }

        public WeakReference<Node> Node { get; }
        public IKNode SolverNode { get; }
    }

    private readonly List<Entry> _entries = [];

    internal List<Entry> Entries => _entries;

    public int Count => _entries.Count;

    public void Clear()
    {
        _entries.Clear();
    }

    internal IKNode GetOrAdd(Node node)
    {
        for (var i = 0; i < _entries.Count; ++i)
        {
            var entry = _entries[i];
            if (entry.Node.TryGetTarget(out var cachedNode) && ReferenceEquals(cachedNode, node))
                return entry.SolverNode;
        }

        var solverNode = new IKNode();
        _entries.Add(new Entry(node, solverNode));
        return solverNode;
    }

    internal IKNode? Get(Node node)
    {
        for (var i = 0; i < _entries.Count; ++i)
        {
            var entry = _entries[i];
            if (entry.Node.TryGetTarget(out var cachedNode) && ReferenceEquals(cachedNode, node))
                return entry.SolverNode;
        }
        return null;
    }
}
