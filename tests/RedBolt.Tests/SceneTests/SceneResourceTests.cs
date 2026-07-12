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
        using var buffer = new MemoryStream();
        Assert.True(resource.Save(buffer, SceneResourceFormat.Json));
        buffer.Position = 0;

        var loaded = new SceneResource("Scenes/Loaded.scene");
        Assert.True(loaded.Load(buffer));

        AssertSceneLoaded(loaded.GetScene());
    }

    [Fact]
    public void Xml_roundtrip_works()
    {
        var resource = CreateSampleSceneResource();
        using var buffer = new MemoryStream();
        Assert.True(resource.Save(buffer, SceneResourceFormat.Xml));
        buffer.Position = 0;

        var loaded = new SceneResource("Scenes/Loaded.scene");
        Assert.True(loaded.Load(buffer));

        AssertSceneLoaded(loaded.GetScene());
    }

    [Fact]
    public void Binary_roundtrip_works()
    {
        var resource = CreateSampleSceneResource();
        using var buffer = new MemoryStream();
        Assert.True(resource.Save(buffer, SceneResourceFormat.Binary));
        buffer.Position = 0;

        var loaded = new SceneResource("Scenes/Loaded.scene");
        Assert.True(loaded.Load(buffer));

        AssertSceneLoaded(loaded.GetScene());
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
            Assert.True(resource.SaveFile(path, SceneResourceFormat.Json));
            Resources.AddResourceDir(tempDir);

            var loaded = Resources.GetResource<SceneResource>("Data/sample.scene.json");
            AssertSceneLoaded(loaded.GetScene());
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static SceneResource CreateSampleSceneResource()
    {
        var resource = new SceneResource("Scenes/Sample.scene");
        var scene = resource.GetScene();
        scene.SetUpdateEnabled(true);
        scene.SetTimeScale(1.5f);
        scene.SetElapsedTime(2.0f);
        scene.SetAsyncLoadingMs(9);
        scene.SetFileName("Scenes/Sample.scene");

        var node = scene.CreateChild("CameraNode", 42, temporary: true);
        node.SetTransform(new Vector3f(1.0f, 2.0f, 3.0f), Quaternion.FromAngleAxis(45.0f, Vector3f.Up), new Vector3f(2.0f, 2.0f, 2.0f));
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

    private static void AssertSceneLoaded(Scene scene)
    {
        Assert.True(scene.IsUpdateEnabled());
        Assert.True(MathDefs.ApproximatelyEquivalent(scene.GetTimeScale(), 1.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(scene.GetElapsedTime(), 2.0f));
        Assert.Equal(9, scene.GetAsyncLoadingMs());
        Assert.Equal("Scenes/Sample.scene", scene.GetFileName());

        var node = scene.GetNode(42);
        Assert.NotNull(node);
        Assert.Equal("CameraNode", node.Name);
        Assert.True(node.IsTemporary);
        Assert.True(node.HasTag("editor"));
        Assert.Equal(100, node.GetVar("Health"));
        Assert.True(node.Position.ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.Equal(55u, node.GetChild("Child")?.Id);

        var camera = scene.GetComponent(24) as Camera;
        Assert.NotNull(camera);
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetFov(), 70.0f));
        Assert.True(camera.IsOrthographic());
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetNearClip(), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(camera.GetFarClip(), 50.0f));
    }
}
