using RedBolt;

namespace RedBolt.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void Geometry_validates_draw_ranges_and_clamps_lod_distance()
    {
        var geometry = new Geometry();

        Assert.False(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 3));
        Assert.False(geometry.SetNumVertexBuffers(RenderApiLimits.MaxVertexStreams));

        geometry.SetRawIndexData(new byte[6], 2);
        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 3));
        geometry.SetLodDistance(-1.0f);

        Assert.Equal(0.0f, geometry.GetLodDistance());
        Assert.Equal(0u, geometry.GetPrimitiveCount());
    }

    [Fact]
    public void Geometry_raw_vertex_data_roundtrips()
    {
        var elements = new[] { new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position) };
        var data = new byte[12];
        var geometry = new Geometry();
        geometry.SetRawVertexData(data, elements);
        geometry.SetRawIndexData(new byte[6], 2);

        geometry.GetRawData(out var vertexData, out var vertexSize, out var indexData, out var indexSize, out var actualElements);

        Assert.Same(data, vertexData);
        Assert.Equal(12u, vertexSize);
        Assert.NotNull(indexData);
        Assert.Equal(2u, indexSize);
        Assert.Equal(elements, actualElements);
    }
}
