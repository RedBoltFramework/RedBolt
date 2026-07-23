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
        Assert.Equal(0, binaryResult.ApplyCount);

        var xml = new XMLFile { Name = string.Empty };
        var root = xml.CreateRoot("object");
        Assert.True(source.SaveXML(root));
        var xmlResult = new TestSerializable();
        Assert.True(xmlResult.LoadXML(root));
        AssertValues(xmlResult);
        Assert.Equal(0, xmlResult.ApplyCount);

        using var json = new JSONValue(JSONValueType.Object);
        Assert.True(source.SaveJSON(json));
        var jsonResult = new TestSerializable();
        Assert.True(jsonResult.LoadJSON(json));
        AssertValues(jsonResult);
        Assert.Equal(0, jsonResult.ApplyCount);

        var previous = Context.Instance;
        Context.SetInstance(new Context());
        try
        {
            var archive = new TestJSONOutputArchive();
            using (archive.OpenUnorderedBlock("object")) source.SerializeInBlock(archive);
            var input = new TestJSONInputArchive(archive.ToJsonString());
            var archiveResult = new TestSerializable();
            using (input.OpenUnorderedBlock("object")) archiveResult.SerializeInBlock(input);
            AssertValues(archiveResult);
            Assert.Equal(1, archiveResult.ApplyCount);
        }
        finally
        {
            Context.SetInstance(previous);
        }
    }

    [Fact]
    public void Serializable_clone_copies_attributes()
    {
        var previous = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            context.AddFactoryReflection<TestSerializable>();
            using var source = new TestSerializable { Number = 7, Label = "clone" };
            using var clone = Assert.IsType<TestSerializable>(source.Clone());
            Assert.Equal(7, clone.Number);
            Assert.Equal("clone", clone.Label);
        }
        finally
        {
            Context.SetInstance(previous);
        }
    }

    [Fact]
    public void Serializable_clone_returns_null_without_factory()
    {
        var previous = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var source = new TestSerializable();
            Assert.Null(source.Clone());
        }
        finally
        {
            Context.SetInstance(previous);
        }
    }

    [Fact]
    public void Serializable_owns_instance_default_values()
    {
        using var serializable = new DefaultSerializable();
        var source = new byte[] { 1, 2, 3 };
        serializable.SetInstanceDefault("Value", source);

        source[0] = 9;
        using var firstValue = serializable.GetInstanceDefault("Value");
        var first = Assert.IsType<byte[]>(firstValue.GetObject());
        Assert.Equal(new byte[] { 1, 2, 3 }, first);

        first[1] = 9;
        using var secondValue = serializable.GetInstanceDefault("Value");
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(secondValue.GetObject()));

        serializable.RemoveInstanceDefault();
        using var removedValue = serializable.GetInstanceDefault("Value");
        Assert.Null(removedValue.GetObject());
    }

    [Fact]
    public void Serializable_skips_non_object_json_attributes()
    {
        using var serializable = new TestSerializable();
        using var json = new JSONValue(JSONValueType.Object);
        using var attributes = new JSONValue(42);
        json.Set("attributes", attributes);

        Assert.True(serializable.LoadJSON(json));
        Assert.Equal(0, serializable.ApplyCount);
    }

    [Fact]
    public void Serializable_exposes_registered_reflection()
    {
        var previous = Context.Instance;
        using var context = new Context();
        Context.SetInstance(context);
        try
        {
            var reflection = context.Reflect<TestSerializable>();
            using var serializable = new TestSerializable();
            using var secondSerializable = new TestSerializable();

            Assert.Same(reflection, serializable.GetReflection());
            Assert.Equal(2, serializable.GetNumAttributes());
            Assert.Equal(2u, reflection.NumAttributes);
            Assert.Same(reflection.Attributes[0], serializable.Attributes[0]);
            Assert.Same(serializable.Attributes[0], secondSerializable.Attributes[0]);
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
            RegisterAttribute("Number", typeof(int), self => ((TestSerializable)self).Number,
                (self, value) => ((TestSerializable)self).Number = (int)(value ?? 0), 0);
            RegisterAttribute("Label", typeof(string), self => ((TestSerializable)self).Label,
                (self, value) => ((TestSerializable)self).Label = (string?)value ?? string.Empty, string.Empty);
        }

        public int Number { get; set; }
        public string Label { get; set; } = string.Empty;
        public int ApplyCount { get; private set; }

        public override void ApplyAttributes()
        {
            ++ApplyCount;
        }
    }

    private sealed class DefaultSerializable : Serializable
    {
        public DefaultSerializable()
        {
            RegisterAttribute("Value", typeof(byte[]), self => ((DefaultSerializable)self).Value,
                (self, value) => ((DefaultSerializable)self).Value = (byte[]?)value ?? [], Array.Empty<byte>());
        }

        public byte[] Value { get; set; } = [];
    }
}
