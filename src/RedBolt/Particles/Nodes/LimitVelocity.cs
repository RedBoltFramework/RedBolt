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
public partial class LimitVelocity : TemplateNode<LimitVelocityInstance, Vector3f, float, Vector3f>
{
    /// <summary>Construct LimitVelocity.</summary>
    public LimitVelocity() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "velocity", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "limit", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "out", ParticleGraphContainerType.Auto)]) { }
    /// <summary>Evaluate size required to place new node instance.</summary>
    public override int EvaluateInstanceSize() => IntPtr.Size;
    /// <summary>Create new instance.</summary>
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new LimitVelocityInstance(); instance.Init(this, layer); return instance; }

    /// <summary>Set Dampen.</summary>
    [Serialized("Dampen")]
    public float Dampen { get; set; }
}

public sealed class LimitVelocityInstance : LimitVelocity.InstanceBase
{
    public override void Update(in UpdateContext context, int numParticles, SparseSpan<Vector3f> velocity,
        SparseSpan<float> limit, SparseSpan<Vector3f> result)
    {
        var dampen = ((LimitVelocity)GetGraphNode()).Dampen;
        if (dampen <= 1e-6f || context.TimeStep < 1e-6f)
        {
            for (var i = 0; i < numParticles; ++i) result[i] = velocity[i];
        }
        else
        {
            var t = 1.0f - MathF.Pow(1.0f - dampen, 20.0f * context.TimeStep);
            for (var i = 0; i < numParticles; ++i)
            {
                var speed = velocity[i].Length;
                var limitValue = limit[i];
                if (speed > limitValue + 1e-6f)
                    result[i] = velocity[i] * (MathDefs.Lerp(speed, limitValue, t) / speed);
            }
        }
    }
}
