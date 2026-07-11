using System.Text;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class JsonXmlResourceTests
{
    private readonly HeadlessEngineFixture _fixture;

    public JsonXmlResourceTests(HeadlessEngineFixture fixture)
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
    public void JSONFile_loads_from_file()
    {
        var tempDir = CreateTempDir();
        try
        {
            var jsonPath = Path.Combine(tempDir, "Data", "config.json");
            File.WriteAllText(jsonPath, "{\"name\":\"redbolt\",\"value\":7}");
            Resources.AddResourceDir(tempDir);

            var jsonFile = Resources.GetResource<JSONFile>("Data/config.json");
            Assert.Equal(7, jsonFile.GetRoot().Get("value").GetInt());
            Assert.Equal(jsonPath, jsonFile.AbsoluteFileName);
            Assert.Contains("redbolt", jsonFile.ToString(""), StringComparison.Ordinal);
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void JSONFile_resource_parsing_and_indentation_match_rbfx()
    {
        var json = new JSONFile();
        Assert.True(json.FromString("{/* comment */\"outer\":{\"value\":1,},}"));
        var original = json.ToString(string.Empty);

        Assert.False(json.FromString("invalid"));
        Assert.Equal(original, json.ToString(string.Empty));
        Assert.Contains("\n----\"value\"", json.ToString("--"), StringComparison.Ordinal);
        Assert.False(JSONFile.ParseJSON("{\"value\":1,}", out _));
    }

    [Fact]
    public void JSONFile_archive_callbacks_roundtrip_values()
    {
        var json = new JSONFile();
        var outputValue = 42;
        Assert.True(json.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("object");
            archive.Serialize("value", ref outputValue);
        }));

        var inputValue = 0;
        Assert.True(json.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("object");
            archive.Serialize("value", ref inputValue);
        }));
        Assert.Equal(outputValue, inputValue);
    }

    [Fact]
    public void XMLFile_loads_from_file()
    {
        var tempDir = CreateTempDir();
        try
        {
            var xmlPath = Path.Combine(tempDir, "Data", "config.xml");
            File.WriteAllText(xmlPath, "<root><child value=\"7\" /></root>");
            Resources.AddResourceDir(tempDir);

            var xmlFile = Resources.GetResource<XMLFile>("Data/config.xml");
            Assert.Equal(7, xmlFile.GetRoot("root").GetChild("child").GetInt("value"));
            Assert.Equal(xmlPath, xmlFile.AbsoluteFileName);
            Assert.Contains("child", xmlFile.ToString(""), StringComparison.Ordinal);
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseJSON_from_string()
    {
        Assert.True(JSONFile.ParseJSON("{\"ok\":true}", out var json));
        Assert.True(json.Get("ok").GetBool());
    }

    [Fact]
    public void XMLFile_FromString_creates_root()
    {
        var xml = new XMLFile("test.xml");
        Assert.True(xml.FromString("<newRoot />"));
        Assert.Equal("newRoot", xml.GetOrCreateRoot("newRoot").Name);
    }

    [Fact]
    public void JSONValue_array_and_object_helpers_work()
    {
        Assert.True(JSONFile.ParseJSON("{}", out var json));
        json.Set("tags", new JSONValue([new JSONValue("engine"), new JSONValue("rbfx")]));
        Assert.Equal(2u, json.Get("tags").Size());
        Assert.True(json.Contains("tags"));
        Assert.Contains("engine", json.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void JSONValue_erase_removes_key()
    {
        Assert.True(JSONFile.ParseJSON("{}", out var json));
        json.Set("enabled", new JSONValue(true));
        json.Erase("enabled");
        Assert.False(json.Contains("enabled"));
    }

    [Fact]
    public void JSONValue_complex_variants_roundtrip()
    {
        var map = new StringVariantMap
        {
            ["enabled"] = new Variant(true),
            ["values"] = new Variant(new VariantVector { new Variant(7), new Variant("rbfx") })
        };
        var json = new JSONValue();
        json.SetVariant(new Variant(map));

        var loaded = json.GetVariant().GetStringVariantMap();
        Assert.Equal(new Variant(true), loaded["enabled"]);
        Assert.Equal(new Variant(7), loaded["values"].GetVariantVector()[0]);
        Assert.Equal(new Variant("rbfx"), loaded["values"].GetVariantVector()[1]);
    }

    [Fact]
    public void JSONValue_replace_recursively_merges_objects()
    {
        Assert.True(JSONFile.ParseJSON("{\"outer\":{\"keep\":1,\"replace\":2}}", out var value));
        Assert.True(JSONFile.ParseJSON("{\"outer\":{\"replace\":3,\"add\":4}}", out var replacement));

        value.ReplaceRecursively(replacement);

        Assert.Equal(1, value.Get("outer").Get("keep").GetInt());
        Assert.Equal(3, value.Get("outer").Get("replace").GetInt());
        Assert.Equal(4, value.Get("outer").Get("add").GetInt());
    }

    [Fact]
    public void XMLElement_hierarchy_helpers_work()
    {
        var xml = new XMLFile("test.xml");
        xml.FromString("<newRoot />");
        var root = xml.GetOrCreateRoot("newRoot");
        var childA = root.CreateChild("child");
        childA.SetVector3("position", new Vector3f(1.0f, 2.0f, 3.0f));
        root.CreateChild("sibling");
        Assert.True(root.HasChild("child"));
        Assert.Equal("sibling", childA.GetNext("sibling").Name);
        Assert.Equal("newRoot", childA.GetParent().Name);
    }

    [Fact]
    public void XMLElement_typed_attributes_work()
    {
        var xml = new XMLFile("test.xml");
        xml.FromString("<newRoot />");
        var child = xml.GetOrCreateRoot("newRoot").CreateChild("child");
        child.SetVector3("position", new Vector3f(1.0f, 2.0f, 3.0f));
        var materialRef = ResourceRef.Parse("Material;Materials/FirstCube");
        child.SetResourceRef("material", materialRef);
        Assert.True(child.GetVector3("position").ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.Equal(materialRef, child.GetResourceRef("material"));
        Assert.Equal(2u, child.GetNumAttributes());
    }

    [Fact]
    public void XMLElement_bounding_box_attribute_works()
    {
        var xml = new XMLFile("test.xml");
        xml.FromString("<newRoot />");
        var child = xml.GetOrCreateRoot("newRoot").CreateChild("child");
        child.SetBoundingBox("bounds", new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(4.0f, 5.0f, 6.0f)));
        Assert.True(child.GetBoundingBox("bounds").Min.ApproximatelyEquivalent(new Vector3f(-1.0f, -2.0f, -3.0f)));
    }

    [Fact]
    public void XMLElement_remove_child_works()
    {
        var xml = new XMLFile("test.xml");
        xml.FromString("<newRoot />");
        var root = xml.GetOrCreateRoot("newRoot");
        root.CreateChild("sibling");
        Assert.True(root.RemoveChild("sibling"));
        Assert.False(root.HasChild("sibling"));
    }

    [Fact]
    public void XMLElement_xpath_and_complex_variants_work()
    {
        var xml = new XMLFile("test.xml");
        Assert.True(xml.FromString("<root><item id=\"1\"/><item id=\"2\"/></root>"));
        var query = new XPathQuery("item[@id=$id]");
        Assert.True(query.SetVariable("id", "2"));
        var results = xml.Root.SelectPrepared(query);
        Assert.Equal(1u, results.Size());
        Assert.Equal("2", results.FirstResult().GetAttribute("id"));

        var holder = xml.Root.CreateChild("variant");
        var value = new Variant(new StringVariantMap
        {
            ["name"] = new Variant("rbfx"),
            ["values"] = new Variant(new VariantVector { new Variant(1), new Variant(true) })
        });
        Assert.True(holder.SetVariant(value));
        Assert.Equal(value, holder.GetVariant());
    }

    [Fact]
    public void XMLFile_patch_add_replace_remove_and_archive_callbacks_work()
    {
        var xml = new XMLFile("test.xml");
        Assert.True(xml.FromString("<root><item value=\"old\"/><remove/></root>"));
        var patch = new XMLFile("patch.xml");
        Assert.True(patch.FromString("""
            <patch>
              <add sel="/root"><added /></add>
              <replace sel="/root/item"><item value="new" /></replace>
              <remove sel="/root/remove" />
            </patch>
            """));
        xml.Patch(patch);
        Assert.Equal("new", xml.Root.GetChild("item").GetAttribute("value"));
        Assert.True(xml.Root.HasChild("added"));
        Assert.False(xml.Root.HasChild("remove"));

        var output = 42;
        Assert.True(xml.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("object");
            archive.Serialize("value", ref output);
        }));
        var input = 0;
        Assert.True(xml.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("object");
            archive.Serialize("value", ref input);
        }));
        Assert.Equal(42, input);
    }

    [Fact]
    public void Localization_multi_language_works()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "loc.json"),
                "{\"Hello\":{\"en\":\"Hello\",\"fi\":\"Hei\"}}");
            Resources.AddResourceDir(tempDir);
            var loc = new Localization();
            loc.LoadJSONFile("Data/loc.json");
            Assert.Equal(2, loc.GetNumLanguages());
            Assert.Equal("en", loc.GetLanguage());
            Assert.Equal("Hello", loc.Get("Hello"));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Localization_language_selection_works()
    {
        var tempDir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Data", "loc.json"),
                "{\"Hello\":{\"en\":\"Hello\",\"fi\":\"Hei\"}}");
            Resources.AddResourceDir(tempDir);
            var loc = new Localization();
            loc.LoadJSONFile("Data/loc.json");
            loc.SetLanguage("fi");
            Assert.Equal(1, loc.GetLanguageIndex("fi"));
            Assert.Equal("Hei", loc.Get("Hello"));
        }
        finally
        {
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Localization_single_language_loading_works()
    {
        Assert.True(JSONFile.ParseJSON("{\"Bye\":\"Moi\"}", out var json));
        var loc = new Localization();
        loc.LoadSingleLanguageJSON(json, "fi");
        loc.SetLanguage("fi");
        Assert.Equal("Moi", loc.Get("Bye"));
    }

    [Fact]
    public void Localization_rejects_out_of_range_lookup_and_sends_change_event()
    {
        Assert.True(JSONFile.ParseJSON("{\"Hello\":{\"en\":\"Hello\",\"fi\":\"Hei\"}}", out var json));
        var loc = new Localization();
        loc.LoadMultipleLanguageJSON(json);
        var eventCount = 0;
        using var subscription = loc.SubscribeToEvent<ChangeLanguage>(loc,
            (ref ChangeLanguage args) => ++eventCount);

        Assert.Equal("Hello", loc.Get("Hello"));
        Assert.Equal("Hello", loc.Get("Hello", 2));
        loc.SetLanguage(1);
        Assert.Equal(1, eventCount);
        Assert.Equal("Hei", loc.Get("Hello"));
    }

    [Fact]
    public void JSONResourceBase_serialization_roundtrips()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("{\"Name\":\"redbolt\",\"Count\":3}"));
        var resource = new SmokeJsonResource("Data/smoke.json");
        Assert.True(resource.Load(source));
        Assert.Equal("redbolt", resource.Value?.Name);
        Assert.Equal(3, resource.Value?.Count);

        using var dest = new MemoryStream();
        resource.Value = new SmokeJsonPayload { Name = "serialized", Count = 7 };
        Assert.True(resource.Save(dest));
        Assert.Contains("serialized", Encoding.UTF8.GetString(dest.ToArray()), StringComparison.Ordinal);
    }

    private sealed class SmokeJsonResource : JSONResource<SmokeJsonPayload>
    {
        public SmokeJsonResource(string name) : base(name) { }
    }

    private sealed class SmokeJsonPayload
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
