// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Bindings.Box2D;
using RedBolt.Math;

internal static class PhysicsUtils2D
{
    public const float DegreesToRadians = MathF.PI / 180.0f;
    public const float RadiansToDegrees = 180.0f / MathF.PI;

    public static B2Vec2 ToB2(Vector2f value)
    {
        return new B2Vec2 { X = value.X, Y = value.Y };
    }

    public static Vector2f ToVector2(B2Vec2 value) => new(value.X, value.Y);

    public static long GetKey(B2BodyId id) =>
        ((long)id.World0 << 48) | ((long)id.Generation << 32) | (uint)id.Index1;

    public static long GetKey(B2ShapeId id) =>
        ((long)id.World0 << 48) | ((long)id.Generation << 32) | (uint)id.Index1;

    public static void SetBodyA(B2DistanceJointDef def, B2BodyId id) =>
        (def.BodyIdAIndex1, def.BodyIdAGeneration, def.BodyIdAWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyB(B2DistanceJointDef def, B2BodyId id) =>
        (def.BodyIdBIndex1, def.BodyIdBGeneration, def.BodyIdBWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyA(B2MotorJointDef def, B2BodyId id) =>
        (def.BodyIdAIndex1, def.BodyIdAGeneration, def.BodyIdAWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyB(B2MotorJointDef def, B2BodyId id) =>
        (def.BodyIdBIndex1, def.BodyIdBGeneration, def.BodyIdBWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyA(B2MouseJointDef def, B2BodyId id) =>
        (def.BodyIdAIndex1, def.BodyIdAGeneration, def.BodyIdAWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyB(B2MouseJointDef def, B2BodyId id) =>
        (def.BodyIdBIndex1, def.BodyIdBGeneration, def.BodyIdBWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyA(B2PrismaticJointDef def, B2BodyId id) =>
        (def.BodyIdAIndex1, def.BodyIdAGeneration, def.BodyIdAWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyB(B2PrismaticJointDef def, B2BodyId id) =>
        (def.BodyIdBIndex1, def.BodyIdBGeneration, def.BodyIdBWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyA(B2RevoluteJointDef def, B2BodyId id) =>
        (def.BodyIdAIndex1, def.BodyIdAGeneration, def.BodyIdAWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyB(B2RevoluteJointDef def, B2BodyId id) =>
        (def.BodyIdBIndex1, def.BodyIdBGeneration, def.BodyIdBWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyA(B2WeldJointDef def, B2BodyId id) =>
        (def.BodyIdAIndex1, def.BodyIdAGeneration, def.BodyIdAWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyB(B2WeldJointDef def, B2BodyId id) =>
        (def.BodyIdBIndex1, def.BodyIdBGeneration, def.BodyIdBWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyA(B2WheelJointDef def, B2BodyId id) =>
        (def.BodyIdAIndex1, def.BodyIdAGeneration, def.BodyIdAWorld0) = (id.Index1, id.Generation, id.World0);
    public static void SetBodyB(B2WheelJointDef def, B2BodyId id) =>
        (def.BodyIdBIndex1, def.BodyIdBGeneration, def.BodyIdBWorld0) = (id.Index1, id.Generation, id.World0);
}
