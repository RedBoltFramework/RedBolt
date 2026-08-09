namespace RedBolt.Resource;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Input;

public class GraphNodeProperty : IArchiveSerializable, IDisposable
{
    /// <summary>Property value.</summary>
    public Variant Value;

    /// <summary>Get property name.</summary>
    public string GetName() => _name;

    /// <summary>Get property name hash.</summary>
    public StringHash GetNameHash() => _nameHash;

    /// <summary>Set property name.</summary>
    public void SetName(string name)
    {
        _name = name;
        _nameHash = name;
    }

    /// <summary>Serialize content from/to archive.</summary>
    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeValue(archive, "name", ref _name);
        ArchiveSerialization.SerializeVariantInBlock(archive, ref Value);
    }

    public void Dispose()
    {
        Value.Dispose();
    }

    /// <summary>Property name.</summary>
    private string _name = string.Empty;

    /// <summary>Property name hash.</summary>
    private StringHash _nameHash;
}

[Reflected]
public partial class GraphNode : RedObject
{
    public GraphNode()
    {
    }

    public override void Dispose()
    {
        for (var index = _outputPins.Count - 1; index >= 0; --index)
            _outputPins[index].Dispose();
        _outputPins.Clear();

        for (var index = _inputPins.Count - 1; index >= 0; --index)
            _inputPins[index].Dispose();
        _inputPins.Clear();

        for (var index = _exitPins.Count - 1; index >= 0; --index)
            _exitPins[index].Dispose();
        _exitPins.Clear();

        for (var index = _enterPins.Count - 1; index >= 0; --index)
            _enterPins[index].Dispose();
        _enterPins.Clear();

        for (var index = _properties.Count - 1; index >= 0; --index)
            _properties[index].Dispose();
        _properties.Clear();

        base.Dispose();
    }


    /// <summary>Return ID.</summary>
    public uint GetID() => _id;

    /// <summary>Return Graph.</summary>
    public Graph? GetGraph() => _graph;

    /// <summary>Return name.</summary>
    public string GetName() => _name;

    /// <summary>Return name hash.</summary>
    public StringHash GetNameHash() => _nameHash;

    /// <summary>Get or add node property.</summary>
    public ref Variant GetOrAddProperty(string name)
    {
        for (var index = 0; index < _properties.Count; ++index)
        {
            if (_properties[index].GetName() == name)
                return ref CollectionsMarshal.AsSpan(_properties)[index].Value;
        }

        var property = new GraphNodeProperty();
        property.SetName(name);
        _properties.Add(property);
        return ref CollectionsMarshal.AsSpan(_properties)[_properties.Count - 1].Value;
    }

    /// <summary>Get node property. Returns null if property is not found.</summary>
    public ref Variant GetProperty(string name)
    {
        for (var index = 0; index < _properties.Count; ++index)
        {
            if (_properties[index].GetName() == name)
                return ref CollectionsMarshal.AsSpan(_properties)[index].Value;
        }
        return ref Unsafe.NullRef<Variant>();
    }

    /// <summary>Get properties.</summary>
    public IReadOnlyList<GraphNodeProperty> GetProperties() => _properties;

    /// <summary>Add property with value.</summary>
    public GraphNode WithProperty(string name, in Variant value)
    {
        var replacement = value.Clone();
        ref var property = ref GetOrAddProperty(name);
        property.Dispose();
        property = replacement;
        return this;
    }

    /// <summary>Get input pin by name.</summary>
    public GraphPinRef<GraphInPin> GetInput(string name)
    {
        for (var index = 0; index < _inputPins.Count; ++index)
        {
            if (_inputPins[index].GetName() == name)
                return new GraphPinRef<GraphInPin>(this, _inputPins[index]);
        }
        return new GraphPinRef<GraphInPin>(this, null);
    }

    /// <summary>Get input pin by index.</summary>
    public GraphPinRef<GraphInPin> GetInput(int index)
    {
        if (index < 0 || index >= _inputPins.Count)
            return default;
        return new GraphPinRef<GraphInPin>(this, _inputPins[index]);
    }

    /// <summary>Get number of input pins.</summary>
    public int GetNumInputs() => _inputPins.Count;

    /// <summary>Get or add input pin.</summary>
    public GraphPinRef<GraphInPin> GetOrAddInput(string name)
    {
        for (var index = 0; index < _inputPins.Count; ++index)
        {
            if (_inputPins[index].GetName() == name)
                return new GraphPinRef<GraphInPin>(this, _inputPins[index]);
        }

        var pin = new GraphInPin();
        pin.SetName(name);
        _inputPins.Add(pin);
        return new GraphPinRef<GraphInPin>(this, pin);
    }

    /// <summary>Add input pin.</summary>
    public GraphNode WithInput(string name, VariantType type = VariantType.None)
    {
        var pin = GetOrAddInput(name);
        pin.GetPin()!.SetName(name);
        pin.GetPin()!.SetType(type);
        return this;
    }

    /// <summary>Add input pin with value.</summary>
    public GraphNode WithInput(string name, in Variant value)
    {
        var pin = GetOrAddInput(name);
        pin.GetPin()!.SetName(name);
        pin.GetPin()!.SetType(value.Type);
        pin.GetPin()!.SetValue(value);
        return this;
    }

    /// <summary>Add input pin connected to the output pin.</summary>
    public GraphNode WithInput(string name, GraphPinRef<GraphOutPin> pin, VariantType type = VariantType.None)
    {
        var inputPin = GetOrAddInput(name);
        inputPin.GetPin()!.SetName(name);
        inputPin.GetPin()!.SetType(type);
        if (pin)
            inputPin.GetPin()!.ConnectTo(pin);
        return this;
    }

    /// <summary>Get or add output pin.</summary>
    public GraphPinRef<GraphOutPin> GetOrAddOutput(string name)
    {
        for (var index = 0; index < _outputPins.Count; ++index)
        {
            if (_outputPins[index].GetName() == name)
                return new GraphPinRef<GraphOutPin>(this, _outputPins[index]);
        }

        var pin = new GraphOutPin();
        pin.SetName(name);
        _outputPins.Add(pin);
        return new GraphPinRef<GraphOutPin>(this, pin);
    }

    /// <summary>Add output pin.</summary>
    public GraphNode WithOutput(string name, VariantType type = VariantType.None)
    {
        var pin = GetOrAddOutput(name);
        pin.GetPin()!.SetName(name);
        pin.GetPin()!.SetType(type);
        return this;
    }

    /// <summary>Get output pin by name.</summary>
    public GraphPinRef<GraphOutPin> GetOutput(string name)
    {
        for (var index = 0; index < _outputPins.Count; ++index)
        {
            if (_outputPins[index].GetName() == name)
                return new GraphPinRef<GraphOutPin>(this, _outputPins[index]);
        }
        return new GraphPinRef<GraphOutPin>(this, null);
    }

    /// <summary>Get output pin by index.</summary>
    public GraphPinRef<GraphOutPin> GetOutput(int index)
    {
        if (index < 0 || index >= _outputPins.Count)
            return default;
        return new GraphPinRef<GraphOutPin>(this, _outputPins[index]);
    }

    /// <summary>Get number of output pins.</summary>
    public int GetNumOutputs() => _outputPins.Count;

    /// <summary>Get or add enter pin.</summary>
    public GraphPinRef<GraphEnterPin> GetOrAddEnter(string name)
    {
        for (var index = 0; index < _enterPins.Count; ++index)
        {
            if (_enterPins[index].GetName() == name)
                return new GraphPinRef<GraphEnterPin>(this, _enterPins[index]);
        }

        var pin = new GraphEnterPin();
        pin.SetName(name);
        _enterPins.Add(pin);
        return new GraphPinRef<GraphEnterPin>(this, pin);
    }

    /// <summary>Get enter pin by name.</summary>
    public GraphPinRef<GraphEnterPin> GetEnter(string name)
    {
        for (var index = 0; index < _enterPins.Count; ++index)
        {
            if (_enterPins[index].GetName() == name)
                return new GraphPinRef<GraphEnterPin>(this, _enterPins[index]);
        }
        return new GraphPinRef<GraphEnterPin>(this, null);
    }

    /// <summary>Get enter pin by index.</summary>
    public GraphPinRef<GraphEnterPin> GetEnter(int index)
    {
        if (index < 0 || index >= _enterPins.Count)
            return default;
        return new GraphPinRef<GraphEnterPin>(this, _enterPins[index]);
    }

    /// <summary>Get number of enter pins.</summary>
    public int GetNumEnters() => _enterPins.Count;

    /// <summary>Get exit pin by name.</summary>
    public GraphPinRef<GraphExitPin> GetExit(string name)
    {
        for (var index = 0; index < _exitPins.Count; ++index)
        {
            if (_exitPins[index].GetName() == name)
                return new GraphPinRef<GraphExitPin>(this, _exitPins[index]);
        }
        return new GraphPinRef<GraphExitPin>(this, null);
    }

    /// <summary>Get exit pin by index.</summary>
    public GraphPinRef<GraphExitPin> GetExit(int index)
    {
        if (index < 0 || index >= _exitPins.Count)
            return default;
        return new GraphPinRef<GraphExitPin>(this, _exitPins[index]);
    }

    /// <summary>Get or add exit pin.</summary>
    public GraphPinRef<GraphExitPin> GetOrAddExit(string name)
    {
        for (var index = 0; index < _exitPins.Count; ++index)
        {
            if (_exitPins[index].GetName() == name)
                return new GraphPinRef<GraphExitPin>(this, _exitPins[index]);
        }

        var pin = new GraphExitPin();
        pin.SetName(name);
        _exitPins.Add(pin);
        return new GraphPinRef<GraphExitPin>(this, pin);
    }

    /// <summary>Get number of exit pins.</summary>
    public int GetNumExits() => _exitPins.Count;

    /// <summary>Set name of the graph node.</summary>
    public void SetName(string name)
    {
        if (_name != name)
        {
            _name = name;
            _nameHash = name;
        }
    }

    /// <summary>Serialize content from/to archive.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeValue(archive, "name", ref _name);
        if (archive.IsInput)
            _nameHash = _name;

        SerializeObjectVector(archive, "properties", "property", _properties);
        SerializeObjectVector(archive, "enter", "pin", _enterPins);
        SerializeObjectVector(archive, "in", "pin", _inputPins);
        SerializeObjectVector(archive, "exit", "pin", _exitPins);
        SerializeObjectVector(archive, "out", "pin", _outputPins);
    }

    /// <summary>Set graph and id. Called by Graph.</summary>
    internal void SetGraph(Graph? graph, uint id)
    {
        _graph = graph;
        _id = id;
    }

    private static void SerializeObjectVector<T>(Archive archive, string name, string itemName, List<T> values)
        where T : IArchiveSerializable, IDisposable, new()
    {
        if (archive.IsInput && !archive.HasElementOrBlock(name))
        {
            for (var index = values.Count - 1; index >= 0; --index)
                values[index].Dispose();
            values.Clear();
            return;
        }
        if (!archive.IsInput && values.Count == 0)
            return;

        var size = archive.IsInput ? 0 : values.Count;
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            for (var index = values.Count - 1; index >= 0; --index)
                values[index].Dispose();
            values.Clear();
            size = block.SizeHint;
            for (var index = 0; index < size; ++index)
                values.Add(new T());

            for (var index = 0; index < size; ++index)
            {
                using var itemBlock = archive.OpenUnorderedBlock(itemName);
                values[index].SerializeInBlock(archive);
            }
        }
        else
        {
            foreach (var value in values)
            {
                using var itemBlock = archive.OpenUnorderedBlock(itemName);
                value.SerializeInBlock(archive);
            }
        }
    }

    /// <summary>Name.</summary>
    private string _name = string.Empty;

    /// <summary>Name hash.</summary>
    private StringHash _nameHash;

    /// <summary>Graph.</summary>
    private Graph? _graph;

    /// <summary>Unique ID within the scene.</summary>
    private uint _id;

    /// <summary>User defined properties of the node.</summary>
    private readonly List<GraphNodeProperty> _properties = [];

    /// <summary>Enter pins. Define execution flow.</summary>
    private readonly List<GraphEnterPin> _enterPins = [];

    /// <summary>Exit pins. Define next nodes in execution flow.</summary>
    private readonly List<GraphExitPin> _exitPins = [];

    /// <summary>Input pins. Define source pin for the data flow.</summary>
    private readonly List<GraphInPin> _inputPins = [];

    /// <summary>Output pins. Define data flow.</summary>
    private readonly List<GraphOutPin> _outputPins = [];
}
