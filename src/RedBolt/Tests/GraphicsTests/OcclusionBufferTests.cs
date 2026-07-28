namespace RedBolt.Tests;

using System.Buffers.Binary;

using RedBolt.Graphics;
using CullMode = global::RedBolt.Graphics.CullMode;
using RedBolt.Math;

public sealed class OcclusionBufferTests
{
    [Fact]
    public void SetSize_matches_Cpp_validation_and_layout()
    {
        var buffer = new OcclusionBuffer();

        Assert.False(buffer.SetSize(10, 9, false));
        Assert.True(buffer.SetSize(16, 9, false));
        Assert.Equal(16, buffer.GetWidth());
        Assert.Equal(10, buffer.GetHeight());
        Assert.False(buffer.IsThreaded());

        var data = Assert.IsType<ArraySegment<int>>(buffer.GetBuffer());
        Assert.Equal(16 * 10, data.Count);
        Assert.Equal(17, data.Offset);

        buffer.Dispose();
        Assert.Null(buffer.GetBuffer());
    }

    [Fact]
    public void Per_thread_data_copy_matches_Cpp_shared_array_value_semantics()
    {
        var source = new OcclusionBufferData
        {
            DataWithSafety = [1, 2, 3],
            DataOffset = 1,
            Used = true
        };

        var copy = source;
        copy.DataWithSafety[1] = 9;
        copy.DataOffset = 2;
        copy.Used = false;

        Assert.Equal(9, source.DataWithSafety![1]);
        Assert.Equal(1, source.DataOffset);
        Assert.True(source.Used);
    }

    [Fact]
    public void Batch_copy_preserves_Cpp_value_and_borrowed_pointer_semantics()
    {
        var vertexData = new byte[] { 1, 2, 3 };
        var source = new OcclusionBatch
        {
            Model = Matrix3x4.Identity,
            VertexData = vertexData,
            VertexSize = 12,
            DrawCount = 3
        };

        var copy = source;
        copy.VertexData[0] = 9;
        copy.VertexSize = 24;

        Assert.Equal(9, source.VertexData![0]);
        Assert.Equal(12u, source.VertexSize);
    }

    [Fact]
    public void Triangle_submission_matches_Cpp_budget_and_rasterization()
    {
        var buffer = new OcclusionBuffer();
        Assert.True(buffer.SetSize(16, 16, false));
        buffer.Clear();
        buffer.SetCullMode(CullMode.None);
        buffer.SetMaxTriangles(1);

        var vertices = PackVertices(
            new Vector3f(-0.75f, -0.75f, 0.5f),
            new Vector3f(0.75f, -0.75f, 0.5f),
            new Vector3f(0.0f, 0.75f, 0.5f));
        Assert.True(buffer.AddTriangles(Matrix3x4.Identity, vertices, 12, 0, 3));
        Assert.False(buffer.AddTriangles(Matrix3x4.Identity, vertices, 12, 0, 3));

        buffer.DrawTriangles();
        buffer.BuildDepthHierarchy();

        Assert.False(buffer.IsVisible(new BoundingBox(
            new Vector3f(-0.05f, -0.05f, 0.75f), new Vector3f(0.05f, 0.05f, 0.8f))));
        Assert.True(buffer.IsVisible(new BoundingBox(
            new Vector3f(-0.05f, -0.05f, 0.25f), new Vector3f(0.05f, 0.05f, 0.3f))));
    }

    private static byte[] PackVertices(params Vector3f[] vertices)
    {
        var result = new byte[vertices.Length * 12];
        for (var i = 0; i < vertices.Length; ++i)
        {
            var offset = i * 12;
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(offset, 4), vertices[i].X);
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(offset + 4, 4), vertices[i].Y);
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(offset + 8, 4), vertices[i].Z);
        }
        return result;
    }
}
