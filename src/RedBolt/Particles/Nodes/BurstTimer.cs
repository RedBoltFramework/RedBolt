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

[Reflected]
public partial class BurstTimer : TemplateNode<BurstTimerInstance, float, float>
{
    /// <summary>Construct BurstTimer.</summary>
    public BurstTimer() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "count", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "out", ParticleGraphContainerType.Auto)]) { }

    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new BurstTimerInstance(); instance.Init(this, layer); return instance; }

    [Serialized("Delay")]
    public float Delay { get; set; }
    [Serialized("Interval")]
    public float Interval { get; set; }
    [Serialized("Cycles")]
    public int Cycles { get; set; }
}

public sealed class BurstTimerInstance : BurstTimer.InstanceBase
{
    public override void Init(ParticleGraphNode node, ParticleGraphLayerInstance layer)
    {
        base.Init(node, layer);
        var burstTimer = (BurstTimer)node;
        TimeToBurst = burstTimer.Delay;
        Counter = unchecked((uint)burstTimer.Cycles);
    }

    public override void Update(in UpdateContext context, int numParticles,
        SparseSpan<float> count, SparseSpan<float> output)
    {
        TimeToBurst -= context.TimeStep;
        if (Counter > 0 && TimeToBurst <= 0.0f)
        {
            // Can't use iterator here as it may use ScalarSpan with infinite iterator.
            for (var i = 0; i < numParticles; ++i) output[i] = count[i];
            TimeToBurst += ((BurstTimer)GetGraphNode()).Interval;
            --Counter;
        }
        else
        {
            for (var i = 0; i < numParticles; ++i) output[i] = 0.0f;
        }
    }

    public float TimeToBurst;
    public uint Counter;
}
