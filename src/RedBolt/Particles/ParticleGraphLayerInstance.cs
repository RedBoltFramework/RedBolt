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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Scene;

/// <summary>
/// Instance of particle graph layer in emitter.
/// </summary>
public class ParticleGraphLayerInstance : IDisposable
{
    /// <summary>
    /// Construct.
    /// </summary>
    public ParticleGraphLayerInstance()
    {
        _activeParticles = 0;
        _destructionQueueSize = 0;
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public void Dispose()
    {
        DisposeNodeInstances();
        DisposeUniforms();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Apply layer settings to the layer instance
    /// </summary>
    public void Apply(ParticleGraphLayer? layer)
    {
        if (layer is null)
            return;
        if (!layer.Commit())
            return;

        DisposeNodeInstances();
        DisposeUniforms();
        _layer = layer;
        var layout = _layer.GetAttributeBufferLayout();
        _attributes = new ParticleGraphMemoryBuffer(layout.AttributeBufferSize);
        _temp = new ParticleGraphMemoryBuffer(_layer.TempBufferSize);

        _attributes.Register<ParticleGraphNodeInstance>(layout.EmitNodePointers, _layer.EmitGraph.NumNodes);
        _attributes.Register<ParticleGraphNodeInstance>(layout.InitNodePointers, _layer.InitGraph.NumNodes);
        _attributes.Register<ParticleGraphNodeInstance>(layout.UpdateNodePointers, _layer.UpdateGraph.NumNodes);
        _attributes.Register<int>(layout.Indices, _layer.Capacity);
        _attributes.Register<int>(layout.ScalarIndices, _layer.Capacity);
        _attributes.Register<int>(layout.NaturalIndices, _layer.Capacity);
        _attributes.Register<int>(layout.DestructionQueue, _layer.Capacity);
        RegisterValueSpans();

        // Initialize indices
        _emitNodeInstances = _attributes.MakeMemory<ParticleGraphNodeInstance>(layout.EmitNodePointers);
        InitNodeInstances(_emitNodeInstances.Span, _layer.EmitGraph);
        _initNodeInstances = _attributes.MakeMemory<ParticleGraphNodeInstance>(layout.InitNodePointers);
        InitNodeInstances(_initNodeInstances.Span, _layer.InitGraph);
        _updateNodeInstances = _attributes.MakeMemory<ParticleGraphNodeInstance>(layout.UpdateNodePointers);
        InitNodeInstances(_updateNodeInstances.Span, _layer.UpdateGraph);

        // Initialize indices
        _indices = _attributes.MakeMemory<int>(layout.Indices);
        _scalarIndices = _attributes.MakeMemory<int>(layout.ScalarIndices);
        _naturalIndices = _attributes.MakeMemory<int>(layout.NaturalIndices);
        for (var i = 0; i < _indices.Length; ++i)
        {
            _indices.Span[i] = i;
            _scalarIndices.Span[i] = 0;
            _naturalIndices.Span[i] = i;
        }
        _destructionQueue = _attributes.MakeMemory<int>(layout.DestructionQueue);
        Reset();
    }

    /// <summary>
    /// Return number of active particles.
    /// </summary>
    public int NumActiveParticles => _activeParticles;

    /// <summary>
    /// Remove all current particles.
    /// </summary>
    public void RemoveAllParticles()
    {
        _activeParticles = 0;
    }

    /// <summary>
    /// Create a new particles. Return true if there was room.
    /// </summary>
    public bool EmitNewParticles(float numParticles = 1.0f)
    {
        _emitCounterReminder += numParticles;

        if (_emitCounterReminder < 1.0f)
            return true;

        var particlesToEmit = (int)_emitCounterReminder;
        _emitCounterReminder -= particlesToEmit;

        particlesToEmit = global::System.Math.Min(particlesToEmit, _indices.Length - _activeParticles);
        if (particlesToEmit == 0)
            return false;

        var startIndex = _activeParticles;
        _activeParticles += particlesToEmit;

        var autoContext = MakeUpdateContext(0.0f);
        autoContext.Indices = autoContext.Indices.Slice(startIndex, particlesToEmit);
        RunGraph(_initNodeInstances.Span, ref autoContext);

        return true;
    }

    /// <summary>
    /// Run update step.
    /// </summary>
    public void Update(float timeStep, bool emitting)
    {
        timeStep *= _layer!.TimeScale;
        var emitContext = MakeUpdateContext(timeStep);
        if (_indices.IsEmpty)
            return;
        if (emitting)
        {
            emitContext.Indices = _indices[..1];
            RunGraph(_emitNodeInstances.Span, ref emitContext);
        }

        var updateContext = MakeUpdateContext(timeStep);
        RunGraph(_updateNodeInstances.Span, ref updateContext);
        DestroyParticles();
        _time += timeStep;
    }

    /// <summary>
    /// Get number of attributes.
    /// </summary>
    public int NumAttributes => _layer!.AttributeLayout.NumAttributes;

    /// <summary>
    /// Get attribute values.
    /// </summary>
    public SparseSpan<T> GetAttributeValues<T>(int attributeIndex)
    {
        if (_activeParticles == 0)
            return default;
        return GetSparse<T>(attributeIndex, _indices.Span[.._activeParticles]);
    }

    public SparseSpan<T> GetSparse<T>(int attributeIndex, Span<int> indices)
    {
        var attribute = _layer!.AttributeLayout.GetSpan(attributeIndex);
        var values = attribute.MakeSpan<T>(_attributes);
        return new SparseSpan<T>(values, indices);
    }

    public SparseSpan<T> GetScalar<T>(int pinIndex)
    {
        var attribute = _layer!.IntermediateValues[pinIndex];
        var values = attribute.MakeSpan<T>(_temp);
        return new SparseSpan<T>(values, _scalarIndices.Span);
    }

    public SparseSpan<T> GetSpan<T>(int pinIndex)
    {
        var attribute = _layer!.IntermediateValues[pinIndex];
        var values = attribute.MakeSpan<T>(_temp);
        return new SparseSpan<T>(values, _naturalIndices.Span);
    }

    /// <summary>
    /// Get emitter.
    /// </summary>
    public ParticleGraphEmitter? Emitter => _emitter;

    /// <summary>
    /// Mark a particle for deletion.
    /// </summary>
    public void MarkForDeletion(int particleIndex)
    {
        if (particleIndex < 0 || particleIndex >= _activeParticles)
            return;

        if (_destructionQueueSize < _destructionQueue.Length)
        {
            _destructionQueue.Span[_destructionQueueSize] = particleIndex;
            ++_destructionQueueSize;
        }
    }

    /// <summary>
    /// Get uniform index. Creates new uniform slot on demand.
    /// </summary>
    public int GetUniformIndex(StringHash stringHash, VariantType variant)
    {
        for (var index = 0; index < _uniforms.Count; ++index)
        {
            var uniform = _uniforms[index];
            if (uniform.Name == stringHash && uniform.Type == variant)
                return index;
        }

        _uniforms.Add(new UniformSlot(stringHash, variant, new Variant(variant)));
        return _uniforms.Count - 1;
    }

    /// <summary>
    /// Get uniform variant by index.
    /// </summary>
    public ref Variant GetUniform(int index)
    {
        return ref CollectionsMarshal.AsSpan(_uniforms)[index].Value;
    }

    /// <summary>
    /// Reset the particle emitter layer completely. Removes current particles, sets emitting state on, and resets the
    /// emission timer.
    /// </summary>
    public void Reset()
    {
        foreach (var node in _emitNodeInstances.Span)
            node.Reset();
        foreach (var node in _initNodeInstances.Span)
            node.Reset();
        foreach (var node in _updateNodeInstances.Span)
            node.Reset();
        _activeParticles = 0;
        _time = 0.0f;
    }

    /// <summary>
    /// Update all drawable attributes. Executed by ParticleGraphEmitter.
    /// </summary>
    public void UpdateDrawables()
    {
        foreach (var node in _initNodeInstances.Span)
            node.UpdateDrawableAttributes();
        foreach (var node in _emitNodeInstances.Span)
            node.UpdateDrawableAttributes();
        foreach (var node in _updateNodeInstances.Span)
            node.UpdateDrawableAttributes();
    }

    /// <summary>
    /// Get effect layer.
    /// </summary>
    public ParticleGraphLayer? Layer => _layer;

    /// <summary>
    /// Handle scene change in instance.
    /// </summary>
    protected void OnSceneSet(Scene? scene)
    {
        foreach (var node in _initNodeInstances.Span)
            node.OnSceneSet(scene);
        foreach (var node in _emitNodeInstances.Span)
            node.OnSceneSet(scene);
        foreach (var node in _updateNodeInstances.Span)
            node.OnSceneSet(scene);
    }

    internal void OnSceneSetFromParticleGraphEmitterFriend(Scene? scene) => OnSceneSet(scene);

    /// <summary>
    /// Set emitter reference.
    /// </summary>
    protected void SetEmitter(ParticleGraphEmitter emitter)
    {
        _emitter = emitter;
    }

    internal void SetEmitterFromParticleGraphEmitterFriend(ParticleGraphEmitter emitter) => SetEmitter(emitter);

    /// <summary>
    /// Initialize update context.
    /// </summary>
    protected UpdateContext MakeUpdateContext(float timeStep)
    {
        var context = new UpdateContext
        {
            Attributes = _attributes,
            TempBuffer = _temp,
            TimeStep = timeStep,
            Time = _time,
            Layer = this
        };
        if (_activeParticles > 0)
            context.Indices = _indices[.._activeParticles];
        return context;
    }

    /// <summary>
    /// Run graph.
    /// </summary>
    protected void RunGraph(Span<ParticleGraphNodeInstance> nodes, ref UpdateContext updateContext)
    {
        foreach (var node in nodes)
            node.Update(ref updateContext);
    }

    protected void DestroyParticles()
    {
        if (_destructionQueueSize == 0)
            return;
        var queue = _destructionQueue.Span[.._destructionQueueSize];
        queue.Sort(static (left, right) => right.CompareTo(left));
        foreach (var index in queue)
        {
            (_indices.Span[index], _indices.Span[_activeParticles - 1]) =
                (_indices.Span[_activeParticles - 1], _indices.Span[index]);
            --_activeParticles;
        }
        _destructionQueueSize = 0;
    }

    protected void InitNodeInstances(Span<ParticleGraphNodeInstance> nodeInstances, ParticleGraph graph)
    {
        for (var i = 0; i < graph.NumNodes; ++i)
        {
            var node = graph.GetNode(i)!;
            nodeInstances[i] = node.CreateInstance(this);
            nodeInstances[i].Reset();
        }
    }

    private void DisposeNodeInstances()
    {
        foreach (var node in _emitNodeInstances.Span)
            node.Dispose();
        foreach (var node in _initNodeInstances.Span)
            node.Dispose();
        foreach (var node in _updateNodeInstances.Span)
            node.Dispose();
        _emitNodeInstances = Memory<ParticleGraphNodeInstance>.Empty;
        _initNodeInstances = Memory<ParticleGraphNodeInstance>.Empty;
        _updateNodeInstances = Memory<ParticleGraphNodeInstance>.Empty;
    }

    private void DisposeUniforms()
    {
        for (var index = 0; index < _uniforms.Count; ++index)
        {
            ref var uniform = ref CollectionsMarshal.AsSpan(_uniforms)[index];
            uniform.Value.Dispose();
        }
        _uniforms.Clear();
    }

    private void RegisterValueSpans()
    {
        for (var index = 0; index < _layer!.AttributeLayout.NumAttributes; ++index)
        {
            _attributes.Register(_layer.AttributeLayout.GetSpan(index),
                _layer.AttributeLayout.GetType(index), _layer.Capacity);
        }

        RegisterGraphValueSpans(_layer.InitGraph);
        RegisterGraphValueSpans(_layer.EmitGraph);
        RegisterGraphValueSpans(_layer.UpdateGraph);
    }

    private void RegisterGraphValueSpans(ParticleGraph graph)
    {
        for (var nodeIndex = 0; nodeIndex < graph.NumNodes; ++nodeIndex)
        {
            var node = graph.GetNode(nodeIndex)!;
            for (var pinIndex = 0; pinIndex < node.NumPins; ++pinIndex)
            {
                var pin = node.GetPin(pinIndex);
                var memory = pin.MemoryReference;
                if (memory.Type == ParticleGraphContainerType.Sparse)
                    continue;
                var elementCount = memory.Type == ParticleGraphContainerType.Scalar ? 1 : _layer!.Capacity;
                _temp.Register(_layer!.IntermediateValues[memory.Index], pin.ValueType, elementCount);
            }
        }
    }

    /// <summary>
    /// Emit counter reminder. When reminder value get over 1 the layer emits particle.
    /// </summary>
    private float _emitCounterReminder;

    /// <summary>
    /// Memory used to store all layer related arrays: nodes, indices, attributes.
    /// </summary>
    private ParticleGraphMemoryBuffer _attributes = new(0);

    /// <summary>
    /// Temp memory needed for graph calculation.
    /// </summary>
    private ParticleGraphMemoryBuffer _temp = new(0);

    /// <summary>
    /// Node instances for emit graph
    /// </summary>
    private Memory<ParticleGraphNodeInstance> _emitNodeInstances;

    /// <summary>
    /// Node instances for initialization graph
    /// </summary>
    private Memory<ParticleGraphNodeInstance> _initNodeInstances;

    /// <summary>
    /// Node instances for update graph
    /// </summary>
    private Memory<ParticleGraphNodeInstance> _updateNodeInstances;

    /// <summary>
    /// All indices of the particle system.
    /// </summary>
    private Memory<int> _indices;

    /// <summary>
    /// All indices set to 0.
    /// </summary>
    private Memory<int> _scalarIndices;

    /// <summary>
    /// All indices going in natural order (0, 1, ...).
    /// </summary>
    private Memory<int> _naturalIndices;

    /// <summary>
    /// Particle indices to be removed.
    /// </summary>
    private Memory<int> _destructionQueue;

    /// <summary>
    /// Number of particles to destroy at end of the frame.
    /// </summary>
    private int _destructionQueueSize;

    /// <summary>
    /// Number of active particles.
    /// </summary>
    private int _activeParticles;

    /// <summary>
    /// Reference to layer.
    /// </summary>
    private ParticleGraphLayer? _layer;

    /// <summary>
    /// Emitter that owns the layer instance.
    /// </summary>
    private ParticleGraphEmitter? _emitter;

    /// <summary>
    /// Time since emitter start.
    /// </summary>
    private float _time;

    private struct UniformSlot
    {
        public UniformSlot(StringHash name, VariantType type, Variant value)
        {
            Name = name;
            Type = type;
            Value = value;
        }

        public StringHash Name;
        public VariantType Type;
        public Variant Value;
    }

    private readonly List<UniformSlot> _uniforms = [];
}
