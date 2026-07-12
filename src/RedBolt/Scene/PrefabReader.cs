using System.Diagnostics;

namespace RedBolt;

/// <summary>
/// Interface of a class that provides prefab data.
/// Useful for writing generic code in Node and Scene serialization.
/// Call order:
/// - ReadNode() should be called first exactly once.
/// - ReadNumComponents() should be called after ReadNode() exactly once.
/// - ReadComponent() should be called after ReadNumComponents() the exact number of times.
/// - ReadNumChildren() should be called after all calls to ReadComponent() exactly once.
/// - BeginChild() and then EndChild() should be called after ReadNumChildren() the exact number of times.
/// - The sequence above should be recursively repeated between BeginChild() and EndChild().
/// - Previously returned pointers should not be used after the call to ReadNode() or ReadComponent().
/// </summary>
public abstract class PrefabReader
{
    public abstract SerializablePrefab? ReadNode();
    public abstract uint ReadNumComponents();
    public abstract SerializablePrefab? ReadComponent();
    public abstract uint ReadNumChildren();
    public abstract void BeginChild();
    public abstract void EndChild();
    public abstract bool IsEOF();
}

/// <summary>
/// Utility class to read prefab data from NodePrefab.
/// </summary>
public sealed class PrefabReaderFromMemory : PrefabReader
{
    public PrefabReaderFromMemory(NodePrefab nodePrefab)
    {
        _nodePrefab = nodePrefab;
        _stack.Add((null, 0));
    }

    public override SerializablePrefab? ReadNode()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");
        return CurrentNode().Node;
    }

    public override uint ReadNumComponents()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        _componentIndex = 0;
        return (uint)CurrentNode().Components.Count;
    }

    public override SerializablePrefab? ReadComponent()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        var components = CurrentNode().Components;
        Debug.Assert(_componentIndex < components.Count);

        var component = components[(int)_componentIndex];
        ++_componentIndex;
        return component;
    }

    public override uint ReadNumChildren()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        var numChildren = (uint)CurrentNode().Children.Count;
        if (numChildren > 0)
            StartChildren();
        else
            UpdateEOF();
        return numChildren;
    }

    public override void BeginChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");
    }

    public override void EndChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        NextNode();
        UpdateEOF();
    }

    public override bool IsEOF() => _stack.Count == 0;

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
        var entry = _stack[^1];

        var maxChildren = entry.ParentNode is not null ? entry.ParentNode.Children.Count : 1;
        ++entry.ChildIndex;
        if (entry.ChildIndex >= maxChildren)
            _stack.RemoveAt(_stack.Count - 1);
        else
            _stack[^1] = entry;
    }

    private void UpdateEOF()
    {
        if (_stack.Count == 1)
            _stack.Clear();
    }

    private readonly NodePrefab _nodePrefab;
    private readonly List<(NodePrefab? ParentNode, uint ChildIndex)> _stack = [];
    private uint _componentIndex;
}

/// <summary>
/// Utility class to read prefab data from Archive.
/// </summary>
public sealed class PrefabReaderFromArchive : PrefabReader, IDisposable
{
    public PrefabReaderFromArchive(Archive archive, string? blockName, PrefabArchiveFlag flags = PrefabArchiveFlag.None)
    {
        _archive = archive;
        _nodeFlags = PrefabTypes.ToNodeFlags(flags);
        _componentFlags = PrefabTypes.ToComponentFlags(flags);

        if (blockName is not null)
        {
            var rootBlock = _archive.OpenUnorderedBlock(blockName);
            _stack.Add((rootBlock, 1));
            _hasRootBlock = true;
        }
        Debug.Assert(archive.IsInput);
    }

    public override SerializablePrefab? ReadNode()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        _buffer.SerializeInBlock(_archive, _nodeFlags);

        return _buffer;
    }

    public override uint ReadNumComponents()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        var numComponents = 0u;
        if (_archive.HasElementOrBlock("components"))
        {
            var block = _archive.OpenArrayBlock("components");
            numComponents = block.SizeHint;
            if (numComponents > 0)
                _stack.Add((block, numComponents));
            else
                block.Dispose();
        }
        return numComponents;
    }

    public override SerializablePrefab? ReadComponent()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        using (var block = _archive.OpenUnorderedBlock("component"))
        {
            _buffer.SerializeInBlock(_archive, _componentFlags);
        }
        NextSerializable();

        return _buffer;
    }

    public override uint ReadNumChildren()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        var numChildren = 0u;
        if (_archive.HasElementOrBlock("nodes"))
        {
            var block = _archive.OpenArrayBlock("nodes");
            numChildren = block.SizeHint;
            if (numChildren > 0)
                _stack.Add((block, numChildren));
            else
            {
                block.Dispose();
                UpdateEOF();
            }
        }
        else
            UpdateEOF();
        return numChildren;
    }

    public override void BeginChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        var block = _archive.OpenUnorderedBlock("node");
        _stack.Add((block, 1));
    }

    public override void EndChild()
    {
        Debug.Assert(!IsEOF(), "There is no more data to read");

        var childBlock = _stack[^1].Block;
        childBlock.Dispose();
        _stack.RemoveAt(_stack.Count - 1);
        NextSerializable();
        UpdateEOF();
    }

    public override bool IsEOF() => _eof;

    public void Dispose()
    {
        for (var i = _stack.Count - 1; i >= 0; --i)
            _stack[i].Block.Dispose();
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

    private void UpdateEOF()
    {
        if (_stack.Count == (_hasRootBlock ? 1 : 0))
        {
            for (var i = _stack.Count - 1; i >= 0; --i)
                _stack[i].Block.Dispose();
            _stack.Clear();
            _eof = true;
        }
    }

    private readonly Archive _archive;
    private readonly PrefabArchiveFlag _nodeFlags;
    private readonly PrefabArchiveFlag _componentFlags;
    private readonly SerializablePrefab _buffer = new();

    private bool _hasRootBlock;
    private readonly List<(ArchiveBlock Block, uint Count)> _stack = [];
    private bool _eof;
}
