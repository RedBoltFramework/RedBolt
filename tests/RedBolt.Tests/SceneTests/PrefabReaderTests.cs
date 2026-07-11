using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabReaderTests
{
    [Fact]
    public void Memory_reader_walks_components_and_nested_children()
    {
        var prefab = new NodePrefab();
        prefab.Node.TypeName = "Root";
        prefab.Node.Id = 1;
        prefab.Components.Add(new SerializablePrefab { TypeName = "Camera", Id = 2 });

        var child = new NodePrefab();
        child.Node.TypeName = "Child";
        child.Node.Id = 3;
        prefab.Children.Add(child);

        var reader = new PrefabReaderFromMemory(prefab);
        Assert.False(reader.IsEOF());
        Assert.Equal("Root", reader.ReadNode()!.TypeName);
        Assert.Equal(1u, reader.ReadNumComponents());
        Assert.Equal("Camera", reader.ReadComponent()!.TypeName);
        Assert.Equal(1u, reader.ReadNumChildren());

        reader.BeginChild();
        Assert.Equal("Child", reader.ReadNode()!.TypeName);
        Assert.Equal(0u, reader.ReadNumComponents());
        Assert.Equal(0u, reader.ReadNumChildren());
        reader.EndChild();

        Assert.True(reader.IsEOF());
    }

    [Fact]
    public void Archive_reader_reads_direct_prefab_blocks()
    {
        var output = new JSONOutputArchive();
        using (output.OpenUnorderedBlock("root"))
        {
            var type = "Root";
            var id = 1u;
            var temporary = false;
            ArchiveSerialization.SerializeValue(output, "type", ref type);
            ArchiveSerialization.SerializeValue(output, "id", ref id);
            ArchiveSerialization.SerializeValue(output, "temporary", ref temporary);
            using (output.OpenArrayBlock("attributes", 0))
            {
            }

            using (output.OpenArrayBlock("components", 1))
            using (output.OpenUnorderedBlock("component"))
            {
                var componentType = "Camera";
                var componentId = 2u;
                var componentTemporary = false;
                ArchiveSerialization.SerializeValue(output, "type", ref componentType);
                ArchiveSerialization.SerializeValue(output, "id", ref componentId);
                ArchiveSerialization.SerializeValue(output, "temporary", ref componentTemporary);
                using (output.OpenArrayBlock("attributes", 0))
                {
                }
            }

            using (output.OpenArrayBlock("nodes", 0))
            {
            }
        }

        var input = new JSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("root"))
        using (var reader = new PrefabReaderFromArchive(input, blockName: null))
        {
            Assert.Equal("Root", reader.ReadNode()!.TypeName);
            Assert.Equal(1u, reader.ReadNumComponents());
            Assert.Equal("Camera", reader.ReadComponent()!.TypeName);
            Assert.Equal(0u, reader.ReadNumChildren());
            Assert.True(reader.IsEOF());
        }
    }
}
