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

using System;
using RedBolt.Core;
using RedBolt.Math;

[Reflected]
public partial class Slerp : TemplateNode<SlerpInstance, Quaternion, Quaternion, float, Quaternion>
{
    /// <summary>Construct Slerp.</summary>
    public Slerp() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "x", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "y", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "t", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "out", ParticleGraphContainerType.Auto)]) { }
    /// <summary>Evaluate size required to place new node instance.</summary>
    public override int EvaluateInstanceSize() => IntPtr.Size;
    /// <summary>Create new instance.</summary>
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new SlerpInstance(); instance.Init(this, layer); return instance; }
}

public sealed class SlerpInstance : Slerp.InstanceBase
{
    public override void Update(in UpdateContext context, int numParticles, SparseSpan<Quaternion> x,
        SparseSpan<Quaternion> y, SparseSpan<float> t, SparseSpan<Quaternion> output)
    { for (var i = 0; i < numParticles; ++i) output[i] = x[i].Slerp(y[i], t[i]); }
}
