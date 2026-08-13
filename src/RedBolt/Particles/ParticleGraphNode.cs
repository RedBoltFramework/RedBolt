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

using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.Resource;
using RedBolt.Scene;

[Reflected(Factory = false)]
public abstract partial class ParticleGraphNode : Serializable
{
    public const int InvalidPin = -1;

    /// <summary>
    /// Construct.
    /// </summary>
    public ParticleGraphNode()
    {
        _graph = null;
        _index = 0;
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>
    /// Get graph.
    /// </summary>
    public ParticleGraph? Graph => _graph;

    /// <summary>
    /// Get number of pins.
    /// </summary>
    public abstract int NumPins { get; }

    /// <summary>
    /// Get pin by index.
    /// </summary>
    public abstract ParticleGraphPin GetPin(int index);

    /// <summary>
    /// Set pin source node and pin.
    /// </summary>
    public bool SetPinSource(int pinIndex, int nodeIndex, int nodePinIndex = 0)
    {
        if (pinIndex < 0 || pinIndex >= NumPins)
        {
            Log.GetLogger().LogError("Pin index out of bounds");
            return false;
        }
        var pin = GetPin(pinIndex);
        return pin.SetSource(nodeIndex, nodePinIndex);
    }

    /// <summary>
    /// Get pin by name.
    /// </summary>
    public ParticleGraphPin? GetPin(string name)
    {
        var index = GetPinIndex(name);
        if (index == InvalidPin)
            return null;
        return GetPin(index);
    }

    /// <summary>
    /// Get pin index by name.
    /// </summary>
    public int GetPinIndex(string name)
    {
        for (var i = 0; i < NumPins; ++i)
        {
            var pin = GetPin(i);
            if (pin.Name == name)
                return i;
        }
        return InvalidPin;
    }

    /// <summary>
    /// Get pin name.
    /// </summary>
    public string GetPinName(int pinIndex) => GetPin(pinIndex).Name;

    /// <summary>
    /// Get pin type.
    /// </summary>
    public VariantType GetPinValueType(int pinIndex) => GetPin(pinIndex).RequestedType;

    /// <summary>
    /// Evaluate size required to place new node instance.
    /// </summary>
    public abstract int EvaluateInstanceSize();

    /// <summary>
    /// Create new node instance.
    /// </summary>
    public abstract ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer);

    /// <summary>
    /// Load node.
    /// </summary>
    public virtual bool Load(ParticleGraphReader reader, GraphNode node)
    {
        if (!LoadProperties(reader, node))
            return false;
        if (!LoadPins(reader, node))
            return false;

        return true;
    }

    /// <summary>
    /// Save node.
    /// </summary>
    public virtual bool Save(ParticleGraphWriter writer, GraphNode node)
    {
        node.SetName(GetTypeName());

        if (!SaveProperties(writer, node))
            return false;
        if (!SavePins(writer, node))
            return false;

        return true;
    }

    /// <summary>
    /// Load input pin.
    /// </summary>
    protected virtual ParticleGraphPin? LoadInputPin(ParticleGraphReader reader, GraphInPin inputPin)
    {
        var pin = GetPin(inputPin.GetName());
        if (pin is not null)
        {
            if (inputPin.GetType() != VariantType.None)
                pin.SetValueTypeFromParticleGraphNodeFriend(inputPin.GetType());
        }
        else
        {
            Log.GetLogger().LogError("Unknown input pin {Type}.{Pin}", GetTypeName(), inputPin.GetName());
            return null;
        }
        return pin;
    }

    /// <summary>
    /// Load output pin.
    /// </summary>
    protected virtual ParticleGraphPin? LoadOutputPin(ParticleGraphReader reader, GraphOutPin outputPin)
    {
        var pin = GetPin(outputPin.GetName());
        if (pin is not null)
        {
            var type = outputPin.GetType();
            if (type != VariantType.None)
                pin.SetValueTypeFromParticleGraphNodeFriend(outputPin.GetType());
        }
        else
        {
            Log.GetLogger().LogError("Unknown output pin {Type}.{Pin}", GetTypeName(), outputPin.GetName());
            return null;
        }
        return pin;
    }

    /// <summary>
    /// Load pins.
    /// </summary>
    protected virtual bool LoadPins(ParticleGraphReader reader, GraphNode node)
    {
        // First pass: validate and connect pins input pins.
        for (var index = 0; index < node.GetNumInputs(); ++index)
        {
            var inputPinRef = node.GetInput(index);
            var inputPin = inputPinRef.GetPin()!;
            var pin = LoadInputPin(reader, inputPin);

            if (pin is null)
                return false;

            System.Diagnostics.Debug.Assert(pin.Name == inputPin.GetName());

            if (inputPin.IsConnected())
            {
                var source = inputPinRef.GetConnectedPin<GraphOutPin>();
                if (!source)
                {
                    Log.GetLogger().LogError("Can't resolve connected pin for {Type}.{Pin}", GetTypeName(), inputPin.GetName());
                    return false;
                }
                var localIndex = reader.ReadNode(source.GetNode()!.GetID());
                var pinIndex = reader.GetInputPinIndex(localIndex, source.GetPin()!.GetName());
                pin.SetSource(localIndex, pinIndex);
            }
        }
        // Second pass: create constant nodes.
        for (var index = 0; index < node.GetNumInputs(); ++index)
        {
            var inputPinRef = node.GetInput(index);
            var inputPin = inputPinRef.GetPin()!;

            var pin = GetPin(inputPin.GetName())!;
            if (!inputPin.IsConnected())
            {
                ref readonly var constValue = ref inputPin.GetValue();
                if (constValue.Type == VariantType.None)
                {
                    Log.GetLogger().LogError("Pin {Type}(#{Index}).{Pin} is not connected and doesn't have value.",
                        GetTypeName(), _index, inputPin.GetName());
                    return false;
                }
                var connectedNode = reader.GetOrAddConstant(constValue);
                pin.SetSource(connectedNode, 0);
            }
        }

        for (var index = 0; index < node.GetNumOutputs(); ++index)
        {
            var outputPinRef = node.GetOutput(index);
            var outputPin = outputPinRef.GetPin()!;
            var pin = LoadOutputPin(reader, outputPin);

            if (pin is null)
                return false;

            System.Diagnostics.Debug.Assert(pin.Name == outputPin.GetName());
        }
        return true;
    }

    /// <summary>
    /// Load property value.
    /// </summary>
    protected virtual bool LoadProperty(GraphNodeProperty prop)
    {
        var reflection = GetReflection();
        if (reflection is not null)
        {
            var attribute = reflection.GetAttribute(prop.GetNameHash());
            if (attribute is not null)
            {
                OnSetAttribute(attribute, prop.Value);
                return true;
            }
        }
        Log.GetLogger().LogError("Unknown property {Type}.{Property}.", GetTypeName(), prop.GetName());
        return false;
    }

    /// <summary>
    /// Load properties.
    /// </summary>
    protected virtual bool LoadProperties(ParticleGraphReader reader, GraphNode node)
    {
        foreach (var property in node.GetProperties())
        {
            if (!LoadProperty(property))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Save pins.
    /// </summary>
    protected virtual bool SavePins(ParticleGraphWriter writer, GraphNode node)
    {
        for (var i = 0; i < NumPins; ++i)
        {
            var pin = GetPin(i);
            if (pin.IsInput)
            {
                var inputPin = node.GetOrAddInput(pin.Name);
                inputPin.GetPin()!.SetType(pin.RequestedType);

                if (pin.Connected)
                {
                    var connectedNode = pin.ConnectedNodeIndex;
                    inputPin.GetPin()!.ConnectTo(writer.GetSourcePin(connectedNode, pin.ConnectedPinIndex));
                }
            }
            else
            {
                var outputPin = node.GetOrAddOutput(pin.Name);
                outputPin.GetPin()!.SetType(pin.RequestedType);
            }
        }
        return true;
    }

    /// <summary>
    /// Save node.
    /// </summary>
    protected virtual bool SaveProperties(ParticleGraphWriter writer, GraphNode node)
    {
        var reflection = GetReflection();
        if (reflection is not null)
        {
            foreach (var attribute in reflection.Attributes)
                OnGetAttribute(attribute, ref node.GetOrAddProperty(attribute.Name));
        }
        return true;
    }

    /// <summary>
    /// Evaluate runtime output pin type.
    /// </summary>
    protected virtual VariantType EvaluateOutputPinTypeCore(ParticleGraphPin pin) => VariantType.None;

    internal VariantType EvaluateOutputPinTypeFromParticleGraphAttributeBuilderFriend(ParticleGraphPin pin)
        => EvaluateOutputPinTypeCore(pin);

    /// <summary>
    /// Set graph reference. Called by ParticleGraph.
    /// </summary>
    protected void SetGraph(ParticleGraph graph, int index)
    {
        _graph = graph;
        _index = index;
    }

    internal void SetGraphFromParticleGraphFriend(ParticleGraph graph, int index) => SetGraph(graph, index);

    /// <summary>
    /// Set pin name.
    /// This method is protected so it can only be accessable to nodes that allow pin renaming.
    /// </summary>
    protected bool SetPinName(int pinIndex, string name)
    {
        if (pinIndex < 0 || pinIndex >= NumPins)
        {
            Log.GetLogger().LogError("Pin index out of bounds.");
            return false;
        }
        var pin = GetPin(pinIndex);
        return pin.SetNameFromParticleGraphNodeFriend(name);
    }

    /// <summary>
    /// Set pin type.
    /// This method is protected so it can only be accessable to nodes that allow pin renaming.
    /// </summary>
    protected bool SetPinValueType(int pinIndex, VariantType type)
    {
        if (pinIndex < 0 || pinIndex >= NumPins)
        {
            Log.GetLogger().LogError("Pin index out of bounds.");
            return false;
        }
        var pin = GetPin(pinIndex);
        return pin.SetValueTypeFromParticleGraphNodeFriend(type);
    }

    /// <summary>
    /// Parent graph.
    /// </summary>
    protected ParticleGraph? _graph;

    /// <summary>
    /// This node index in the graph.
    /// </summary>
    protected int _index;
}
