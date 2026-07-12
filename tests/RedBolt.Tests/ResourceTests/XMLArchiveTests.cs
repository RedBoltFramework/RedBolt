namespace RedBolt.Tests;

public sealed class XMLArchiveTests
{
    [Fact]
    public void XMLArchiveRoundTripsVariantContainersCStringAndBytes()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var variant = new Variant(new VariantVector { new(42), new("value") });
            using var originalText = new CString("XML CString");
            var text = originalText;
            var bytes = new byte[] { 0x12, 0x34, 0xab };
            var output = new XMLOutputArchive();
            using (output.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(output, ref variant);
                ArchiveSerialization.SerializeValue(output, "text", ref text);
                output.SerializeBytes("bytes", bytes);
            }

            var xml = output.ToXmlString();
            Assert.Contains("XML CString", xml);
            Assert.Contains("1234AB", xml);

            var loaded = default(Variant);
            var loadedText = new CString();
            var loadedBytes = new byte[bytes.Length];
            var input = new XMLInputArchive(xml);
            using (input.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(input, ref loaded);
                ArchiveSerialization.SerializeValue(input, "text", ref loadedText);
                input.SerializeBytes("bytes", loadedBytes);
            }
            input.Flush();

            Assert.Equal(42, loaded.GetVariantVector()[0].GetInt());
            Assert.Equal("value", loaded.GetVariantVector()[1].GetString());
            Assert.Equal("XML CString", loadedText.ConvertToString());
            Assert.Equal(bytes, loadedBytes);
            loadedText.Dispose();
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }
}
