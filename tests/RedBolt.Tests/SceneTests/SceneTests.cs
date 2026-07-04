using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneTests
{
    [Fact]
    public void Root_initializes_serializable_state()
    {
        var scene = new Scene();
        Assert.NotEqual(0u, scene.Root.Id);
        Assert.True(scene.Root.GetNumAttributes() > 0);
    }

    [Fact]
    public void Empty_state_helpers_work()
    {
        var scene = new Scene();
        Assert.True(scene.IsEmpty());
        Assert.True(scene.IsEmpty(ignoreComponents: true));
    }

    [Fact]
    public void Update_time_helpers_work()
    {
        var scene = new Scene();
        scene.SetTimeScale(2.0f);
        scene.SetElapsedTime(1.0f);
        scene.Update(0.5f);

        scene.SetUpdateEnabled(false);
        scene.Update(1.0f);
        Assert.True(MathDefs.ApproximatelyEquivalent(scene.GetElapsedTime(), 2.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(scene.GetTimeScale(), 2.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(scene.GetEffectiveTimeScale(), 0.0f));
    }

    [Fact]
    public void Async_and_file_metadata_helpers_work()
    {
        var scene = new Scene();
        scene.SetUpdateEnabled(true);
        scene.SetAsyncLoadingMs(0);
        scene.SetFileName("Scenes/Test.scene");

        Assert.True(scene.IsUpdateEnabled());
        Assert.Equal(1, scene.GetAsyncLoadingMs());
        Assert.False(scene.IsAsyncLoading());
        Assert.True(MathDefs.ApproximatelyEquivalent(scene.GetAsyncProgress(), 1.0f));
        Assert.Equal("Scenes/Test.scene", scene.GetFileName());
        Assert.NotEqual(0u, scene.GetChecksum());
    }

    [Fact]
    public void Node_and_component_lookup_works()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Lookup", 99);
        node.AddTag("lookup");
        var camera = node.CreateComponent<Camera>(124);

        Assert.Same(node, scene.GetNode(99));
        Assert.Same(camera, scene.GetComponent(124));
        Assert.Same(node, scene.GetNodesWithTag("lookup").Single());
    }

    [Fact]
    public void Full_maps_contain_all_nodes_and_components()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Test");
        var camera = node.CreateComponent<Camera>();

        Assert.True(scene.GetAllNodes().ContainsKey(node.Id));
        Assert.True(scene.GetAllComponents().ContainsKey(camera.Id));
    }
}
