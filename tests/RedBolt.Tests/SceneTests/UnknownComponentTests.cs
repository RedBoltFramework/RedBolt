using System.Text;
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
        const string json = """
        {
          "UpdateEnabled": true,
          "TimeScale": 1.0,
          "ElapsedTime": 0.0,
          "AsyncLoadingMs": 5,
          "FileName": "Scenes/Test.scene",
          "Root": {
            "Id": 1,
            "Name": "",
            "Enabled": true,
            "Temporary": false,
            "Position": { "X": 0, "Y": 0, "Z": 0 },
            "Rotation": { "W": 1, "X": 0, "Y": 0, "Z": 0 },
            "Scale": { "X": 1, "Y": 1, "Z": 1 },
            "Tags": [],
            "Variables": [],
            "Components": [
              {
                "TypeName": "Missing.Component",
                "Id": 42,
                "Temporary": true,
                "Attributes": []
              }
            ],
            "Children": []
          }
        }
        """;

        var resource = new SceneResource("Scenes/Test.scene");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        Assert.True(resource.Load(stream));

        var unknown = Assert.IsType<UnknownComponent>(resource.GetScene().GetComponent(42));
        Assert.Equal("Missing.Component", unknown.GetOriginalTypeName());
        Assert.True(unknown.IsTemporary);
    }
}
