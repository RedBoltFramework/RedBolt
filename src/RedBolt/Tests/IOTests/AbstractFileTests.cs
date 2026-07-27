using RedBolt;

using RedBolt.IO;
namespace RedBolt.Tests;
using File = global::RedBolt.IO.File;

public sealed class AbstractFileTests
{
    [Fact]
    public void Class_kind_construction_and_destruction_match_cpp()
    {
        Assert.True(typeof(AbstractFile).IsAbstract);
        Assert.False(typeof(AbstractFile).IsSealed);
        Assert.Equal(typeof(Deserializer), typeof(AbstractFile).BaseType);
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(AbstractFile)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(AbstractFile)));

        var constructors = typeof(AbstractFile).GetConstructors();
        Assert.Equal(2, constructors.Length);
        Assert.All(constructors, constructor => Assert.True(constructor.IsPublic));

        var dispose = typeof(AbstractFile).GetMethod(nameof(IDisposable.Dispose));
        Assert.NotNull(dispose);
        Assert.True(dispose.IsVirtual);
        Assert.False(dispose.IsFinal);
        Assert.Equal(typeof(AbstractFile), dispose.DeclaringType);

        Assert.Equal(typeof(File), typeof(File).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
        Assert.Equal(typeof(MemoryBuffer), typeof(MemoryBuffer).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
        Assert.Equal(typeof(NamedPipe), typeof(NamedPipe).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
        Assert.Equal(typeof(VectorBuffer), typeof(VectorBuffer).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
    }

    [Fact]
    public void Constructors_and_default_operations_match_cpp()
    {
        var emptyFile = new TestAbstractFile();
        var sizedFile = new TestAbstractFile(42);

        Assert.Equal(string.Empty, emptyFile.Name);
        Assert.Equal(string.Empty, emptyFile.GetName());
        Assert.Equal(string.Empty, emptyFile.AbsoluteName);
        Assert.True(emptyFile.IsOpen);
        Assert.Equal(0u, emptyFile.Size);
        Assert.Equal(42u, sizedFile.Size);

        emptyFile.SetName("Test");
        Assert.Equal("Test", emptyFile.Name);
        Assert.Equal("Test", emptyFile.GetName());
        Assert.Equal("Test", emptyFile.AbsoluteName);
        emptyFile.Close();
    }

    [Fact]
    public void VectorBuffer_uses_rbfx_default_name_until_explicitly_set()
    {
        var buffer = new VectorBuffer();

        Assert.Equal("VectorBuffer", buffer.Name);
        Assert.Equal("VectorBuffer", buffer.GetName());

        buffer.SetName("TestBuffer");

        Assert.Equal("TestBuffer", buffer.Name);
        Assert.Equal("TestBuffer", buffer.GetName());
        Assert.Equal("TestBuffer", buffer.AbsoluteName);
        Assert.True(buffer.IsOpen);
    }

    [Fact]
    public unsafe void MemoryBuffer_exposes_data_pointer_for_managed_backing_storage()
    {
        var bytes = new byte[] { 10, 20, 30, 40 };
        var buffer = new MemoryBuffer(bytes.AsMemory());

        var data = (byte*)buffer.GetData();
        Assert.NotEqual(nint.Zero, (nint)data);
        Assert.Equal(10, data[0]);
        Assert.Equal(20, data[1]);
        Assert.Equal(30, data[2]);
        Assert.Equal(40, data[3]);
    }

    [Fact]
    public unsafe void MemoryBuffer_string_constructor_exposes_utf8_data_pointer()
    {
        var buffer = new MemoryBuffer("rbfx");

        var data = new ReadOnlySpan<byte>((void*)buffer.GetData(), checked((int)buffer.Size));
        Assert.Equal("rbfx"u8.ToArray(), data.ToArray());
        Assert.True(buffer.IsReadOnly);
    }

    [Fact]
    public void MemoryBuffer_byte_array_constructor_is_writable_and_shares_storage()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var buffer = new MemoryBuffer(bytes);

        Assert.False(buffer.IsReadOnly);

        buffer.Seek(1);
        Assert.True(buffer.WriteUByte(9));

        Assert.Equal(new byte[] { 1, 9, 3, 4 }, bytes);
    }

    [Fact]
    public unsafe void MemoryBuffer_class_kind_and_copy_construction_match_cpp()
    {
        Assert.False(typeof(MemoryBuffer).IsValueType);
        Assert.False(typeof(MemoryBuffer).IsSealed);
        Assert.Equal(typeof(AbstractFile), typeof(MemoryBuffer).BaseType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(MemoryBuffer)));

        var bytes = new byte[] { 1, 2, 3, 4 };
        var source = new MemoryBuffer(bytes);
        source.SetName("Memory");
        source.Seek(2);

        var copy = (MemoryBuffer)source.Clone();

        Assert.NotSame(source, copy);
        Assert.Equal(source.GetData(), copy.GetData());
        Assert.Equal(source.Size, copy.Size);
        Assert.Equal(source.Position, copy.Position);
        Assert.Equal(source.Name, copy.Name);
        Assert.Equal(source.IsReadOnly, copy.IsReadOnly);

        copy.Seek(1);
        Assert.True(copy.WriteUByte(9));
        Assert.Equal(2u, source.Position);
        Assert.Equal(new byte[] { 1, 9, 3, 4 }, bytes);

        source.Dispose();
        copy.Seek(0);
        Assert.Equal(1, copy.ReadUByte());
        copy.Dispose();
        copy.Dispose();
    }

    [Fact]
    public void MemoryBuffer_copy_preserves_read_only_state()
    {
        using var source = new MemoryBuffer(new byte[] { 1, 2, 3 }.AsMemory());
        using var copy = (MemoryBuffer)source.Clone();

        Assert.True(copy.IsReadOnly);
    }

    [Fact]
    public unsafe void VectorBuffer_exposes_data_pointers_for_managed_storage()
    {
        var buffer = new VectorBuffer(new byte[] { 42 });

        Assert.NotEqual(nint.Zero, buffer.GetData());
        Assert.NotEqual(nint.Zero, buffer.GetModifiableData());
        Assert.Equal(42, ((byte*)buffer.GetData())[0]);

        Assert.True(buffer.WriteUByte(9));
        Assert.Equal(9, ((byte*)buffer.GetData())[0]);

        buffer.Clear();
        Assert.Equal(nint.Zero, buffer.GetData());
        Assert.Equal(nint.Zero, buffer.GetModifiableData());
    }

    private sealed class TestAbstractFile : AbstractFile
    {
        public TestAbstractFile()
        {
        }

        public TestAbstractFile(uint size)
            : base(size)
        {
        }

        public override uint Read(Span<byte> destination) => 0;

        public override uint Seek(uint position) => Position;

        public override uint Write(ReadOnlySpan<byte> data) => 0;
    }
}
