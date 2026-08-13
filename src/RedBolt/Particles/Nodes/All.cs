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

internal static class All
{
    internal static void RegisterGraphNodes(ParticleGraphSystem system)
    {
        system.Register<Add>();
        system.Register<Bounce>();
        system.Register<BurstTimer>();
        system.Register<Cone>();
        system.Register<Constant>();
        system.Register<Curve>();
        system.Register<Destroy>();
        system.Register<Divide>();
        system.Register<Emit>();
        system.Register<Expire>();
        system.Register<GetAttribute>();
        system.Register<GetUniform>();
        system.Register<Lerp>();
        system.Register<Make>();
        system.Register<Move>();
        system.Register<Multiply>();
        system.Register<Negate>();
        system.Register<Print>();
        system.Register<Random>();
        system.Register<RenderBillboard>();
        system.Register<SetAttribute>();
        system.Register<SetUniform>();
        system.Register<Slerp>();
        system.Register<Subtract>();
        system.Register<TimeStep>();
        system.Register<TimeStepScale>();
        system.Register<EffectTime>();
        system.Register<NormalizedEffectTime>();
        system.Register<RenderMesh>();
        system.Register<Sphere>();
        system.Register<Hemisphere>();
        system.Register<LimitVelocity>();
        system.Register<ApplyForce>();
        system.Register<Normalized>();
        system.Register<Length>();
        system.Register<Break>();
        system.Register<Circle>();
        system.Register<Box>();
        system.Register<Cast>();
        system.Register<Noise3D>();
        system.Register<CurlNoise3D>();
    }
}
