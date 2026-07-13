using RedBolt;

namespace RedBolt.Tests;

public sealed class ResourceTests
{
    [Fact]
    public void Failed_load_restores_done_state()
    {
        var resource = new FailingResource();

        Assert.False(resource.Load(new MemoryStream()));
        Assert.Equal(AsyncLoadState.Done, resource.AsyncLoadState);
    }

    [Fact]
    public void Empty_resource_name_is_allowed()
    {
        var resource = new FailingResource();

        resource.SetName(string.Empty);

        Assert.Equal(string.Empty, resource.Name);
    }

    [Fact]
    public void Resource_metadata_preserves_variants_order_and_xml_roundtrip()
    {
        var resource = new MetadataResource();
        resource.AddMetadata("Count", new Variant(42));
        resource.AddMetadata("Label", new Variant("Smoke"));
        resource.AddMetadata("Count", new Variant(7));

        Assert.Equal(["Count", "Label"], resource.GetMetadataKeys());

        var xml = new XMLFile(string.Empty);
        var root = xml.CreateRoot("resource");
        resource.SaveMetadata(root);

        var loaded = new MetadataResource();
        loaded.LoadMetadata(root);
        Assert.Equal(new Variant(7), loaded.GetMetadata("Count"));
        Assert.Equal(new Variant("Smoke"), loaded.GetMetadata("Label"));
    }

    [Fact]
    public void Resource_metadata_loads_json_array()
    {
        var entry = new JSONValue(JSONValueType.Object);
        entry.Set("name", new JSONValue("Enabled"));
        entry.Set("type", new JSONValue("Bool"));
        entry.Set("value", new JSONValue(true));

        var resource = new MetadataResource();
        resource.LoadMetadata([entry]);

        Assert.Equal(new Variant(true), resource.GetMetadata("Enabled"));
    }

    [Fact]
    public void Resource_reference_helpers_preserve_names_and_types()
    {
        var resource = new FailingResource();
        resource.SetName("Textures/Test.png");
        var defaultType = new StringHash("DefaultResource");

        Assert.Equal("Textures/Test.png", Resource.GetResourceName(resource));
        Assert.Equal(resource.GetTypeNameHash(), Resource.GetResourceType(resource, defaultType));
        Assert.Equal(new ResourceRef(resource.GetTypeNameHash(), "Textures/Test.png"),
            Resource.GetResourceRef(resource, defaultType));
        Assert.Equal(string.Empty, Resource.GetResourceName(null));
        Assert.Equal(defaultType, Resource.GetResourceType(null, defaultType));

        var names = Resource.GetResourceNames<FailingResource>([resource, null]);
        Assert.Equal(["Textures/Test.png", string.Empty], names);

        var references = Resource.GetResourceRefList<FailingResource>([resource, null]);
        Assert.Equal(typeof(FailingResource).GetTypeId().Value, references.Type);
        Assert.Equal(["Textures/Test.png", string.Empty], references.Names);
    }

    private sealed class FailingResource : Resource
    {
        public FailingResource()
            : base(string.Empty)
        {
        }

        public override bool BeginLoad(Stream source) => false;
    }

    private sealed class MetadataResource : ResourceWithMetadata
    {
        public MetadataResource()
            : base(string.Empty)
        {
        }

        public void LoadMetadata(XMLElement source) => LoadMetadataFromXML(source);

        public void LoadMetadata(IReadOnlyList<JSONValue> source) => LoadMetadataFromJSON(source);

        public void SaveMetadata(XMLElement destination) => SaveMetadataToXML(destination);
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BinaryFileTests
{
    public BinaryFileTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Archive_callbacks_roundtrip_values()
    {
        var file = new BinaryFile();
        var outputValue = 42;

        Assert.True(file.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenSequentialBlock("object");
            archive.Serialize("value", ref outputValue);
        }));

        var inputValue = 0;
        Assert.True(file.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenSequentialBlock("object");
            archive.Serialize("value", ref inputValue);
        }));
        Assert.Equal(outputValue, inputValue);
    }

    [Fact]
    public void ReadLines_does_not_append_line_after_terminal_newline()
    {
        var file = new BinaryFile();
        file.SetText("first\nsecond\n");

        Assert.Equal(["first", "second"], file.ReadLines());
    }
}
