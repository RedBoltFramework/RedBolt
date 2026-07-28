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

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Logging;

[Flags]
public enum ParticleGraphPinFlag
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0x0,
    /// <summary>
    /// Output pin is the same as unset Input pin flag.
    /// </summary>
    Output = 0x0,
    /// <summary>
    /// Input pin.
    /// </summary>
    Input = 0x1,
    /// <summary>
    /// Pin name defined in runtime.
    /// </summary>
    MutableName = 0x2,
    /// <summary>
    /// Pin value type defined in runtime.
    /// </summary>
    MutableType = 0x4,
}

/// <summary>
/// Reference to a pin buffer in a particle graph.
/// </summary>
public struct ParticleGraphPinRef
{
    public ParticleGraphPinRef()
    {
        Type = ParticleGraphContainerType.Auto;
        Index = 0;
    }

    public ParticleGraphPinRef(ParticleGraphContainerType type, int index)
    {
        Type = type;
        Index = index;
    }

    public ParticleGraphContainerType Type;
    public int Index;
}

/// <summary>
/// Pin of a node in particle graph.
/// </summary>
public class ParticleGraphPin : ICloneable
{
    /// <summary>
    /// Construct default pin.
    /// </summary>
    public ParticleGraphPin()
    {
    }

    /// <summary>
    /// Construct pin.
    /// </summary>
    public ParticleGraphPin(ParticleGraphPinFlag flags, string name, VariantType type = VariantType.None,
        ParticleGraphContainerType container = ParticleGraphContainerType.Auto)
    {
        _containerType = container;
        _name = name;
        _nameHash = new StringHash(name);
        _sourceNode = unchecked((int)uint.MaxValue);
        _flags = flags;
        _requestedValueType = type;
    }

    /// <summary>
    /// Construct pin.
    /// </summary>
    public ParticleGraphPin(ParticleGraphPinFlag flags, string name, ParticleGraphContainerType container)
    {
        _containerType = container;
        _name = name;
        _nameHash = new StringHash(name);
        _sourceNode = unchecked((int)uint.MaxValue);
        _flags = flags;
        _requestedValueType = VariantType.None;
    }

    protected ParticleGraphPin(ParticleGraphPin source)
    {
        _containerType = source._containerType;
        _valueType = source._valueType;
        _name = source._name;
        _nameHash = source._nameHash;
        _sourceNode = source._sourceNode;
        _sourcePin = source._sourcePin;
        _flags = source._flags;
        _requestedValueType = source._requestedValueType;
        _attributeIndex = source._attributeIndex;
        _memory = source._memory;
    }

    public ParticleGraphPin Clone() => new(this);

    object ICloneable.Clone() => Clone();

    /// <summary>
    /// Get input pin flag.
    /// </summary>
    public bool IsInput => (_flags & ParticleGraphPinFlag.Input) != 0;

    /// <summary>
    /// Get pin flags.
    /// </summary>
    public ParticleGraphPinFlag Flags => _flags;

    /// <summary>
    /// Name of the pin for visual editor.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Name hash of the pin.
    /// </summary>
    public StringHash NameHash => _nameHash;

    /// <summary>
    /// Requested value type of the pin. VAR_NONE for autodetected value type.
    /// </summary>
    public VariantType RequestedType => _requestedValueType;

    /// <summary>
    /// Value type of the pin evaluated at the runtime.
    /// </summary>
    public VariantType ValueType => _valueType;

    /// <summary>
    /// Get attribute index for sparse span.
    /// </summary>
    public int AttributeIndex => _attributeIndex;

    /// <summary>
    /// Get reference to memory descriptor for the pin.
    /// </summary>
    public ParticleGraphPinRef MemoryReference => _memory;

    public ParticleGraphContainerType ContainerType => _memory.Type;

    /// <summary>
    /// Get a copy of the pin setup but with a different value type.
    /// </summary>
    public ParticleGraphPin WithType(VariantType type)
    {
        return new ParticleGraphPin(_flags, _name, type, _containerType);
    }

    /// <summary>
    /// Set source node and pin indices.
    /// </summary>
    public bool SetSource(int nodeIndex, int pinIndex = 0)
    {
        if (!IsInput)
        {
            Log.GetLogger().LogError("Can't set source to output pin {Name}", Name);
            return false;
        }

        _sourceNode = nodeIndex;
        _sourcePin = pinIndex;
        return true;
    }

    /// <summary>
    /// Get true if connected to node.
    /// </summary>
    public bool Connected => _sourceNode != unchecked((int)uint.MaxValue);

    /// <summary>
    /// Get connected node index.
    /// </summary>
    public int ConnectedNodeIndex => _sourceNode;

    /// <summary>
    /// Get connected pin index.
    /// </summary>
    public int ConnectedPinIndex => _sourcePin;

    /// <summary>
    /// Set pin name and hash.
    /// </summary>
    protected bool SetName(string name)
    {
        if (_name == name)
            return true;

        if ((_flags & ParticleGraphPinFlag.MutableName) == 0)
        {
            Log.GetLogger().LogError("Can't change name of {Name} pin.", Name);
            return false;
        }

        _name = name;
        _nameHash = new StringHash(_name);
        return true;
    }

    /// <summary>
    /// Set pin value type.
    /// </summary>
    protected bool SetValueType(VariantType valueType)
    {
        if (_requestedValueType == valueType)
            return true;
        if ((_flags & ParticleGraphPinFlag.MutableType) == 0)
        {
            Log.GetLogger().LogError("Can't change type of {Name} pin from {OldType} to {NewType}.", Name,
                Variant.GetTypeNameList()[(int)_requestedValueType], Variant.GetTypeNameList()[(int)valueType]);
            return false;
        }

        _requestedValueType = valueType;
        return true;
    }

    /// <summary>
    /// Get input pin flag.
    /// </summary>
    protected void SetIsInput(bool isInput)
    {
        if (isInput)
            _flags |= ParticleGraphPinFlag.Input;
        else
            _flags &= ~ParticleGraphPinFlag.Input;
    }

    /// <summary>
    /// Container type: span, sparse or scalar.
    /// </summary>
    internal ParticleGraphContainerType _containerType = ParticleGraphContainerType.Auto;

    /// <summary>
    /// Value type at runtime.
    /// </summary>
    internal VariantType _valueType = VariantType.None;

    /// <summary>
    /// Name of the pin for visual editor.
    /// </summary>
    internal string _name = string.Empty;

    /// <summary>
    /// Pin name hash.
    /// </summary>
    private StringHash _nameHash = new();

    /// <summary>
    /// Source node.
    /// </summary>
    internal int _sourceNode = unchecked((int)uint.MaxValue);

    /// <summary>
    /// Source node pin index.
    /// </summary>
    internal int _sourcePin;

    /// <summary>
    /// Is input pin.
    /// </summary>
    private ParticleGraphPinFlag _flags = ParticleGraphPinFlag.Input;

    /// <summary>
    /// Value type (float, vector3, etc).
    /// </summary>
    internal VariantType _requestedValueType = VariantType.None;

    /// <summary>
    /// Index of attribute. Only valid for sparse pins.
    /// </summary>
    internal int _attributeIndex;

    /// <summary>
    /// Reference to a memory block that corresponds to the pin value.
    /// </summary>
    internal ParticleGraphPinRef _memory = new();
}

public class ParticleGraphTypedPin<T> : ParticleGraphPin
{
    public ParticleGraphTypedPin(ParticleGraphPinFlag flags, string name)
        : base(flags, name, VariantTypeUtility.GetVariantType<T>())
    {
    }

    public ParticleGraphTypedPin(string name)
        : base(ParticleGraphPinFlag.Input, name, VariantTypeUtility.GetVariantType<T>())
    {
    }

    private ParticleGraphTypedPin(ParticleGraphTypedPin<T> source)
        : base(source)
    {
    }

    public new ParticleGraphTypedPin<T> Clone() => new(this);
}
