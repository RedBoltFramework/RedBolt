using System.Text;

namespace RedBolt;

public partial class Graph : RedObject
{
    public const uint MaxId = uint.MaxValue;
    public const uint FirstId = 1;

    public Graph()
    {
    }

    /// <summary>Destruct. Free all resources.</summary>
    public override void Dispose()
    {
        Clear();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
    }

    /// <summary>Get number of nodes in the graph.</summary>
    public int GetNumNodes() => _nodes.Count;

    /// <summary>Get node ids.</summary>
    public void GetNodeIds(List<uint> ids)
    {
        ids.Clear();
        ids.Capacity = Math.Max(ids.Capacity, _nodes.Count);
        foreach (var node in _nodes)
            ids.Add(node.Key);
    }

    /// <summary>Get node by id.</summary>
    public GraphNode? GetNode(uint id)
    {
        return _nodes.GetValueOrDefault(id);
    }

    /// <summary>Load graph from xml. Return true if successful.</summary>
    public bool LoadXML(string xml)
    {
        using var buffer = new MemoryBuffer(Encoding.UTF8.GetBytes(xml));
        using var file = new XMLFile();
        if (!file.Load(buffer))
            return false;

        return file.LoadObject(this);
    }

    /// <summary>Load graph from json. Return true if successful.</summary>
    public bool LoadJSON(string json)
    {
        using var buffer = new MemoryBuffer(Encoding.UTF8.GetBytes(json));
        var file = new JSONFile();
        if (!file.BeginLoad(buffer))
            return false;

        return file.LoadObject(this);
    }

    /// <summary>Serialize content from/to archive.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        var size = archive.IsInput ? 0 : _nodes.Count;
        using var block = archive.OpenArrayBlock("nodes", size);
        if (archive.IsInput)
        {
            _nodes.Clear();
            size = block.SizeHint;
            for (var index = 0; index < size; ++index)
            {
                using var nodeBlock = archive.OpenUnorderedBlock("node");
                var id = 0u;
                ArchiveSerialization.SerializeValue(archive, "id", ref id);
                var node = new GraphNode();
                node.SetGraph(this, id);
                _nodes[id] = node;
                node.SerializeInBlock(archive);
            }
        }
        else
        {
            foreach (var item in _nodes)
            {
                using var nodeBlock = archive.OpenUnorderedBlock("node");
                var id = item.Key;
                ArchiveSerialization.SerializeValue(archive, "id", ref id);
                item.Value.SerializeInBlock(archive);
            }
        }
    }

    /// <summary>Add node to the graph.</summary>
    public void Add(GraphNode? node)
    {
        // Skip if no node provided.
        if (node is null)
            return;

        // Skip if already added.
        if (node.GetGraph() == this)
            return;

        // Detach from previous graph.
        if (node.GetGraph() is { } previousGraph)
            previousGraph.Remove(node);

        // If the new node has an ID of zero (default), assign an ID now
        var id = node.GetID();
        if (id == 0)
        {
            id = GetFreeNodeID();
            _nodes[id] = node;
            node.SetGraph(this, id);
            return;
        }

        if (!_nodes.ContainsKey(id))
        {
            if (id >= _lastKnownNodeId)
            {
                if (id == MaxId - 1)
                    _lastKnownNodeId = FirstId;
                else
                    _lastKnownNodeId = id + 1;
            }
            _nodes[id] = node;
            node.SetGraph(this, id);
            return;
        }

        id = GetFreeNodeID();
        _nodes[id] = node;
        node.SetGraph(this, id);
    }

    /// <summary>Create node at the graph.</summary>
    public GraphNode Create(string name)
    {
        var node = new GraphNode();
        node.SetName(name);
        Add(node);
        return node;
    }

    /// <summary>Add node to the graph.</summary>
    public void Remove(GraphNode? node)
    {
        // Skip if no node provided.
        if (node is null)
            return;

        // Skip if already removed.
        if (node.GetGraph() != this)
            return;

        var id = node.GetID();
        _nodes.Remove(id);

        // Keep id after deletion.
        node.SetGraph(null, id);
    }

    /// <summary>Remove all nodes.</summary>
    public void Clear()
    {
        _lastKnownNodeId = FirstId;

        foreach (var item in _nodes)
            item.Value.SetGraph(null, item.Key);
        _nodes.Clear();
    }

    /// <summary>Connect exit pin to enter pin.</summary>
    public void Connect(GraphPinRef<GraphExitPin> pin, GraphPinRef<GraphEnterPin> target)
    {
    }

    /// <summary>Connect input pin to output pin.</summary>
    public void Connect(GraphPinRef<GraphInPin> pin, GraphPinRef<GraphOutPin> target)
    {
    }

    /// <summary>Get pin connected to the exit pin.</summary>
    public GraphPinRef<GraphEnterPin> GetConnectedPin(GraphExitPin pin)
    {
        if (pin._targetNode == 0)
            return default;

        if (GetNode(pin._targetNode) is { } target)
            return target.GetEnter(pin._targetPin);
        return default;
    }

    /// <summary>Get pin connected to the input pin.</summary>
    public GraphPinRef<GraphOutPin> GetConnectedPin(GraphInPin pin)
    {
        if (pin._targetNode == 0)
            return default;

        if (GetNode(pin._targetNode) is { } target)
            return target.GetOutput(pin._targetPin);
        return default;
    }

    private uint GetFreeNodeID()
    {
        for (;;)
        {
            var ret = _lastKnownNodeId;
            if (_lastKnownNodeId < MaxId)
                ++_lastKnownNodeId;
            else
                _lastKnownNodeId = FirstId;

            if (!_nodes.ContainsKey(ret))
                return ret;
        }
    }

    /// <summary>Last known node ID.</summary>
    private uint _lastKnownNodeId = FirstId;

    /// <summary>Replicated scene nodes by ID.</summary>
    private readonly Dictionary<uint, GraphNode> _nodes = [];
}
