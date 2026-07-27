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
public abstract class PrefabWriter : IDisposable
{
    public virtual void Dispose()
    {
    }

    public abstract void WriteNode(uint id, Serializable node);
    public abstract void WriteNumComponents(int numComponents);
    public abstract void WriteComponent(uint id, Serializable component);
    public abstract void WriteNumChildren(int numChildren);
    public abstract void BeginChild();
    public abstract void EndChild();
    public abstract bool IsEOF();
    public abstract PrefabSaveFlag GetFlags();
}

/// <summary>Utility class to write prefab data to NodePrefab.</summary>
public class PrefabWriterToMemory : PrefabWriter
{
    public PrefabWriterToMemory(NodePrefab nodePrefab, PrefabSaveFlag flags = PrefabSaveFlag.None)
    {
        _nodePrefab = nodePrefab;
        _flags = flags;
        _stack.Add((null, 0));

        _nodePrefab.Clear();
    }

    public override void WriteNode(uint id, Serializable node)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var nodePrefab = CurrentNode().Node;
        nodePrefab.SetId((SerializableId)id);
        nodePrefab.Import(node, _flags);
        nodePrefab.SetType(StringHash.Empty);
    }

    public override void WriteNumComponents(int numComponents)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var components = CurrentNode().Components;
        var size = numComponents;
        for (var index = components.Count - 1; index >= size; --index)
        {
            components[index].Dispose();
            components.RemoveAt(index);
        }
        for (var index = components.Count; index < size; ++index)
            components.Add(new SerializablePrefab());
        _componentIndex = 0;
    }

    public override void WriteComponent(uint id, Serializable component)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var components = CurrentNode().Components;
        Debug.Assert(_componentIndex < components.Count);

        var componentPrefab = components[_componentIndex];
        componentPrefab.SetId((SerializableId)id);
        componentPrefab.Import(component, _flags);

        ++_componentIndex;
    }

    public override void WriteNumChildren(int numChildren)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        if (numChildren > 0)
        {
            var children = CurrentNode().Children;
            var size = numChildren;
            for (var index = children.Count - 1; index >= size; --index)
            {
                children[index].Dispose();
                children.RemoveAt(index);
            }
            for (var index = children.Count; index < size; ++index)
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

    public override void Dispose()
    {
        _stack.Clear();
        base.Dispose();
    }

    private NodePrefab CurrentNode()
    {
        var (parentNode, childIndex) = _stack[^1];
        return parentNode is not null ? parentNode.Children[childIndex] : _nodePrefab;
    }

    private void StartChildren()
    {
        _stack.Add((CurrentNode(), 0));
    }

    private void NextNode()
    {
        var (parentNode, childIndex) = _stack[^1];

        var maxChildren = parentNode is not null ? parentNode.Children.Count : 1;
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

    private readonly List<(NodePrefab? ParentNode, int ChildIndex)> _stack = [];
    private int _componentIndex;
}

/// <summary>Utility class to write prefab data to Archive.</summary>
public class PrefabWriterToArchive : PrefabWriter
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
            _stack.Add((new ArchiveBlock(rootBlock), 1));
            _hasRootBlock = true;
        }

        Debug.Assert(!_archive.IsInput);
    }

    public override void WriteNode(uint id, Serializable node)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        _buffer.SetId((SerializableId)id);
        _buffer.Import(node, _saveFlags);
        _buffer.SerializeInBlock(_archive, _nodeFlags);
    }

    public override void WriteNumComponents(int numComponents)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        if (numComponents > 0 || !_archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            var block = _archive.OpenArrayBlock("components", numComponents);
            if (numComponents > 0)
                _stack.Add((new ArchiveBlock(block), numComponents));
            else
                block.Dispose();
        }
    }

    public override void WriteComponent(uint id, Serializable component)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        _buffer.SetId((SerializableId)id);
        _buffer.Import(component, _saveFlags);

        using (var block = _archive.OpenUnorderedBlock("component"))
        {
            _buffer.SerializeInBlock(_archive, _componentFlags);
        }

        NextSerializable();
    }

    public override void WriteNumChildren(int numChildren)
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        if (numChildren > 0 || !_archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            var block = _archive.OpenArrayBlock("nodes", numChildren);
            if (numChildren > 0)
                _stack.Add((new ArchiveBlock(block), numChildren));
            else
            {
                block.Dispose();
                CheckEOF();
            }
        }
        else
            CheckEOF();
    }

    public override void BeginChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to write");

        var block = _archive.OpenUnorderedBlock("node");
        _stack.Add((new ArchiveBlock(block), 1));
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

    public override void Dispose()
    {
        for (var index = _stack.Count - 1; index >= 0; --index)
            _stack[index].Block.Dispose();
        _stack.Clear();
        _buffer.Dispose();
        _eof = true;
        base.Dispose();
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
            for (var index = _stack.Count - 1; index >= 0; --index)
                _stack[index].Block.Dispose();
            _stack.Clear();
            _eof = true;
        }
    }

    private readonly Archive _archive;
    private readonly PrefabArchiveFlag _nodeFlags;
    private readonly PrefabArchiveFlag _componentFlags;
    private readonly PrefabSaveFlag _saveFlags;
    private readonly SerializablePrefab _buffer = new();

    private bool _hasRootBlock;
    private readonly List<(ArchiveBlock Block, int Count)> _stack = [];
    private bool _eof;
}
