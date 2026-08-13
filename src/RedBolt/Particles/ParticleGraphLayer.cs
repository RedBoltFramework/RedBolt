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

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Scene;

[Reflected]
public partial class ParticleGraphLayer : Serializable
{
    private const float DefaultDuration = 1.0f;
    private const float DefaultTimeScale = 1.0f;
    private const int DefaultCapacity = 16;

    /// <summary>
    /// Layout of attribute buffer
    /// </summary>
    public struct AttributeBufferLayout
    {
        /// <summary>
        /// Required attribute buffer size.
        /// </summary>
        public int AttributeBufferSize;

        /// <summary>
        /// Emit node pointers.
        /// </summary>
        public ParticleGraphSpan EmitNodePointers;

        /// <summary>
        /// Init node pointers.
        /// </summary>
        public ParticleGraphSpan InitNodePointers;

        /// <summary>
        /// Update node pointers.
        /// </summary>
        public ParticleGraphSpan UpdateNodePointers;

        /// <summary>
        /// Node instances.
        /// </summary>
        public ParticleGraphSpan NodeInstances;

        /// <summary>
        /// Indices.
        /// </summary>
        public ParticleGraphSpan Indices;

        /// <summary>
        /// Indices.
        /// </summary>
        public ParticleGraphSpan ScalarIndices;

        /// <summary>
        /// Indices.
        /// </summary>
        public ParticleGraphSpan NaturalIndices;

        /// <summary>
        /// Indices to destroy.
        /// </summary>
        public ParticleGraphSpan DestructionQueue;

        /// <summary>
        /// Particle attribute values.
        /// </summary>
        public ParticleGraphSpan Values;

        /// <summary>
        /// Evaluate attribute buffer size and layout.
        /// </summary>
        public void EvaluateLayout(ParticleGraphLayer layer)
        {
            var emitGraphNodes = layer._emit.NumNodes;
            var initGraphNodes = layer._init.NumNodes;
            var updateGraphNodes = layer._update.NumNodes;
            AttributeBufferSize = 0;
            EmitNodePointers = Append<ParticleGraphNodeInstance>(ref this, emitGraphNodes);
            InitNodePointers = Append<ParticleGraphNodeInstance>(ref this, initGraphNodes);
            UpdateNodePointers = Append<ParticleGraphNodeInstance>(ref this, updateGraphNodes);
            var instanceSize = 0;
            for (var i = 0; i < emitGraphNodes; ++i)
            {
                var node = layer._emit.GetNode(i)!;
                instanceSize += node.EvaluateInstanceSize();
            }
            for (var i = 0; i < initGraphNodes; ++i)
            {
                var node = layer._init.GetNode(i)!;
                instanceSize += node.EvaluateInstanceSize();
            }
            for (var i = 0; i < updateGraphNodes; ++i)
            {
                var node = layer._update.GetNode(i)!;
                instanceSize += node.EvaluateInstanceSize();
            }
            NodeInstances = Append(ref this, instanceSize);
            Indices = Append<int>(ref this, layer._capacity);
            ScalarIndices = Append<int>(ref this, layer._capacity);
            NaturalIndices = Append<int>(ref this, layer._capacity);
            DestructionQueue = Append<int>(ref this, layer._capacity);
            Values = Append(ref this, 0);
        }
    }

    /// <summary>
    /// Construct.
    /// </summary>
    public ParticleGraphLayer()
    {
        _capacity = DefaultCapacity;
        _timeScale = 1.0f;
        _duration = DefaultDuration;
        _loop = false;
        _emit = new ParticleGraph();
        _init = new ParticleGraph();
        _update = new ParticleGraph();
        Invalidate();
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        _update.Dispose();
        _init.Dispose();
        _emit.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Get or set maximum number of particles the layer can hold.
    /// </summary>
    private static object? GetCapacityDefault() => DefaultCapacity;
    private static object? GetTimeScaleDefault() => DefaultTimeScale;
    private static object? GetDurationDefault() => DefaultDuration;

    [Serialized("Capacity", DefaultSource = nameof(GetCapacityDefault))]
    public int Capacity
    {
        get => _capacity;
        set
        {
            _capacity = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Get or set time step scale.
    /// </summary>
    [Serialized("TimeScale", DefaultSource = nameof(GetTimeScaleDefault))]
    public float TimeScale
    {
        get => _timeScale;
        set
        {
            _timeScale = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Get or set effect duration in seconds.
    /// </summary>
    [Serialized("Duration", DefaultSource = nameof(GetDurationDefault))]
    public float Duration
    {
        get => _duration;
        set => _duration = global::System.Math.Max(1e-6f, value);
    }

    /// <summary>
    /// Get or set effect loop flag.
    /// </summary>
    [Serialized("Loop")]
    public bool Loop
    {
        get => _loop;
        set
        {
            _loop = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Get emit graph.
    /// </summary>
    public ParticleGraph EmitGraph => _emit;

    /// <summary>
    /// Get initialization graph.
    /// </summary>
    public ParticleGraph InitGraph => _init;

    /// <summary>
    /// Get update graph.
    /// </summary>
    public ParticleGraph UpdateGraph => _update;

    /// <summary>
    /// Invalidate graph layer state.
    /// Call this method when something is changed in the layer graphs and it requires new preparation.
    /// </summary>
    public void Invalidate()
    {
        _committed = null;
        _attributeBufferLayout = default;
        _tempMemory.Reset(0);
        _attributes.Reset(0, 0);
    }

    /// <summary>
    /// Prepare layer for execution. Returns false if graph is invalid. See output logs for errors.
    /// </summary>
    public bool Commit()
    {
        if (_committed.HasValue)
            return _committed.Value;
        _committed = false;
        // Evaluate attribute buffer layout except attributes size.
        _attributeBufferLayout.EvaluateLayout(this);

        _attributes.Reset(_attributeBufferLayout.AttributeBufferSize, _capacity);
        _tempMemory.Reset(_capacity);
        // Allocate memory for each pin.
        {
            var builder = new ParticleGraphAttributeBuilder(_attributes, _attributeBufferLayout, _capacity, _tempMemory);
            if (!builder.Build(_init))
                return false;
            _attributeBufferLayout = builder.Layout;
        }
        {
            var builder = new ParticleGraphAttributeBuilder(_attributes, _attributeBufferLayout, _capacity, _tempMemory);
            if (!builder.Build(_emit))
                return false;
            _attributeBufferLayout = builder.Layout;
        }
        {
            var builder = new ParticleGraphAttributeBuilder(_attributes, _attributeBufferLayout, _capacity, _tempMemory);
            if (!builder.Build(_update))
                return false;
            _attributeBufferLayout = builder.Layout;
        }
        _attributeBufferLayout.AttributeBufferSize = _attributes.RequiredMemory;
        ref var values = ref _attributeBufferLayout.Values;
        values = new ParticleGraphSpan(values.Offset, _attributeBufferLayout.AttributeBufferSize - values.Offset);
        _committed = true;
        return _committed.Value;
    }

    /// <summary>
    /// Return attribute buffer layout.
    /// </summary>
    public AttributeBufferLayout GetAttributeBufferLayout() => _attributeBufferLayout;

    /// <summary>
    /// Return attributes memory layout.
    /// </summary>
    public ParticleGraphAttributeLayout AttributeLayout => _attributes;

    /// <summary>
    /// Return intermediate memory layout.
    /// </summary>
    public ParticleGraphBufferLayout IntermediateValues => _tempMemory;

    /// <summary>
    /// Return size of temp buffer in bytes.
    /// </summary>
    public int TempBufferSize => _tempMemory.RequiredMemory;

    /// <summary>
    /// Serialize from/to archive.
    /// </summary>
    public override void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(archive, "capacity", ref _capacity, DefaultCapacity,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "duration", ref _duration, DefaultDuration,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "timeScale", ref _timeScale, DefaultTimeScale,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "loop", ref _loop, false,
            ArchiveSerialization.SerializeValue);

        SerializeOptionalGraph(archive, "emit", _emit);
        SerializeOptionalGraph(archive, "init", _init);
        SerializeOptionalGraph(archive, "update", _update);

        if (archive.IsInput)
            Commit();
    }

    private static ParticleGraphSpan Append(ref AttributeBufferLayout layout, int bytes)
    {
        var span = new ParticleGraphSpan(layout.AttributeBufferSize, bytes);
        layout.AttributeBufferSize += bytes;
        return span;
    }

    private static ParticleGraphSpan Append<T>(ref AttributeBufferLayout layout, int count)
    {
        var elementSize = typeof(T) == typeof(ParticleGraphNodeInstance)
            ? IntPtr.Size
            : System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return Append(ref layout, elementSize * count);
    }

    private static void SerializeOptionalGraph(Archive archive, string name, ParticleGraph value)
    {
        if (archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            if (archive.IsInput && !archive.HasElementOrBlock(name))
                return;
            if (!archive.IsInput && value.NumNodes == 0)
                return;
        }

        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive);
    }

    /// <summary>
    /// Is the effect committed.
    /// </summary>
    private bool? _committed;

    /// <summary>
    /// Maximum number of particles.
    /// </summary>
    private int _capacity = DefaultCapacity;

    /// <summary>
    /// Time step scale.
    /// </summary>
    private float _timeScale = 1.0f;

    /// <summary>
    /// Effect duration in seconds.
    /// </summary>
    private float _duration = DefaultDuration;

    /// <summary>
    /// Loop effect.
    /// </summary>
    private bool _loop;

    /// <summary>
    /// Emission graph.
    /// </summary>
    private readonly ParticleGraph _emit;

    /// <summary>
    /// Initialization graph.
    /// </summary>
    private readonly ParticleGraph _init;

    /// <summary>
    /// Update graph.
    /// </summary>
    private readonly ParticleGraph _update;

    /// <summary>
    /// Attribute buffer layout.
    /// </summary>
    private AttributeBufferLayout _attributeBufferLayout;

    /// <summary>
    /// Attributes memory layout.
    /// </summary>
    private readonly ParticleGraphAttributeLayout _attributes = new();

    /// <summary>
    /// Intermediate memory layout.
    /// </summary>
    private readonly ParticleGraphBufferLayout _tempMemory = new();
}

internal class ParticleGraphAttributeBuilder
{
    public ParticleGraphAttributeBuilder(ParticleGraphAttributeLayout attributes,
        ParticleGraphLayer.AttributeBufferLayout layout, int capacity, ParticleGraphBufferLayout tempBufferLayout)
    {
        Attributes = attributes;
        Layout = layout;
        TempBufferLayout = tempBufferLayout;
        Capacity = capacity;
    }

    public bool EvaluateValueType(ParticleGraphNode node, ParticleGraphPin pin)
    {
        var pinAccess = pin.GetParticleGraphAttributeBuilderFriendAccess();
        // Evaluate output pin value for output pins.
        pinAccess.ValueType = pinAccess.RequestedValueType;
        if (pinAccess.ValueType == VariantType.None && !pin.IsInput)
        {
            pinAccess.ValueType = node.EvaluateOutputPinTypeFromParticleGraphAttributeBuilderFriend(pin);
            if (pinAccess.ValueType == VariantType.None)
            {
                Log.GetLogger().LogError("Can't detect output pin {Type}.{Pin} type", node.GetTypeName(), pin.Name);
                return false;
            }
        }
        return true;
    }

    public bool BuildNode(ParticleGraph graph, int i)
    {
        // Configure pin nodes.
        var node = graph.GetNode(i)!;

        var defaultOutputType = ParticleGraphContainerType.Scalar;

        // Connect input pins.
        for (var pinIndex = 0; pinIndex < node.NumPins; ++pinIndex)
        {
            var pin = node.GetPin(pinIndex);
            var pinAccess = pin.GetParticleGraphAttributeBuilderFriendAccess();

            if (!EvaluateValueType(node, pin))
                return false;

            // Connect input pin.
            if (pin.IsInput)
            {
                if (pinAccess.ContainerType == ParticleGraphContainerType.Sparse)
                {
                    Log.GetLogger().LogError("Sparse input pin {Type}.{Pin} is not supported", node.GetTypeName(), pin.Name);
                    return false;
                }
                if (pinAccess.SourceNode == ParticleGraph.InvalidNodeIndex)
                {
                    Log.GetLogger().LogError("Source node is not set for {Type}.{Pin}", node.GetTypeName(), pin.Name);
                    return false;
                }
                if (pinAccess.SourceNode >= i)
                {
                    Log.GetLogger().LogError("Graph can't forward reference nodes");
                    return false;
                }
                var sourceNode = graph.GetNode(pinAccess.SourceNode)!;
                if (pinAccess.SourcePin >= sourceNode.NumPins)
                {
                    Log.GetLogger().LogError("Reference to a missing pin");
                    return false;
                }
                var sourcePin = sourceNode.GetPin(pinAccess.SourcePin);
                var sourcePinAccess = sourcePin.GetParticleGraphAttributeBuilderFriendAccess();
                if (sourcePin.IsInput)
                {
                    Log.GetLogger().LogError("Source pin isn't output pin");
                    return false;
                }
                pinAccess.Memory = sourcePinAccess.Memory;
                // Detect default output type.
                if (pinAccess.Memory.Type != ParticleGraphContainerType.Scalar)
                    defaultOutputType = ParticleGraphContainerType.Span;
                // Evaluate input pin type.
                if (pinAccess.RequestedValueType == VariantType.None)
                    pinAccess.ValueType = sourcePinAccess.ValueType;
                else if (pinAccess.RequestedValueType != sourcePinAccess.ValueType)
                {
                    Log.GetLogger().LogError("Source pin {SourceType}.{SourcePin} type {SourceValueType} doesn't match input pin {Type}.{Pin} type {ValueType}",
                        sourceNode.GetTypeName(), sourcePin.Name, Variant.GetTypeNameList()[(int)sourcePin.ValueType],
                        node.GetTypeName(), pin.Name, Variant.GetTypeNameList()[(int)pin.RequestedType]);
                    return false;
                }
            }
        }
        // Allocate memory for output pins.
        for (var pinIndex = 0; pinIndex < node.NumPins; ++pinIndex)
        {
            var pin = node.GetPin(pinIndex);
            var pinAccess = pin.GetParticleGraphAttributeBuilderFriendAccess();

            if (!pin.IsInput)
            {
                // Allocate attribute buffer if this is a new attribute
                if (pinAccess.ContainerType == ParticleGraphContainerType.Sparse)
                {
                    pinAccess.AttributeIndex = Attributes.GetOrAddAttribute(pinAccess.Name, pinAccess.RequestedValueType);

                    // For output sparse pin the memory is the same as the attribute memory
                    if (!pin.IsInput)
                        pinAccess.Memory = new ParticleGraphPinRef(ParticleGraphContainerType.Sparse, pinAccess.AttributeIndex);
                }
                // Allocate temp buffer for an output pin
                else
                {
                    // Allocate temp buffer
                    var containerType = pinAccess.ContainerType;
                    if (containerType == ParticleGraphContainerType.Auto)
                        containerType = defaultOutputType;
                    pinAccess.Memory = new ParticleGraphPinRef(containerType,
                        TempBufferLayout.Allocate(containerType, pinAccess.ValueType));
                }
            }
        }
        return true;
    }

    public bool Build(ParticleGraph graph)
    {
        var graphNodes = graph.NumNodes;

        for (var i = 0; i < graphNodes; ++i)
        {
            if (!BuildNode(graph, i))
                return false;
        }
        return true;
    }

    public readonly ParticleGraphAttributeLayout Attributes;
    public ParticleGraphLayer.AttributeBufferLayout Layout;
    public readonly ParticleGraphBufferLayout TempBufferLayout;
    public readonly int Capacity;
}
