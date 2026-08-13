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
public partial class Length : PatternMatchingNode
{
    private static readonly NodePattern[] LengthPatterns =
    [
        Helpers.MakePattern<Vector3f, float>(Update, new ParticleGraphTypedPin<Vector3f>("x"),
            new ParticleGraphTypedPin<float>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<Vector2f, float>(Update, new ParticleGraphTypedPin<Vector2f>("x"),
            new ParticleGraphTypedPin<float>(ParticleGraphPinFlag.Output, "out"))
    ];

    /// <summary>Construct Length.</summary>
    public Length() : base(LengthPatterns) { }

    public static void Update(in UpdateContext context, int count, SparseSpan<Vector3f> x, SparseSpan<float> output)
    { for (var i = 0; i < count; ++i) output[i] = x[i].Length; }
    public static void Update(in UpdateContext context, int count, SparseSpan<Vector2f> x, SparseSpan<float> output)
    { for (var i = 0; i < count; ++i) output[i] = x[i].Length; }
}
