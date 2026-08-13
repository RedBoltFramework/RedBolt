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

/// <summary>Sample curve operator.</summary>
[Reflected]
public partial class Curve : ParticleGraphNode
{
    public class Instance : ParticleGraphNodeInstance
    {
        public Instance(Curve node) => _node = node;
        public Curve GetNodeInstance() => _node;

        public override void Update(ref UpdateContext context)
        {
            var input = _node._pins[0];
            var output = _node._pins[1];
            switch (output.ValueType)
            {
                case VariantType.Int: Sample<int>(in context, input, output); break;
                case VariantType.Int64: Sample<long>(in context, input, output); break;
                case VariantType.Bool: Sample<bool>(in context, input, output); break;
                case VariantType.Float: Sample<float>(in context, input, output); break;
                case VariantType.Double: Sample<double>(in context, input, output); break;
                case VariantType.Vector2: Sample<Vector2f>(in context, input, output); break;
                case VariantType.Vector3: Sample<Vector3f>(in context, input, output); break;
                case VariantType.Vector4: Sample<Vector4f>(in context, input, output); break;
                case VariantType.Quaternion: Sample<Quaternion>(in context, input, output); break;
                case VariantType.Color: Sample<Color>(in context, input, output); break;
                case VariantType.String: Sample<string>(in context, input, output); break;
                case VariantType.Buffer: Sample<byte[]>(in context, input, output); break;
                case VariantType.ResourceRef: Sample<ResourceRef>(in context, input, output); break;
                case VariantType.ResourceRefList: Sample<ResourceRefList>(in context, input, output); break;
                case VariantType.Vector2i: Sample<Vector2i>(in context, input, output); break;
                case VariantType.Vector3i: Sample<Vector3i>(in context, input, output); break;
                default: throw new InvalidOperationException("Unsupported particle curve output type.");
            }
        }

        public void Sample<T>(in UpdateContext context, ParticleGraphPin inputPin, ParticleGraphPin outputPin)
        {
            var time = context.GetSpan<float>(inputPin.MemoryReference);
            var output = context.GetSpan<T>(outputPin.MemoryReference);
            for (var i = 0; i < context.Indices.Length; ++i)
            {
                var sampled = _node.Sample(time[i]);
                output[i] = sampled.Get<T>();
                sampled.Dispose();
            }
        }

        protected readonly Curve _node;
    }

    /// <summary>Construct.</summary>
    public Curve()
    {
        _duration = 1.0f;
        _isLooped = false;
        _curve = new VariantCurve();
        _pins =
        [
            new ParticleGraphPin(ParticleGraphPinFlag.Input, "t", VariantType.Float),
            new ParticleGraphPin(ParticleGraphPinFlag.MutableType, "out")
        ];
    }

    public override void Dispose()
    {
        _curve.Dispose();
        base.Dispose();
    }

    public override int NumPins => _pins.Length;
    public override ParticleGraphPin GetPin(int index) => _pins[index];
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer) => new Instance(this);

    private static object? GetDurationDefault() => 1.0f;

    [Serialized("Duration", DefaultSource = nameof(GetDurationDefault))]
    public float Duration { get => _duration; set => _duration = value; }
    [Serialized("IsLooped")]
    public bool IsLooped { get => _isLooped; set => _isLooped = value; }
    public VariantCurve GetCurve() => _curve;
    public void SetCurve(VariantCurve curve)
    {
        var replacement = curve.Clone();
        _curve.Dispose();
        _curve = replacement;
        SetPinValueType(1, _curve.GetType());
    }

    [Serialized("Curve")]
    private VariantCurve SerializedCurve { get => _curve.Clone(); set => SetCurve(value); }

    public Variant Sample(float time)
    {
        uint frameIndex = 0;
        return _curve.Sample(time, _duration, _isLooped, ref frameIndex);
    }

    public VariantType EvaluateOutputPinType(ParticleGraphPin pin) => EvaluateOutputPinTypeCore(pin);

    protected override VariantType EvaluateOutputPinTypeCore(ParticleGraphPin pin) => _curve.GetType();

    private float _duration;
    private bool _isLooped;
    private VariantCurve _curve;
    private readonly ParticleGraphPin[] _pins;
}
