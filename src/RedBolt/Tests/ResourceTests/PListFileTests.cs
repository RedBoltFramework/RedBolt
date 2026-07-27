using RedBolt;

using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PListFileTests
{
    [Fact]
    public void Value_clone_and_equality_match_value_semantics()
    {
        using var sourceValue = new PListValue(1.0f);
        using var original = new PListValue(new Dictionary<string, PListValue>
        {
            ["value"] = sourceValue
        });
        using var clone = Assert.IsType<PListValue>(original.Clone());

        Assert.Equal(original, clone);
        Assert.True(original == clone);
        clone.ConvertToValueMap()["value"].SetFloat(float.BitIncrement(1.0f));
        Assert.NotEqual(original, clone);
        Assert.True(original != clone);
    }

    [Fact]
    public void Value_uses_active_union_and_recursive_value_lifetime()
    {
        Assert.False(typeof(PListValue).IsSealed);

        var unionType = typeof(PListValue).GetNestedType("ValueUnion",
            System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(System.Runtime.InteropServices.LayoutKind.Explicit,
            unionType.StructLayoutAttribute!.Value);
        foreach (var field in unionType.GetFields(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            Assert.Equal(0, field.GetCustomAttributes(
                typeof(System.Runtime.InteropServices.FieldOffsetAttribute), false)
                .Cast<System.Runtime.InteropServices.FieldOffsetAttribute>().Single().Value);
        }

        using var scalar = new PListValue(42);
        var referenceValueField = typeof(PListValue).GetField("_referenceValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.Null(referenceValueField.GetValue(scalar));

        using var sourceValue = new PListValue("value");
        using var valueMap = new PListValue(new Dictionary<string, PListValue>
        {
            ["key"] = sourceValue
        });
        var storedValue = valueMap.ValueMap["key"];
        Assert.NotSame(sourceValue, storedValue);

        valueMap.CopyFrom(valueMap);
        Assert.Equal("value", valueMap.ValueMap["key"].String);

        valueMap.SetInt(7);
        Assert.Equal(PListValueType.None, storedValue.Type);
    }

    private readonly HeadlessEngineFixture _fixture;

    public PListFileTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RedBolt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Data"));
        return dir;
    }

    private PListFile LoadSample()
    {
        var tempDir = CreateTempDir();
        var path = Path.Combine(tempDir, "Data", "sample.plist");
        System.IO.File.WriteAllText(path,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>" +
            "<key>Health</key><integer>42</integer>" +
            "<key>Visible</key><true/>" +
            "<key>Speed</key><real>3.5</real>" +
            "<key>Rect</key><string>{{1,2},{3,4}}</string>" +
            "<key>Vector2</key><string>{5,6}</string>" +
            "<key>Vector3</key><string>{7,8,9}</string>" +
            "<key>Nested</key><dict><key>Name</key><string>RedBolt</string></dict>" +
            "<key>Items</key><array><string>A</string><integer>2</integer></array>" +
            "</dict></plist>");
        Resources.AddResourceDir(tempDir);
        return Resources.GetResource<PListFile>("Data/sample.plist");
    }

    [Fact]
    public void Scalar_loading_works()
    {
        var plist = LoadSample();
        Assert.Equal(42, plist.Root["Health"].Int);
        Assert.True(plist.Root["Visible"].Bool);
        Assert.True(MathDefs.Equals(plist.Root["Speed"].Float, 3.5f));
    }

    [Fact]
    public void Geometry_string_parsing_works()
    {
        var plist = LoadSample();
        Assert.Equal(new IntRect(1, 2, 4, 6), plist.Root["Rect"].IntRect);
        Assert.Equal(new Vector2i(5, 6), plist.Root["Vector2"].IntVector2);
        Assert.Equal(new Vector3i(7, 8, 9), plist.Root["Vector3"].IntVector3);
    }

    [Fact]
    public void Dict_and_array_loading_works()
    {
        var plist = LoadSample();
        Assert.Equal("RedBolt", plist.Root["Nested"].ValueMap["Name"].String);
        Assert.Equal(2, plist.Root["Items"].ValueVector.Count);
        Assert.Equal(2, plist.Root["Items"].ValueVector[1].Int);
    }

    [Fact]
    public void Copy_and_equality_work()
    {
        var plist = LoadSample();
        using var clone = new PListValue(plist.Root["Items"]);
        Assert.True(clone.Equals(plist.Root["Items"]));
    }
}
