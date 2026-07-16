using System.Buffers.Binary;

namespace RedBolt.Tests;

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
