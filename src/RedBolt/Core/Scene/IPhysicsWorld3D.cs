// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

using RedBolt.Math;

/// <summary>Backend-independent physics simulation world.</summary>
public interface IPhysicsWorld3D : IFixedUpdateSource
{
    /// <summary>Return or set world gravity.</summary>
    Vector3f Gravity { get; set; }

    /// <summary>Return or set whether automatic physics updates are enabled.</summary>
    bool UpdateEnabled { get; set; }

    /// <summary>Return or set whether the world is advanced only by explicit updates.</summary>
    bool ManualUpdate { get; set; }

    /// <summary>Return or set whether transforms are interpolated between fixed updates.</summary>
    bool Interpolation { get; set; }

    /// <summary>Return the fixed simulation frequency.</summary>
    int Fps { get; }

    /// <summary>Step the simulation forward.</summary>
    void Update(float timeStep);

    /// <summary>Advance an explicitly synchronized set of fixed updates.</summary>
    void CustomUpdate(uint numSteps, float fixedTimeStep, float overtime,
        long? networkFrame = null, uint synchronizedStepOffset = 0);

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
