using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class WorldOriginTests
{
    private readonly HeadlessEngineFixture _fixture;

    public WorldOriginTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void World_origin_matches_cpp_attributes_and_defaults()
    {
        using var worldOrigin = new WorldOrigin();

        Assert.False(typeof(WorldOrigin).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(WorldOrigin)));

        Assert.Equal(WorldOrigin.MaxDistance, worldOrigin.GetMaxDistance());
        Assert.Equal(WorldOrigin.DefaultStep, worldOrigin.GetStep());
        Assert.Equal(1u, worldOrigin.GetUpdateX());
        Assert.Equal(0u, worldOrigin.GetUpdateY());
        Assert.Equal(1u, worldOrigin.GetUpdateZ());
        Assert.Equal(UpdateEvent.UseUpdate, worldOrigin.GetUpdateEventMask());

        using var maxDistance = worldOrigin.GetAttribute("Max Distance");
        using var step = worldOrigin.GetAttribute("Step");
        using var updateX = worldOrigin.GetAttribute("Update X");
        using var updateY = worldOrigin.GetAttribute("Update Y");
        using var updateZ = worldOrigin.GetAttribute("Update Z");
        Assert.Equal(WorldOrigin.MaxDistance, maxDistance.GetUInt());
        Assert.Equal(WorldOrigin.DefaultStep, step.GetUInt());
        Assert.Equal(true, updateX.GetObject());
        Assert.Equal(false, updateY.GetObject());
        Assert.Equal(true, updateZ.GetObject());
    }

    [Fact]
    public void World_origin_update_accessors_and_distance_logic_work()
    {
        using var scene = new SceneType();
        var node = scene.CreateChild("Origin");
        var worldOrigin = node.CreateComponent<WorldOrigin>();

        worldOrigin.SetMaxDistance(100u);
        worldOrigin.SetStep(32u);
        worldOrigin.SetUpdateX(0u);
        worldOrigin.SetUpdateY(1u);
        worldOrigin.SetUpdateZ(0u);
        node.SetPosition(new Vector3f(1000.0f, 1000.0f, 1000.0f));

        worldOrigin.Update(0.016f);
        Assert.Equal(Vector3i.Zero, scene.GetWorldOrigin());
        _fixture.Context.GetSubsystem<WorkQueue>()!.CompleteAll();

        Assert.Equal(new Vector3i(0, 992, 0), scene.GetWorldOrigin());
        Assert.Equal(8.0f, node.GetWorldPosition().Y);
        Assert.Equal(100u, worldOrigin.GetMaxDistance());
        Assert.Equal(32u, worldOrigin.GetStep());
        Assert.Equal(0u, worldOrigin.GetUpdateX());
        Assert.Equal(1u, worldOrigin.GetUpdateY());
        Assert.Equal(0u, worldOrigin.GetUpdateZ());
    }

    [Fact]
    public void World_origin_move_updates_root_transform_like_cpp()
    {
        using var scene = new SceneType();
        var child = scene.CreateChild("Child");
        child.SetPosition(new Vector3f(10.0f, 20.0f, 30.0f));

        scene.UpdateWorldOrigin(new Vector3i(1, 2, 3));

        Assert.Equal(Vector3f.Zero, scene.GetPosition());
        Assert.Equal(new Vector3f(9.0f, 18.0f, 27.0f), child.GetWorldPosition());
    }

    [Fact]
    public void World_origin_transform_conversions_match_cpp_contract()
    {
        using var scene = new SceneType();
        scene.SetWorldOrigin(new Vector3i(100, -20, 300));
        var transform = new Transform(
            new Vector3f(1.5f, 2.5f, 3.5f),
            new Quaternion(10.0f, 20.0f, 30.0f),
            new Vector3f(2.0f, 3.0f, 4.0f));

        var absolute = scene.ToAbsoluteWorldTransform(transform);
        Assert.Equal(new Vector3d(101.5, -17.5, 303.5), absolute.Position);
        Assert.Equal(transform.Rotation, absolute.Rotation);
        Assert.Equal(transform.Scale, absolute.Scale);

        var relative = scene.ToRelativeWorldTransform(absolute);
        Assert.Equal(transform, relative);
    }

    [Fact]
    public void World_origin_events_carry_the_same_payload_in_both_phases()
    {
        using var scene = new SceneType();
        SceneEvents.WorldOriginUpdateEvent? update = null;
        SceneEvents.WorldOriginPostUpdateEvent? postUpdate = null;
        scene.SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>((ref SceneEvents.WorldOriginUpdateEvent eventData) => update = eventData);
        scene.SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>((ref SceneEvents.WorldOriginPostUpdateEvent eventData) => postUpdate = eventData);

        var newOrigin = new Vector3i(10, 20, 30);
        scene.UpdateWorldOrigin(newOrigin);

        Assert.True(update.HasValue);
        Assert.True(postUpdate.HasValue);
        Assert.Same(scene, update.Value.Scene);
        Assert.Same(scene, postUpdate.Value.Scene);
        Assert.Equal(update.Value.OldOrigin, postUpdate.Value.OldOrigin);
        Assert.Equal(update.Value.NewOrigin, postUpdate.Value.NewOrigin);
        Assert.Equal(update.Value.Delta, postUpdate.Value.Delta);
    }
}
