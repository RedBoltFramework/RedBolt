namespace RedBolt.Tests;

public sealed class WorldOriginTests
{
    [Fact]
    public void World_origin_matches_cpp_attributes_and_defaults()
    {
        var worldOrigin = new WorldOrigin();

        Assert.Equal(WorldOrigin.MaxDistance, worldOrigin.GetMaxDistance());
        Assert.Equal(WorldOrigin.DefaultStep, worldOrigin.GetStep());
        Assert.Equal(1u, worldOrigin.GetUpdateX());
        Assert.Equal(0u, worldOrigin.GetUpdateY());
        Assert.Equal(1u, worldOrigin.GetUpdateZ());
        Assert.Equal(UpdateEvent.UseUpdate, worldOrigin.GetUpdateEventMask());

        Assert.Equal(WorldOrigin.MaxDistance, worldOrigin.GetAttribute("Max Distance"));
        Assert.Equal(WorldOrigin.DefaultStep, worldOrigin.GetAttribute("Step"));
        Assert.Equal(true, worldOrigin.GetAttribute("Update X"));
        Assert.Equal(false, worldOrigin.GetAttribute("Update Y"));
        Assert.Equal(true, worldOrigin.GetAttribute("Update Z"));
    }

    [Fact]
    public void World_origin_update_accessors_and_distance_logic_work()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Origin");
        var worldOrigin = node.CreateComponent<WorldOrigin>();

        worldOrigin.SetMaxDistance(100u);
        worldOrigin.SetStep(32u);
        worldOrigin.SetUpdateX(0u);
        worldOrigin.SetUpdateY(1u);
        worldOrigin.SetUpdateZ(0u);
        node.SetPosition(new Vector3f(1000.0f, 1000.0f, 1000.0f));

        worldOrigin.Update(0.016f);

        Assert.Equal(new Vector3i(0, 992, 0), scene.GetWorldOrigin());
        Assert.Equal(8.0f, node.GetWorldPosition().Y);
        Assert.Equal(100u, worldOrigin.GetMaxDistance());
        Assert.Equal(32u, worldOrigin.GetStep());
        Assert.Equal(0u, worldOrigin.GetUpdateX());
        Assert.Equal(1u, worldOrigin.GetUpdateY());
        Assert.Equal(0u, worldOrigin.GetUpdateZ());
    }
}
