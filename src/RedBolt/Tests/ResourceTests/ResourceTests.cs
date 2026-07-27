using RedBolt;

namespace RedBolt.Tests;

public sealed class ResourceTests
{
    [Fact]
    public void Resource_preserves_cpp_class_kind_and_base_state()
    {
        using var resource = new Resource();

        Assert.True(typeof(Resource).IsClass);
        Assert.False(typeof(Resource).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(Resource).GetInterfaces());
        Assert.Equal(typeof(int), typeof(Resource).GetProperty(nameof(Resource.MemoryUse))!.PropertyType);
        Assert.Equal(string.Empty, resource.GetName());
        Assert.Equal(StringHash.Empty, resource.GetNameHash());
        Assert.Equal(0, resource.GetMemoryUse());
        Assert.Equal(AsyncLoadState.Done, resource.GetAsyncLoadState());
        Assert.Equal(string.Empty, resource.GetAbsoluteFileName());
    }

    [Fact]
    public void Simple_resource_preserves_cpp_class_kind_and_save_format_selection()
    {
        using var resource = new EmptySimpleResource();
        using var destination = new VectorBuffer();

        Assert.True(typeof(SimpleResource).IsClass);
        Assert.True(typeof(SimpleResource).IsAbstract);
        Assert.False(typeof(SimpleResource).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(SimpleResource).GetInterfaces());
        Assert.True(typeof(InternalResourceFormat).IsValueType);

        resource.SetSaveFormatHint(InternalResourceFormat.Binary);
        Assert.True(resource.Save(destination));
        Assert.Equal((byte)'{', destination.Buffer[0]);
    }

    [Fact]
    public void Resource_with_metadata_preserves_cpp_class_kind()
    {
        Assert.True(typeof(ResourceWithMetadata).IsClass);
        Assert.False(typeof(ResourceWithMetadata).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(ResourceWithMetadata).GetInterfaces());
    }

    [Fact]
    public void Resource_metadata_copies_owned_variant_values()
    {
        using var source = new MetadataResource();
        using var copy = new MetadataResource();
        using var value = new Variant(new byte[] { 1, 2, 3 });

        source.AddMetadata("Buffer", value);
        value.GetBuffer()[0] = 9;
        copy.CopyMetadata(source);
        source.GetMetadata("Buffer").GetBuffer()[0] = 8;
        source.CopyMetadata(source);

        Assert.Equal(8, source.GetMetadata("Buffer").GetBuffer()[0]);
        Assert.Equal(1, copy.GetMetadata("Buffer").GetBuffer()[0]);

        source.RemoveMetadata("Buffer");
        Assert.True(source.GetMetadata("Buffer").IsEmpty);
        Assert.Equal(1, copy.GetMetadata("Buffer").GetBuffer()[0]);
    }

    [Fact]
    public void Failed_load_restores_done_state()
    {
        var resource = new FailingResource();

        Assert.False(resource.Load(new MemoryBuffer(Array.Empty<byte>())));
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

        var xml = new XMLFile { Name = string.Empty };
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
        Assert.Equal(typeof(FailingResource).GetTypeId(), references.Type);
        Assert.Equal(["Textures/Test.png", string.Empty], references.Names);
    }

    private sealed class FailingResource : Resource
    {
        public FailingResource()
        {
        }

        public override bool BeginLoad(Deserializer source) => false;
    }

    private sealed class EmptySimpleResource : SimpleResource
    {
        public override void SerializeInBlock(Archive archive)
        {
        }
    }

    private sealed class MetadataResource : ResourceWithMetadata
    {
        public MetadataResource()
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
    private sealed class DerivedBinaryFile : BinaryFile
    {
    }

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

    [Fact]
    public void Class_kind_and_owned_buffer_lifetime_match_native()
    {
        var file = new DerivedBinaryFile();
        file.SetData([1]);
        var buffer = file.GetMutableBuffer();
        Assert.NotEqual(nint.Zero, buffer.GetData());

        file.Dispose();

        var pinnedBufferField = typeof(VectorBuffer).GetField("_pinnedBuffer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pinnedBuffer = (System.Runtime.InteropServices.GCHandle)pinnedBufferField.GetValue(buffer)!;
        Assert.False(pinnedBuffer.IsAllocated);
    }
}
