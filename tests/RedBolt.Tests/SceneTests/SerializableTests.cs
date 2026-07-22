using RedBolt;

namespace RedBolt.Tests;

public sealed class SerializableTests
{
    [Fact]
    public void Serializable_roundtrips_binary_xml_json_and_archive()
    {
        var source = new TestSerializable { Number = 42, Label = "rbfx" };

        using var binary = new VectorBuffer();
        Assert.True(source.Save(binary));
        binary.Seek(0);
        var binaryResult = new TestSerializable();
        Assert.True(binaryResult.Load(binary));
        AssertValues(binaryResult);

        var xml = new XMLFile { Name = string.Empty };
        var root = xml.CreateRoot("object");
        Assert.True(source.SaveXML(root));
        var xmlResult = new TestSerializable();
        Assert.True(xmlResult.LoadXML(root));
        AssertValues(xmlResult);

        var json = new JSONValue(JSONValueType.Object);
        Assert.True(source.SaveJSON(json));
        var jsonResult = new TestSerializable();
        Assert.True(jsonResult.LoadJSON(json));
        AssertValues(jsonResult);

        var previous = Context.Instance;
        Context.SetInstance(new Context());
        try
        {
            var archive = new JSONOutputArchive();
            using (archive.OpenUnorderedBlock("object")) source.SerializeInBlock(archive);
            var input = new JSONInputArchive(archive.ToJsonString());
            var archiveResult = new TestSerializable();
            using (input.OpenUnorderedBlock("object")) archiveResult.SerializeInBlock(input);
            AssertValues(archiveResult);
        }
        finally
        {
            Context.SetInstance(previous);
        }
    }

    [Fact]
    public void Serializable_clone_copies_attributes()
    {
        var source = new TestSerializable { Number = 7, Label = "clone" };
        var clone = Assert.IsType<TestSerializable>(source.Clone());
        Assert.Equal(7, clone.Number);
        Assert.Equal("clone", clone.Label);
    }

    [Fact]
    public void Serializable_exposes_registered_reflection()
    {
        var previous = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var reflection = context.Reflect<TestSerializable>();
            var serializable = new TestSerializable();

            Assert.Same(reflection, serializable.GetReflection());
            Assert.Equal(2u, serializable.GetNumAttributes());
        }
        finally
        {
            Context.SetInstance(previous);
        }
    }

    private static void AssertValues(TestSerializable value)
    {
        Assert.Equal(42, value.Number);
        Assert.Equal("rbfx", value.Label);
    }

    private sealed class TestSerializable : Serializable
    {
        public TestSerializable()
        {
            RegisterAttribute("Number", typeof(int), _ => Number, (_, value) => Number = (int)(value ?? 0), 0);
            RegisterAttribute("Label", typeof(string), _ => Label, (_, value) => Label = (string?)value ?? string.Empty, string.Empty);
        }

        public int Number { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
