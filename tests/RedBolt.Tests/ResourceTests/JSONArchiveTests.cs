namespace RedBolt.Tests;

public sealed class JSONArchiveTests
{
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
            var output = new JSONOutputArchive();
            using (output.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(output, ref variant);
                ArchiveSerialization.SerializeValue(output, "text", ref text);
            }

            var json = output.ToJsonString();
            Assert.Contains("JSON CString", json);

            var loaded = default(Variant);
            var loadedText = new CString();
            var input = new JSONInputArchive(json);
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
