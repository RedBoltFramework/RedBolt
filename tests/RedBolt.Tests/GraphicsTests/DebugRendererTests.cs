namespace RedBolt.Tests;

public sealed class DebugRendererTests
{
    [Fact]
    public void EndFrameClearsAccumulatedGeometry()
    {
        using var renderer = new DebugRenderer();
        renderer.AddLine(Vector3f.Zero, Vector3f.One, Color.White);
        renderer.AddTriangle(Vector3f.Zero, Vector3f.Right, Vector3f.Up, Color.White, false);

        Assert.True(renderer.HasContent());

        renderer.HandleEndFrame();

        Assert.False(renderer.HasContent());
    }

    [Fact]
    public void DebugLineStoresCppFields()
    {
        var start = new Vector3f(1.0f, 2.0f, 3.0f);
        var end = new Vector3f(4.0f, 5.0f, 6.0f);
        var line = new DebugLine(start, end, 0x12345678u);

        Assert.Equal(start, line.Start);
        Assert.Equal(end, line.End);
        Assert.Equal(0x12345678u, line.Color);
    }

    [Fact]
    public void DebugTriangleStoresCppFields()
    {
        var v1 = new Vector3f(1.0f, 2.0f, 3.0f);
        var v2 = new Vector3f(4.0f, 5.0f, 6.0f);
        var v3 = new Vector3f(7.0f, 8.0f, 9.0f);
        var triangle = new DebugTriangle(v1, v2, v3, 0x87654321u);

        Assert.Equal(v1, triangle.VertexA);
        Assert.Equal(v2, triangle.VertexB);
        Assert.Equal(v3, triangle.VertexC);
        Assert.Equal(0x87654321u, triangle.Color);
    }
}
