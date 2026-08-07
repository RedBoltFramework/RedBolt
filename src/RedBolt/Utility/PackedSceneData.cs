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

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Scene;

/// <summary>Packed node data.</summary>
public class PackedNodeData : ICloneable
{
    public PackedNodeData()
    {
    }

    /// <summary>Create from existing node.</summary>
    public PackedNodeData(Node node)
    {
        _id = node.Id;
        _parentId = node.Parent is not null ? node.Parent.Id : 0;
        _indexInParent = node.GetIndexInParent();
        _name = node.Name;

        ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var archive = new BinaryOutputArchive(Context.Instance, _data);

            using var block = archive.OpenUnorderedBlock("node");
            node.SerializeInBlock(archive, true, PrefabSaveFlag.CompactAttributeNames);
            _scopeHint = node.GetEffectiveScopeHint();
        });
    }

    private PackedNodeData(PackedNodeData rhs)
    {
        _id = rhs._id;
        _parentId = rhs._parentId;
        _indexInParent = rhs._indexInParent;
        _name = rhs._name;
        _data = (VectorBuffer)rhs._data.Clone();
        _scopeHint = rhs._scopeHint;
    }

    public PackedNodeData Clone() => new(this);

    object ICloneable.Clone() => Clone();

    /// <summary>Spawn exact node in the scene. May fail.</summary>
    public Node? SpawnExact(Scene scene)
    {
        var parent = scene.GetNode(_parentId);
        if (parent is null)
            return null;

        var node = parent.CreateChild(_name, _id);
        if (node.Id != _id)
        {
            node.Remove();
            return null;
        }

        ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var view = new MemoryBuffer(_data.GetBuffer());
            using var archive = new BinaryInputArchive(Context.Instance, view);

            using var block = archive.OpenUnorderedBlock("node");
            node.SerializeInBlock(archive, true, PrefabSaveFlag.CompactAttributeNames);
        });

        parent.ReorderChild(node, _indexInParent);
        return node;
    }

    /// <summary>Spawn similar node at the parent.</summary>
    public Node SpawnCopy(Node parent)
    {
        var node = parent.CreateChild(_name);

        ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var view = new MemoryBuffer(_data.GetBuffer());
            using var archive = new BinaryInputArchive(Context.Instance, view);

            using var block = archive.OpenUnorderedBlock("node");
            node.SerializeInBlock(archive, true, PrefabSaveFlag.CompactAttributeNames);
        });

        return node;
    }

    /// <summary>Return node ID.</summary>
    public uint Id => _id;

    /// <summary>
    /// Return whether the node spawn would affect the entire scene.
    /// Used to correctly handle undo/redo of node creation.
    /// </summary>
    public AttributeScopeHint EffectiveScopeHint => _scopeHint;

    private uint _id;
    private uint _parentId;
    private int _indexInParent;
    private string _name = string.Empty;
    private VectorBuffer _data = new();
    private AttributeScopeHint _scopeHint;
}

/// <summary>Packed component data.</summary>
public class PackedComponentData : ICloneable
{
    public PackedComponentData()
    {
    }

    /// <summary>Create from existing component.</summary>
    public PackedComponentData(Component component)
    {
        _id = component.Id;
        _nodeId = component.TryGetNode() is not null ? component.Node.Id : 0;
        _indexInParent = component.GetIndexInParent();
        _type = component.GetTypeNameHash();

        ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var archive = new BinaryOutputArchive(Context.Instance, _data);

            using var block = archive.OpenUnorderedBlock("component");
            component.SerializeInBlock(archive, true);
        });
    }

    private PackedComponentData(PackedComponentData rhs)
    {
        _id = rhs._id;
        _nodeId = rhs._nodeId;
        _indexInParent = rhs._indexInParent;
        _type = rhs._type;
        _data = (VectorBuffer)rhs._data.Clone();
    }

    public PackedComponentData Clone() => new(this);

    object ICloneable.Clone() => Clone();

    /// <summary>Spawn exact component in the scene. May fail.</summary>
    public Component? SpawnExact(Scene scene)
    {
        var node = scene.GetNode(_nodeId);
        if (node is null)
            return null;

        var component = node.CreateComponent(_type, _id);
        if (component is null)
            return null;

        if (component.Id != _id)
        {
            component.Remove();
            return null;
        }

        ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var view = new MemoryBuffer(_data.GetBuffer());
            using var archive = new BinaryInputArchive(Context.Instance, view);

            using var block = archive.OpenUnorderedBlock("component");
            component.SerializeInBlock(archive, true);
        });

        node.ReorderComponent(component, _indexInParent);
        return component;
    }

    /// <summary>Spawn similar component at the node. May fail only if component type is unknown.</summary>
    public Component? SpawnCopy(Node node)
    {
        var component = node.CreateComponent(_type, _id);
        if (component is null)
            return null;

        ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var view = new MemoryBuffer(_data.GetBuffer());
            using var archive = new BinaryInputArchive(Context.Instance, view);

            using var block = archive.OpenUnorderedBlock("component");
            component.SerializeInBlock(archive, true);
        });

        return component;
    }

    /// <summary>Update attributes of existing component.</summary>
    public void Update(Component component)
    {
        ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var view = new MemoryBuffer(_data.GetBuffer());
            using var archive = new BinaryInputArchive(Context.Instance, view);

            using var block = archive.OpenUnorderedBlock("component");
            component.SerializeInBlock(archive, true);
        });
    }

    /// <summary>Return component ID.</summary>
    public uint Id => _id;

    public StringHash Type => _type;

    private uint _id;
    private uint _nodeId;
    private int _indexInParent;
    private StringHash _type;
    private VectorBuffer _data = new();
}
