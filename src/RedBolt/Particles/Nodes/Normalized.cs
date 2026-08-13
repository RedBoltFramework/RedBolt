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

using RedBolt.Core;
using RedBolt.Math;

[Reflected]
public partial class Normalized : PatternMatchingNode
{
    private static readonly NodePattern[] NormalizedPatterns =
    [
        Helpers.MakePattern<Vector3f, Vector3f>(Update, new ParticleGraphTypedPin<Vector3f>("x"),
            new ParticleGraphTypedPin<Vector3f>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<Vector2f, Vector2f>(Update, new ParticleGraphTypedPin<Vector2f>("x"),
            new ParticleGraphTypedPin<Vector2f>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<Quaternion, Quaternion>(Update, new ParticleGraphTypedPin<Quaternion>("x"),
            new ParticleGraphTypedPin<Quaternion>(ParticleGraphPinFlag.Output, "out"))
    ];

    /// <summary>Construct Normalized.</summary>
    public Normalized() : base(NormalizedPatterns) { }

    public static void Update(in UpdateContext context, int count, SparseSpan<Vector3f> x, SparseSpan<Vector3f> output)
    { for (var i = 0; i < count; ++i) output[i] = x[i].Normalized; }
    public static void Update(in UpdateContext context, int count, SparseSpan<Vector2f> x, SparseSpan<Vector2f> output)
    { for (var i = 0; i < count; ++i) output[i] = x[i].Normalized; }
    public static void Update(in UpdateContext context, int count, SparseSpan<Quaternion> x, SparseSpan<Quaternion> output)
    { for (var i = 0; i < count; ++i) output[i] = x[i].Normalized; }
}
