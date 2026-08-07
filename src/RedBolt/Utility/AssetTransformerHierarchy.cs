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

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Extensions.Logging;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Logging;

/// <summary>Container for a hierarchy of AssetTransformer instances.</summary>
/// <remarks>Inner (more specific) transformers are preferred to outer (more generic) transformers.</remarks>
public class AssetTransformerHierarchy : RedObject
{
    public AssetTransformerHierarchy(bool isPostTransform)
    {
        _isPostTransform = isPostTransform;
        Clear();
    }

    /// <summary>Clear all cached transformers and dependencies.</summary>
    public void Clear()
    {
        _root = new TreeNode();
        _dependencyRoot = new DependencyGraphNode();
        _dependencyNodes = new Dictionary<string, DependencyGraphNode>(StringComparer.Ordinal)
        {
            [string.Empty] = _dependencyRoot,
        };
    }

    /// <summary>Add new transformer.</summary>
    public void AddTransformer(string path, AssetTransformer transformer)
    {
        var pathComponents = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var node = _root.GetOrCreateChild(pathComponents);
        node.Transformers.Add(transformer);
    }

    /// <summary>Remove all transformers of specified type.</summary>
    public bool RemoveTransformers(Type typeInfo)
    {
        var anyRemoved = false;
        _root.Iterate(node =>
        {
            var oldSize = node.Transformers.Count;
            node.Transformers.RemoveAll(transformer => transformer.GetType() == typeInfo);
            anyRemoved = anyRemoved || oldSize != node.Transformers.Count;
        });
        return anyRemoved;
    }

    /// <summary>Add dependency between transformer types.</summary>
    public bool AddDependency(string transformerClass, string dependsOn)
    {
        var rootNode = GetOrCreateDependencyNode(transformerClass);
        var dependencyNode = GetOrCreateDependencyNode(dependsOn);
        if (DependsOn(dependencyNode, rootNode))
        {
            Log.GetLogger().LogError(
                "Cannot add dependency '{DependsOn} before {TransformerClass}' because it would create circular dependency",
                dependsOn, transformerClass);
            return false;
        }

        rootNode.Dependencies.Add(dependencyNode);
        return true;
    }

    /// <summary>Should be called after all dependencies are added.</summary>
    public void CommitDependencies()
    {
        foreach (var node in _dependencyNodes.Values)
            node.Depth = null;
        _dependencyRoot.Depth = 0;

        // Grows during iteration!
        var processQueue = new List<DependencyGraphNode> { _dependencyRoot };
        for (var i = 0; i < processQueue.Count; ++i)
        {
            var parentNode = processQueue[i];
            foreach (var childNode in parentNode.Dependencies)
            {
                childNode.Depth = Math.Max(parentNode.Depth!.Value + 1, childNode.Depth ?? 0u);
                processQueue.Add(childNode);
            }
        }

        var orderedNodes = _dependencyNodes.Values.ToList();
        orderedNodes.Sort(static (lhs, rhs) =>
        {
            Debug.Assert(lhs.Depth.HasValue && rhs.Depth.HasValue);
            return rhs.Depth!.Value.CompareTo(lhs.Depth!.Value);
        });

        _dependencyOrder.Clear();
        for (var i = 0; i < orderedNodes.Count; ++i)
        {
            var node = orderedNodes[i];
            if (node.Name.Length != 0)
                _dependencyOrder.TryAdd(new StringHash(node.Name), checked((uint)i + 1));
        }
    }

    /// <summary>Enumerate all transformers for given path and any flavor.</summary>
    public List<AssetTransformer> GetTransformerCandidates(string resourcePath)
    {
        return GetTransformerCandidates(resourcePath, ApplicationFlavor.Universal);
    }

    /// <summary>Enumerate all transformers for given path and flavor.</summary>
    public List<AssetTransformer> GetTransformerCandidates(string resourcePath, in ApplicationFlavor flavor)
    {
        var result = new List<TransformerInfo>();
        var path = resourcePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var requestedFlavor = flavor;
        _root.IterateNodesChildFirst(path, 0, (inverseDepth, node) =>
        {
            foreach (var transformer in node.Transformers)
            {
                if (transformer.IsPostTransform() != _isPostTransform)
                    continue;

                var flavorMatchPenalty = requestedFlavor.Matches(transformer.GetFlavor());
                if (flavorMatchPenalty.HasValue)
                {
                    result.Add(new TransformerInfo(
                        transformer, GetTypeOrder(transformer.GetTypeNameHash()),
                        checked((uint)inverseDepth), flavorMatchPenalty.Value));
                }
            }
        });

        result = result
            .OrderBy(static info => info.TypeOrder)
            .ThenBy(static info => info.PathOrder)
            .ThenBy(static info => info.FlavorOrder)
            .ToList();
        FilterDuplicates(result);
        return Reduce(result);
    }

    private class DependencyGraphNode
    {
        public uint? Depth;
        public string Name = string.Empty;
        public readonly List<DependencyGraphNode> Dependencies = [];
    }

    private class TreeNode
    {
        public TreeNode()
        {
        }

        public TreeNode(string name)
        {
            Name = name;
        }

        public TreeNode GetOrCreateChild(string name)
        {
            if (Children.TryGetValue(name, out var child))
                return child;

            child = new TreeNode(name);
            Children.Add(name, child);
            return child;
        }

        public TreeNode GetOrCreateChild(IReadOnlyList<string> path, int index = 0)
        {
            if (index == path.Count)
                return this;

            return GetOrCreateChild(path[index]).GetOrCreateChild(path, index + 1);
        }

        public TreeNode? GetChild(string name)
        {
            return Children.GetValueOrDefault(name);
        }

        public void Iterate(Action<TreeNode> callback)
        {
            callback(this);
            foreach (var child in Children.Values)
                child.Iterate(callback);
        }

        public void IterateNodesChildFirst(
            IReadOnlyList<string> path, int index, Action<int, TreeNode> callback)
        {
            if (index != path.Count)
            {
                var child = GetChild(path[index]);
                child?.IterateNodesChildFirst(path, index + 1, callback);
            }

            callback(path.Count - index, this);
        }

        public string Name = string.Empty;
        public readonly List<AssetTransformer> Transformers = [];
        public readonly SortedDictionary<string, TreeNode> Children = new(StringComparer.Ordinal);
    }

    private DependencyGraphNode GetOrCreateDependencyNode(string name)
    {
        if (_dependencyNodes.TryGetValue(name, out var node))
            return node;

        var newNode = new DependencyGraphNode
        {
            Name = name,
        };

        _dependencyNodes.Add(name, newNode);
        _dependencyRoot.Dependencies.Add(newNode);
        return newNode;
    }

    private bool DependsOn(DependencyGraphNode queryNode, DependencyGraphNode dependencyNode)
    {
        foreach (var dependency in queryNode.Dependencies)
        {
            if (ReferenceEquals(dependency, dependencyNode))
                return true;
        }

        foreach (var dependency in queryNode.Dependencies)
        {
            if (DependsOn(dependency, dependencyNode))
                return true;
        }

        return false;
    }

    private uint GetTypeOrder(StringHash type)
    {
        return _dependencyOrder.GetValueOrDefault(type);
    }

    private readonly bool _isPostTransform;

    private TreeNode _root = null!;

    private DependencyGraphNode _dependencyRoot = null!;
    private Dictionary<string, DependencyGraphNode> _dependencyNodes = null!;
    private readonly Dictionary<StringHash, uint> _dependencyOrder = [];

    private readonly record struct TransformerInfo(
        AssetTransformer Transformer, uint TypeOrder, uint PathOrder, uint FlavorOrder);

    private static void FilterDuplicates(List<TransformerInfo> result)
    {
        var usedTypes = new HashSet<StringHash>();
        for (var i = 0; i < result.Count;)
        {
            var info = result[i];
            if (info.Transformer.IsSingleInstanced() && usedTypes.Contains(info.Transformer.GetTypeNameHash()))
                result.RemoveAt(i);
            else
            {
                usedTypes.Add(info.Transformer.GetTypeNameHash());
                ++i;
            }
        }
    }

    private static List<AssetTransformer> Reduce(IReadOnlyList<TransformerInfo> source)
    {
        var result = new List<AssetTransformer>();
        foreach (var info in source)
            result.Add(info.Transformer);
        return result;
    }
}
