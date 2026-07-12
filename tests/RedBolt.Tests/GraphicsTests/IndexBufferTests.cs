namespace RedBolt.Tests.GraphicsTests;

public sealed class IndexBufferTests
{
    [Fact]
    public void Pack_and_unpack_16_bit_indices_preserve_start_and_offset()
    {
        var source = new uint[] { 2, 4, 6 };
        var packed = new byte[10];
        IndexBuffer.PackIndexData(source, packed, largeIndices: false, start: 1, count: 3, offset: 10);

        var unpacked = new uint[3];
        IndexBuffer.UnpackIndexData(packed, largeIndices: false, start: 1, count: 3, unpacked);

        Assert.Equal(new uint[] { 12, 14, 16 }, unpacked);
        Assert.Equal((byte)0, packed[0]);
        Assert.Equal((byte)0, packed[8]);
        Assert.Equal(IndexBufferType.UInt16, IndexBuffer.GetIndexBufferType(new IndexBuffer()));
    }

    [Fact]
    public void Pack_and_unpack_32_bit_indices_preserve_start_and_offset()
    {
        var source = new uint[] { 2, 4, 6 };
        var packed = new byte[20];
        IndexBuffer.PackIndexData(source, packed, largeIndices: true, start: 1, count: 3, offset: 10);

        var unpacked = new uint[3];
        IndexBuffer.UnpackIndexData(packed, largeIndices: true, start: 1, count: 3, unpacked);

        Assert.Equal(new uint[] { 12, 14, 16 }, unpacked);
        Assert.Equal((byte)0, packed[0]);
        Assert.Equal((byte)0, packed[16]);
    }

    [Fact]
    public void Shadow_data_supports_partial_updates_and_used_vertex_range()
    {
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetSize(5, largeIndices: false);
        indexBuffer.SetShadowed(true);
        indexBuffer.SetUnpackedData(new uint[] { 4, 7 }, start: 1, count: 2);

        Assert.Equal(new uint[] { 0, 4, 7, 0, 0 }, indexBuffer.GetUnpackedData());
        Assert.True(indexBuffer.GetUsedVertexRange(1, 2, out var minVertex, out var vertexCount));
        Assert.Equal(4u, minVertex);
        Assert.Equal(4u, vertexCount);
        Assert.False(indexBuffer.GetUsedVertexRange(4, 2, out _, out _));
    }

    [Fact]
    public void Dynamic_index_buffer_grows_and_commits()
    {
        var dynamicBuffer = new DynamicIndexBuffer();
        Assert.True(dynamicBuffer.Initialize(2, largeIndices: false));

        var first = dynamicBuffer.AddIndices(2);
        IndexBuffer.PackIndexData(new uint[] { 1, 3 }, first.Data.Span, largeIndices: false, start: 0, count: 2);
        var secondStart = dynamicBuffer.AddIndices(2, new byte[] { 4, 0, 5, 0 });
        dynamicBuffer.Commit();

        Assert.Equal(0u, first.StartIndex);
        Assert.Equal(2u, secondStart);
        Assert.Equal(4u, dynamicBuffer.GetIndexBuffer().GetIndexCount());
        Assert.Equal(new uint[] { 1, 3, 4, 5 }, dynamicBuffer.GetIndexBuffer().GetUnpackedData());
    }
}
