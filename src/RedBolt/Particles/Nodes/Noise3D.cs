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
public partial class Noise3D : TemplateNode<Noise3DInstance, Vector3f, float>
{
    /// <summary>Construct Noise3D.</summary>
    public Noise3D() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "x", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "out", ParticleGraphContainerType.Auto)]) { }
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new Noise3DInstance(); instance.Init(this, layer); return instance; }
}

public sealed class Noise3DInstance : Noise3D.InstanceBase
{
    public Noise3DInstance() => Noise = new PerlinNoise(RandomEngine.GetDefaultEngine());
    public override void Update(in UpdateContext context, int numParticles,
        SparseSpan<Vector3f> x, SparseSpan<float> output)
    { for (var i = 0; i < numParticles; ++i) output[i] = Generate(x[i]); }
    public float Generate(in Vector3f position) => (float)Noise.GetDouble(position.X, position.Y, position.Z);
    public readonly PerlinNoise Noise;
}
