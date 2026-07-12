using System.Diagnostics;

namespace RedBolt;

/// <summary>
/// Interface of a class that writes prefab data.
/// Useful for writing generic code in Node and Scene serialization.
/// Call order:
/// - WriteNode() should be called first exactly once.
/// - WriteNumComponents() should be called after WriteNode() exactly once.
/// - WriteComponent() should be called after WriteNumComponents() the exact number of times.
/// - WriteNumChildren() should be called after all calls to WriteComponent() exactly once.
/// - BeginChild() and then EndChild() should be called after WriteNumChildren() the exact number of times.
/// - The sequence above should be recursively repeated between BeginChild() and EndChild().
/// </summary>
public abstract class PrefabWriter
{
    public abstract void WriteNode(uint id, Serializable? node);
    public abstract void WriteNumComponents(uint numComponents);
    public abstract void WriteComponent(uint id, Serializable? component);
    public abstract void WriteNumChildren(uint numChildren);
    public abstract void BeginChild();
    public abstract void EndChild();
    public abstract bool IsEOF();
    public abstract PrefabSaveFlag GetFlags();
}

/// <summary>Utility class to write prefab data to NodePrefab.</summary>
public sealed class PrefabWriterToMemory : PrefabWriter
{
    public PrefabWriterToMemory(NodePrefab nodePrefab, PrefabSaveFlag flags = PrefabSaveFlag.None)
    {
        _nodePrefab = nodePrefab;
        _flags = flags;
        _stack.Add((null, 0));

        _nodePrefab.Clear();
    }

    public override void WriteNode(uint id, Serializable? node)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var nodePrefab = CurrentNode().Node;
        nodePrefab.Id = id;
        nodePrefab.Import(node, _flags);
        nodePrefab.TypeName = string.Empty;
    }

    public override void WriteNumComponents(uint numComponents)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var components = CurrentNode().Components;
        components.Clear();
        for (var index = 0u; index < numComponents; ++index)
            components.Add(new SerializablePrefab());
    }

    public override void WriteComponent(uint id, Serializable? component)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var components = CurrentNode().Components;
        Debug.Assert(_componentIndex < components.Count);

        var componentPrefab = components[(int)_componentIndex];
        componentPrefab.Id = id;
        componentPrefab.Import(component, _flags);

        ++_componentIndex;
    }

    public override void WriteNumChildren(uint numChildren)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        if (numChildren > 0)
        {
            var children = CurrentNode().Children;
            children.Clear();
            for (var index = 0u; index < numChildren; ++index)
                children.Add(new NodePrefab());
            StartChildren();
        }
        else
            CheckEOF();
    }

    public override void BeginChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");
    }

    public override void EndChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        NextNode();
        CheckEOF();
    }

    public override bool IsEOF() => _stack.Count == 0;

    public override PrefabSaveFlag GetFlags() => _flags;

    private NodePrefab CurrentNode()
    {
        var (parentNode, childIndex) = _stack[^1];
        return parentNode is not null ? parentNode.Children[(int)childIndex] : _nodePrefab;
    }

    private void StartChildren()
    {
        _stack.Add((CurrentNode(), 0));
    }

    private void NextNode()
    {
        var (parentNode, childIndex) = _stack[^1];

        var maxChildren = parentNode is not null ? (uint)parentNode.Children.Count : 1u;
        ++childIndex;
        if (childIndex >= maxChildren)
            _stack.RemoveAt(_stack.Count - 1);
        else
            _stack[^1] = (parentNode, childIndex);
    }

    private void CheckEOF()
    {
        if (_stack.Count == 1)
            _stack.Clear();
    }

    private readonly NodePrefab _nodePrefab;
    private readonly PrefabSaveFlag _flags;

    private readonly List<(NodePrefab? ParentNode, uint ChildIndex)> _stack = [];
    private uint _componentIndex;
}

/// <summary>Utility class to write prefab data to Archive.</summary>
public sealed class PrefabWriterToArchive : PrefabWriter, IDisposable
{
    public PrefabWriterToArchive(
        Archive archive,
        string? blockName,
        PrefabSaveFlag saveFlags = PrefabSaveFlag.None,
        PrefabArchiveFlag archiveFlags = PrefabArchiveFlag.None)
    {
        _archive = archive;
        _nodeFlags = PrefabTypes.ToNodeFlags(archiveFlags);
        _componentFlags = PrefabTypes.ToComponentFlags(archiveFlags);
        _saveFlags = saveFlags;

        if (blockName is not null)
        {
            var rootBlock = _archive.OpenUnorderedBlock(blockName);
            _stack.Add((rootBlock, 1));
            _hasRootBlock = true;
        }

        Debug.Assert(!_archive.IsInput);
    }

    public override void WriteNode(uint id, Serializable? node)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        _buffer.Id = id;
        _buffer.Import(node, _saveFlags);
        _buffer.SerializeInBlock(_archive, _nodeFlags);
    }

    public override void WriteNumComponents(uint numComponents)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        if (numComponents > 0)
        {
            var block = _archive.OpenArrayBlock("components", numComponents);
            _stack.Add((block, numComponents));
        }
    }

    public override void WriteComponent(uint id, Serializable? component)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        _buffer.Id = id;
        _buffer.Import(component, _saveFlags);

        using (var block = _archive.OpenUnorderedBlock("component"))
        {
            _buffer.SerializeInBlock(_archive, _componentFlags);
        }

        NextSerializable();
    }

    public override void WriteNumChildren(uint numChildren)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        if (numChildren > 0)
        {
            var block = _archive.OpenArrayBlock("nodes", numChildren);
            _stack.Add((block, numChildren));
        }
        else
            CheckEOF();
    }

    public override void BeginChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var block = _archive.OpenUnorderedBlock("node");
        _stack.Add((block, 1));
    }

    public override void EndChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        _stack[^1].Block.Dispose();
        _stack.RemoveAt(_stack.Count - 1);
        NextSerializable();
        CheckEOF();
    }

    public override bool IsEOF() => _eof;

    public override PrefabSaveFlag GetFlags() => _saveFlags;

    public void Dispose()
    {
        for (var index = _stack.Count - 1; index >= 0; --index)
            _stack[index].Block.Dispose();
        _stack.Clear();
        _eof = true;
    }

    private void NextSerializable()
    {
        if (_stack.Count != 0)
        {
            var entry = _stack[^1];
            --entry.Count;
            if (entry.Count == 0)
            {
                entry.Block.Dispose();
                _stack.RemoveAt(_stack.Count - 1);
            }
            else
                _stack[^1] = entry;
        }
    }

    private void CheckEOF()
    {
        if (_stack.Count == (_hasRootBlock ? 1 : 0))
        {
            Dispose();
            _eof = true;
        }
    }

    private readonly Archive _archive;
    private readonly PrefabArchiveFlag _nodeFlags;
    private readonly PrefabArchiveFlag _componentFlags;
    private readonly PrefabSaveFlag _saveFlags;
    private readonly SerializablePrefab _buffer = new();

    private bool _hasRootBlock;
    private readonly List<(ArchiveBlock Block, uint Count)> _stack = [];
    private bool _eof;
}
