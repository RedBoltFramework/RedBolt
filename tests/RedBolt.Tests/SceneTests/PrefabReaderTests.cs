using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PrefabReaderTests
{
    [Fact]
    public void Memory_reader_walks_components_and_nested_children()
    {
        var prefab = new NodePrefab();
        prefab.Node.SetType("Root");
        prefab.Node.SetId((SerializableId)1);
        var component = new SerializablePrefab();
        component.SetType("Camera");
        component.SetId((SerializableId)2);
        prefab.Components.Add(component);

        var child = new NodePrefab();
        child.Node.SetType("Child");
        child.Node.SetId((SerializableId)3);
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
            ArchiveSerialization.SerializeValue(output, "_typeName", ref type);
            output.SerializeVle("_id", ref id);
            ArchiveSerialization.SerializeValue(output, "_temporary", ref temporary);
            using (output.OpenArrayBlock("attributes", 0))
            {
            }

            using (output.OpenArrayBlock("components", 1))
            using (output.OpenUnorderedBlock("component"))
            {
                var componentType = "Camera";
                var componentId = 2u;
                var componentTemporary = false;
                ArchiveSerialization.SerializeValue(output, "_typeName", ref componentType);
                output.SerializeVle("_id", ref componentId);
                ArchiveSerialization.SerializeValue(output, "_temporary", ref componentTemporary);
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
            Assert.Empty(reader.ReadNode()!.TypeName);
            Assert.Equal(1u, reader.ReadNumComponents());
            Assert.Equal("Camera", reader.ReadComponent()!.TypeName);
            Assert.Equal(0u, reader.ReadNumChildren());
            Assert.True(reader.IsEOF());
        }
    }
}
