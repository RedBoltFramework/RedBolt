namespace RedBolt.Tests;

public sealed class Base64ArchiveTests
{
    [Fact]
    public void Base64ArchiveRoundTripsBinaryArchivePayload()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var value = new Variant(new Vector3f(1, 2, 3));
            using var originalText = new CString("Archive CString");
            var textToWrite = originalText;
            var output = new Base64OutputArchive();
            using (output.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(output, ref value);
                ArchiveSerialization.SerializeValue(output, "text", ref textToWrite);
            }
            var encoded = output.GetBase64();

            Assert.NotEmpty(encoded);

            var loaded = default(Variant);
            var loadedText = new CString();
            var input = new Base64InputArchive(encoded);
            using (input.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(input, ref loaded);
                ArchiveSerialization.SerializeValue(input, "text", ref loadedText);
            }
            input.Flush();

            Assert.Equal(new Vector3f(1, 2, 3), loaded.GetVector3());
            Assert.Equal("Archive CString", loadedText.ConvertToString());
            loadedText.Dispose();
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }
}
