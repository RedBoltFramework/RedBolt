//
// Copyright (c) 2021 the rbfx project.
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

namespace RedBolt.Particles;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Particles.ParticleGraphNodes;
using RedBolt.Resource;

public class ParticleGraph : RedObject
{
    public const int InvalidNodeIndex = -1;

    /// <summary>
    /// Construct.
    /// </summary>
    public ParticleGraph()
    {
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        for (var index = _nodes.Count - 1; index >= 0; --index)
            _nodes[index].Dispose();
        _nodes.Clear();
        base.Dispose();
    }

    /// <summary>
    /// Add node to the graph.
    /// Returns node index;
    /// </summary>
    public int Add(ParticleGraphNode? node)
    {
        if (node is null)
        {
            Log.GetLogger().LogError("Can't add empty node");
            return 0;
        }
        var index = _nodes.Count;
        _nodes.Add(node);
        node.SetGraphFromParticleGraphFriend(this, index);
        return index;
    }

    /// <summary>
    /// Get number of nodes.
    /// </summary>
    public int NumNodes => _nodes.Count;

    /// <summary>
    /// Get node by index.
    /// </summary>
    public ParticleGraphNode? GetNode(int index)
    {
        if (index < 0 || index >= _nodes.Count)
        {
            Log.GetLogger().LogError("Node index out of bounds");
            return null;
        }
        return _nodes[index];
    }

    /// <summary>
    /// Load graph.
    /// </summary>
    public bool LoadGraph(Graph graph)
    {
        using var reader = new ParticleGraphReader(this, graph);
        return reader.Read();
    }

    /// <summary>
    /// Save graph.
    /// </summary>
    public bool SaveGraph(Graph graph)
    {
        graph.Clear();
        var writer = new ParticleGraphWriter(this, graph);
        return writer.Write();
    }

    /// <summary>
    /// Serialize from/to archive. Return true if successful.
    /// </summary>
    public override void SerializeInBlock(Archive archive)
    {
        using var graph = new Graph();
        if (archive.IsInput)
        {
            graph.SerializeInBlock(archive);
            LoadGraph(graph);
        }
        else
        {
            SaveGraph(graph);
            graph.SerializeInBlock(archive);
        }
    }

    /// <summary>
    /// Nodes in the graph;
    /// </summary>
    private readonly List<ParticleGraphNode> _nodes = [];
}

public class ParticleGraphWriter
{
    public ParticleGraphWriter(ParticleGraph particleGraph, Graph graph)
    {
        _particleGraph = particleGraph;
        _graph = graph;
        _system = particleGraph.GetSubsystem<ParticleGraphSystem>();

        _nodes.Capacity = particleGraph.NumNodes;
        for (var i = 0; i < particleGraph.NumNodes; ++i)
            _nodes.Add(0);
    }

    /// <summary>
    /// Write graph.
    /// </summary>
    public bool Write()
    {
        for (var i = 0; i < _particleGraph.NumNodes; ++i)
        {
            var id = WriteNode(i);
            if (id == 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Write graph node. Return node id.
    /// </summary>
    public uint WriteNode(int index)
    {
        if (_nodes[index] != 0)
            return _nodes[index];

        var node = _particleGraph.GetNode(index)!;
        var outNode = _graph.Create(node.GetTypeName());
        _nodes[index] = outNode.GetID();
        if (!node.Save(this, outNode))
            return 0;
        return 1;
    }

    /// <summary>
    /// Get source pin reference.
    /// </summary>
    public GraphPinRef<GraphOutPin> GetSourcePin(int connectedNode, int connectedPinIndex)
    {
        var node = _particleGraph.GetNode(connectedNode)!;
        var outNode = _nodes[connectedNode];
        var pin = node.GetPin(connectedPinIndex);
        return _graph.GetNode(outNode)!.GetOrAddOutput(pin.Name);
    }

    private readonly ParticleGraph _particleGraph;
    private readonly Graph _graph;
    private readonly ParticleGraphSystem _system;
    private readonly List<uint> _nodes = [];
}

public class ParticleGraphReader : IDisposable
{
    public ParticleGraphReader(ParticleGraph particleGraph, Graph graph)
    {
        _particleGraph = particleGraph;
        _graph = graph;
        _system = particleGraph.GetSubsystem<ParticleGraphSystem>();
        graph.GetNodeIds(_ids);
    }

    public void Dispose()
    {
        foreach (var constant in _constants.Keys)
        {
            var ownedConstant = constant;
            ownedConstant.Dispose();
        }
        _constants.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Load graph.
    /// </summary>
    public bool Read()
    {
        foreach (var id in _ids)
        {
            if (ReadNode(id) == ParticleGraph.InvalidNodeIndex)
                return false;
        }
        return true;
    }

    public int ReadNode(uint id)
    {
        if (_nodes.TryGetValue(id, out var existing))
        {
            if (existing == ParticleGraph.InvalidNodeIndex)
                Log.GetLogger().LogError("Loop detected at particle graph");
            return existing;
        }
        _nodes[id] = ParticleGraph.InvalidNodeIndex;

        var srcNode = _graph.GetNode(id)!;
        var newNode = _system.CreateObject(srcNode.GetNameHash());
        if (newNode is null)
        {
            Log.GetLogger().LogError("Unknown node type {Type}", srcNode.GetName());
            return ParticleGraph.InvalidNodeIndex;
        }
        if (!newNode.Load(this, srcNode))
        {
            newNode.Dispose();
            return ParticleGraph.InvalidNodeIndex;
        }

        for (var i = 0; i < newNode.NumPins; ++i)
        {
            var pin = newNode.GetPin(i);
            if (pin.IsInput && pin.RequestedType != VariantType.None)
            {
                if (pin.ConnectedNodeIndex == ParticleGraph.InvalidNodeIndex)
                {
                    var constNode = new Constant();
                    using var value = new Variant(pin.RequestedType);
                    constNode.SetValue(value);
                    var constIndex = _particleGraph.Add(constNode);
                    newNode.SetPinSource(i, constIndex, 0);
                }
            }
        }

        var dstIndex = _particleGraph.Add(newNode);
        _nodes[id] = dstIndex;
        return dstIndex;
    }

    public int GetOrAddConstant(in Variant constValue)
    {
        if (!_constants.TryGetValue(constValue, out var index))
        {
            var constNode = new Constant();
            constNode.SetValue(constValue);
            index = _particleGraph.Add(constNode);
            _constants.Add(constValue.Clone(), index);
        }
        return index;
    }

    public int GetInputPinIndex(int nodeIndex, string name)
    {
        var node = _particleGraph.GetNode(nodeIndex);
        if (node is null)
            return ParticleGraphNode.InvalidPin;
        return node.GetPinIndex(name);
    }

    private readonly ParticleGraph _particleGraph;
    private readonly Graph _graph;
    private readonly ParticleGraphSystem _system;
    private readonly List<uint> _ids = [];
    private readonly Dictionary<uint, int> _nodes = [];
    private readonly Dictionary<Variant, int> _constants = [];
}
