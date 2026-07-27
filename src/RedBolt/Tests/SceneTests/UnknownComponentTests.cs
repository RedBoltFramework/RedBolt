using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class UnknownComponentTests
{
    [Fact]
    public void Placeholder_load_helpers_mark_temporary()
    {
        using var component = new UnknownComponent();
        using var binary = new MemoryBuffer(new byte[] { 1, 2, 3 });
        binary.Seek(1);

        Assert.False(typeof(UnknownComponent).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(UnknownComponent)));
        Assert.True(component.Load(binary));
        Assert.True(component.IsTemporary);
        Assert.Equal(1u, binary.Position);

        component.SetTemporary(false);
        using var xmlFile = new XMLFile { Name = "test.xml" };
        Assert.True(component.LoadXML(xmlFile.CreateRoot("unknown")));
        Assert.True(component.IsTemporary);

        component.SetTemporary(false);
        Assert.True(component.LoadJSON(new JSONValue(JSONValueType.Object)));
        Assert.True(component.IsTemporary);
    }

    [Fact]
    public void SceneResource_creates_unknown_component_placeholder()
    {
        if (!Context.Instance.IsReflected<Node>())
            Node.RegisterObject(Context.Instance);
        if (!Context.Instance.IsReflected<Scene>())
            Scene.RegisterObject(Context.Instance);
        if (!Context.Instance.IsReflected<UnknownComponent>())
            UnknownComponent.RegisterObject(Context.Instance);
        using var source = new SceneResource { Name = "Scenes/Test.scene" };
        using var prefab = new NodePrefab();
        var componentPrefab = new SerializablePrefab();
        componentPrefab.SetType("Missing.Component");
        componentPrefab.SetId((SerializableId)42);
        prefab.Components.Add(componentPrefab);
        using var reader = new PrefabReaderFromMemory(prefab);
        Assert.True(source.Scene.Load(reader));

        using var stream = new VectorBuffer();
        Assert.True(source.Save(stream, InternalResourceFormat.Json));
        stream.Seek(0);
        using var resource = new SceneResource { Name = "Scenes/Loaded.scene" };
        Assert.True(resource.Load(stream));

        var unknown = Assert.IsType<UnknownComponent>(resource.Scene.GetComponent(42));
        Assert.DoesNotContain(unknown.Attributes, attribute => attribute.Name == "Original Type Name");
        Assert.False(unknown.IsTemporary);
    }
}
