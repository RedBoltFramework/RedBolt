using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void VectorBuffer_typed_write_read_roundtrips()
    {
        var buffer = new VectorBuffer();
        Assert.True(buffer.WriteInt(42));
        Assert.True(buffer.WriteString("redbolt"));
        Assert.True(buffer.WriteVector3(new Vector3(1.0f, 2.0f, 3.0f)));
        Assert.True(buffer.WriteVLE(300u));

        buffer.Seek(0);
        Assert.Equal(42, buffer.ReadInt());
        Assert.Equal("redbolt", buffer.ReadString());
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), buffer.ReadVector3());
        Assert.Equal(300u, buffer.ReadVLE());
    }

    [Fact]
    public void MemoryBuffer_read_only_works()
    {
        var buffer = new VectorBuffer();
        buffer.WriteInt(42);
        buffer.WriteString("redbolt");
        buffer.WriteVector3(new Vector3(1.0f, 2.0f, 3.0f));
        buffer.WriteVLE(300u);

        var memory = new MemoryBuffer(buffer.GetBuffer());
        Assert.Equal(42, memory.ReadInt());
        Assert.Equal("redbolt", memory.ReadString());
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), memory.ReadVector3());
        Assert.Equal(300u, memory.ReadVLE());
        Assert.True(memory.Eof);
    }

    [Fact]
    public void Line_read_write_works()
    {
        var buffer = new VectorBuffer();
        buffer.WriteLine("first");
        buffer.WriteLine("second");
        buffer.Seek(0);
        Assert.Equal("first", buffer.ReadLine());
        Assert.Equal("second", buffer.ReadLine());
    }
}
