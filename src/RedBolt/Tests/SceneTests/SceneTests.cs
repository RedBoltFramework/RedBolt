namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneTests
{
    [Fact]
    public void Scene_is_root_node_and_async_progress_is_value_type()
    {
        var scene = new Scene();
        Assert.IsAssignableFrom<Node>(scene);
        Assert.Same(scene, scene.GetScene());
        Assert.Null(typeof(Scene).GetProperty("Root"));
        Assert.True(typeof(AsyncProgress).IsValueType);
        Assert.NotEqual(0u, scene.Id);
        Assert.True(scene.GetNumAttributes() > 0);
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
        Assert.True(MathDefs.Equals(scene.GetElapsedTime(), 2.0f));
        Assert.True(MathDefs.Equals(scene.GetTimeScale(), 2.0f));
        Assert.True(MathDefs.Equals(scene.GetEffectiveTimeScale(), 0.0f));
    }

    [Fact]
    public void Structural_scene_events_and_registries_follow_mutations()
    {
        var scene = new Scene();
        using var receiver = new TestEventReceiver();
        var addedNodes = 0;
        var removedNodes = 0;
        var addedComponents = 0;
        var removedComponents = 0;
        var addedTags = 0;
        var removedTags = 0;

        Context.Instance.SubscribeToEvent<SceneEvents.NodeAddedEvent>(receiver, scene, (ref SceneEvents.NodeAddedEvent _) => ++addedNodes);
        Context.Instance.SubscribeToEvent<SceneEvents.NodeRemovedEvent>(receiver, scene, (ref SceneEvents.NodeRemovedEvent _) => ++removedNodes);
        Context.Instance.SubscribeToEvent<SceneEvents.ComponentAddedEvent>(receiver, scene, (ref SceneEvents.ComponentAddedEvent _) => ++addedComponents);
        Context.Instance.SubscribeToEvent<SceneEvents.ComponentRemovedEvent>(receiver, scene, (ref SceneEvents.ComponentRemovedEvent _) => ++removedComponents);
        Context.Instance.SubscribeToEvent<SceneEvents.NodeTagAddedEvent>(receiver, scene, (ref SceneEvents.NodeTagAddedEvent _) => ++addedTags);
        Context.Instance.SubscribeToEvent<SceneEvents.NodeTagRemovedEvent>(receiver, scene, (ref SceneEvents.NodeTagRemovedEvent _) => ++removedTags);

        var node = scene.CreateChild("Tracked", 99);
        node.AddTag("tracked");
        var camera = node.CreateComponent<Camera>(124);

        Assert.Same(node, scene.GetNode(99));
        Assert.Same(camera, scene.GetComponent(124));
        Assert.Same(node, scene.GetNodesWithTag("tracked").Single());

        node.RemoveComponent(camera);
        node.RemoveTag("tracked");
        scene.RemoveChild(node);

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
        using var receiver = new TestEventReceiver();
        var forcedUpdate = 0;
        var forcedPostUpdate = 0;
        var regularUpdate = 0;

        receiver.SubscribeToEvent<SceneEvents.SceneForcedUpdateEvent>(
            scene, (ref SceneEvents.SceneForcedUpdateEvent _) => ++forcedUpdate);
        receiver.SubscribeToEvent<SceneEvents.SceneForcedPostUpdateEvent>(
            scene, (ref SceneEvents.SceneForcedPostUpdateEvent _) => ++forcedPostUpdate);
        receiver.SubscribeToEvent<SceneEvents.SceneUpdateEvent>(
            scene, (ref SceneEvents.SceneUpdateEvent _) => ++regularUpdate);

        scene.SetUpdateEnabled(false);
        scene.Update(-0.25f);

        Assert.Equal(1, forcedUpdate);
        Assert.Equal(1, forcedPostUpdate);
        Assert.Equal(0, regularUpdate);
        Assert.True(MathDefs.Equals(scene.GetElapsedTime(), 0.0f));
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
        Assert.True(MathDefs.Equals(scene.GetAsyncProgress(), 1.0f));
        Assert.Equal("Scenes/Test.scene", scene.GetFileName());
        Assert.NotEqual(0u, scene.GetChecksum());
    }

    [Fact]
    public void Threaded_update_state_follows_work_queue()
    {
        var scene = new Scene();
        var workQueue = scene.GetSubsystem<WorkQueue>()!;

        scene.BeginThreadedUpdate();
        Assert.Equal(workQueue.IsMultithreaded(), scene.IsThreadedUpdate());

        scene.EndThreadedUpdate();
        Assert.False(scene.IsThreadedUpdate());
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
        var texture = new Texture2D { Name = resourceName };
        Assert.True(cache.AddManualResource(texture));

        try
        {
            var scene = new Scene();
            scene.AddLightmap(resourceName);

            Assert.Equal(1, scene.GetNumLightmaps());
            Assert.Same(texture, scene.GetLightmapTexture(0));
            Assert.Null(scene.GetLightmapTexture(1));

            scene.ReloadLightmaps();
            Assert.Same(texture, scene.GetLightmapTexture(0));

            scene.ResetLightmaps();
            Assert.Equal(0, scene.GetNumLightmaps());
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
        var rootId = scene.Id;

        scene.Dispose();

        Assert.Single(scene.GetAllNodes());
        Assert.Same(scene, scene.GetNode(rootId));
        Assert.Null(scene.Scene);
        Assert.Equal(0u, scene.Id);
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
    public void Scene_archive_roundtrip_preserves_hierarchy()
    {
        var scene = new Scene();
        var child = scene.CreateChild("Archived");
        child.SetPosition(new Vector3f(1, 2, 3));
        child.SetVar("score", 42);
        child.CreateComponent<Camera>().SetFov(73);

        var json = new TestJSONOutputArchive();
        using (json.OpenUnorderedBlock("scene"))
            scene.SerializeInBlock(json);

        var loadedScene = new Scene();
        var input = new TestJSONInputArchive(json.ToJsonString());
        using (input.OpenUnorderedBlock("scene"))
            loadedScene.SerializeInBlock(input);

        var loadedChild = loadedScene.GetChild("Archived");
        Assert.NotNull(loadedChild);
        Assert.True(loadedChild!.Position.ApproximatelyEquivalent(new Vector3f(1, 2, 3)));
        Assert.Equal(42, loadedChild.GetVar("score"));
        Assert.True(MathDefs.Equals(loadedChild.GetComponent<Camera>()!.GetFov(), 73));
    }

    [Fact]
    public void Scene_load_entrypoints_read_xml_archive_and_json_node_formats()
    {
        var source = new Scene();
        source.CreateChild("Loaded").CreateComponent<Camera>().SetFov(71);

        using var xmlFile = new XMLFile { Name = string.Empty };
        using var xmlArchive = new XMLOutputArchive(
            Context.Instance, xmlFile.GetOrCreateRoot(SceneResource.GetXmlRootName()), xmlFile);
        using (xmlArchive.OpenUnorderedBlock(SceneResource.GetXmlRootName()))
            source.SerializeInBlock(xmlArchive);

        var loadedFromXml = new Scene();
        Assert.True(loadedFromXml.LoadXML(xmlFile.GetRoot()));
        Assert.NotNull(loadedFromXml.GetChild("Loaded"));

        var json = new JSONValue();
        Assert.True(source.SaveJSON(json));

        var loadedFromJson = new Scene();
        Assert.True(loadedFromJson.LoadJSON(json));
        Assert.NotNull(loadedFromJson.GetChild("Loaded"));
    }

    [Fact]
    public void Scene_stream_load_entrypoints_read_xml_resource_and_json_node_formats()
    {
        var sourceResource = new SceneResource { Name = string.Empty };
        sourceResource.Scene.CreateChild("Loaded").CreateComponent<Camera>().SetFov(79);

        using var xml = new VectorBuffer();
        Assert.True(sourceResource.Save(xml, InternalResourceFormat.Xml));
        xml.Seek(0);

        var loadedFromXml = new Scene();
        Assert.True(loadedFromXml.Load(xml));
        Assert.True(loadedFromXml.GetChild("Loaded") is not null, $"count={loadedFromXml.GetNumChildren()}");

        var jsonValue = new JSONValue();
        Assert.True(sourceResource.Scene.SaveJSON(jsonValue));
        using var json = new MemoryBuffer(System.Text.Encoding.UTF8.GetBytes(jsonValue.ToString()));
        json.Seek(0);

        var loadedFromJson = new Scene();
        Assert.True(loadedFromJson.Load(json));
        Assert.True(loadedFromJson.GetChild("Loaded") is not null, $"count={loadedFromJson.GetNumChildren()}");
    }

    [Fact]
    public void Node_xml_roundtrip_preserves_hierarchy()
    {
        var source = new Scene();
        source.CreateChild("Loaded").CreateComponent<Camera>().SetFov(83);

        using var stream = new VectorBuffer();
        Assert.True(source.SaveXML(stream));
        stream.Seek(0);

        var xml = new XMLFile { Name = string.Empty };
        Assert.True(xml.Load(stream));

        var loaded = new Scene();
        Assert.True(loaded.LoadXML(xml.GetRoot()));
        Assert.Equal(83.0f, loaded.GetChild("Loaded")?.GetComponent<Camera>()?.GetFov());
    }

    [Fact]
    public void Scene_binary_roundtrip_preserves_hierarchy()
    {
        var source = new Scene();
        var child = source.CreateChild("Loaded");
        child.SetPosition(new Vector3f(1, 2, 3));
        child.CreateComponent<Camera>().SetFov(87);

        using var stream = new VectorBuffer();
        Assert.True(source.Save(stream), "save");
        stream.Seek(0);

        var loaded = new Scene();
        Assert.True(loaded.Load(stream), "load");

        var loadedChild = loaded.GetChild("Loaded");
        Assert.NotNull(loadedChild);
        Assert.True(loadedChild!.Position.ApproximatelyEquivalent(new Vector3f(1, 2, 3)));
        Assert.Equal(87.0f, loadedChild.GetComponent<Camera>()?.GetFov());
    }

    [Fact]
    public void Scene_legacy_xml_and_json_roundtrip_preserve_hierarchy()
    {
        var source = new Scene();
        source.CreateChild("Loaded").CreateComponent<Camera>().SetFov(89);

        using var xml = new VectorBuffer();
        Assert.True(source.SaveXML(xml));
        xml.Seek(0);
        var loadedFromXml = new Scene();
        Assert.True(loadedFromXml.LoadXML(xml));
        Assert.Equal(89.0f, loadedFromXml.GetChild("Loaded")?.GetComponent<Camera>()?.GetFov());

        using var json = new VectorBuffer();
        Assert.True(source.SaveJSON(json));
        json.Seek(0);
        var loadedFromJson = new Scene();
        Assert.True(loadedFromJson.LoadJSON(json));
        Assert.Equal(89.0f, loadedFromJson.GetChild("Loaded")?.GetComponent<Camera>()?.GetFov());

        var xmlBuffer = new VectorBuffer();
        Assert.True(source.SaveXML(xmlBuffer));
        xmlBuffer.Seek(0);
        var loadedFromXmlBuffer = new Scene();
        Assert.True(loadedFromXmlBuffer.LoadXML(xmlBuffer));
        Assert.Equal(89.0f, loadedFromXmlBuffer.GetChild("Loaded")?.GetComponent<Camera>()?.GetFov());

        var jsonBuffer = new VectorBuffer();
        Assert.True(source.SaveJSON(jsonBuffer));
        jsonBuffer.Seek(0);
        var loadedFromJsonBuffer = new Scene();
        Assert.True(loadedFromJsonBuffer.LoadJSON(jsonBuffer));
        Assert.Equal(89.0f, loadedFromJsonBuffer.GetChild("Loaded")?.GetComponent<Camera>()?.GetFov());
    }

    [Fact]
    public void Scene_instantiation_rewrites_ids_and_applies_transform()
    {
        var source = new Scene();
        var sourceChild = source.CreateChild("Source");
        sourceChild.CreateComponent<Camera>(17).SetFov(91);

        var position = new Vector3f(4, 5, 6);
        var rotation = new Quaternion(10, 20, 30);

        var json = new JSONValue();
        Assert.True(source.SaveJSON(json));
        var instantiatedFromJson = new Scene().InstantiateJSON(json, position, rotation);
        Assert.NotNull(instantiatedFromJson);
        Assert.NotEqual(source.Id, instantiatedFromJson!.Id);
        Assert.True(instantiatedFromJson.Position.ApproximatelyEquivalent(position));
        Assert.Equal(91.0f, instantiatedFromJson.GetChild("Source")?.GetComponent<Camera>()?.GetFov());

        var xmlFile = new XMLFile { Name = string.Empty };
        Assert.True(source.SaveXML(xmlFile.CreateRoot("node")));
        var instantiatedFromXml = new Scene().InstantiateXML(xmlFile.GetRoot(), position, rotation);
        Assert.NotNull(instantiatedFromXml);
        Assert.True(instantiatedFromXml!.Position.ApproximatelyEquivalent(position));

        var xmlBuffer = new VectorBuffer();
        Assert.True(source.SaveXML(xmlBuffer));
        xmlBuffer.Seek(0);
        var instantiatedFromXmlBuffer = new Scene().InstantiateXML(xmlBuffer, position, rotation);
        Assert.NotNull(instantiatedFromXmlBuffer);

        var binary = new VectorBuffer();
        Assert.True(sourceChild.Save(binary));
        binary.Seek(0);
        var instantiatedFromBinary = new Scene().Instantiate(binary, position, rotation);
        Assert.NotNull(instantiatedFromBinary);
        Assert.True(instantiatedFromBinary!.Position.ApproximatelyEquivalent(position));

        var jsonBuffer = new VectorBuffer();
        Assert.True(source.SaveJSON(jsonBuffer));
        jsonBuffer.Seek(0);
        var instantiatedFromJsonBuffer = new Scene().InstantiateJSON(jsonBuffer, position, rotation);
        Assert.NotNull(instantiatedFromJsonBuffer);
    }

    [Fact]
    public void Scene_clear_resets_state_and_id_generators()
    {
        var scene = new Scene();
        scene.CreateChild("Before", 100).CreateComponent<Camera>(200);
        scene.SetFileName("Scenes/Before.scene");
        scene.SetUpdateEvents(new StringVector { "CustomUpdate" });

        scene.Clear();

        Assert.True(scene.IsEmpty());
        Assert.Equal(string.Empty, scene.Name);
        Assert.Equal(string.Empty, scene.FileName);
        Assert.Equal(0u, scene.Checksum);
        Assert.Equal(Scene.DefaultUpdateEvents, scene.GetUpdateEvents());

        Assert.Equal(2u, scene.CreateChild("After").Id);
        Assert.Equal(1u, scene.CreateComponent<Camera>().Id);
    }

    [Fact]
    public void Scene_binary_async_load_initializes_and_stops_progress()
    {
        using var saved = new VectorBuffer();
        var sourceScene = new Scene();
        Assert.True(sourceScene.Save(saved));

        var file = new VectorBuffer(saved.Buffer.ToArray());
        var scene = new Scene();

        Assert.True(scene.LoadAsync(file, LoadMode.SceneAndResources));
        Assert.True(scene.IsAsyncLoading());
        Assert.Equal(LoadMode.SceneAndResources, scene.GetAsyncLoadMode());
        Assert.True(MathDefs.Equals(scene.GetAsyncProgress(), 1.0f));

        scene.Update(0.0f);

        Assert.False(scene.IsAsyncLoading());

        scene.StopAsyncLoading();

        Assert.False(scene.IsAsyncLoading());
        Assert.True(MathDefs.Equals(scene.GetAsyncProgress(), 1.0f));
    }

    [Fact]
    public void Scene_xml_async_load_initializes_and_finishes_progress()
    {
        using var saved = new VectorBuffer();
        var sourceScene = new Scene();
        Assert.True(sourceScene.SaveXML(saved));

        var file = new VectorBuffer(saved.Buffer.ToArray());
        var scene = new Scene();

        Assert.True(scene.LoadAsyncXML(file, LoadMode.SceneAndResources));
        Assert.True(scene.IsAsyncLoading());
        Assert.Equal(LoadMode.SceneAndResources, scene.GetAsyncLoadMode());

        scene.Update(0.0f);

        Assert.False(scene.IsAsyncLoading());
        Assert.True(MathDefs.Equals(scene.GetAsyncProgress(), 1.0f));
    }

    [Fact]
    public void Scene_json_async_load_initializes_and_finishes_progress()
    {
        using var saved = new VectorBuffer();
        var sourceScene = new Scene();
        Assert.True(sourceScene.SaveJSON(saved));

        var file = new VectorBuffer(saved.Buffer.ToArray());
        var scene = new Scene();

        Assert.True(scene.LoadAsyncJSON(file, LoadMode.SceneAndResources));
        Assert.True(scene.IsAsyncLoading());
        Assert.Equal(LoadMode.SceneAndResources, scene.GetAsyncLoadMode());

        scene.Update(0.0f);

        Assert.False(scene.IsAsyncLoading());
        Assert.True(MathDefs.Equals(scene.GetAsyncProgress(), 1.0f));
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

    [Fact]
    public void Reflected_attribute_adds_scene_factory_reflection()
    {
        var context = new Context();

        context.RegisterAttributedType<Scene>();

        Assert.True(context.IsReflected<Scene>());
        using var created = Assert.IsType<Scene>(context.CreateObject(ObjectReflection<Scene>.TypeId));
        Assert.Equal(10, created.NumAttributes);
        using var name = created.GetAttribute("Name");
        using var timeScale = created.GetAttribute("Time Scale");
        using var elapsedTime = created.GetAttribute("Elapsed Time");
        using var nextNodeId = created.GetAttribute("Next Node ID");
        using var nextComponentId = created.GetAttribute("Next Component ID");
        using var variables = created.GetAttribute("Variables");
        using var updateEvents = created.GetAttribute("Update Events");
        using var worldOrigin = created.GetAttribute("World Origin");
        using var elapsedTimeWrap = created.GetAttribute("Elapsed Time Wrap");
        using var lightmaps = created.GetAttribute("Lightmaps");
        Assert.Equal(string.Empty, name.GetObject());
        Assert.Equal(1.0f, timeScale.GetObject());
        Assert.Equal(0.0f, elapsedTime.GetObject());
        Assert.Equal(2u, nextNodeId.GetUInt());
        Assert.Equal(1u, nextComponentId.GetUInt());
        Assert.IsType<StringVariantMap>(variables.GetObject());
        Assert.Equal(Scene.DefaultUpdateEvents, updateEvents.GetObject());
        Assert.Equal(IntVector3.Zero, worldOrigin.GetObject());
        Assert.Equal(new Vector3d(1_000_000_000_000.0, 1024.0, 1.0), elapsedTimeWrap.GetObject());
        Assert.Equal(ObjectReflection<Texture2D>.TypeId, Assert.IsType<ResourceRefList>(lightmaps.GetObject()).Type);
    }
}
