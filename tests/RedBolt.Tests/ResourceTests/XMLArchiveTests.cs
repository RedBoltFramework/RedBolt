namespace RedBolt.Tests;

public sealed class XMLArchiveTests
{
    [Fact]
    public void XMLArchiveMatchesCppClassKindsLifetimeAndUnorderedAttributeSemantics()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            Assert.True(typeof(XMLArchiveBase<>).IsAbstract);
            Assert.False(typeof(XMLOutputArchive).IsSealed);
            Assert.False(typeof(XMLInputArchive).IsSealed);
            Assert.False(typeof(XMLOutputArchiveBlock).IsSealed);
            Assert.False(typeof(XMLInputArchiveBlock).IsSealed);
            Assert.True(typeof(XMLAttributeReference).IsValueType);
            Assert.DoesNotContain(typeof(ICloneable), typeof(XMLOutputArchive).GetInterfaces());
            Assert.DoesNotContain(typeof(ICloneable), typeof(XMLInputArchive).GetInterfaces());

            using var xmlFile = new XMLFile { Name = "Data/archive.xml" };
            using (var output = new XMLOutputArchive(xmlFile))
            {
                var first = 1;
                var second = 2;
                using (output.OpenUnorderedBlock("Archive"))
                {
                    output.Serialize("value", ref first);
                    output.Serialize("value", ref second);
                }

                Assert.True(output.IsEof);
                Assert.Equal(xmlFile.Name, output.Name);
                Assert.Contains("value=\"2\"", output.ToXmlString());
                Assert.Throws<ArchiveException>(() => output.OpenUnorderedBlock("Again"));
            }

            using var input = new XMLInputArchive(xmlFile);
            var loaded = 0;
            using (input.OpenUnorderedBlock("Archive"))
                input.Serialize("value", ref loaded);

            Assert.Equal(2, loaded);
            Assert.True(input.IsEof);
            Assert.Equal(xmlFile.Name, input.Name);
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

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
