using RedBolt;

namespace RedBolt.Tests;

public sealed class AbstractFileTests
{
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
    public void VectorBuffer_does_not_expose_stable_data_pointer_for_managed_storage()
    {
        var buffer = new VectorBuffer();

        Assert.Equal(nint.Zero, buffer.GetData());
        Assert.Equal(nint.Zero, buffer.GetModifiableData());

        Assert.True(buffer.WriteUByte(42));
        Assert.Equal(nint.Zero, buffer.GetData());
        Assert.Equal(nint.Zero, buffer.GetModifiableData());

        buffer.Clear();
        Assert.Equal(nint.Zero, buffer.GetData());
        Assert.Equal(nint.Zero, buffer.GetModifiableData());
    }
}
