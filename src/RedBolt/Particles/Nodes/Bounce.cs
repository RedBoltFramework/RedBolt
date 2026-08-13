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
using RedBolt.Scene;

[Reflected]
public partial class Bounce : TemplateNode<BounceInstance, Vector3f, Vector3f, Vector3f, Vector3f>
{
    /// <summary>Construct Bounce.</summary>
    public Bounce() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "position", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "velocity", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "newPosition", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Output, "newVelocity", ParticleGraphContainerType.Auto)]) { }
    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new BounceInstance(); instance.Init(this, layer); return instance; }

    [Serialized("Dampen")] public float Dampen { get; set; }
    [Serialized("BounceFactor")] public float BounceFactor { get; set; }
}

public sealed class BounceInstance : Bounce.InstanceBase
{
    public override void Update(in UpdateContext context, int numParticles, SparseSpan<Vector3f> position,
        SparseSpan<Vector3f> velocity, SparseSpan<Vector3f> newPosition, SparseSpan<Vector3f> newVelocity)
    {
        var node = GetNode();
        var physics = GetPhysicsWorld(GetScene());

        for (var i = 0; i < numParticles; ++i)
        {
            newPosition[i] = position[i];
            newVelocity[i] = velocity[i];
            var resultPosition = newPosition[i];
            var resultVelocity = newVelocity[i];
            RayCastAndBounce(in context, node, physics, ref resultPosition, ref resultVelocity);
            newPosition[i] = resultPosition;
            newVelocity[i] = resultVelocity;
        }
    }

    public void RayCastAndBounce(in UpdateContext context, Node? node, IPhysicsWorld? physics,
        ref Vector3f position, ref Vector3f velocity)
    {
        if (physics is not null)
        {
            var bounce = (Bounce)GetGraphNode();
            velocity += physics.Gravity * context.TimeStep;
            var offset = velocity * context.TimeStep;
            var distance = offset.Length;
            if (distance > 1e-6f)
            {
                var worldPosition = node!.LocalToWorld(position);
                const float radius = 0.1f;
                var result = default(PhysicsSphereCastResult);
                physics.SphereCast(ref result,
                    new Ray(worldPosition, offset * (1.0f / distance)), radius, distance);
                if (result.HasHit)
                {
                    worldPosition = worldPosition.Lerp(result.Position, 0.99f);
                    position = node.WorldToLocal(worldPosition);
                    var bounceScale = (1.0f + bounce.BounceFactor) * velocity.DotProduct(result.Normal);
                    velocity -= result.Normal * bounceScale;
                    if (bounce.Dampen > 0.0f)
                        velocity *= 1.0f - bounce.Dampen;
                }
                else
                    position += offset;
            }
            return;
        }
        position += velocity * context.TimeStep;
    }

    private static IPhysicsWorld? GetPhysicsWorld(Scene? scene)
    {
        if (scene is null)
            return null;

        foreach (var component in scene.Components)
        {
            if (component is IPhysicsWorld physics)
                return physics;
        }
        return null;
    }
}
