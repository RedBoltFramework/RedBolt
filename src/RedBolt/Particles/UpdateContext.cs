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

public struct UpdateContext
{
    /// <summary>
    /// Current frame time step.
    /// </summary>
    public float TimeStep;

    /// <summary>
    /// Time since emitter start.
    /// </summary>
    public float Time;

    public Memory<int> Indices;
    public ParticleGraphMemoryBuffer Attributes;
    public ParticleGraphMemoryBuffer TempBuffer;
    public ParticleGraphLayerInstance Layer;

    public readonly SparseSpan<T> GetSpan<T>(ParticleGraphPinRef pin)
    {
        switch (pin.Type)
        {
            case ParticleGraphContainerType.Span:
                return Layer.GetSpan<T>(pin.Index);
            case ParticleGraphContainerType.Scalar:
                return Layer.GetScalar<T>(pin.Index);
            case ParticleGraphContainerType.Sparse:
                return Layer.GetSparse<T>(pin.Index, Indices.Span);
            default:
                throw new InvalidOperationException("Invalid particle graph pin container type.");
        }
    }
}
