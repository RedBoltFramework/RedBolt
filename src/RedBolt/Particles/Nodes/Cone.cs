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
public partial class Cone : TemplateNode<ConeInstance, Vector3f, Vector3f>
{
    /// <summary>Construct Cone.</summary>
    public Cone() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "position", ParticleGraphContainerType.Span),
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "velocity", ParticleGraphContainerType.Span)])
    {
        Rotation = new Quaternion();
    }
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new ConeInstance(); instance.Init(this, layer); return instance; }

    [Serialized("Radius")] public float Radius { get; set; }
    [Serialized("Radius Thickness")] public float RadiusThickness { get; set; }
    [Serialized("Angle")] public float Angle { get; set; }
    [Serialized("Length")] public float Length { get; set; }
    [Serialized("Translation")] public Vector3f Translation { get; set; }
    private static object? GetRotationDefault() => new Quaternion();

    [Serialized("Rotation", DefaultSource = nameof(GetRotationDefault))] public Quaternion Rotation { get; set; }
    [Serialized("Scale")] public Vector3f Scale { get; set; }
    [Serialized("From")] public int From { get; set; }
}

public sealed class ConeInstance : Cone.InstanceBase
{
    public override void Update(in UpdateContext context, int numParticles,
        SparseSpan<Vector3f> position, SparseSpan<Vector3f> velocity)
    {
        var cone = (Cone)GetGraphNode();
        var transform = new Matrix3x4(cone.Translation, cone.Rotation, cone.Scale);
        var directionTransform = transform.Matrix3;
        for (var i = 0; i < numParticles; ++i)
        {
            Generate(out var localPosition, out var localVelocity);
            position[i] = transform * localPosition;
            velocity[i] = directionTransform * localVelocity;
        }
    }

    public void Generate(out Vector3f position, out Vector3f velocity)
    {
        var cone = (Cone)GetGraphNode();
        var angle = MathDefs.Random(360.0f);
        var radius = MathDefs.Sqrt(MathDefs.Random()) * MathDefs.Sin(global::System.Math.Clamp(cone.Angle, 0.0f, 89.999f));
        var height = MathDefs.Sqrt(1.0f - radius * radius);
        var cosine = MathDefs.Cos(angle);
        var sine = MathDefs.Sin(angle);
        var direction = new Vector3f(cosine * radius, sine * radius, height);
        var r = cone.Radius;
        if (cone.RadiusThickness > 0.0f && (EmitFrom)cone.From != EmitFrom.Surface)
            r *= 1.0f - MathDefs.Random() * cone.RadiusThickness;
        switch ((EmitFrom)cone.From)
        {
            case EmitFrom.Base:
                velocity = direction;
                position = new Vector3f(cosine * r, sine * r, 0.0f);
                break;
            default:
                velocity = direction;
                position = direction * MathDefs.Random(cone.Length) + new Vector3f(cosine * r, sine * r, 0.0f);
                break;
        }
    }
}
