namespace RedBolt.Tests;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.JoltPhysics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltMathTypeCompatibilityTests
{
    public JoltMathTypeCompatibilityTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void RedBolt_types_match_Jolt_projected_abi_layout()
    {
        Assert.Equal(12, Unsafe.SizeOf<Vector3f>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Vector3f>(nameof(Vector3f.X)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<Vector3f>(nameof(Vector3f.Y)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<Vector3f>(nameof(Vector3f.Z)));

        Assert.Equal(12, Unsafe.SizeOf<Vector3fAbi>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Vector3fAbi>(nameof(Vector3fAbi.X)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<Vector3fAbi>(nameof(Vector3fAbi.Y)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<Vector3fAbi>(nameof(Vector3fAbi.Z)));

        Assert.Equal(16, Unsafe.SizeOf<Quaternion>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.W)));
        Assert.Equal((IntPtr)4, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.X)));
        Assert.Equal((IntPtr)8, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.Y)));
        Assert.Equal((IntPtr)12, Marshal.OffsetOf<Quaternion>(nameof(Quaternion.Z)));

        Assert.Equal(24, Unsafe.SizeOf<Ray>());
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<Ray>(nameof(Ray.Origin)));
        Assert.Equal((IntPtr)12, Marshal.OffsetOf<Ray>(nameof(Ray.Direction)));
    }

    [Fact]
    public void RedBolt_math_values_round_trip_through_Jolt()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();

        var vector = new Vector3f(1.25f, -2.5f, 3.75f);
        world.World.SetGravity(vector);
        Assert.Equal(vector, world.World.GetGravity());

        var rotation = new Quaternion(0.5f, -0.25f, 0.125f, 0.75f).Normalized;
        using var settings = new BodyCreationSettings
        {
            MPosition = vector,
            MRotation = rotation
        };
        Assert.Equal(vector, settings.MPosition);
        Assert.Equal(rotation, settings.MRotation);

        var floorNode = scene.CreateChild("Floor");
        floorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(4.0f, 1.0f, 4.0f));
        floorNode.CreateComponent<RigidBody>();

        var ray = new Ray(new Vector3f(0.0f, 2.0f, 0.0f), Vector3f.Down)
        {
            Direction = new Vector3f(0.0f, -4.0f, 0.0f)
        };
        var hit = new JoltRayCastResult { BodyId = uint.MaxValue, Fraction = 1.0f };
        Assert.True(world.NarrowPhaseQuery.CastRay(in ray, ref hit));
        Assert.InRange(hit.Fraction, 0.35f, 0.4f);
    }
}
