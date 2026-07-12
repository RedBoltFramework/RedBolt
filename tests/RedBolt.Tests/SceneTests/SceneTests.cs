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
    public void Structural_scene_events_and_registries_follow_mutations()
    {
        var scene = new Scene();
        var receiver = new object();
        var addedNodes = 0;
        var removedNodes = 0;
        var addedComponents = 0;
        var removedComponents = 0;
        var addedTags = 0;
        var removedTags = 0;

        using var nodeAdded = Context.Instance.SubscribeToEvent<SceneEvents.NodeAddedEvent>(receiver, scene, (ref SceneEvents.NodeAddedEvent _) => ++addedNodes);
        using var nodeRemoved = Context.Instance.SubscribeToEvent<SceneEvents.NodeRemovedEvent>(receiver, scene, (ref SceneEvents.NodeRemovedEvent _) => ++removedNodes);
        using var componentAdded = Context.Instance.SubscribeToEvent<SceneEvents.ComponentAddedEvent>(receiver, scene, (ref SceneEvents.ComponentAddedEvent _) => ++addedComponents);
        using var componentRemoved = Context.Instance.SubscribeToEvent<SceneEvents.ComponentRemovedEvent>(receiver, scene, (ref SceneEvents.ComponentRemovedEvent _) => ++removedComponents);
        using var tagAdded = Context.Instance.SubscribeToEvent<SceneEvents.NodeTagAddedEvent>(receiver, scene, (ref SceneEvents.NodeTagAddedEvent _) => ++addedTags);
        using var tagRemoved = Context.Instance.SubscribeToEvent<SceneEvents.NodeTagRemovedEvent>(receiver, scene, (ref SceneEvents.NodeTagRemovedEvent _) => ++removedTags);

        var node = scene.CreateChild("Tracked", 99);
        node.AddTag("tracked");
        var camera = node.CreateComponent<Camera>(124);

        Assert.Same(node, scene.GetNode(99));
        Assert.Same(camera, scene.GetComponent(124));
        Assert.Same(node, scene.GetNodesWithTag("tracked").Single());

        node.RemoveComponent(camera);
        node.RemoveTag("tracked");
        scene.Root.RemoveChild(node);

        Assert.Null(scene.GetNode(99));
        Assert.Null(scene.GetComponent(124));
        Assert.Empty(scene.GetNodesWithTag("tracked"));
        Assert.Equal(1, addedNodes);
        Assert.Equal(1, removedNodes);
        Assert.Equal(1, addedComponents);
        Assert.Equal(1, removedComponents);
        Assert.Equal(1, addedTags);
        Assert.Equal(1, removedTags);
    }

    [Fact]
    public void Forced_update_events_run_while_scene_updates_are_disabled()
    {
        var scene = new Scene();
        var receiver = new object();
        var forcedUpdate = 0;
        var forcedPostUpdate = 0;
        var regularUpdate = 0;

        using var forcedUpdateSubscription =
            Context.Instance.SubscribeToEvent<SceneEvents.SceneForcedUpdateEvent>(receiver, scene, (ref SceneEvents.SceneForcedUpdateEvent _) => ++forcedUpdate);
        using var forcedPostUpdateSubscription =
            Context.Instance.SubscribeToEvent<SceneEvents.SceneForcedPostUpdateEvent>(receiver, scene, (ref SceneEvents.SceneForcedPostUpdateEvent _) => ++forcedPostUpdate);
        using var updateSubscription =
            Context.Instance.SubscribeToEvent<SceneEvents.SceneUpdateEvent>(receiver, scene, (ref SceneEvents.SceneUpdateEvent _) => ++regularUpdate);

        scene.SetUpdateEnabled(false);
        scene.Update(-0.25f);

        Assert.Equal(1, forcedUpdate);
        Assert.Equal(1, forcedPostUpdate);
        Assert.Equal(0, regularUpdate);
        Assert.True(MathDefs.ApproximatelyEquivalent(scene.GetElapsedTime(), 0.0f));
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

    [Fact]
    public void Lightmap_resources_can_be_added_reloaded_and_reset()
    {
        var resourceName = $"Lightmaps/Test-{Guid.NewGuid():N}.png";
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        var texture = new Texture2D(resourceName);
        Assert.True(cache.AddManualResource(texture));

        try
        {
            var scene = new Scene();
            scene.AddLightmap(resourceName);

            Assert.Equal(1u, scene.GetNumLightmaps());
            Assert.Same(texture, scene.GetLightmapTexture(0));
            Assert.Null(scene.GetLightmapTexture(1));

            scene.ReloadLightmaps();
            Assert.Same(texture, scene.GetLightmapTexture(0));

            scene.ResetLightmaps();
            Assert.Equal(0u, scene.GetNumLightmaps());
            Assert.Null(scene.GetLightmapTexture(0));
        }
        finally
        {
            cache.ReleaseResource(resourceName, force: true);
        }
    }

    [Fact]
    public void Dispose_clears_scene_nodes_and_components()
    {
        var scene = new Scene();
        scene.CreateChild("Temporary").CreateComponent<Camera>();

        scene.Dispose();

        Assert.Single(scene.GetAllNodes());
        Assert.Same(scene.Root, scene.GetNode(scene.Root.Id));
        Assert.Empty(scene.GetAllComponents());
    }

    [Fact]
    public void Free_node_and_component_ids_advance_independently()
    {
        var scene = new Scene();

        var firstNodeId = scene.GetFreeNodeID();
        var secondNodeId = scene.GetFreeNodeID();
        var firstComponentId = scene.GetFreeComponentID();
        var secondComponentId = scene.GetFreeComponentID();

        Assert.NotEqual(firstNodeId, secondNodeId);
        Assert.NotEqual(firstComponentId, secondComponentId);
    }

    [Fact]
    public void Update_event_configuration_preserves_forced_event_names()
    {
        var scene = new Scene();
        scene.SetUpdateEvents(new StringVector
        {
            "@CustomForcedUpdate",
            "CustomUpdate",
            string.Empty
        });

        var events = scene.GetUpdateEvents();

        Assert.Equal(["@CustomForcedUpdate", "CustomUpdate", string.Empty], events);
    }

    [Fact]
    public void Component_indexes_track_matching_components()
    {
        var scene = new Scene();
        scene.CreateComponentIndex<Camera>();

        var node = scene.CreateChild("Indexed");
        Assert.False(scene.CreateComponentIndex(typeof(Light).GetClassName()));
        var camera = node.CreateComponent<Camera>();
        var light = node.CreateComponent<Light>();

        Assert.Contains(camera, scene.GetComponentIndex<Camera>());
        Assert.Empty(scene.GetComponentIndex<Light>());

        node.RemoveComponent(camera);
        Assert.Empty(scene.GetComponentIndex<Camera>());
        node.RemoveComponent(light);
    }

    [Fact]
    public void Required_package_files_ignore_empty_packages()
    {
        var scene = new Scene();
        using var package = new PackageFile();

        scene.AddRequiredPackageFile(null);
        scene.AddRequiredPackageFile(package);

        Assert.Empty(scene.GetRequiredPackageFiles());

        scene.ClearRequiredPackageFiles();
        Assert.Empty(scene.GetRequiredPackageFiles());
    }
}
