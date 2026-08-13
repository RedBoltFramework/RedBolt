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
public partial class Make : PatternMatchingNode
{
    private static readonly NodePattern[] MakePatterns =
    [
        Helpers.MakePattern<float, float, Vector2f>(Update,
            new ParticleGraphTypedPin<float>("x"), new ParticleGraphTypedPin<float>("y"),
            new ParticleGraphTypedPin<Vector2f>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<int, int, Vector2i>(Update,
            new ParticleGraphTypedPin<int>("x"), new ParticleGraphTypedPin<int>("y"),
            new ParticleGraphTypedPin<Vector2i>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<float, float, float, Vector3f>(Update,
            new ParticleGraphTypedPin<float>("x"), new ParticleGraphTypedPin<float>("y"),
            new ParticleGraphTypedPin<float>("z"),
            new ParticleGraphTypedPin<Vector3f>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<Vector3f, Quaternion, Vector3f, Matrix3x4>(Update,
            new ParticleGraphTypedPin<Vector3f>("translation"), new ParticleGraphTypedPin<Quaternion>("rotation"),
            new ParticleGraphTypedPin<Vector3f>("scale"),
            new ParticleGraphTypedPin<Matrix3x4>(ParticleGraphPinFlag.Output, "out")),
        Helpers.MakePattern<float, float, float, Quaternion>(Update,
            new ParticleGraphTypedPin<float>("pitch"), new ParticleGraphTypedPin<float>("yaw"),
            new ParticleGraphTypedPin<float>("roll"),
            new ParticleGraphTypedPin<Quaternion>(ParticleGraphPinFlag.Output, "out"))
    ];

    /// <summary>Construct Make.</summary>
    public Make() : base(MakePatterns) { }

    public static void Update(in UpdateContext context, int count, SparseSpan<float> x, SparseSpan<float> y,
        SparseSpan<Vector2f> output)
    { for (var i = 0; i < count; ++i) output[i] = new Vector2f(x[i], y[i]); }
    public static void Update(in UpdateContext context, int count, SparseSpan<int> x, SparseSpan<int> y,
        SparseSpan<Vector2i> output)
    { for (var i = 0; i < count; ++i) output[i] = new Vector2i(x[i], y[i]); }
    public static void Update(in UpdateContext context, int count, SparseSpan<float> x, SparseSpan<float> y,
        SparseSpan<float> z, SparseSpan<Vector3f> output)
    { for (var i = 0; i < count; ++i) output[i] = new Vector3f(x[i], y[i], z[i]); }
    public static void Update(in UpdateContext context, int count, SparseSpan<Vector3f> translation,
        SparseSpan<Quaternion> rotation, SparseSpan<Vector3f> scale, SparseSpan<Matrix3x4> output)
    { for (var i = 0; i < count; ++i) output[i] = new Matrix3x4(translation[i], rotation[i], scale[i]); }
    public static void Update(in UpdateContext context, int count, SparseSpan<float> pitch, SparseSpan<float> yaw,
        SparseSpan<float> roll, SparseSpan<Quaternion> output)
    { for (var i = 0; i < count; ++i) output[i] = new Quaternion(pitch[i], yaw[i], roll[i]); }
}
