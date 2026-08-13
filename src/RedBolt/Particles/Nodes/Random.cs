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

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Operation on attribute.</summary>
[Reflected]
public partial class Random : ParticleGraphNode
{
    private sealed class MinAttributeAccessor : AttributeAccessor
    {
        public override void Get(Serializable ptr, ref Variant dest)
        {
            dest = ((Random)ptr)._min.Clone();
        }

        public override void Set(Serializable ptr, Variant src)
        {
            ((Random)ptr).SetMin(src);
        }
    }

    private sealed class MaxAttributeAccessor : AttributeAccessor
    {
        public override void Get(Serializable ptr, ref Variant dest)
        {
            dest = ((Random)ptr)._max.Clone();
        }

        public override void Set(Serializable ptr, Variant src)
        {
            ((Random)ptr).SetMax(src);
        }
    }

    protected class Instance : ParticleGraphNodeInstance
    {
        public Instance(Random node) => _node = node;

        public override void Update(ref UpdateContext context)
        {
            var pin = _node._pins[0];
            switch (pin.ValueType)
            {
                case VariantType.Int: Generate<int>(in context, pin); break;
                case VariantType.Int64: Generate<long>(in context, pin); break;
                case VariantType.Bool: Generate<bool>(in context, pin); break;
                case VariantType.Float: Generate<float>(in context, pin); break;
                case VariantType.Double: Generate<double>(in context, pin); break;
                case VariantType.Vector2: Generate<Vector2f>(in context, pin); break;
                case VariantType.Vector3: Generate<Vector3f>(in context, pin); break;
                case VariantType.Vector4: Generate<Vector4f>(in context, pin); break;
                case VariantType.Quaternion: Generate<Quaternion>(in context, pin); break;
                case VariantType.Color: Generate<Color>(in context, pin); break;
                case VariantType.String: Generate<string>(in context, pin); break;
                case VariantType.Buffer: Generate<byte[]>(in context, pin); break;
                case VariantType.ResourceRef: Generate<ResourceRef>(in context, pin); break;
                case VariantType.ResourceRefList: Generate<ResourceRefList>(in context, pin); break;
                case VariantType.Vector2i: Generate<Vector2i>(in context, pin); break;
                case VariantType.Vector3i: Generate<Vector3i>(in context, pin); break;
                default: throw new InvalidOperationException("Unsupported particle random value type.");
            }
        }

        private void Generate<T>(in UpdateContext context, ParticleGraphPin pin)
        {
            var span = context.GetSpan<T>(pin.MemoryReference);
            for (var i = 0; i < context.Indices.Length; ++i)
            {
                var value = _node._min.Lerp(_node._max, MathDefs.Random());
                span[i] = value.Get<T>();
                value.Dispose();
            }
        }

        protected readonly Random _node;
    }

    /// <summary>Construct.</summary>
    public Random()
    {
        _pins = [new ParticleGraphPin(ParticleGraphPinFlag.MutableType, "out", ParticleGraphContainerType.Span)];
        _min = new Variant(0.0f);
        _max = new Variant(1.0f);
    }

    public override void Dispose()
    {
        _max.Dispose();
        _min.Dispose();
        base.Dispose();
    }

    public override int NumPins => _pins.Length;
    public override ParticleGraphPin GetPin(int index) => _pins[index];
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer) => new Instance(this);

    public ref readonly Variant GetMin() => ref _min;
    public void SetMin(in Variant value) { var replacement = value.Clone(); _min.Dispose(); _min = replacement; }
    public ref readonly Variant GetMax() => ref _max;
    public void SetMax(in Variant value) { var replacement = value.Clone(); _max.Dispose(); _max = replacement; }

    [Serialized("Min", typeof(float), AccessorType = typeof(MinAttributeAccessor))]
    private Variant SerializedMin { get => _min.Clone(); set => SetMin(value); }
    [Serialized("Max", typeof(float), AccessorType = typeof(MaxAttributeAccessor))]
    private Variant SerializedMax { get => _max.Clone(); set => SetMax(value); }

    protected readonly ParticleGraphPin[] _pins;
    protected Variant _min;
    protected Variant _max;
}
