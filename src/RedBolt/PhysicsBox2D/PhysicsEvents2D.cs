// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Math;
using RedBolt.Scene;

/// <summary>2D physics contact point reported as part of a collision manifold.</summary>
public struct PhysicsContact2D
{
    /// <summary>Contact position in world space.</summary>
    public Vector2f Position;
    /// <summary>Contact normal in world space, pointing from body A toward body B.</summary>
    public Vector2f Normal;
    /// <summary>Signed contact distance. Negative values indicate penetration.</summary>
    public float Distance;
}

/// <summary>2D physics event payloads.</summary>
public static class PhysicsEvents2D
{
    public struct PhysicsBeginContact2DEvent
    {
        public PhysicsWorld2D World;
        public RigidBody2D BodyA;
        public RigidBody2D BodyB;
        public Node NodeA;
        public Node NodeB;
        public IReadOnlyList<PhysicsContact2D> Contacts;
        public CollisionShape2D ShapeA;
        public CollisionShape2D ShapeB;
    }

    public struct PhysicsUpdateContact2DEvent
    {
        public PhysicsWorld2D World;
        public RigidBody2D BodyA;
        public RigidBody2D BodyB;
        public Node NodeA;
        public Node NodeB;
        public IReadOnlyList<PhysicsContact2D> Contacts;
        public CollisionShape2D ShapeA;
        public CollisionShape2D ShapeB;
        public bool Enabled;
    }

    public struct PhysicsEndContact2DEvent
    {
        public PhysicsWorld2D World;
        public RigidBody2D BodyA;
        public RigidBody2D BodyB;
        public Node NodeA;
        public Node NodeB;
        public IReadOnlyList<PhysicsContact2D> Contacts;
        public CollisionShape2D ShapeA;
        public CollisionShape2D ShapeB;
    }

    public struct PhysicsPreStep2DEvent
    {
        public PhysicsWorld2D World;
        public float TimeStep;
    }

    public struct PhysicsPostStep2DEvent
    {
        public PhysicsWorld2D World;
        public float TimeStep;
    }

    public struct NodeBeginContact2DEvent
    {
        public RigidBody2D Body;
        public RigidBody2D OtherBody;
        public Node OtherNode;
        public IReadOnlyList<PhysicsContact2D> Contacts;
        public CollisionShape2D Shape;
        public CollisionShape2D OtherShape;
    }

    public struct NodeUpdateContact2DEvent
    {
        public RigidBody2D Body;
        public RigidBody2D OtherBody;
        public Node OtherNode;
        public IReadOnlyList<PhysicsContact2D> Contacts;
        public CollisionShape2D Shape;
        public CollisionShape2D OtherShape;
        public bool Enabled;
    }

    public struct NodeEndContact2DEvent
    {
        public RigidBody2D Body;
        public RigidBody2D OtherBody;
        public Node OtherNode;
        public IReadOnlyList<PhysicsContact2D> Contacts;
        public CollisionShape2D Shape;
        public CollisionShape2D OtherShape;
    }
}
