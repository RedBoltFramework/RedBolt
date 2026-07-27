namespace RedBolt.Tests;

public sealed class JSONArchiveTests
{
    [Fact]
    public void JSONArchivesBorrowTheirJSONFile()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var jsonFile = new JSONFile();
            var output = new JSONOutputArchive(jsonFile);
            var value = 42;
            using (output.OpenUnorderedBlock("Root"))
                output.Serialize("value", ref value);
            output.Dispose();

            Assert.Equal(42, jsonFile.Root.Get("value").GetInt());

            var input = new JSONInputArchive(jsonFile);
            var loaded = 0;
            using (input.OpenUnorderedBlock("Root"))
                input.Serialize("value", ref loaded);
            input.Dispose();

            Assert.Equal(42, loaded);
            Assert.Equal(42, jsonFile.Root.Get("value").GetInt());
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }

    [Fact]
    public void JSONArchiveTypesMatchNativeCopyShape()
    {
        Assert.False(typeof(JSONOutputArchive).IsSealed);
        Assert.False(typeof(JSONInputArchive).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(JSONOutputArchive)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(JSONInputArchive)));
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(JSONOutputArchiveBlock)));
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(JSONInputArchiveBlock)));
    }

    [Fact]
    public void JSONArchiveRoundTripsVariantContainersAndCString()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var variant = new Variant(new VariantVector { new(42), new("value") });
            using var originalText = new CString("JSON CString");
            var text = originalText;
            var output = new TestJSONOutputArchive();
            using (output.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(output, ref variant);
                ArchiveSerialization.SerializeValue(output, "text", ref text);
            }

            var json = output.ToJsonString();
            Assert.Contains("JSON CString", json);

            var loaded = default(Variant);
            var loadedText = new CString();
            var input = new TestJSONInputArchive(json);
            using (input.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(input, ref loaded);
                ArchiveSerialization.SerializeValue(input, "text", ref loadedText);
            }
            input.Flush();

            Assert.Equal(42, loaded.GetVariantVector()[0].GetInt());
            Assert.Equal("value", loaded.GetVariantVector()[1].GetString());
            Assert.Equal("JSON CString", loadedText.ConvertToString());
            loadedText.Dispose();
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }
}
