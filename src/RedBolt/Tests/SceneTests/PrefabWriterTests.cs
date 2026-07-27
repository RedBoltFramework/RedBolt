using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabWriterTests
{
    [Fact]
    public void Writer_types_match_native_extensible_noncopyable_classes_and_managed_size_mapping()
    {
        Assert.True(typeof(PrefabWriter).IsAbstract);
        Assert.False(typeof(PrefabWriterToMemory).IsSealed);
        Assert.False(typeof(PrefabWriterToArchive).IsSealed);
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(PrefabWriter)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(PrefabWriter)));
        Assert.Equal(typeof(int), typeof(PrefabWriter).GetMethod(nameof(PrefabWriter.WriteNumComponents))!
            .GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), typeof(PrefabWriter).GetMethod(nameof(PrefabWriter.WriteNumChildren))!
            .GetParameters()[0].ParameterType);
    }

    [Fact]
    public void Archive_writer_keeps_buffer_alive_until_destruction()
    {
        var source = new TestSerializable { Number = 42, Label = "root" };
        var output = new TestJSONOutputArchive();
        var writer = new PrefabWriterToArchive(output, blockName: "root");

        writer.WriteNode(1, source);
        writer.WriteNumComponents(0);
        writer.WriteNumChildren(0);

        var buffer = (SerializablePrefab)typeof(PrefabWriterToArchive)
            .GetField("_buffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(writer)!;
        Assert.True(writer.IsEOF());
        Assert.NotEmpty(buffer.Attributes);

        writer.Dispose();

        Assert.Empty(buffer.Attributes);
    }

    [Fact]
    public void Memory_writer_writes_nested_nodes_and_components()
    {
        var source = new TestSerializable { Number = 42, Label = "root" };
        var component = new TestSerializable { Number = 7, Label = "component" };
        var prefab = new NodePrefab();
        using var writer = new PrefabWriterToMemory(prefab, PrefabSaveFlag.EnumsAsStrings);

        writer.WriteNode(1, source);
        writer.WriteNumComponents(1);
        writer.WriteComponent(2, component);
        writer.WriteNumChildren(1);
        writer.BeginChild();
        writer.WriteNode(3, source);
        writer.WriteNumComponents(0);
        writer.WriteNumChildren(0);
        writer.EndChild();

        Assert.True(writer.IsEOF());
        Assert.Equal(PrefabSaveFlag.EnumsAsStrings, writer.GetFlags());
        Assert.Equal((SerializableId)1, prefab.Node.Id);
        Assert.Empty(prefab.Node.TypeName);
        Assert.Single(prefab.Components);
        Assert.Equal((SerializableId)2, prefab.Components[0].Id);
        Assert.Equal("component", prefab.Components[0].Attributes.Single(attribute => attribute.Name == "Label").Value.GetString());
        Assert.Single(prefab.Children);
        Assert.Equal((SerializableId)3, prefab.Children[0].Node.Id);
        Assert.Empty(prefab.Children[0].Node.TypeName);
    }

    [Fact]
    public void Archive_writer_roundtrips_through_prefab_reader()
    {
        var source = new TestSerializable { Number = 42, Label = "root" };
        var component = new TestSerializable { Number = 7, Label = "component" };
        var output = new TestJSONOutputArchive();
        using (var writer = new PrefabWriterToArchive(output, blockName: "root"))
        {
            writer.WriteNode(1, source);
            writer.WriteNumComponents(1);
            writer.WriteComponent(2, component);
            writer.WriteNumChildren(0);
            Assert.True(writer.IsEOF());
        }

        var input = new TestJSONInputArchive(output.ToJsonString());
        using var reader = new PrefabReaderFromArchive(input, blockName: "root");
        Assert.Equal((SerializableId)1, reader.ReadNode()!.Id);
        Assert.Equal(1, reader.ReadNumComponents());
        Assert.Equal((SerializableId)2, reader.ReadComponent()!.Id);
        Assert.Equal(0, reader.ReadNumChildren());
        Assert.True(reader.IsEOF());
    }

    [Fact]
    public void Compact_attribute_names_roundtrip_through_prefab()
    {
        var source = new TestSerializable { Number = 42, Label = "compact" };
        var prefab = new SerializablePrefab();

        prefab.Import(source, PrefabSaveFlag.CompactAttributeNames | PrefabSaveFlag.SaveDefaultValues);

        Assert.NotEmpty(prefab.Attributes);
        Assert.All(prefab.Attributes, attribute => Assert.True(string.IsNullOrEmpty(attribute.Name)));
        Assert.All(prefab.Attributes, attribute => Assert.NotEqual(StringHash.Empty, attribute.NameHash));

        var destination = new TestSerializable();
        prefab.Export(destination);

        Assert.Equal(42, destination.Number);
        Assert.Equal("compact", destination.Label);
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

        public override ObjectReflection? GetReflection()
        {
            return Context.Instance.Reflect<TestSerializable>();
        }
    }
}
