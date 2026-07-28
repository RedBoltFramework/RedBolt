namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

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

    [Fact]
    public void Geometry_vertex_buffer_resize_preserves_existing_streams()
    {
        var first = new VertexBuffer();
        var second = new VertexBuffer();
        var geometry = new Geometry();
        Assert.True(geometry.SetVertexBuffer(0, first));

        Assert.True(geometry.SetNumVertexBuffers(2));
        Assert.Same(first, geometry.GetVertexBuffer(0));
        Assert.Null(geometry.GetVertexBuffer(1));
        Assert.True(geometry.SetVertexBuffer(1, second));

        Assert.True(geometry.SetNumVertexBuffers(1));
        Assert.Same(first, geometry.GetVertexBuffer(0));
    }

    [Fact]
    public void Geometry_ray_hit_distance_uses_raw_triangle_data()
    {
        var elements = new[] { new VertexElement(VertexElementType.Vector3, VertexElementSemantic.Position) };
        var vertexData = new byte[3 * 3 * sizeof(float)];
        var positions = new[]
        {
            new Vector3f(0.0f, 0.0f, 0.0f),
            new Vector3f(1.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 1.0f, 0.0f)
        };
        for (var i = 0; i < positions.Length; ++i)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(positions[i].X), 0, vertexData, i * 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(positions[i].Y), 0, vertexData, i * 12 + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(positions[i].Z), 0, vertexData, i * 12 + 8, 4);
        }

        var indexData = new byte[3 * sizeof(ushort)];
        for (var i = 0; i < 3; ++i)
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)i), 0, indexData, i * sizeof(ushort), sizeof(ushort));

        var geometry = new Geometry();
        geometry.SetRawVertexData(vertexData, elements);
        geometry.SetRawIndexData(indexData, sizeof(ushort));
        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 3, false));

        var distance = geometry.GetHitDistance(new Ray(new Vector3f(0.25f, 0.25f, 1.0f),
            new Vector3f(0.0f, 0.0f, -1.0f)), out var normal);

        Assert.Equal(1.0f, distance);
        Assert.Equal(new Vector3f(0.0f, 0.0f, 1.0f), normal);
        Assert.True(geometry.IsInside(new Ray(new Vector3f(0.25f, 0.25f, -1.0f),
            new Vector3f(0.0f, 0.0f, 1.0f))));
    }

    [Fact]
    public void Geometry_raw_data_exposes_shadowed_index_buffer_bytes()
    {
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        Assert.True(indexBuffer.SetSize(3));
        indexBuffer.SetUnpackedData([0, 1, 2]);

        var geometry = new Geometry();
        geometry.SetIndexBuffer(indexBuffer);
        geometry.GetRawData(out _, out _, out var indexData, out var indexSize, out _);

        Assert.NotNull(indexData);
        Assert.Same(indexBuffer.GetRawBuffer()!.GetShadowData(), indexData);
        Assert.Equal(2u, indexSize);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 2, 0 }, indexData);
    }
}
