namespace RedBolt;

/// <summary>Abstract graph node pin.</summary>
public class GraphPin : IArchiveSerializable
{
    /// <summary>Get name of the pin.</summary>
    public string GetName() => _name;

    /// <summary>Serialize content from/to archive.</summary>
    public virtual void SerializeInBlock(Archive archive)
    {
        archive.Serialize("name", ref _name);
    }

    /// <summary>Set name of the pin. Executed by GraphNode.</summary>
    internal void SetName(string name)
    {
        _name = name;
    }

    /// <summary>Pin name.</summary>
    protected string _name = string.Empty;
}

/// <summary>Abstract graph data flow node pin. Has pin type.</summary>
public class GraphDataPin : GraphPin
{
    /// <summary>Get pin type.</summary>
    public new VariantType GetType() => _type;

    /// <summary>Set pin type.</summary>
    public void SetType(VariantType type)
    {
        _type = type;
    }

    /// <summary>Serialize content from/to archive.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeEnum(archive, "type", ref _type, Variant.GetTypeNameList());
    }

    /// <summary>Pin type.</summary>
    protected VariantType _type = VariantType.None;
}

/// <summary>Graph node pin that connects to other pins.</summary>
public class GraphOutPin : GraphDataPin
{
}

/// <summary>Graph node pin with connection.</summary>
public class GraphInPin : GraphDataPin
{
    /// <summary>Connect to other pin.</summary>
    public bool ConnectTo(GraphPinRef<GraphOutPin> pin)
    {
        if (pin)
        {
            _targetNode = pin.GetNode()!.GetID();
            _targetPin = pin.GetPin()!.GetName();
            return true;
        }

        Disconnect();
        return false;
    }

    /// <summary>Disconnect pin.</summary>
    public void Disconnect()
    {
        _targetNode = 0;
        _targetPin = string.Empty;
    }

    /// <summary>Get true if pin is connected.</summary>
    public bool IsConnected() => _targetNode != 0;

    /// <summary>Get value.</summary>
    public Variant GetValue() => _value;

    /// <summary>Set value. Disconnects pin from an Out pin.</summary>
    public void SetValue(Variant variant)
    {
        _value = variant;
        Disconnect();
    }

    /// <summary>Serialize content from/to archive.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeValue(archive, "node", ref _targetNode);
        ArchiveSerialization.SerializeValue(archive, "pin", ref _targetPin);

        var value = _value;
        ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, _type);
        if (archive.IsInput)
            _value = value;
    }

    /// <summary>Target node.</summary>
    private uint _targetNode;

    /// <summary>Target pin name.</summary>
    private string _targetPin = string.Empty;

    /// <summary>Target node.</summary>
    private Variant _value;
}

/// <summary>Graph node execution flow "enter" pin. May be connected to multiple exit pins.</summary>
public class GraphEnterPin : GraphPin
{
}

/// <summary>Graph node execution flow "exit" pin. May be connected to one enter pin.</summary>
public class GraphExitPin : GraphPin
{
    /// <summary>Connect to other pin.</summary>
    public bool ConnectTo(GraphPinRef<GraphEnterPin> pin)
    {
        if (pin)
        {
            _targetNode = pin.GetNode()!.GetID();
            _targetPin = pin.GetPin()!.GetName();
            return true;
        }

        Disconnect();
        return false;
    }

    /// <summary>Disconnect pin.</summary>
    public void Disconnect()
    {
        _targetNode = 0;
        _targetPin = string.Empty;
    }

    /// <summary>Get true if pin is connected.</summary>
    public bool IsConnected() => _targetNode != 0;

    /// <summary>Serialize content from/to archive.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeValue(archive, "node", ref _targetNode);
        ArchiveSerialization.SerializeValue(archive, "pin", ref _targetPin);

        if (archive.IsInput && (_targetNode == 0 || string.IsNullOrEmpty(_targetPin)))
            Disconnect();
    }

    /// <summary>Target node.</summary>
    private uint _targetNode;

    /// <summary>Target pin name.</summary>
    private string _targetPin = string.Empty;
}

/// <summary>Reference to a graph pin and its node.</summary>
public readonly struct GraphPinRef<TPin> where TPin : GraphPin
{
    internal GraphPinRef(GraphNode? node, TPin? pin)
    {
        _node = node;
        _pin = pin;
    }

    public GraphNode? GetNode() => _node;
    public TPin? GetPin() => _pin;

    public static bool operator true(GraphPinRef<TPin> value) => value._node is not null && value._pin is not null;
    public static bool operator false(GraphPinRef<TPin> value) => value._node is null || value._pin is null;
    public static bool operator !(GraphPinRef<TPin> value) => value._node is null || value._pin is null;
    public static implicit operator GraphNode?(GraphPinRef<TPin> value) => value._node;
    public static implicit operator TPin?(GraphPinRef<TPin> value) => value._pin;

    // TODO: port GraphNode::GetGraph()->GetConnectedPin once Graph.h is ported.
    public GraphPinRef<TConnectedPin> GetConnectedPin<TConnectedPin>() where TConnectedPin : GraphPin
    {
        return default;
    }

    private readonly GraphNode? _node;
    private readonly TPin? _pin;
}

/// <summary>Partial graph node identity surface required by GraphPin references.</summary>
public partial class GraphNode : Object
{
    public uint GetID() => _id;
    public string GetName() => _name;

    internal void SetID(uint id)
    {
        _id = id;
    }

    internal void SetName(string name)
    {
        _name = name;
    }

    private uint _id;
    private string _name = string.Empty;
}
