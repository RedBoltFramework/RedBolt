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
using RedBolt.Scene;

/// <summary>
/// Operation on attribute
/// </summary>
[Reflected]
public partial class Constant : ParticleGraphNode
{
    private sealed class ValueAttributeAccessor : AttributeAccessor
    {
        public override void Get(Serializable ptr, ref Variant dest)
        {
            dest = ((Constant)ptr)._value.Clone();
        }

        public override void Set(Serializable ptr, Variant src)
        {
            ((Constant)ptr).SetValue(src);
        }
    }

    protected class Instance : ParticleGraphNodeInstance
    {
        public Instance(Constant node)
        {
            _node = node;
        }

        public override void Update(ref UpdateContext context)
        {
            var pin0 = _node._pins[0];
            switch (_node._value.Type)
            {
                case VariantType.Int:
                    context.GetSpan<int>(pin0.MemoryReference)[0] = _node._value.GetInt();
                    break;
                case VariantType.Bool:
                    context.GetSpan<bool>(pin0.MemoryReference)[0] = _node._value.GetBool();
                    break;
                case VariantType.Int64:
                    context.GetSpan<long>(pin0.MemoryReference)[0] = _node._value.GetInt64();
                    break;
                case VariantType.Float:
                    context.GetSpan<float>(pin0.MemoryReference)[0] = _node._value.GetFloat();
                    break;
                case VariantType.Vector2:
                    context.GetSpan<Vector2f>(pin0.MemoryReference)[0] = _node._value.GetVector2();
                    break;
                case VariantType.Vector3:
                    context.GetSpan<Vector3f>(pin0.MemoryReference)[0] = _node._value.GetVector3();
                    break;
                case VariantType.Vector4:
                    context.GetSpan<Vector4f>(pin0.MemoryReference)[0] = _node._value.GetVector4();
                    break;
                case VariantType.Vector2i:
                    context.GetSpan<Vector2i>(pin0.MemoryReference)[0] = _node._value.GetIntVector2();
                    break;
                case VariantType.Vector3i:
                    context.GetSpan<Vector3i>(pin0.MemoryReference)[0] = _node._value.GetIntVector3();
                    break;
                case VariantType.Quaternion:
                    context.GetSpan<Quaternion>(pin0.MemoryReference)[0] = _node._value.GetQuaternion();
                    break;
                case VariantType.Matrix3:
                    context.GetSpan<Matrix3>(pin0.MemoryReference)[0] = _node._value.GetMatrix3();
                    break;
                case VariantType.Matrix3x4:
                    context.GetSpan<Matrix3x4>(pin0.MemoryReference)[0] = _node._value.GetMatrix3x4();
                    break;
                case VariantType.Matrix4:
                    context.GetSpan<Matrix4>(pin0.MemoryReference)[0] = _node._value.GetMatrix4();
                    break;
                case VariantType.Color:
                    context.GetSpan<Color>(pin0.MemoryReference)[0] = _node._value.GetColor();
                    break;
                case VariantType.VariantCurve:
                    context.GetSpan<VariantCurve>(pin0.MemoryReference)[0] = _node._value.GetVariantCurve();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported particle constant value type.");
            }
        }

        protected Constant _node;
    }

    /// <summary>
    /// Construct.
    /// </summary>
    public Constant()
    {
        _pins =
        [
            new ParticleGraphPin(ParticleGraphPinFlag.MutableType, "out", VariantType.None,
                ParticleGraphContainerType.Scalar)
        ];
    }

    public override void Dispose()
    {
        _value.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Get number of pins.
    /// </summary>
    public override int NumPins => _pins.Length;

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
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer) => new Instance(this);

    /// <summary>
    /// Serialize from/to archive. Return true if successful.
    /// </summary>
    //bool Serialize(Archive& archive) override;

    public ref readonly Variant GetValue() => ref _value;

    public void SetValue(in Variant value)
    {
        var replacement = value.Clone();
        _value.Dispose();
        _value = replacement;
        SetPinValueType(0, value.Type);
    }

    [Serialized("Value", typeof(float), AccessorType = typeof(ValueAttributeAccessor))]
    private Variant SerializedValue
    {
        get => _value.Clone();
        set => SetValue(value);
    }

    /// <summary>
    /// Pins
    /// </summary>
    protected readonly ParticleGraphPin[] _pins;

    /// <summary>
    /// Value
    /// </summary>
    protected Variant _value;
}
