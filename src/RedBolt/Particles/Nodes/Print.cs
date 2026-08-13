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
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;

/// <summary>Operation on attribute.</summary>
[Reflected]
public partial class Print : ParticleGraphNode
{
    protected class Instance : ParticleGraphNodeInstance
    {
        public Instance(Print node) => _node = node;
        public override void Update(ref UpdateContext context)
        {
            var pin = _node._pins[0];
            switch (pin.ValueType)
            {
                case VariantType.Int: LogSpan<int>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Int64: LogSpan<long>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Bool: LogSpan<bool>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Float: LogSpan<float>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Double: LogSpan<double>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Vector2: LogSpan<Vector2f>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Vector3: LogSpan<Vector3f>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Vector4: LogSpan<Vector4f>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Quaternion: LogSpan<Quaternion>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Color: LogSpan<Color>(in context, pin, static value => new Variant(value)); break;
                case VariantType.String: LogSpan<string>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Buffer: LogSpan<byte[]>(in context, pin, static value => new Variant(value)); break;
                case VariantType.ResourceRef: LogSpan<ResourceRef>(in context, pin, static value => new Variant(value)); break;
                case VariantType.ResourceRefList: LogSpan<ResourceRefList>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Vector2i: LogSpan<Vector2i>(in context, pin, static value => new Variant(value)); break;
                case VariantType.Vector3i: LogSpan<Vector3i>(in context, pin, static value => new Variant(value)); break;
                default: throw new InvalidOperationException("Unsupported particle print value type.");
            }
        }

        private static void LogSpan<T>(in UpdateContext context, ParticleGraphPin pin, Func<T, Variant> makeVariant)
        {
            var span = context.GetSpan<T>(pin.MemoryReference);
            for (var i = 0; i < context.Indices.Length; ++i)
            {
                var value = makeVariant(span[i]);
                Log.GetLogger().Write(LogLevel.Info, value.ToString());
                value.Dispose();
            }
        }

        protected readonly Print _node;
    }

    /// <summary>Construct.</summary>
    public Print() => _pins = [new ParticleGraphPin(ParticleGraphPinFlag.Input, "value")];
    public override int NumPins => _pins.Length;
    public override ParticleGraphPin GetPin(int index) => _pins[index];
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer) => new Instance(this);

    protected readonly ParticleGraphPin[] _pins;
}
