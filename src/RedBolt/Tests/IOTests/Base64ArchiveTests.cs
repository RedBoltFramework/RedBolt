using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;

namespace RedBolt.Tests;

public sealed class Base64ArchiveTests
{
    [Fact]
    public void Archive_class_kinds_match_native_noncopyable_classes()
    {
        Type[] types =
        [
            typeof(BinaryOutputArchiveBlock),
            typeof(BinaryOutputArchive),
            typeof(BinaryInputArchive),
            typeof(Base64OutputArchive),
            typeof(Base64InputArchive)
        ];

        foreach (var type in types)
        {
            Assert.True(type.IsClass);
            Assert.False(type.IsSealed);
            Assert.False(typeof(ICloneable).IsAssignableFrom(type));
        }

        Assert.True(typeof(BinaryInputArchiveBlock).IsClass);
        Assert.False(typeof(BinaryInputArchiveBlock).IsSealed);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(BinaryInputArchiveBlock)));
    }

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
            using var output = new Base64OutputArchive();
            Assert.IsAssignableFrom<BinaryOutputArchive>(output);
            using (output.OpenUnorderedBlock("Root"))
            {
                ArchiveSerialization.SerializeVariantInBlock(output, ref value);
                ArchiveSerialization.SerializeValue(output, "text", ref textToWrite);
            }
            var encoded = output.GetBase64();

            Assert.NotEmpty(encoded);

            var loaded = default(Variant);
            var loadedText = new CString();
            using var input = new Base64InputArchive(encoded);
            Assert.IsAssignableFrom<BinaryInputArchive>(input);
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

    [Fact]
    public void BinaryOutputArchive_reports_inline_path()
    {
        using var buffer = new VectorBuffer();
        using var archive = new BinaryOutputArchive(new Context(), buffer);

        using (archive.OpenUnorderedBlock("Root"))
        {
            Assert.Equal("Root", archive.CurrentBlockPath);
            using (archive.OpenSequentialBlock("Inline"))
                Assert.Equal("Root/?", archive.CurrentBlockPath);
            Assert.Equal("Root", archive.CurrentBlockPath);
        }
    }

    [Fact]
    public void BinaryOutputArchive_flushes_delayed_block_exception_before_next_block()
    {
        var serializer = new FailingSerializer();
        using var archive = new BinaryOutputArchive(new Context(), serializer);
        var sizeHint = 0;
        var value = 42;

        archive.BeginBlock("Root", ref sizeHint, true, ArchiveBlockType.Sequential);
        archive.Serialize("value", ref value);
        archive.EndBlock();

        var exception = Assert.Throws<ArchiveException>(() =>
            archive.BeginBlock("Next", ref sizeHint, false, ArchiveBlockType.Sequential));
        Assert.Contains("Root/<block guard>", exception.Message);
    }

    [Fact]
    public void BinaryOutputArchiveBlock_disposes_owned_safe_buffer()
    {
        using var parentSerializer = new VectorBuffer();
        var block = new BinaryOutputArchiveBlock("Safe", ArchiveBlockType.Sequential, parentSerializer, true);
        var outputBuffer = Assert.IsType<VectorBuffer>(block.GetSerializer());
        outputBuffer.SetData([1]);
        outputBuffer.GetData();
        var pinnedBufferField = typeof(VectorBuffer).GetField("_pinnedBuffer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        Assert.True(((System.Runtime.InteropServices.GCHandle)pinnedBufferField.GetValue(outputBuffer)!).IsAllocated);
        block.Dispose();
        Assert.False(((System.Runtime.InteropServices.GCHandle)pinnedBufferField.GetValue(outputBuffer)!).IsAllocated);
    }

    [Fact]
    public void BinaryInputArchiveBlock_clone_copies_state_without_reading_again()
    {
        using var deserializer = new VectorBuffer([0]);
        var block = new BinaryInputArchiveBlock("Safe", ArchiveBlockType.Sequential, deserializer, true,
            uint.MaxValue);
        var position = deserializer.Position;

        var clone = Assert.IsType<BinaryInputArchiveBlock>(block.Clone());

        Assert.NotSame(block, clone);
        Assert.Equal(position, deserializer.Position);
        Assert.Equal(block.GetNextElementPosition(), clone.GetNextElementPosition());
    }

    private sealed class FailingSerializer : Serializer
    {
        public override uint Write(ReadOnlySpan<byte> data) => 0;
    }
}
