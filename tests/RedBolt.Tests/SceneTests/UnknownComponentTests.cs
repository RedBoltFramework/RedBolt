using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class UnknownComponentTests
{
    [Fact]
    public void Placeholder_load_helpers_mark_temporary()
    {
        var component = new UnknownComponent();
        using var stream = new MemoryStream();
        Assert.True(component.Load(stream));
        Assert.True(component.IsTemporary);
        Assert.True(component.LoadXML(new XMLFile("test.xml").CreateRoot("unknown")));
        Assert.True(component.LoadJSON(new JSONValue(JSONValueType.Object)));
        Assert.True(component.IsTemporary);
    }

    [Fact]
    public void SceneResource_creates_unknown_component_placeholder()
    {
        var source = new SceneResource("Scenes/Test.scene");
        var prefab = new NodePrefab
        {
            Components =
            {
                new SerializablePrefab
                {
                    TypeName = "Missing.Component",
                    Id = 42,
                    Temporary = false
                }
            }
        };
        var reader = new PrefabReaderFromMemory(prefab);
        Assert.True(source.GetScene().Root.Load(reader));

        using var stream = new MemoryStream();
        Assert.True(source.Save(stream, SceneResourceFormat.Json));
        stream.Position = 0;
        var resource = new SceneResource("Scenes/Loaded.scene");
        Assert.True(resource.Load(stream));

        var unknown = Assert.IsType<UnknownComponent>(resource.GetScene().GetComponent(42));
        Assert.Equal("Missing.Component", unknown.GetOriginalTypeName());
        Assert.False(unknown.IsTemporary);
    }
}
