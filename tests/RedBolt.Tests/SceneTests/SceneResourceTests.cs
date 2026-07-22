using System.Text;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneResourceTests
{
    private readonly HeadlessEngineFixture _fixture;

    public SceneResourceTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Scene_resource_matches_native_extensible_noncopyable_owned_shape()
    {
        Assert.False(typeof(SceneResource).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(SceneResource)));
        Assert.Single(typeof(SceneResource).GetConstructors());
        Assert.NotNull(typeof(SceneResource).GetConstructor(Type.EmptyTypes));
        Assert.Equal(typeof(SceneResource),
            typeof(SceneResource).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
    }

    [Fact]
    public void Engine_registers_scene_and_graphics_resource_factories()
    {
        Assert.True(_fixture.Context.GetReflection<Node>().HasObjectFactory());
        Assert.True(_fixture.Context.GetReflection<SceneResource>().HasObjectFactory());
        Assert.True(_fixture.Context.GetReflection<Camera>().HasObjectFactory());
        Assert.True(_fixture.Context.GetReflection<Material>().HasObjectFactory());
        Assert.True(_fixture.Context.GetReflection<Model>().HasObjectFactory());
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RedBolt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Data"));
        return dir;
    }

    [Fact]
    public void Removed_ids_can_be_reused()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Reused", 42);
        var camera = node.CreateComponent<Camera>(24);

        node.RemoveComponent(camera);
        scene.Root.RemoveChild(node);

        var nodeAgain = scene.CreateChild("Again", 42);
        var cameraAgain = nodeAgain.CreateComponent<Camera>(24);
        Assert.Equal(42u, nodeAgain.Id);
        Assert.Equal(24u, cameraAgain.Id);
    }

    [Fact]
    public void Json_roundtrip_works()
    {
        var resource = CreateSampleSceneResource();
        using var buffer = new VectorBuffer();
        Assert.True(resource.Save(buffer, InternalResourceFormat.Json));
        buffer.Seek(0);

        var loaded = new SceneResource { Name = "Scenes/Loaded.scene" };
        Assert.True(loaded.Load(buffer));

        AssertSceneLoaded(loaded.Scene, "VectorBuffer");
    }

    [Fact]
    public void Xml_roundtrip_works()
    {
        var resource = CreateSampleSceneResource();
        using var buffer = new VectorBuffer();
        Assert.True(resource.Save(buffer, InternalResourceFormat.Xml));
        buffer.Seek(0);

        var loaded = new SceneResource { Name = "Scenes/Loaded.scene" };
        Assert.True(loaded.Load(buffer));

        AssertSceneLoaded(loaded.Scene, "VectorBuffer");
    }

    [Fact]
    public void Binary_roundtrip_works()
    {
        var resource = CreateSampleSceneResource();
        using var buffer = new VectorBuffer();
        Assert.True(resource.Save(buffer, InternalResourceFormat.Binary));
        buffer.Seek(0);

        var loaded = new SceneResource { Name = "Scenes/Loaded.scene" };
        Assert.True(loaded.Load(buffer));

        AssertSceneLoaded(loaded.Scene);
    }

    [Fact]
    public void BeginLoad_parses_text_resource_before_EndLoad()
    {
        using var source = new MemoryBuffer("{ invalid json");
        using var resource = new SceneResource();

        Assert.False(resource.BeginLoad(source));
    }

    [Fact]
    public void Legacy_xml_scene_loads_through_scene_fallback()
    {
        using var source = new MemoryBuffer("<scene><node></node></scene>");
        using var resource = new SceneResource();

        Assert.True(resource.Load(source));
        Assert.Equal(1, resource.Scene.Root.GetNumChildren());
    }

    [Fact]
    public void Save_as_prefab_uses_prefab_resource_layout()
    {
        using var resource = CreateSampleSceneResource();
        using var buffer = new VectorBuffer();
        Assert.True(resource.Save(buffer, InternalResourceFormat.Json, asPrefab: true));
        buffer.Seek(0);

        using var prefab = new PrefabResource();
        Assert.True(prefab.Load(buffer));
        Assert.Equal("CameraNode", prefab.GetNodePrefab().GetNodeName());
    }

    [Fact]
    public void Reload_signals_can_cancel_scene_reload()
    {
        var resource = CreateSampleSceneResource();
        using var buffer = new VectorBuffer();
        Assert.True(resource.Save(buffer, InternalResourceFormat.Json));
        buffer.Seek(0);

        var beginCalled = false;
        var endSuccess = true;

        void OnReloadBegin(SceneResource _, ref bool cancelReload)
        {
            beginCalled = true;
            cancelReload = true;
        }

        void OnReloadEnd(SceneResource _, bool success) => endSuccess = success;

        resource.OnReloadBegin += OnReloadBegin;
        resource.OnReloadEnd += OnReloadEnd;
        try
        {
            Assert.True(resource.Load(buffer));
            Assert.True(beginCalled);
            Assert.False(endSuccess);
        }
        finally
        {
            resource.OnReloadBegin -= OnReloadBegin;
            resource.OnReloadEnd -= OnReloadEnd;
        }
    }

    [Fact]
    public void Resource_cache_loads_scene_resource_from_file()
    {
        var tempDir = CreateTempDir();
        var fileSystem = _fixture.Engine.GetSubsystem<VirtualFileSystem>()!;
        using var mount = new MountPointGuard(fileSystem.MountRoot());
        try
        {
            var path = Path.Combine(tempDir, "Data", "sample.scene.json");
            var resource = CreateSampleSceneResource();
            Assert.True(resource.SaveFile(new FileIdentifier(path), InternalResourceFormat.Json));
            Resources.AddResourceDir(tempDir);

            var loaded = Resources.GetResource<SceneResource>("Data/sample.scene.json");
            AssertSceneLoaded(loaded.Scene, "Data/sample.scene.json");
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static SceneResource CreateSampleSceneResource()
    {
        var resource = new SceneResource { Name = "Scenes/Sample.scene" };
        var scene = resource.Scene;
        scene.SetUpdateEnabled(true);
        scene.SetTimeScale(1.5f);
        scene.SetElapsedTime(2.0f);
        scene.SetAsyncLoadingMs(9);
        scene.SetFileName("Scenes/Sample.scene");

        var node = scene.CreateChild("CameraNode", 42);
        node.SetTransform(new Vector3f(1.0f, 2.0f, 3.0f), new Quaternion(45.0f, Vector3f.Up), new Vector3f(2.0f, 2.0f, 2.0f));
        node.SetTags(["editor", "camera"]);
        node.SetVar("Health", 100);

        var camera = node.CreateComponent<Camera>(24);
        camera.SetFov(70.0f);
        camera.SetOrthographic(true);
        camera.SetNearClip(0.5f);
        camera.SetFarClip(50.0f);

        node.CreateChild("Child", 55).SetPosition(new Vector3f(0.0f, 1.0f, 0.0f));
        return resource;
    }

    private static void AssertSceneLoaded(Scene scene, string expectedFileName = "")
    {
        Assert.True(scene.IsUpdateEnabled());
        Assert.True(MathDefs.Equals(scene.GetTimeScale(), 1.0f));
        Assert.True(MathDefs.Equals(scene.GetElapsedTime(), 0.0f));
        Assert.Equal(5, scene.GetAsyncLoadingMs());
        Assert.Equal(expectedFileName, scene.GetFileName());

        var node = scene.GetNode(42);
        Assert.NotNull(node);
        Assert.Equal("CameraNode", node.Name);
        Assert.False(node.IsTemporary);
        Assert.True(node.HasTag("editor"));
        Assert.Equal(100, node.GetVar("Health"));
        Assert.True(node.Position.ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.Equal(55u, node.GetChild("Child")?.Id);

        var camera = scene.GetComponent(24) as Camera;
        Assert.NotNull(camera);
        Assert.True(MathDefs.Equals(camera.GetFov(), 70.0f));
        Assert.True(camera.IsOrthographic());
        Assert.True(MathDefs.Equals(camera.GetNearClip(), 0.0f));
        Assert.True(MathDefs.Equals(camera.GetFarClip(), 50.0f));
    }
}
