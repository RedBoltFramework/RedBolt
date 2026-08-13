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

[Reflected]
public partial class Cast : PatternMatchingNode
{
    private static readonly NodePattern[] CastPatterns =
    [
        Helpers.MakePattern<float, int>(Update, new ParticleGraphTypedPin<float>("x"),
            new ParticleGraphTypedPin<int>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<int, float>(Update, new ParticleGraphTypedPin<int>("x"),
            new ParticleGraphTypedPin<float>(ParticleGraphPinFlag.Output, "out"))
    ];

    /// <summary>Construct Cast.</summary>
    public Cast() : base(CastPatterns) { }

    public static void Update(in UpdateContext context, int count, SparseSpan<float> x, SparseSpan<int> output)
    { for (var i = 0; i < count; ++i) output[i] = (int)x[i]; }
    public static void Update(in UpdateContext context, int count, SparseSpan<int> x, SparseSpan<float> output)
    { for (var i = 0; i < count; ++i) output[i] = x[i]; }
}
