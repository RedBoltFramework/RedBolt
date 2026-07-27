using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;
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
            Assert.Equal(2, typeof(XMLOutputArchive).GetConstructors().Length);
            Assert.Equal(2, typeof(XMLInputArchive).GetConstructors().Length);
            Assert.NotNull(typeof(XMLAttributeReference).GetProperty(nameof(XMLAttributeReference.Element)));
            Assert.NotNull(typeof(XMLAttributeReference).GetProperty(nameof(XMLAttributeReference.AttributeName)));
            Assert.Null(typeof(XMLAttributeReference).GetMethod("GetElement"));
            Assert.Null(typeof(XMLAttributeReference).GetMethod("GetAttributeName"));
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
                Assert.Contains("value=\"2\"", xmlFile.Document.ToString());
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
    public void XMLInputArchiveUsesNativeNarrowingConversions()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var xmlFile = new XMLFile();
            var root = xmlFile.GetOrCreateRoot("Root");
            root.SetInt("signedByte", 255);
            root.SetUInt("unsignedByte", 256);
            root.SetInt("signedShort", 65535);
            root.SetUInt("unsignedShort", 65536);

            using var input = new XMLInputArchive(xmlFile);
            sbyte signedByte = 0;
            byte unsignedByte = 0;
            short signedShort = 0;
            ushort unsignedShort = 0;
            using (input.OpenUnorderedBlock("Root"))
            {
                input.Serialize("signedByte", ref signedByte);
                input.Serialize("unsignedByte", ref unsignedByte);
                input.Serialize("signedShort", ref signedShort);
                input.Serialize("unsignedShort", ref unsignedShort);
            }

            Assert.Equal(-1, signedByte);
            Assert.Equal(0, unsignedByte);
            Assert.Equal(-1, signedShort);
            Assert.Equal(0, unsignedShort);
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
            using var xmlFile = new XMLFile();
            using var output = new XMLOutputArchive(
                context, xmlFile.GetOrCreateRoot("Root"), xmlFile);
            using (output.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(output, ref variant);
                ArchiveSerialization.SerializeValue(output, "text", ref text);
                output.SerializeBytes("bytes", bytes);
            }

            var xml = xmlFile.Document.ToString();
            Assert.Contains("XML CString", xml);
            Assert.Contains("1234AB", xml);

            var loaded = default(Variant);
            var loadedText = new CString();
            var loadedBytes = new byte[bytes.Length];
            using var input = new XMLInputArchive(xmlFile);
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
