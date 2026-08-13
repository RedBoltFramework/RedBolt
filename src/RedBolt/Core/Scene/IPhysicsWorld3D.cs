//
// Copyright (c) 2026 RedBolt contributors.
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

namespace RedBolt.Scene;

using RedBolt.Math;

/// <summary>Backend-independent physics simulation world.</summary>
public interface IPhysicsWorld3D : IFixedUpdateSource
{
    /// <summary>Return or set world gravity.</summary>
    Vector3f Gravity { get; set; }

    /// <summary>Return or set whether automatic physics updates are enabled.</summary>
    bool UpdateEnabled { get; set; }

    /// <summary>Step the simulation forward.</summary>
    void Update(float timeStep);

    /// <summary>Sweep a sphere through the world and return the closest hit.</summary>
    void SphereCast(ref PhysicsSphereCastResult result, Ray ray, float radius, float maxDistance);
}

/// <summary>Backend-independent sphere cast result.</summary>
public struct PhysicsSphereCastResult
{
    /// <summary>Whether the sphere hit anything.</summary>
    public bool HasHit;
    /// <summary>Hit position in world space.</summary>
    public Vector3f Position;
    /// <summary>Hit normal in world space.</summary>
    public Vector3f Normal;
    /// <summary>Distance from the ray origin.</summary>
    public float Distance;
}
