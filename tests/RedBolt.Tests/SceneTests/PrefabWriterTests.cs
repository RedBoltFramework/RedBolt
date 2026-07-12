namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabWriterTests
{
    [Fact]
    public void Memory_writer_writes_nested_nodes_and_components()
    {
        var source = new TestSerializable { Number = 42, Label = "root" };
        var component = new TestSerializable { Number = 7, Label = "component" };
        var prefab = new NodePrefab();
        var writer = new PrefabWriterToMemory(prefab, PrefabSaveFlag.EnumsAsStrings);

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
        Assert.Equal(1u, prefab.Node.Id);
        Assert.Empty(prefab.Node.TypeName);
        Assert.Single(prefab.Components);
        Assert.Equal(2u, prefab.Components[0].Id);
        Assert.Equal("component", prefab.Components[0].Attributes.Single(attribute => attribute.Name == "Label").Value.GetString());
        Assert.Single(prefab.Children);
        Assert.Equal(3u, prefab.Children[0].Node.Id);
        Assert.Empty(prefab.Children[0].Node.TypeName);
    }

    [Fact]
    public void Archive_writer_roundtrips_through_prefab_reader()
    {
        var source = new TestSerializable { Number = 42, Label = "root" };
        var component = new TestSerializable { Number = 7, Label = "component" };
        var output = new JSONOutputArchive();
        using (var writer = new PrefabWriterToArchive(output, blockName: "root"))
        {
            writer.WriteNode(1, source);
            writer.WriteNumComponents(1);
            writer.WriteComponent(2, component);
            writer.WriteNumChildren(0);
            Assert.True(writer.IsEOF());
        }

        var input = new JSONInputArchive(output.ToJsonString());
        using var reader = new PrefabReaderFromArchive(input, blockName: "root");
        Assert.Equal(1u, reader.ReadNode()!.Id);
        Assert.Equal(1u, reader.ReadNumComponents());
        Assert.Equal(2u, reader.ReadComponent()!.Id);
        Assert.Equal(0u, reader.ReadNumChildren());
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
            RegisterAttribute("Number", typeof(int), _ => Number, (_, value) => Number = (int)(value ?? 0), 0);
            RegisterAttribute("Label", typeof(string), _ => Label, (_, value) => Label = (string?)value ?? string.Empty, string.Empty);
        }

        public int Number { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
