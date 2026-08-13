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

namespace RedBolt.Particles.ParticleGraphNodes;

using System;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;

/// <summary>Operation on uniform.</summary>
[Reflected(Factory = false)]
public abstract partial class Uniform : ParticleGraphNode
{
    protected Uniform()
    {
    }

    /// <summary>Set uniform name.</summary>
    public void SetUniformName(string name) => SetPinName(0, name);
    /// <summary>Get uniform name.</summary>
    public string GetUniformName() => GetPinName(0);
    /// <summary>Set uniform type.</summary>
    public virtual void SetUniformType(VariantType valueType) => SetPinValueType(0, valueType);
    /// <summary>Get uniform type.</summary>
    public VariantType GetUniformType() => GetPinValueType(0);
}

/// <summary>Get particle uniform value.</summary>
[Reflected]
public partial class GetUniform : Uniform
{
    protected class Instance : ParticleGraphNodeInstance
    {
        public Instance(GetUniform node, int uniformIndex) { _node = node; _uniformIndex = uniformIndex; }

        public override void Update(ref UpdateContext context)
        {
            var pin = _node._pins[0];
            ref var uniform = ref context.Layer.GetUniform(_uniformIndex);
            switch (pin.ValueType)
            {
                case VariantType.Int: Get<int>(in context, pin, uniform); break;
                case VariantType.Int64: Get<long>(in context, pin, uniform); break;
                case VariantType.Bool: Get<bool>(in context, pin, uniform); break;
                case VariantType.Float: Get<float>(in context, pin, uniform); break;
                case VariantType.Double: Get<double>(in context, pin, uniform); break;
                case VariantType.Vector2: Get<Vector2f>(in context, pin, uniform); break;
                case VariantType.Vector3: Get<Vector3f>(in context, pin, uniform); break;
                case VariantType.Vector4: Get<Vector4f>(in context, pin, uniform); break;
                case VariantType.Quaternion: Get<Quaternion>(in context, pin, uniform); break;
                case VariantType.Color: Get<Color>(in context, pin, uniform); break;
                case VariantType.String: Get<string>(in context, pin, uniform); break;
                case VariantType.Buffer: Get<byte[]>(in context, pin, uniform); break;
                case VariantType.ResourceRef: Get<ResourceRef>(in context, pin, uniform); break;
                case VariantType.ResourceRefList: Get<ResourceRefList>(in context, pin, uniform); break;
                case VariantType.Vector2i: Get<Vector2i>(in context, pin, uniform); break;
                case VariantType.Vector3i: Get<Vector3i>(in context, pin, uniform); break;
                default: throw new InvalidOperationException("Unsupported particle uniform type.");
            }
        }

        private static void Get<T>(in UpdateContext context, ParticleGraphPin pin, in Variant uniform)
            => context.GetSpan<T>(pin.MemoryReference)[0] = uniform.Get<T>();

        protected readonly GetUniform _node;
        protected readonly int _uniformIndex;
    }

    public GetUniform()
    {
        _pins = [new ParticleGraphPin(ParticleGraphPinFlag.MutableName | ParticleGraphPinFlag.MutableType,
            "uniform", VariantType.Float, ParticleGraphContainerType.Scalar)];
    }

    public override int NumPins => _pins.Length;
    public override ParticleGraphPin GetPin(int index) => _pins[index];
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
        => new Instance(this, layer.GetUniformIndex(new StringHash(GetUniformName()), GetUniformType()));

    protected override ParticleGraphPin? LoadOutputPin(ParticleGraphReader reader, GraphOutPin pin)
    { SetPinName(0, pin.GetName()); SetPinValueType(0, pin.GetType()); return _pins[0]; }

    protected readonly ParticleGraphPin[] _pins;
}

/// <summary>Set particle uniform value.</summary>
[Reflected]
public partial class SetUniform : Uniform
{
    private class Instance : ParticleGraphNodeInstance
    {
        public Instance(SetUniform node, int uniformIndex) { _node = node; _uniformIndex = uniformIndex; }

        public override void Update(ref UpdateContext context)
        {
            var destination = _node._pins[0];
            var source = _node._pins[1];
            switch (destination.ValueType)
            {
                case VariantType.Int: Set<int>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Int64: Set<long>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Bool: Set<bool>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Float: Set<float>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Double: Set<double>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Vector2: Set<Vector2f>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Vector3: Set<Vector3f>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Vector4: Set<Vector4f>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Quaternion: Set<Quaternion>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Color: Set<Color>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.String: Set<string>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Buffer: SetBuffer(in context, _uniformIndex, destination, source); break;
                case VariantType.ResourceRef: Set<ResourceRef>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.ResourceRefList: SetResourceRefList(in context, _uniformIndex, destination, source); break;
                case VariantType.Vector2i: Set<Vector2i>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                case VariantType.Vector3i: Set<Vector3i>(in context, _uniformIndex, destination, source, static value => new Variant(value)); break;
                default: throw new InvalidOperationException("Unsupported particle uniform type.");
            }
        }

        private static void Set<T>(in UpdateContext context, int uniformIndex, ParticleGraphPin destinationPin,
            ParticleGraphPin sourcePin, Func<T, Variant> makeVariant)
        {
            var source = context.GetSpan<T>(sourcePin.MemoryReference);
            var destination = context.GetSpan<T>(destinationPin.MemoryReference);
            var value = source[0];
            var replacement = makeVariant(value);
            ref var uniform = ref context.Layer.GetUniform(uniformIndex);
            uniform.Dispose();
            uniform = replacement;
            destination[0] = value;
        }

        private static void SetBuffer(in UpdateContext context, int uniformIndex,
            ParticleGraphPin destinationPin, ParticleGraphPin sourcePin)
        {
            var source = context.GetSpan<byte[]>(sourcePin.MemoryReference);
            var destination = context.GetSpan<byte[]>(destinationPin.MemoryReference);
            var value = source[0];
            var replacement = new Variant(value);
            ref var uniform = ref context.Layer.GetUniform(uniformIndex);
            uniform.Dispose();
            uniform = replacement;
            if (destination[0].Length != value.Length)
                destination[0] = new byte[value.Length];
            value.CopyTo(destination[0], 0);
        }

        private static void SetResourceRefList(in UpdateContext context, int uniformIndex,
            ParticleGraphPin destinationPin, ParticleGraphPin sourcePin)
        {
            var source = context.GetSpan<ResourceRefList>(sourcePin.MemoryReference);
            var destination = context.GetSpan<ResourceRefList>(destinationPin.MemoryReference);
            var value = source[0];
            var replacement = new Variant(value);
            ref var uniform = ref context.Layer.GetUniform(uniformIndex);
            uniform.Dispose();
            uniform = replacement;
            destination[0].Type = value.Type;
            destination[0].Names.Clear();
            destination[0].Names.AddRange(value.Names);
        }

        protected readonly SetUniform _node;
        protected readonly int _uniformIndex;
    }

    public SetUniform()
    {
        _pins =
        [
            new ParticleGraphPin(ParticleGraphPinFlag.MutableName | ParticleGraphPinFlag.MutableType,
                "uniform", VariantType.Float, ParticleGraphContainerType.Scalar),
            new ParticleGraphPin(ParticleGraphPinFlag.Input | ParticleGraphPinFlag.MutableType,
                "", VariantType.Float, ParticleGraphContainerType.Scalar)
        ];
    }

    public override int NumPins => _pins.Length;
    public override ParticleGraphPin GetPin(int index) => _pins[index];
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
        => new Instance(this, layer.GetUniformIndex(new StringHash(GetUniformName()), GetUniformType()));

    public override void SetUniformType(VariantType valueType)
    { SetPinValueType(0, valueType); SetPinValueType(1, valueType); }

    protected override ParticleGraphPin? LoadOutputPin(ParticleGraphReader reader, GraphOutPin pin)
    { SetPinName(0, pin.GetName()); SetPinValueType(0, pin.GetType()); return _pins[0]; }

    protected readonly ParticleGraphPin[] _pins;
}
