//
// Copyright (c) 2021-2022 the rbfx project.
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

namespace RedBolt.Particles.ParticleGraphNodes;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.Resource;

public delegate void NodePatternUpdate(ref UpdateContext context, Span<ParticleGraphPinRef> pinRefs);

public class NodePattern
{
    public const int ExpectedNumberOfPins = 4;

    public NodePattern(NodePatternUpdate update)
    {
        UpdateFunction = update;
    }

    public NodePattern WithPin(ParticleGraphPin pin)
    {
        Pins.Add(pin);
        return this;
    }

    public bool Match(IReadOnlyList<ParticleGraphPin> pins)
    {
        if (pins.Count != Pins.Count)
            return false;
        for (var i = 0; i < pins.Count; ++i)
        {
            if (pins[i].NameHash != Pins[i].NameHash)
                return false;
            if (pins[i].ValueType != Pins[i].RequestedType)
                return false;
        }
        return true;
    }

    public VariantType EvaluateOutputPinType(IReadOnlyList<ParticleGraphPin> pins, ParticleGraphPin outputPin)
    {
        if (pins.Count != Pins.Count)
            return VariantType.None;
        var result = VariantType.None;
        for (var i = 0; i < pins.Count; ++i)
        {
            if (pins[i].NameHash != Pins[i].NameHash)
                return VariantType.None;
            if (pins[i].IsInput)
            {
                if (pins[i].ValueType != Pins[i].RequestedType)
                    return VariantType.None;
            }
            else if (outputPin.NameHash == Pins[i].NameHash)
                result = Pins[i].RequestedType;
        }
        return result;
    }

    public NodePatternUpdate UpdateFunction;
    public readonly List<ParticleGraphPin> Pins = [];
}

/// <summary>
/// Graph node that adapts to input pins dynamically.
/// </summary>
public class PatternMatchingNode : ParticleGraphNode
{
    public class Instance : ParticleGraphNodeInstance
    {
        /// <summary>
        /// Construct.
        /// </summary>
        public Instance(PatternMatchingNode node, NodePattern pattern)
        {
            Node = node;
            Pattern = pattern;
        }

        /// <summary>
        /// Update particles.
        /// </summary>
        public override void Update(ref UpdateContext context)
        {
            Node.Update(ref context, Pattern);
        }

        public readonly PatternMatchingNode Node;
        public readonly NodePattern Pattern;
    }

    /// <summary>
    /// Construct.
    /// </summary>
    public PatternMatchingNode(IReadOnlyList<NodePattern> patterns)
    {
        _patterns = patterns;
    }

    /// <summary>
    /// Get number of pins.
    /// </summary>
    public override int NumPins => _pins.Count;

    /// <summary>
    /// Get pin by index.
    /// </summary>
    public override ParticleGraphPin GetPin(int index) => _pins[index];

    /// <summary>
    /// Evaluate size required to place new node instance.
    /// </summary>
    public override int EvaluateInstanceSize() => IntPtr.Size;

    /// <summary>
    /// Create new node instance.
    /// </summary>
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    {
        foreach (var pattern in _patterns)
        {
            if (pattern.Match(_pins))
                return new Instance(this, pattern);
        }
        Log.GetLogger().LogError("No matching pattern found for graph node {Type}", GetTypeName());
        return new NopInstance();
    }

    /// <summary>
    /// Load input pin.
    /// </summary>
    protected override ParticleGraphPin? LoadInputPin(ParticleGraphReader reader, GraphInPin inputPin)
    {
        var index = GetPinIndex(inputPin.GetName());
        if (index != InvalidPin)
        {
            if (inputPin.GetType() != VariantType.None)
                SetPinValueType(index, inputPin.GetType());
            return GetPin(index);
        }
        var pin = new ParticleGraphPin(ParticleGraphPinFlag.Input, inputPin.GetName(), inputPin.GetType());
        _pins.Add(pin);
        return pin;
    }

    /// <summary>
    /// Load output pin.
    /// </summary>
    protected override ParticleGraphPin? LoadOutputPin(ParticleGraphReader reader, GraphOutPin outputPin)
    {
        var index = GetPinIndex(outputPin.GetName());
        if (index != InvalidPin)
        {
            if (outputPin.GetType() != VariantType.None)
                SetPinValueType(index, outputPin.GetType());
            return GetPin(index);
        }
        var pin = new ParticleGraphPin(ParticleGraphPinFlag.Output, outputPin.GetName(), outputPin.GetType());
        _pins.Add(pin);
        return pin;
    }

    /// <summary>
    /// Evaluate runtime output pin type.
    /// </summary>
    protected override VariantType EvaluateOutputPinTypeCore(ParticleGraphPin pin)
    {
        foreach (var pattern in _patterns)
        {
            var type = pattern.EvaluateOutputPinType(_pins, pin);
            if (type != VariantType.None)
                return type;
        }
        return VariantType.None;
    }

    /// <summary>
    /// Update particles.
    /// </summary>
    protected void Update(ref UpdateContext context, NodePattern pattern)
    {
        Span<ParticleGraphPinRef> pinRefs = stackalloc ParticleGraphPinRef[_pins.Count];
        var allScalar = true;
        for (var index = 0; index < _pins.Count; ++index)
        {
            var pin = _pins[index];
            pinRefs[index] = pin.MemoryReference;
            allScalar &= pin.ContainerType == ParticleGraphContainerType.Scalar;
        }

        // Index optimization
        if (allScalar && context.Indices.Length > 1)
        {
            var contextCopy = context;
            contextCopy.Indices = contextCopy.Indices[..1];
            pattern.UpdateFunction(ref contextCopy, pinRefs);
            return;
        }

        pattern.UpdateFunction(ref context, pinRefs);
    }

    private sealed class NopInstance : ParticleGraphNodeInstance
    {
        public override void Update(ref UpdateContext context) { }
    }

    protected readonly IReadOnlyList<NodePattern> _patterns;
    protected readonly List<ParticleGraphPin> _pins = [];
}
