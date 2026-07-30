namespace RedBolt.Tests;

using RedBolt.Core;
using RedBolt.JoltPhysics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JoltPhysicsTests
{
    public JoltPhysicsTests(HeadlessEngineFixture fixture)
    {
        Physics.RegisterPhysicsLibrary(fixture.Context);
    }

    [Fact]
    public void Dynamic_body_falls_collides_and_can_be_raycast()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        world.ManualUpdate = true;
        world.Interpolation = false;

        var floorNode = scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.CreateComponent<CollisionShape>().SetBox(new Vector3f(20.0f, 1.0f, 20.0f));
        floorNode.CreateComponent<RigidBody>();

        var boxNode = scene.CreateChild("Box");
        boxNode.SetPosition(new Vector3f(0.0f, 4.0f, 0.0f));
        boxNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = boxNode.CreateComponent<RigidBody>();
        body.Mass = 1.0f;

        var collisionStarts = 0;
        var collisions = 0;
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionStartEvent>(
            (ref PhysicsEvents.PhysicsCollisionStartEvent _) => ++collisionStarts);
        scene.SubscribeToEvent<PhysicsEvents.PhysicsCollisionEvent>(
            (ref PhysicsEvents.PhysicsCollisionEvent _) => ++collisions);

        for (var i = 0; i < 180; ++i)
            world.Update(1.0f / 60.0f);

        Assert.InRange(boxNode.WorldPosition.Y, 0.45f, 0.65f);
        Assert.NotNull(body.JoltBodyId);
        Assert.True(collisionStarts > 0);
        Assert.True(collisions > 0);

        Assert.True(world.TryRaycastSingle(
            new Ray(new Vector3f(0.0f, 5.0f, 0.0f), Vector3f.Down), 10.0f, out var hit));
        Assert.Same(body, hit.Body);
        Assert.InRange(hit.Position.Y, 0.9f, 1.1f);
    }

    [Fact]
    public void Physics_components_publish_reflection_attributes_and_native_access()
    {
        using var scene = new Scene();
        var world = scene.CreateComponent<PhysicsWorld>();
        var node = scene.CreateChild("Body");
        node.CreateComponent<CollisionShape>().SetSphere(1.0f);
        var body = node.CreateComponent<RigidBody>();

        Assert.NotNull(world.World);
        Assert.NotNull(world.BodyInterface);
        Assert.NotNull(world.NarrowPhaseQuery);
        Assert.NotNull(body.JoltBodyId);

        var worldAttributes = Context.Instance.GetReflection(ObjectReflection<PhysicsWorld>.TypeId)!.Attributes!;
        Assert.Contains(worldAttributes, attribute => attribute.Name == "Gravity");
        Assert.Contains(worldAttributes, attribute => attribute.Name == "Physics FPS");
        Assert.Contains(worldAttributes, attribute => attribute.Name == "Interpolation");

        var bodyAttributes = Context.Instance.GetReflection(ObjectReflection<RigidBody>.TypeId)!.Attributes!;
        Assert.Contains(bodyAttributes, attribute => attribute.Name == "Mass");
        Assert.Contains(bodyAttributes, attribute => attribute.Name == "Collision Layer");
        Assert.Contains(bodyAttributes, attribute => attribute.Name == "Collision Mask");
    }
}
