//
// Copyright (c) 2008-2022 the Urho3D project.
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

namespace RedBolt.PhysicsJolt;

/// <summary>Collision shape type.</summary>
public enum ShapeType
{
    Box = 0,
    Sphere,
    StaticPlane,
    Cylinder,
    Capsule,
    Cone,
    TriangleMesh,
    ConvexHull,
    Terrain,
    GImpactMesh
}

/// <summary>Rigid body collision event signaling mode.</summary>
public enum CollisionEventMode
{
    Never = 0,
    Active,
    Always
}

/// <summary>Physics raycast result.</summary>
public struct PhysicsRaycastResult
{
    /// <summary>Hit rigid body.</summary>
    public RigidBody? Body;
    /// <summary>Hit position in world space.</summary>
    public Vector3f Position;
    /// <summary>Hit normal in world space.</summary>
    public Vector3f Normal;
    /// <summary>Distance from the ray origin.</summary>
    public float Distance;
    /// <summary>Normalized hit distance in the requested ray interval.</summary>
    public float HitFraction;
}

/// <summary>Physics contact point reported as part of a collision manifold.</summary>
public struct PhysicsContact
{
    /// <summary>Contact position in world space.</summary>
    public Vector3f Position;
    /// <summary>Contact normal in world space, pointing from body A toward body B.</summary>
    public Vector3f Normal;
    /// <summary>Signed contact distance. Negative values indicate penetration.</summary>
    public float Distance;
    /// <summary>Applied impulse. Jolt does not expose the solved impulse in contact callbacks.</summary>
    public float Impulse;
}
