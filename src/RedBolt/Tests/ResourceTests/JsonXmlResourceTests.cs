using System.Xml.Linq;
using System.Xml.XPath;
using RedBolt;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
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
            System.IO.File.WriteAllText(jsonPath, "{\"name\":\"redbolt\",\"value\":7}");
            Resources.AddResourceDir(tempDir);

            var jsonFile = Resources.GetResource<JSONFile>("Data/config.json");
            Assert.Equal(7, jsonFile.Root.Get("value").GetInt());
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
        using var value = new JSONValue();
        Assert.False(JSONFile.ParseJSON("{\"value\":1,}", value));
    }

    [Fact]
    public void JSONFile_matches_native_root_ownership_and_archive_exception_boundary()
    {
        Assert.True(typeof(JSONFile).IsClass);
        Assert.False(typeof(JSONFile).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(JSONFile).GetInterfaces());

        var json = new JSONFile();
        var root = json.Root;
        Assert.True(json.FromString("{\"value\":1}"));
        Assert.Same(root, json.Root);
        Assert.Equal(1, root.Get("value").GetInt());

        Assert.False(json.SaveObjectCallback(_ => throw new ArchiveException("archive failure")));
        Assert.Throws<ArgumentException>(() => json.SaveObjectCallback(_ => throw new ArgumentException("user failure")));

        json.Dispose();
        Assert.True(root.IsNull);
    }

    [Fact]
    public void JSONFile_registers_and_serializes_named_objects()
    {
        using var context = new Context();
        JSONFile.RegisterObject(context);
        using var json = Assert.IsType<JSONFile>(context.CreateObject(ObjectReflection<JSONFile>.TypeId));

        var source = new JSONArchiveTestValue { Value = 42 };
        Assert.True(json.SaveObject("test", source));
        Assert.Equal(42, json.Root.Get("value").GetInt());

        var loaded = new JSONArchiveTestValue();
        Assert.True(json.LoadObject("test", loaded));
        Assert.Equal(42, loaded.Value);
    }

    [Fact]
    public void JSONFile_preserves_parse_target_and_formats_null_and_empty_indentation()
    {
        using var value = new JSONValue(42);
        Assert.False(JSONFile.ParseJSON("invalid", value, false));
        Assert.Equal(42, value.GetInt());

        using var json = new JSONFile();
        Assert.Equal("null", json.ToString());
        Assert.True(json.FromString("{\"outer\":{\"value\":1}}"));
        Assert.Contains("\n\"outer\"", json.ToString(string.Empty), StringComparison.Ordinal);
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
            System.IO.File.WriteAllText(xmlPath, "<root><child value=\"7\" /></root>");
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
        using var json = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{\"ok\":true}", json));
        Assert.True(json.Get("ok").GetBool());
    }

    private sealed class JSONArchiveTestValue : IArchiveSerializable
    {
        public int Value { get; set; }

        public void SerializeInBlock(Archive archive)
        {
            var value = Value;
            ArchiveSerialization.SerializeValue(archive, "value", ref value);
            Value = value;
        }
    }

    [Fact]
    public void XMLFile_FromString_creates_root()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<newRoot />"));
        Assert.Equal("newRoot", xml.GetOrCreateRoot("newRoot").Name);
    }

    [Fact]
    public void JSONValue_array_and_object_helpers_work()
    {
        using var json = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{}", json));
        json.Set("tags", new JSONValue([new JSONValue("engine"), new JSONValue("rbfx")]));
        Assert.Equal(2, json.Get("tags").Size());
        Assert.True(json.Contains("tags"));
        Assert.Contains("engine", json.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void JSONValue_matches_native_copy_lifetime_and_number_equality()
    {
        Assert.True(typeof(JSONValue).IsClass);
        Assert.False(typeof(JSONValue).IsSealed);
        Assert.Contains(typeof(ICloneable), typeof(JSONValue).GetInterfaces());
        Assert.Contains(typeof(IDisposable), typeof(JSONValue).GetInterfaces());

        using var source = new JSONValue(new Dictionary<string, JSONValue>
        {
            ["nested"] = new JSONValue([new JSONValue("source")])
        });
        using var copy = Assert.IsType<JSONValue>(source.Clone());
        source.Get("nested")[0].Set("changed");
        Assert.Equal("source", copy.Get("nested")[0].GetString());
        source.Set("nested", source.Get("nested"));
        Assert.Equal("changed", source.Get("nested")[0].GetString());
        source.Set(source.GetObject());
        Assert.Equal("changed", source.Get("nested")[0].GetString());

        using var array = new JSONValue([new JSONValue("value")]);
        array[0] = array[0];
        array.Set(array.GetArray());
        Assert.Equal("value", array[0].GetString());

        using var signed = new JSONValue(1);
        using var unsigned = new JSONValue(1u);
        Assert.Equal(signed, unsigned);
        Assert.Equal(signed.GetHashCode(), unsigned.GetHashCode());
        Assert.Same(JSONValue.Empty, source.Get("missing"));

        using var indexedObject = new JSONValue();
        Assert.Same(JSONValue.Empty, indexedObject.Get("value"));
        Assert.True(indexedObject.IsNull);
        indexedObject["value"].Set(42);
        Assert.Equal(42, indexedObject.Get("value").GetInt());
    }

    [Fact]
    public void JSONValue_erase_removes_key()
    {
        using var json = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{}", json));
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

        Assert.True(json.Get("value").IsObject);
        Assert.True(json.Get("value").Contains("enabled"));
        var loaded = json.GetVariant().GetStringVariantMap();
        Assert.Equal(new Variant(true), loaded["enabled"]);
        Assert.Equal(new Variant(7), loaded["values"].GetVariantVector()[0]);
        Assert.Equal(new Variant("rbfx"), loaded["values"].GetVariantVector()[1]);
    }

    [Fact]
    public void JSONValue_matches_native_type_names_and_null_equality()
    {
        using var left = new JSONValue();
        using var right = new JSONValue();

        Assert.False(left.Equals(right));
        Assert.Equal("Unsigned", JSONValue.GetNumberTypeName(JSONNumberType.UInt));
        Assert.Equal("Real", JSONValue.GetNumberTypeName(JSONNumberType.FloatDouble));
        Assert.Equal(JSONNumberType.UInt, JSONValue.GetNumberTypeFromName("Unsigned"));
        Assert.Equal(JSONNumberType.FloatDouble, JSONValue.GetNumberTypeFromName("Real"));

        using var emptyKeyValue = new JSONValue(1);
        left.Set(string.Empty, emptyKeyValue);
        Assert.Equal(1, left.Get(string.Empty).GetInt());

        using var context = new Context();
        using var resourceRefList = new JSONValue();
        resourceRefList.SetVariantValue(new Variant(new ResourceRefList()), context);
        Assert.Equal(string.Empty, resourceRefList.GetString());
    }

    [Fact]
    public void JSONValue_replace_recursively_merges_objects()
    {
        using var value = new JSONValue();
        using var replacement = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{\"outer\":{\"keep\":1,\"replace\":2}}", value));
        Assert.True(JSONFile.ParseJSON("{\"outer\":{\"replace\":3,\"add\":4}}", replacement));

        value.ReplaceRecursively(replacement);

        Assert.Equal(1, value.Get("outer").Get("keep").GetInt());
        Assert.Equal(3, value.Get("outer").Get("replace").GetInt());
        Assert.Equal(4, value.Get("outer").Get("add").GetInt());
    }

    [Fact]
    public void XMLElement_hierarchy_helpers_work()
    {
        var xml = new XMLFile { Name = "test.xml" };
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
        var xml = new XMLFile { Name = "test.xml" };
        xml.FromString("<newRoot />");
        var child = xml.GetOrCreateRoot("newRoot").CreateChild("child");
        child.SetVector3("position", new Vector3f(1.0f, 2.0f, 3.0f));
        var materialRef = ResourceRef.Parse("Material;Materials/FirstCube");
        child.SetResourceRef(materialRef);
        Assert.True(child.GetVector3("position").ApproximatelyEquivalent(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.Equal(materialRef, child.GetResourceRef());
        Assert.Equal(2, child.GetNumAttributes());
    }

    [Fact]
    public void XMLElement_64_bit_and_generic_vector_attributes_work()
    {
        var xml = new XMLFile { Name = "test.xml" };
        xml.FromString("<newRoot />");
        var child = xml.GetOrCreateRoot("newRoot").CreateChild("child");

        child.SetUInt64("unsigned", ulong.MaxValue);
        child.SetInt64("signed", long.MinValue);
        child.SetVector4("vector", new Vector4f(1.0f, 2.0f, 3.0f, 4.0f));

        Assert.Equal(ulong.MaxValue, child.GetUInt64("unsigned"));
        Assert.Equal(long.MinValue, child.GetInt64("signed"));
        Assert.True(child.GetVector("vector").ApproximatelyEquivalent(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f)));
    }

    [Fact]
    public void XMLElement_remove_child_works()
    {
        var xml = new XMLFile { Name = "test.xml" };
        xml.FromString("<newRoot />");
        var root = xml.GetOrCreateRoot("newRoot");
        root.CreateChild("sibling");
        Assert.True(root.RemoveChild("sibling"));
        Assert.False(root.HasChild("sibling"));
    }

    [Fact]
    public void XMLElement_matches_native_class_and_copy_shape()
    {
        Assert.True(typeof(XMLElement).IsClass);
        Assert.False(typeof(XMLElement).IsSealed);
        Assert.Contains(typeof(ICloneable), typeof(XMLElement).GetInterfaces());
        Assert.Contains(typeof(IDisposable), typeof(XMLElement).GetInterfaces());

        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><first/><second/></root>"));
        var source = xml.GetRoot().GetChild("first");
        var copy = Assert.IsType<XMLElement>(source.Clone());
        Assert.Equal("first", copy.Name);
        copy.Dispose();
        Assert.Equal("first", source.Name);

        var assigned = new XMLElement();
        Assert.Same(assigned, assigned.CopyFrom(source));
        Assert.Equal("first", assigned.Name);
        Assert.Same(xml, assigned.File);
        Assert.Equal(typeof(WeakReference<XMLFile>), typeof(XMLElement)
            .GetField("_file", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .FieldType);
    }

    [Fact]
    public void XMLElement_native_child_text_and_buffer_semantics_work()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><holder><child/></holder><empty/></root>"));
        var holder = xml.GetRoot().GetChild("holder");
        Assert.True(holder.SetValue("first"));
        Assert.True(holder.HasChild("child"));
        Assert.True(holder.SetValue("second"));
        Assert.True(holder.HasChild("child"));
        Assert.Contains("second", holder.Node!.ToString(), StringComparison.Ordinal);
        Assert.True(xml.GetRoot().GetChild("empty").RemoveChildren());

        var bytes = new byte[] { 1, 20, 255 };
        Assert.True(holder.SetBuffer("buffer", bytes));
        Assert.Equal("1 20 255", holder.GetAttribute("buffer"));
        Assert.Equal(bytes, holder.GetBuffer("buffer"));
        Span<byte> destination = stackalloc byte[3];
        Assert.True(holder.GetBuffer("buffer", destination));
        Assert.True(destination.SequenceEqual(bytes));
        Span<byte> tooSmall = stackalloc byte[2];
        Assert.False(holder.GetBuffer("buffer", tooSmall));
    }

    [Fact]
    public void XMLElement_native_bounding_box_and_named_variant_map_work()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root/>"));
        var bounds = new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(4.0f, 5.0f, 6.0f));
        Assert.True(xml.GetRoot().SetBoundingBox(bounds));
        Assert.Equal("-1 -2 -3", xml.GetRoot().GetAttribute("min"));
        Assert.True(xml.GetRoot().GetBoundingBox().Min.ApproximatelyEquivalent(bounds.Min));
        Assert.True(xml.GetRoot().GetBoundingBox().Max.ApproximatelyEquivalent(bounds.Max));

        var child = xml.GetRoot().CreateChild("variant");
        child.SetString("name", "named-key");
        child.SetString("type", "Int");
        child.SetInt("value", 42);
        var map = xml.GetRoot().GetVariantMap();
        Assert.Equal(42, map[new StringHash("named-key")].GetInt());
    }

    [Fact]
    public void XMLElement_xpath_results_are_independent_and_iterable()
    {
        Assert.False(typeof(XPathResultSet).IsSealed);
        Assert.Contains(typeof(ICloneable), typeof(XPathResultSet).GetInterfaces());
        Assert.Contains(typeof(IDisposable), typeof(XPathResultSet).GetInterfaces());
        Assert.Equal(typeof(WeakReference<XMLFile>), typeof(XPathResultSet)
            .GetField("_file", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .FieldType);

        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item id=\"1\"/><item id=\"2\"/></root>"));
        var results = xml.GetRoot().Select("item");
        Assert.Equal(2, results.Size());
        using var resultsCopy = Assert.IsType<XPathResultSet>(results.Clone());
        Assert.Equal(2, resultsCopy.Size());
        var first = results.FirstResult();
        var firstAgain = results.FirstResult();
        Assert.NotSame(first, firstAgain);
        Assert.Equal("1", first.GetAttribute("id"));
        Assert.Equal("2", first.NextResult().GetAttribute("id"));
        Assert.True(first.NextResult().IsNull);
        using var assignedResults = new XPathResultSet();
        Assert.Same(assignedResults, assignedResults.CopyFrom(results));
        results.Dispose();
        Assert.Equal(2, assignedResults.Size());
        Assert.Equal("1", assignedResults.FirstResult().GetAttribute("id"));
    }

    [Fact]
    public void XMLElement_xpath_attribute_results_match_native_attribute_semantics()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item id=\"1\"/></root>"));

        var attribute = xml.GetRoot().SelectSingle("item/@id");
        Assert.Equal("id", attribute.Name);
        Assert.Equal("1", attribute.GetAttribute("ignored"));
        Assert.True(attribute.HasAttribute("id"));
        Assert.False(attribute.HasAttribute("ignored"));
        Assert.True(attribute.SetAttribute("ignored", "2"));
        Assert.Equal("2", xml.GetRoot().GetChild("item").GetAttribute("id"));
        Assert.True(attribute.RemoveAttribute("ignored"));
        Assert.False(xml.GetRoot().GetChild("item").HasAttribute("id"));
    }

    [Fact]
    public void XMLElement_unnamed_child_traversal_and_removal_include_text_nodes()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root>prefix<child/>suffix</root>"));
        var root = xml.GetRoot();

        var text = root.GetChild();
        Assert.True(text.NotNull);
        Assert.Equal(string.Empty, text.Name);
        Assert.Equal("child", text.GetNext().Name);
        Assert.True(root.RemoveChildren());
        Assert.True(root.GetChild().IsNull);
    }

    [Fact]
    public void XMLElement_xpath_and_complex_variants_work()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item id=\"1\"/><item id=\"2\"/></root>"));
        var query = new XPathQuery("item[@id=$id]", "id:String");
        Assert.True(query.SetVariable("id", "2"));
        var results = xml.GetRoot().SelectPrepared(query);
        Assert.Equal(1, results.Size());
        Assert.Equal("2", results.FirstResult().GetAttribute("id"));

        var holder = xml.GetRoot().CreateChild("variant");
        var value = new Variant(new StringVariantMap
        {
            ["name"] = new Variant("rbfx"),
            ["values"] = new Variant(new VariantVector { new Variant(1), new Variant(true) })
        });
        Assert.True(holder.SetVariant(value));
        Assert.Equal(value, holder.GetVariant());
    }

    [Fact]
    public void XMLElement_custom_serializable_variants_roundtrip()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root/>"));
        var holder = xml.GetRoot().CreateChild("variant");
        var source = new Node();
        source.SetName("custom-node");
        var value = Variant.MakeCustomValue<Serializable>(source);

        Assert.True(holder.SetVariantValue(value));
        var loaded = holder.GetVariantValue(VariantType.Custom, _fixture.Context);
        Assert.Equal("custom-node", Assert.IsType<Node>(loaded.GetCustom<Serializable>()).Name);
    }

    [Fact]
    public void XPathQuery_matches_native_class_lifetime_and_binding()
    {
        Assert.True(typeof(XPathQuery).IsClass);
        Assert.False(typeof(XPathQuery).IsSealed);
        Assert.Contains(typeof(IDisposable), typeof(XPathQuery).GetInterfaces());
        Assert.DoesNotContain(typeof(ICloneable), typeof(XPathQuery).GetInterfaces());

        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item/><item/></root>"));
        var query = new XPathQuery();
        Assert.Equal(string.Empty, query.Query);
        Assert.Null(query.CompiledQuery);
        Assert.Null(query.XPathVariableSet);
        Assert.False(query.EvaluateToBool(xml.GetRoot()));
        Assert.Equal(0.0f, query.EvaluateToFloat(xml.GetRoot()));
        Assert.Equal(string.Empty, query.EvaluateToString(xml.GetRoot()));
        Assert.True(query.Evaluate(xml.GetRoot()).Empty());

        Assert.True(query.SetQuery("count(item)", bind: false));
        Assert.Null(query.CompiledQuery);
        Assert.Equal(0.0f, query.EvaluateToFloat(xml.GetRoot()));
        query.Bind();
        Assert.True(query.EvaluateToBool(xml.GetRoot()));
        Assert.Equal(2.0f, query.EvaluateToFloat(xml.GetRoot()));
        Assert.Equal("2", query.EvaluateToString(xml.GetRoot()));
        Assert.Throws<XPathException>(() => query.Evaluate(xml.GetRoot()));

        Assert.True(query.SetQuery("100000000000000000000"));
        Assert.Equal("100000000000000000000", query.EvaluateToString(xml.GetRoot()));
        Assert.True(query.SetQuery("0.0000001"));
        Assert.Equal("0.0000001", query.EvaluateToString(xml.GetRoot()));

        query.Dispose();
        Assert.Equal(string.Empty, query.Query);
        Assert.Null(query.CompiledQuery);
        Assert.Null(query.XPathVariableSet);
    }

    [Fact]
    public void XPathQuery_uses_native_typed_variable_set_semantics()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item enabled=\"true\" weight=\"2.5\" name=\"O'Reilly\"/></root>"));
        using var query = new XPathQuery(
            "item[@enabled=$enabled and @weight=$weight and @name=$name]",
            "enabled:Bool, weight:Float, name:String");

        Assert.False(query.SetVariable("enabled", 1.0f));
        Assert.True(query.SetVariable("enabled", true));
        Assert.True(query.SetVariable("weight", 2.5f));
        Assert.True(query.SetVariable("name", "O'Reilly"));
        Assert.True(query.SetVariable("unused", true));
        Assert.Equal(1, query.Evaluate(xml.GetRoot()).Size());

        Assert.True(query.SetQuery("item[@name=$name]"));
        Assert.Equal(1, query.Evaluate(xml.GetRoot()).Size());

        Assert.False(query.SetQuery("$value", "value:Bool,value:String", bind: false));
        Assert.Equal(string.Empty, query.Query);
        Assert.Null(query.CompiledQuery);
        Assert.False(query.SetQuery("$value", "value:Unknown", bind: false));
        Assert.Equal(string.Empty, query.Query);
    }

    [Fact]
    public void XPathQuery_result_set_variables_are_independent_snapshots()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item id=\"1\"/><item id=\"2\"/></root>"));
        var source = xml.GetRoot().Select("item");
        using var query = new XPathQuery("$items[@id='2']", "items:ResultSet");

        Assert.False(query.SetVariable("items", new XPathResultSet()));
        Assert.True(query.SetVariable("items", source));
        source.Dispose();

        var result = query.Evaluate(xml.GetRoot());
        Assert.Equal(1, result.Size());
        Assert.Equal("2", result.FirstResult().GetAttribute("id"));
    }

    [Fact]
    public void XPathQuery_result_set_variables_preserve_cross_document_nodes()
    {
        var sourceFile = new XMLFile { Name = "source.xml" };
        var contextFile = new XMLFile { Name = "context.xml" };
        Assert.True(sourceFile.FromString("<root><item id=\"source\"/></root>"));
        Assert.True(contextFile.FromString("<root><item id=\"context\"/></root>"));
        var source = sourceFile.GetRoot().Select("item");
        using var query = new XPathQuery("$items", "items:ResultSet");

        Assert.True(query.SetVariable("items", source));
        var result = query.Evaluate(contextFile.GetRoot());
        Assert.Equal("source", result.FirstResult().GetAttribute("id"));
    }

    [Fact]
    public void XPathQuery_bind_failure_preserves_previous_compiled_query()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item id=\"1\"/><item id=\"2\"/></root>"));
        using var query = new XPathQuery("item[@id='1']");

        Assert.True(query.SetQuery("item[", bind: false));
        Assert.Equal("1", query.Evaluate(xml.GetRoot()).FirstResult().GetAttribute("id"));
        Assert.Throws<XPathException>(() => query.Bind());
        Assert.Equal("1", query.Evaluate(xml.GetRoot()).FirstResult().GetAttribute("id"));
        Assert.Throws<XPathException>(() => new XPathQuery("item[@id=$id]"));
    }

    [Fact]
    public void XMLFile_patch_add_replace_remove_and_archive_callbacks_work()
    {
        var xml = new XMLFile { Name = "test.xml" };
        Assert.True(xml.FromString("<root><item value=\"old\"/><remove/></root>"));
        var patch = new XMLFile { Name = "patch.xml" };
        Assert.True(patch.FromString("""
            <patch>
              <add sel="/root"><added /></add>
              <replace sel="/root/item"><item value="new" /></replace>
              <remove sel="/root/remove" />
            </patch>
            """));
        xml.Patch(patch);
        Assert.Equal("new", xml.GetRoot().GetChild("item").GetAttribute("value"));
        Assert.True(xml.GetRoot().HasChild("added"));
        Assert.False(xml.GetRoot().HasChild("remove"));

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
    public void XMLFile_matches_native_class_ownership_and_empty_source_behavior()
    {
        Assert.True(typeof(XMLFile).IsClass);
        Assert.False(typeof(XMLFile).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(XMLFile).GetInterfaces());
        Assert.Null(typeof(XMLFile).GetProperty("Root"));
        Assert.Null(typeof(XMLFile).GetMethod("GetDocument"));

        var xml = new XMLFile();
        Assert.Equal("<?xml version=\"1.0\"?>\n", xml.ToString());
        Assert.True(xml.FromString("<root/>"));
        Assert.StartsWith("<?xml version=\"1.0\"?>\n<root />", xml.ToString(), StringComparison.Ordinal);
        var memoryUse = xml.GetMemoryUse();
        Assert.False(xml.FromString(string.Empty));
        Assert.Equal("root", xml.GetRoot().Name);
        xml.GetRoot().CreateChild("child");
        Assert.Equal(memoryUse, xml.GetMemoryUse());
        xml.Dispose();
        Assert.Null(xml.Document);
    }

    [Fact]
    public void XMLFile_registers_and_serializes_named_objects()
    {
        using var context = new Context();
        XMLFile.RegisterObject(context);
        using var xml = Assert.IsType<XMLFile>(context.CreateObject(ObjectReflection<XMLFile>.TypeId));

        var source = new JSONArchiveTestValue { Value = 42 };
        Assert.True(xml.SaveObject("test", source));
        Assert.Equal("test", xml.GetRoot().Name);
        Assert.Equal(42, xml.GetRoot().GetInt("value"));

        var loaded = new JSONArchiveTestValue();
        Assert.True(xml.LoadObject("test", loaded));
        Assert.Equal(42, loaded.Value);
    }

    [Fact]
    public void XMLFile_load_uses_native_default_node_set()
    {
        var xml = new XMLFile();
        Assert.True(xml.FromString("<?xml version=\"1.0\"?><root>  <child/><!--comment--><?pi data?></root>"));
        var nodes = xml.Document.Root!.Nodes().ToArray();
        Assert.Single(nodes);
        Assert.IsType<XElement>(nodes[0]);
    }

    [Fact]
    public void XMLFile_archive_callbacks_only_consume_archive_exceptions()
    {
        var xml = new XMLFile();
        Assert.True(xml.FromString("<old/>"));
        Assert.False(xml.SaveObjectCallback(_ => throw new ArchiveException("archive failure")));
        Assert.True(xml.GetRoot().IsNull);
        Assert.Throws<ArgumentException>(() => xml.SaveObjectCallback(_ => throw new ArgumentException("user failure")));

        Assert.True(xml.FromString("<root/>"));
        Assert.False(xml.LoadObjectCallback(_ => throw new ArchiveException("archive failure")));
        Assert.Throws<ArgumentException>(() => xml.LoadObjectCallback(_ => throw new ArgumentException("user failure")));
    }

    [Fact]
    public void XMLFile_patch_replaces_with_first_node_and_keeps_text_kinds_separate()
    {
        var xml = new XMLFile();
        Assert.True(xml.FromString("<root><![CDATA[a]]><old/></root>"));
        var patch = new XMLFile();
        Assert.True(patch.FromString("""
            <patch>
              <add sel="/root/old" pos="before">b</add>
              <replace sel="/root/old"><first/><second/></replace>
            </patch>
            """));

        xml.Patch(patch);

        var nodes = xml.Document.Root!.Nodes().ToArray();
        Assert.IsType<XCData>(nodes[0]);
        Assert.IsType<XText>(nodes[1]);
        Assert.Equal("first", Assert.IsType<XElement>(nodes[2]).Name.LocalName);
        Assert.False(xml.GetRoot().HasChild("second"));
    }

    [Fact]
    public void XMLFile_inherit_patches_independent_document()
    {
        var tempDir = CreateTempDir();
        var id = Guid.NewGuid().ToString("N");
        var baseName = $"Data/{id}-base.xml";
        var patchName = $"Data/{id}-patch.xml";
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, baseName), "<root><base/></root>");
            System.IO.File.WriteAllText(Path.Combine(tempDir, patchName),
                $"<patch inherit=\"{baseName}\"><add sel=\"/root\"><added/></add></patch>");
            Resources.AddResourceDir(tempDir);

            var patched = Resources.GetResourceOrNull<XMLFile>(patchName);
            var original = Resources.GetResourceOrNull<XMLFile>(baseName);

            Assert.NotNull(patched);
            Assert.NotNull(original);
            Assert.True(patched.GetRoot().HasChild("base"));
            Assert.True(patched.GetRoot().HasChild("added"));
            Assert.True(original.GetRoot().HasChild("base"));
            Assert.False(original.GetRoot().HasChild("added"));
        }
        finally
        {
            Resources.ReleaseResource<XMLFile>(patchName);
            Resources.ReleaseResource<XMLFile>(baseName);
            Resources.RemoveResourceDir(tempDir);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Localization_multi_language_works()
    {
        var tempDir = CreateTempDir();
        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "loc.json"),
                "{\"Hello\":{\"en\":\"Hello\",\"fi\":\"Hei\"}}");
            Resources.AddResourceDir(tempDir);
            using var loc = new Localization();
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
            System.IO.File.WriteAllText(Path.Combine(tempDir, "Data", "loc.json"),
                "{\"Hello\":{\"en\":\"Hello\",\"fi\":\"Hei\"}}");
            Resources.AddResourceDir(tempDir);
            using var loc = new Localization();
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
        using var json = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{\"Bye\":\"Moi\"}", json));
        using var loc = new Localization();
        loc.LoadSingleLanguageJSON(json, "fi");
        loc.SetLanguage("fi");
        Assert.Equal("Moi", loc.Get("Bye"));
    }

    [Fact]
    public void Localization_string_pattern_matches_the_complete_identifier()
    {
        using var json = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{\"Hello\":{\"en\":\"Localized\"},\"HelloSuffix\":{\"en\":\"Wrong\"}}", json));
        using var loc = new Localization();
        loc.LoadMultipleLanguageJSON(json);
        loc.SetStringPattern("Hello");

        Assert.Equal("Localized", loc.Get("Hello"));
        Assert.Equal("HelloSuffix", loc.Get("HelloSuffix"));
    }

    [Fact]
    public void Localization_rejects_out_of_range_lookup_and_sends_change_event()
    {
        using var json = new JSONValue();
        Assert.True(JSONFile.ParseJSON("{\"Hello\":{\"en\":\"Hello\",\"fi\":\"Hei\"}}", json));
        using var loc = new Localization();
        loc.LoadMultipleLanguageJSON(json);
        var eventCount = 0;
        loc.SubscribeToEvent<ChangeLanguage>(loc,
            (ref ChangeLanguage args) => ++eventCount);

        Assert.Equal("Hello", loc.Get("Hello"));
        Assert.Equal("Hello", loc.Get("Hello", 2));
        loc.SetLanguage(1);
        Assert.Equal(1, eventCount);
        Assert.Equal("Hei", loc.Get("Hello"));
    }

}
